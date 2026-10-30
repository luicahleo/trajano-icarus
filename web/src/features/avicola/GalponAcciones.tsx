import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import type { Galpon } from '../../lib/tipos';
import { actualizarGalpon, ajustarInventarioGalpon, desactivarGalpon } from './api';
import { useFuncionalidad } from '../auth/useFuncionalidad';
export function GalponAcciones({ galpon }: { galpon: Galpon }) {
  const puede = useFuncionalidad('Galpones');
  const qc = useQueryClient();
  const [dialogo, setDialogo] = useState<'editar' | 'inventario' | 'desactivar' | null>(null);
  const [numero, setNumero] = useState(galpon.numero),
    [descripcion, setDescripcion] = useState(galpon.descripcion ?? ''),
    [capacidad, setCapacidad] = useState(String(galpon.capacidadMaxima)),
    [poblado, setPoblado] = useState(galpon.fechaNacimientoLote),
    [descarte, setDescarte] = useState(galpon.fechaDescarteLote ?? ''),
    [gallinas, setGallinas] = useState(String(galpon.gallinasActuales));
  const hoy = new Date().toISOString().slice(0, 10);
  const cerrar = () => setDialogo(null);
  const refrescar = () => {
    void qc.invalidateQueries({ queryKey: ['avicola', 'galpones'] });
    cerrar();
  };
  const editar = useMutation({
    mutationFn: () =>
      actualizarGalpon(galpon.id, {
        numero,
        descripcion: descripcion || null,
        capacidadMaxima: Number(capacidad),
        fechaNacimientoLote: poblado,
        fechaDescarteLote: descarte || null,
      }),
    onSuccess: refrescar,
  });
  const inventario = useMutation({
    mutationFn: () => ajustarInventarioGalpon(galpon.id, Number(gallinas)),
    onSuccess: refrescar,
  });
  const desactivar = useMutation({
    mutationFn: () => desactivarGalpon(galpon.id),
    onSuccess: refrescar,
  });
  if (!puede) return null;
  return (
    <>
      <Button onClick={() => setDialogo('editar')}>Editar</Button>
      <Button onClick={() => setDialogo('inventario')}>Inventario</Button>
      <Button onClick={() => setDialogo('desactivar')}>Desactivar</Button>
      <Dialog open={dialogo === 'editar'} onClose={cerrar} fullWidth maxWidth="xs">
        <DialogTitle>Editar galpón</DialogTitle>
        <DialogContent>
          <TextField
            label="Número"
            value={numero}
            onChange={(e) => setNumero(e.target.value)}
            fullWidth
            margin="dense"
          />
          <TextField
            label="Descripción"
            value={descripcion}
            onChange={(e) => setDescripcion(e.target.value)}
            fullWidth
            margin="dense"
          />
          <TextField
            label="Capacidad máxima"
            value={capacidad}
            onChange={(e) => setCapacidad(e.target.value)}
            fullWidth
            margin="dense"
          />
          <TextField
            label="Fecha de poblado del lote"
            type="date"
            value={poblado}
            onChange={(e) => setPoblado(e.target.value)}
            slotProps={{ htmlInput: { max: hoy }, inputLabel: { shrink: true } }}
            fullWidth
            margin="dense"
          />
          <TextField
            label="Fecha de descarte del lote"
            type="date"
            value={descarte}
            onChange={(e) => setDescarte(e.target.value)}
            slotProps={{
              htmlInput: { min: poblado || undefined, max: hoy },
              inputLabel: { shrink: true },
            }}
            helperText="Opcional; se conserva para auditorías. Dejar vacía si el lote sigue activo."
            fullWidth
            margin="dense"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={cerrar}>Cancelar</Button>
          <Button onClick={() => editar.mutate()}>Guardar</Button>
        </DialogActions>
      </Dialog>
      <Dialog open={dialogo === 'inventario'} onClose={cerrar} fullWidth maxWidth="xs">
        <DialogTitle>Ajustar inventario</DialogTitle>
        <DialogContent>
          <TextField
            label="Gallinas actuales"
            value={gallinas}
            onChange={(e) => setGallinas(e.target.value)}
            fullWidth
            margin="dense"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={cerrar}>Cancelar</Button>
          <Button onClick={() => inventario.mutate()}>Guardar</Button>
        </DialogActions>
      </Dialog>
      <Dialog open={dialogo === 'desactivar'} onClose={cerrar}>
        <DialogTitle>Desactivar galpón</DialogTitle>
        <DialogContent>¿Confirmás la desactivación?</DialogContent>
        <DialogActions>
          <Button onClick={cerrar}>Cancelar</Button>
          <Button onClick={() => desactivar.mutate()}>Confirmar</Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
