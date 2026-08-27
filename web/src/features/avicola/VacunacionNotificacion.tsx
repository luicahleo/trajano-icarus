import { Box, Button, Chip, List, ListItem, ListItemText, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import type { Galpon, TareaVacunacionResumen } from '../../lib/tipos';
import { useAuth } from '../auth/AuthContext';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { obtenerNotificacionVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION } from './constantes';
import { CompletarTareaDialog } from './CompletarTareaDialog';
import { CancelarTareaDialog } from './CancelarTareaDialog';

export function VacunacionNotificacion({ galpones }: { galpones: Galpon[] }) {
  const puede = useFuncionalidad('Vacunacion');
  const { rol } = useAuth();
  const [completando, setCompletando] = useState<TareaVacunacionResumen | null>(null);
  const [cancelando, setCancelando] = useState<TareaVacunacionResumen | null>(null);
  const notificacion = useQuery({ queryKey: CLAVE_NOTIFICACION_VACUNACION, queryFn: obtenerNotificacionVacunacion, enabled: puede });
  if (!puede) return null;

  const numeroGalpon = (id: string) => galpones.find((g) => g.id === id)?.numero ?? '—';
  const itemTarea = (tarea: TareaVacunacionResumen) => (
    <ListItem key={tarea.id} secondaryAction={
      <Box sx={{ display: 'flex', gap: 1 }}>
        <Button size="small" variant="contained" onClick={() => setCompletando(tarea)}>Completar</Button>
        {rol === 'Cliente' && <Button size="small" color="error" onClick={() => setCancelando(tarea)}>Cancelar</Button>}
      </Box>
    }>
      <ListItemText
        primary={`Galpón ${numeroGalpon(tarea.galponId)} — ${tarea.vacuna}`}
        secondary={`Día ${tarea.edadDia} · programada ${tarea.fechaProgramada}${tarea.modoAplicacion ? ` · ${tarea.modoAplicacion}` : ''}`}
      />
    </ListItem>
  );

  const vencidasYHoy = notificacion.data?.vencidasYHoy ?? [];
  const proximas = notificacion.data?.proximas ?? [];

  return (
    <Box component="section" sx={{ my: 3 }}>
      <Typography variant="h5">Vacunación</Typography>
      {vencidasYHoy.length > 0 && (
        <>
          <Chip color="warning" size="small" label={`${vencidasYHoy.length} para hoy o vencidas`} sx={{ my: 1 }} />
          <List aria-label="Tareas de vacunación de hoy y vencidas">{vencidasYHoy.map(itemTarea)}</List>
        </>
      )}
      {proximas.length > 0 && (
        <>
          <Typography variant="h6" sx={{ mt: 2 }}>Próximas (7 días)</Typography>
          <List aria-label="Próximas vacunaciones">{proximas.map(itemTarea)}</List>
        </>
      )}
      {notificacion.data && vencidasYHoy.length === 0 && proximas.length === 0 && <Typography sx={{ mt: 1 }}>No hay vacunaciones pendientes ni próximas.</Typography>}
      <CompletarTareaDialog tarea={completando} abierto={completando !== null} alCerrar={() => setCompletando(null)} />
      <CancelarTareaDialog tarea={cancelando} abierto={cancelando !== null} alCerrar={() => setCancelando(null)} />
    </Box>
  );
}
