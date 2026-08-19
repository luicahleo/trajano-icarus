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
import type { EficienciaDia, MortalidadRegistro, RecogidaResumen } from '../../lib/tipos';
import { ApiError } from '../../lib/http';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { desactivarMortalidad, desactivarProduccion, listarMortalidad, listarProduccion, obtenerEficiencia, obtenerGalpon } from './api';
import { hoyIso } from './constantes';
import { formatearConteo } from './formatos';
import { RegistrarBajasDialog } from './RegistrarBajasDialog';
import { EditarBajasDialog } from './EditarBajasDialog';
import { EditarRecogidaDialog } from './EditarRecogidaDialog';

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
  const [recogidaEditada, setRecogidaEditada] = useState<RecogidaResumen | null>(null);
  const [bajasEditadas, setBajasEditadas] = useState<MortalidadRegistro | null>(null);
  const [registroAEliminar, setRegistroAEliminar] = useState<Evento | null>(null);
  const queryClient = useQueryClient();
  const esHoy = fecha === hoyIso();
  const galpon = useQuery({ queryKey: ['avicola', 'galpon', galponId], queryFn: () => obtenerGalpon(galponId), enabled: Boolean(galponId) });
  const produccion = useQuery({ queryKey: ['avicola', 'produccion', galponId, fecha], queryFn: () => listarProduccion(galponId, fecha), enabled: Boolean(galponId) });
  const mortalidad = useQuery({ queryKey: ['avicola', 'mortalidad', galponId, fecha], queryFn: () => listarMortalidad(galponId, fecha), enabled: Boolean(galponId) });
  const eficiencia = useQuery({ queryKey: ['avicola', 'eficiencia', galponId, fecha, fecha], queryFn: () => obtenerEficiencia(galponId, fecha, fecha), enabled: Boolean(galponId) });
  const puedeProduccion = useFuncionalidad('ProduccionHuevos');
  const puedeMortalidad = useFuncionalidad('Mortalidad');
  const eliminar = useMutation({
    mutationFn: (evento: Evento) => evento.tipo === 'recogida' ? desactivarProduccion(evento.datos.id) : desactivarMortalidad(evento.datos.id),
    onSuccess: () => { void queryClient.invalidateQueries({ queryKey: ['avicola', 'produccion'] }); void queryClient.invalidateQueries({ queryKey: ['avicola', 'mortalidad'] }); void queryClient.invalidateQueries({ queryKey: ['avicola', 'galpon'] }); void queryClient.invalidateQueries({ queryKey: ['avicola', 'eficiencia'] }); setRegistroAEliminar(null); },
  });

  if (galpon.isLoading || produccion.isLoading || mortalidad.isLoading || eficiencia.isLoading) {
    return <Container sx={{ py: 3 }}><CircularProgress aria-label="Cargando" /></Container>;
  }
  if (galpon.isError && galpon.error instanceof ApiError && galpon.error.status === 404) {
    return <Container sx={{ py: 3 }}><Alert severity="error">No se encontró el galpón.</Alert></Container>;
  }
  const error = galpon.error ?? produccion.error ?? mortalidad.error ?? eficiencia.error;
  if (error) {
    return <Container sx={{ py: 3 }}><Alert severity="error" action={<Button onClick={() => { void galpon.refetch(); void produccion.refetch(); void mortalidad.refetch(); void eficiencia.refetch(); }}>Reintentar</Button>}>No se pudo cargar el galpón.</Alert></Container>;
  }
  if (!galpon.data || !produccion.data || !mortalidad.data) return null;

  const eventos: Evento[] = [
    ...mortalidad.data.registros.map((datos) => ({ hora: datos.hora, tipo: 'bajas' as const, datos })),
    ...produccion.data.recogidas.map((datos) => ({ hora: datos.hora ?? '', tipo: 'recogida' as const, datos })),
  ].sort((a, b) => a.hora.localeCompare(b.hora));
  const dia = diaEficiencia(eficiencia.data?.dias);

  return <Container sx={{ py: 2 }}>
    <Box sx={{ display: 'flex', flexDirection: { xs: 'column', sm: 'row' }, justifyContent: 'space-between', alignItems: { sm: 'center' }, gap: 1 }}>
      <Box><Typography variant="h4">Galpón {galpon.data.numero}</Typography><Typography>{galpon.data.gallinasActuales} / {galpon.data.capacidadMaxima} gallinas</Typography></Box>
      <Box sx={{ display: 'flex', flexDirection: 'row', gap: 1, alignItems: 'center' }}>{dia && <Typography>{dia.eficiencia.toLocaleString('es-ES')} %</Typography>}{dia?.bajoUmbral && <Chip size="small" color="error" label="Bajo umbral — considerar descarte" />}<Button component={Link} to={`/avicola/galpones/${galponId}/eficiencia`}>Ver eficiencia</Button></Box>
    </Box>
    <TextField label="Fecha" type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: hoyIso() } }} sx={{ mt: 2 }} />
    {!esHoy && <Alert severity="info" sx={{ mt: 2 }}>Día sellado: no se puede corregir</Alert>}
    {esHoy && <Box sx={{ display: 'flex', flexDirection: 'row', gap: 1, mt: 2 }}>{puedeProduccion && <Button variant="contained">Registrar recogida</Button>}{puedeMortalidad && <Button variant="contained" onClick={() => setRegistrandoBajas(true)}>Registrar bajas</Button>}</Box>}
    <Typography variant="h6" sx={{ mt: 3 }}>Total del día: {produccion.data.totalVendible} huevos vendibles · {produccion.data.totalDescarte} de descarte · {mortalidad.data.totalMuertas} bajas</Typography>
    <List aria-label="Registros del día">{eventos.map((evento) => <ListItem key={`${evento.tipo}-${evento.datos.id}`} secondaryAction={esHoy && ((evento.tipo === 'recogida' && puedeProduccion) || (evento.tipo === 'bajas' && puedeMortalidad)) && <Box sx={{ display: 'flex', flexDirection: 'row' }}><Button size="small" onClick={() => evento.tipo === 'recogida' ? setRecogidaEditada(evento.datos) : setBajasEditadas(evento.datos)}>Editar</Button><Button size="small" onClick={() => setRegistroAEliminar(evento)}>Eliminar</Button></Box>}>
      <ListItemText primary={evento.tipo === 'bajas' ? `${evento.hora.slice(0, 5)} — ${evento.datos.cantidadMuertas} bajas` : `${evento.hora.slice(0, 5)} — ${formatearConteo(evento.datos.cantidadMaples, evento.datos.unidadesIncompletas)}`} secondary={evento.tipo === 'recogida' && evento.datos.totalDescarte > 0 ? `descarte ${formatearConteo(evento.datos.maplesDescarte, evento.datos.unidadesDescarte)}` : undefined} />
    </ListItem>)}</List>
    <RegistrarBajasDialog galponId={galponId} abierto={registrandoBajas} alCerrar={() => setRegistrandoBajas(false)} />
    <EditarRecogidaDialog recogida={recogidaEditada} abierto={recogidaEditada !== null} alCerrar={() => setRecogidaEditada(null)} />
    <EditarBajasDialog registro={bajasEditadas} abierto={bajasEditadas !== null} alCerrar={() => setBajasEditadas(null)} />
    <Dialog open={registroAEliminar !== null} onClose={() => setRegistroAEliminar(null)}><DialogTitle>Eliminar registro</DialogTitle><DialogContent>El registro se desactiva; no se borra. Si era una baja, las gallinas vuelven al inventario.</DialogContent><DialogActions><Button onClick={() => setRegistroAEliminar(null)}>Cancelar</Button><Button onClick={() => registroAEliminar && eliminar.mutate(registroAEliminar)} disabled={eliminar.isPending}>Confirmar</Button></DialogActions></Dialog>
  </Container>;
}
