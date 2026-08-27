import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  List,
  ListItem,
  ListItemText,
  TextField,
  Typography,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { useState } from 'react';
import type { EficienciaDia, MortalidadRegistro, RecogidaResumen, TareaVacunacionResumen } from '../../lib/tipos';
import { ApiError } from '../../lib/http';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { desactivarMortalidad, desactivarProduccion, listarMortalidad, listarProduccion, obtenerEficiencia, obtenerGalpon, listarTareasVacunacion, quitarPlanVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION, CLAVE_TAREAS_VACUNACION, hoyIso } from './constantes';
import { formatearConteo } from './formatos';
import { RegistrarBajasDialog } from './RegistrarBajasDialog';
import { RegistrarRecogidaDialog } from './RegistrarRecogidaDialog';
import { EditarBajasDialog } from './EditarBajasDialog';
import { EditarRecogidaDialog } from './EditarRecogidaDialog';
import { AsignarPlanDialog } from './AsignarPlanDialog';

type Evento =
  | { hora: string; tipo: 'recogida'; datos: NonNullable<Awaited<ReturnType<typeof listarProduccion>>['recogidas']>[number] }
  | { hora: string; tipo: 'bajas'; datos: NonNullable<Awaited<ReturnType<typeof listarMortalidad>>['registros']>[number] };

function diaEficiencia(dias: EficienciaDia[] | undefined): EficienciaDia | undefined {
  return dias?.[0];
}

export function GalponPage() {
  const { galponId = '' } = useParams();
  const [fecha, setFecha] = useState(hoyIso());
  const [registrandoBajas, setRegistrandoBajas] = useState(false);
  const [registrandoRecogida, setRegistrandoRecogida] = useState(false);
  const [recogidaEditada, setRecogidaEditada] = useState<RecogidaResumen | null>(null);
  const [bajasEditadas, setBajasEditadas] = useState<MortalidadRegistro | null>(null);
  const [registroAEliminar, setRegistroAEliminar] = useState<Evento | null>(null);
  const [asignandoPlan, setAsignandoPlan] = useState(false);
  const queryClient = useQueryClient();
  const esHoy = fecha === hoyIso();
  const galpon = useQuery({ queryKey: ['avicola', 'galpon', galponId], queryFn: () => obtenerGalpon(galponId), enabled: Boolean(galponId) });
  const puedeProduccion = useFuncionalidad('ProduccionHuevos');
  const puedeMortalidad = useFuncionalidad('Mortalidad');
  const puedeVacunacion = useFuncionalidad('Vacunacion');
  const puedeEstructura = useFuncionalidad('Galpones');
  const produccion = useQuery({ queryKey: ['avicola', 'produccion', galponId, fecha], queryFn: () => listarProduccion(galponId, fecha), enabled: Boolean(galponId) && puedeProduccion });
  const mortalidad = useQuery({ queryKey: ['avicola', 'mortalidad', galponId, fecha], queryFn: () => listarMortalidad(galponId, fecha), enabled: Boolean(galponId) && puedeMortalidad });
  const eficiencia = useQuery({ queryKey: ['avicola', 'eficiencia', galponId, fecha, fecha], queryFn: () => obtenerEficiencia(galponId, fecha, fecha), enabled: Boolean(galponId) && puedeProduccion });
  const tareasVacunacion = useQuery({ queryKey: [...CLAVE_TAREAS_VACUNACION, galponId], queryFn: () => listarTareasVacunacion(galponId), enabled: Boolean(galponId) && puedeVacunacion });
  const quitarPlan = useMutation({
    mutationFn: () => quitarPlanVacunacion(galponId),
    onSuccess: () => { void queryClient.invalidateQueries({ queryKey: CLAVE_TAREAS_VACUNACION }); void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACION_VACUNACION }); },
  });
  const eliminar = useMutation({
    mutationFn: (evento: Evento) => evento.tipo === 'recogida' ? desactivarProduccion(evento.datos.id) : desactivarMortalidad(evento.datos.id),
    onSuccess: () => { void queryClient.invalidateQueries({ queryKey: ['avicola', 'produccion'] }); void queryClient.invalidateQueries({ queryKey: ['avicola', 'mortalidad'] }); void queryClient.invalidateQueries({ queryKey: ['avicola', 'galpon'] }); void queryClient.invalidateQueries({ queryKey: ['avicola', 'eficiencia'] }); setRegistroAEliminar(null); },
  });

  if (galpon.isLoading || (puedeProduccion && (produccion.isLoading || eficiencia.isLoading)) || (puedeMortalidad && mortalidad.isLoading)) {
    return <Container sx={{ py: 3 }}><CircularProgress aria-label="Cargando" /></Container>;
  }
  if (galpon.isError && galpon.error instanceof ApiError && galpon.error.status === 404) {
    return <Container sx={{ py: 3 }}><Alert severity="error">No se encontró el galpón.</Alert></Container>;
  }
  const error = galpon.error ?? (puedeProduccion ? produccion.error ?? eficiencia.error : null) ?? (puedeMortalidad ? mortalidad.error : null);
  if (error) {
    return <Container sx={{ py: 3 }}><Alert severity="error" action={<Button onClick={() => { void galpon.refetch(); void produccion.refetch(); void mortalidad.refetch(); void eficiencia.refetch(); }}>Reintentar</Button>}>No se pudo cargar el galpón.</Alert></Container>;
  }
  if (!galpon.data || (puedeProduccion && !produccion.data) || (puedeMortalidad && !mortalidad.data)) return null;

  const eventos: Evento[] = [
    ...(mortalidad.data?.registros ?? []).map((datos) => ({ hora: datos.hora, tipo: 'bajas' as const, datos })),
    ...(produccion.data?.recogidas ?? []).map((datos) => ({ hora: datos.hora ?? '', tipo: 'recogida' as const, datos })),
  ].sort((a, b) => a.hora.localeCompare(b.hora));
  const dia = diaEficiencia(eficiencia.data?.dias);
  const planVigente = (tareasVacunacion.data ?? []).find((t) => t.estado === 'Pendiente')?.programaNombre ?? null;

  return <Container sx={{ py: 2 }}>
      <Box sx={{ display: 'flex', flexDirection: { xs: 'column', sm: 'row' }, justifyContent: 'space-between', alignItems: { sm: 'center' }, gap: 1 }}>
      <Box><Typography variant="h4">Galpón {galpon.data.numero}</Typography><Typography>{galpon.data.gallinasActuales} / {galpon.data.capacidadMaxima} gallinas</Typography></Box>
      <Box sx={{ display: 'flex', flexDirection: 'row', gap: 1, alignItems: 'center' }}>{puedeProduccion && dia && <Typography>{dia.eficiencia.toLocaleString('es-ES')} %</Typography>}{puedeProduccion && dia?.bajoUmbral && <Chip size="small" color="error" label="Bajo umbral — considerar descarte" />}{puedeProduccion && <Button component={Link} to={`/avicola/galpones/${galponId}/eficiencia`}>Ver eficiencia</Button>}</Box>
    </Box>
    <TextField label="Fecha" type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: hoyIso() } }} sx={{ mt: 2 }} />
    {!esHoy && <Alert severity="info" sx={{ mt: 2 }}>Día sellado: no se puede corregir</Alert>}
    {esHoy && <Box sx={{ display: 'flex', flexDirection: 'row', gap: 1, mt: 2 }}>{puedeProduccion && <Button variant="contained" onClick={() => setRegistrandoRecogida(true)}>Registrar recogida</Button>}{puedeMortalidad && <Button variant="contained" onClick={() => setRegistrandoBajas(true)}>Registrar bajas</Button>}</Box>}
    <Typography variant="h6" sx={{ mt: 3 }}>Total del día: {puedeProduccion ? `${produccion.data?.totalVendible ?? 0} huevos vendibles · ${produccion.data?.totalDescarte ?? 0} de descarte` : ''}{puedeProduccion && puedeMortalidad ? ' · ' : ''}{puedeMortalidad ? `${mortalidad.data?.totalMuertas ?? 0} bajas` : ''}</Typography>
    <List aria-label="Registros del día">{eventos.map((evento) => <ListItem key={`${evento.tipo}-${evento.datos.id}`} secondaryAction={esHoy && ((evento.tipo === 'recogida' && puedeProduccion) || (evento.tipo === 'bajas' && puedeMortalidad)) && <Box sx={{ display: 'flex', flexDirection: 'row' }}><Button size="small" onClick={() => evento.tipo === 'recogida' ? setRecogidaEditada(evento.datos) : setBajasEditadas(evento.datos)}>Editar</Button><Button size="small" onClick={() => setRegistroAEliminar(evento)}>Eliminar</Button></Box>}>
      <ListItemText primary={evento.tipo === 'bajas' ? `${evento.hora.slice(0, 5)} — ${evento.datos.cantidadMuertas} bajas` : `${evento.hora.slice(0, 5)} — ${formatearConteo(evento.datos.cantidadMaples, evento.datos.unidadesIncompletas)}`} secondary={evento.tipo === 'recogida' && evento.datos.totalDescarte > 0 ? `descarte ${formatearConteo(evento.datos.maplesDescarte, evento.datos.unidadesDescarte)}` : undefined} />
    </ListItem>)}</List>
    <RegistrarBajasDialog galponId={galponId} abierto={registrandoBajas} alCerrar={() => setRegistrandoBajas(false)} />
    <RegistrarRecogidaDialog galponId={galponId} abierto={registrandoRecogida} alCerrar={() => setRegistrandoRecogida(false)} />
    <EditarRecogidaDialog recogida={recogidaEditada} abierto={recogidaEditada !== null} alCerrar={() => setRecogidaEditada(null)} />
    <EditarBajasDialog registro={bajasEditadas} abierto={bajasEditadas !== null} alCerrar={() => setBajasEditadas(null)} />
    <Dialog open={registroAEliminar !== null} onClose={() => setRegistroAEliminar(null)}><DialogTitle>Eliminar registro</DialogTitle><DialogContent>El registro se desactiva; no se borra. Si era una baja, las gallinas vuelven al inventario.</DialogContent><DialogActions><Button onClick={() => setRegistroAEliminar(null)}>Cancelar</Button><Button onClick={() => registroAEliminar && eliminar.mutate(registroAEliminar)} disabled={eliminar.isPending}>Confirmar</Button></DialogActions></Dialog>
      {puedeVacunacion && <Box component="section" sx={{ mt: 4 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="h6">Vacunación</Typography>
        {puedeEstructura && <Box sx={{ display: 'flex', gap: 1 }}>
          <Button size="small" variant="outlined" onClick={() => setAsignandoPlan(true)}>Asignar plan</Button>
          {(tareasVacunacion.data ?? []).some((t) => t.estado === 'Pendiente') && <Button size="small" color="error" onClick={() => quitarPlan.mutate()} disabled={quitarPlan.isPending}>Quitar plan</Button>}
        </Box>}
      </Box>
      <Typography variant="body2" color={planVigente ? 'text.primary' : 'text.secondary'} sx={{ my: 1 }}>
        {planVigente ? `Plan asignado: ${planVigente}` : 'Sin plan asignado'}
      </Typography>
      {tareasVacunacion.isError && <Alert severity="error">No se pudo cargar la vacunación.</Alert>}
      <List aria-label="Historial de vacunación">
        {(tareasVacunacion.data ?? []).map((t: TareaVacunacionResumen) => (
          <ListItem key={t.id}>
            <ListItemText
              primary={<>{t.vacuna} <Chip size="small" label={t.estado} color={t.estado === 'Completada' ? 'success' : t.estado === 'Cancelada' ? 'default' : 'warning'} /></>}
              secondary={`Día ${t.edadDia} · programada ${t.fechaProgramada}${t.fechaAplicacion ? ` · aplicada ${t.fechaAplicacion}` : ''}${t.avesVacunadas ? ` · ${t.avesVacunadas} aves` : ''}${t.motivoCancelacion ? ` · motivo: ${t.motivoCancelacion}` : ''}`}
            />
          </ListItem>
        ))}
      </List>
      <AsignarPlanDialog galponId={galponId} abierto={asignandoPlan} alCerrar={() => setAsignandoPlan(false)} />
    </Box>}
  </Container>;
}
