import { Alert, Box, Button, Checkbox, Chip, CircularProgress, Container, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, List, ListItem, ListItemText, TextField, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { ApiError } from '../../../lib/http';
import type { ProgramaVacunacionResumen } from '../../../lib/tipos';
import { actualizarProgramaVacunacion, crearProgramaVacunacion, desactivarProgramaVacunacion, importarCronogramaExcel, listarProgramasVacunacion } from '../../avicola/api';
import { CLAVE_PROGRAMAS_VACUNACION, hoyIso } from '../../avicola/constantes';

interface FormularioPrograma { nombre: string; fechaEmision: string; cantidadAves: string; observaciones: string; }

const formularioVacio: FormularioPrograma = { nombre: '', fechaEmision: hoyIso(), cantidadAves: '', observaciones: '' };

export function AdminVacunacionPage() {
  const queryClient = useQueryClient();
  const [incluirInactivos, setIncluirInactivos] = useState(false);
  const [editando, setEditando] = useState<ProgramaVacunacionResumen | null>(null);
  const [formAbierto, setFormAbierto] = useState(false);
  const [form, setForm] = useState<FormularioPrograma>(formularioVacio);
  const [subiendoEn, setSubiendoEn] = useState<ProgramaVacunacionResumen | null>(null);
  const inputArchivo = useRef<HTMLInputElement>(null);

  const programas = useQuery({
    queryKey: [...CLAVE_PROGRAMAS_VACUNACION, incluirInactivos],
    queryFn: () => listarProgramasVacunacion(incluirInactivos),
  });

  const guardar = useMutation({
    mutationFn: () => {
      const datos = { nombre: form.nombre.trim(), fechaEmision: form.fechaEmision, cantidadAves: Number(form.cantidadAves), observaciones: form.observaciones.trim() || null };
      return editando ? actualizarProgramaVacunacion(editando.id, datos) : crearProgramaVacunacion(datos);
    },
    onSuccess: () => { setFormAbierto(false); setEditando(null); void queryClient.invalidateQueries({ queryKey: CLAVE_PROGRAMAS_VACUNACION }); },
  });

  const desactivar = useMutation({
    mutationFn: (id: string) => desactivarProgramaVacunacion(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: CLAVE_PROGRAMAS_VACUNACION }),
  });

  const subirExcel = useMutation({
    mutationFn: ({ id, archivo }: { id: string; archivo: File }) => importarCronogramaExcel(id, archivo),
    onSuccess: () => { setSubiendoEn(null); void queryClient.invalidateQueries({ queryKey: CLAVE_PROGRAMAS_VACUNACION }); },
  });

  const erroresImportacion = subirExcel.error instanceof ApiError
    ? Object.values(subirExcel.error.erroresValidacion ?? {}).flat()
    : [];

  if (programas.isLoading) return <Container sx={{ py: 3 }}><CircularProgress aria-label="Cargando" /></Container>;
  if (programas.isError) return <Container sx={{ py: 3 }}><Alert severity="error">No se pudo cargar el catálogo de vacunación.</Alert></Container>;

  return (
    <Container sx={{ py: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', my: 2 }}>
        <Typography variant="h4">Programas de vacunación</Typography>
        <Button variant="contained" onClick={() => { setEditando(null); setForm(formularioVacio); setFormAbierto(true); }}>Nuevo programa</Button>
      </Box>
      <FormControlLabel control={<Checkbox checked={incluirInactivos} onChange={(e) => setIncluirInactivos(e.target.checked)} />} label="Incluir inactivos" />
      <List aria-label="Programas de vacunación">
        {(programas.data ?? []).map((p) => (
          <ListItem key={p.id} secondaryAction={
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button size="small" onClick={() => { setEditando(p); setForm({ nombre: p.nombre, fechaEmision: p.fechaEmision, cantidadAves: String(p.cantidadAves), observaciones: p.observaciones ?? '' }); setFormAbierto(true); }}>Editar</Button>
              <Button size="small" onClick={() => { setSubiendoEn(p); inputArchivo.current?.click(); }}>Subir Excel</Button>
              {p.estaActivo && <Button size="small" color="error" onClick={() => desactivar.mutate(p.id)}>Desactivar</Button>}
            </Box>
          }>
            <ListItemText primary={<>{p.nombre} {!p.estaActivo && <Chip size="small" label="Inactivo" />}</>} secondary={`Emitido ${p.fechaEmision} · para ${p.cantidadAves} aves`} />
          </ListItem>
        ))}
      </List>
      <input ref={inputArchivo} type="file" accept=".xlsx,.xls" aria-label="Archivo Excel" style={{ display: 'none' }}
        onChange={(e) => { const archivo = e.target.files?.[0]; if (archivo && subiendoEn) subirExcel.mutate({ id: subiendoEn.id, archivo }); e.target.value = ''; }} />
      {(subirExcel.isPending || subirExcel.isError || subirExcel.isSuccess) && subiendoEn && (
        <Dialog open onClose={() => { setSubiendoEn(null); subirExcel.reset(); }}>
          <DialogTitle>Importar cronograma — {subiendoEn.nombre}</DialogTitle>
          <DialogContent>
            {subirExcel.isPending && <Typography>Subiendo…</Typography>}
            {subirExcel.isSuccess && <Alert severity="success">Cronograma importado: {subirExcel.data.itemsImportados} ítems.</Alert>}
            {subirExcel.isError && (
              <Alert severity="error">
                No se importó nada. Corregí el archivo y volvé a subirlo:
                <ul>{erroresImportacion.length > 0 ? erroresImportacion.map((m) => <li key={m}>{m}</li>) : <li>{subirExcel.error instanceof ApiError ? subirExcel.error.message : 'Error de importación.'}</li>}</ul>
              </Alert>
            )}
          </DialogContent>
          <DialogActions><Button onClick={() => { setSubiendoEn(null); subirExcel.reset(); }}>Cerrar</Button></DialogActions>
        </Dialog>
      )}
      <Dialog open={formAbierto} onClose={() => setFormAbierto(false)}>
        <DialogTitle>{editando ? 'Editar programa' : 'Nuevo programa'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 2 }}>
          <TextField label="Nombre" value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} fullWidth />
          <TextField label="Fecha de emisión" type="date" value={form.fechaEmision} onChange={(e) => setForm({ ...form, fechaEmision: e.target.value })} slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: hoyIso() } }} fullWidth />
          <TextField label="Cantidad de aves" value={form.cantidadAves} onChange={(e) => setForm({ ...form, cantidadAves: e.target.value })} inputMode="numeric" fullWidth />
          <TextField label="Observaciones" value={form.observaciones} onChange={(e) => setForm({ ...form, observaciones: e.target.value })} multiline fullWidth />
          {guardar.isError && <Alert severity="error">{guardar.error instanceof ApiError ? guardar.error.message : 'No se pudo guardar el programa.'}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setFormAbierto(false)}>Cancelar</Button>
          <Button onClick={() => guardar.mutate()} disabled={guardar.isPending || !form.nombre.trim() || Number(form.cantidadAves) <= 0}>Guardar</Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
