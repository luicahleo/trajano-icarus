import { Box, Button, Chip, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { TablaDatos } from '../../app/ui/TablaDatos';
import type { Columna } from '../../app/ui/TablaDatos';
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
  const notificacion = useQuery({
    queryKey: CLAVE_NOTIFICACION_VACUNACION,
    queryFn: obtenerNotificacionVacunacion,
    enabled: puede,
  });
  if (!puede) return null;

  const numeroGalpon = (id: string) => galpones.find((g) => g.id === id)?.numero ?? '—';
  // La aplicación es el modo indicado en el plan; si no lo trae, sirve la
  // observación programada del ítem.
  const aplicacion = (tarea: TareaVacunacionResumen) =>
    tarea.modoAplicacion ?? tarea.observacionesProgramadas ?? '—';

  const columnas: Columna<TareaVacunacionResumen>[] = [
    { clave: 'galpon', encabezado: 'Galpón', render: (t) => numeroGalpon(t.galponId) },
    { clave: 'vacuna', encabezado: 'Vacuna', render: (t) => t.vacuna },
    { clave: 'plan', encabezado: 'Plan', render: (t) => t.programaNombre ?? '—' },
    { clave: 'dia', encabezado: 'Día', alinear: 'right', render: (t) => t.edadDia },
    { clave: 'programada', encabezado: 'Programada', render: (t) => t.fechaProgramada },
    {
      clave: 'aplicacion',
      encabezado: 'Aplicación',
      render: (t) => (
        <Typography variant="body2" sx={{ maxWidth: 320 }}>
          {aplicacion(t)}
        </Typography>
      ),
    },
    {
      clave: 'acciones',
      encabezado: '',
      alinear: 'right',
      render: (t) => (
        <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end' }}>
          <Button size="small" variant="contained" onClick={() => setCompletando(t)}>
            Completar
          </Button>
          {rol === 'Cliente' && (
            <Button size="small" color="error" onClick={() => setCancelando(t)}>
              Cancelar
            </Button>
          )}
        </Box>
      ),
    },
  ];

  const vencidasYHoy = notificacion.data?.vencidasYHoy ?? [];
  const proximas = notificacion.data?.proximas ?? [];

  return (
    <Box component="section" sx={{ my: 3 }}>
      <Typography variant="h5">Vacunación</Typography>
      {vencidasYHoy.length > 0 && (
        <>
          <Chip
            color="warning"
            size="small"
            label={`${vencidasYHoy.length} para hoy o vencidas`}
            sx={{ my: 1 }}
          />
          <TablaDatos
            etiqueta="Tareas de vacunación de hoy y vencidas"
            columnas={columnas}
            filas={vencidasYHoy}
            claveDeFila={(t) => t.id}
          />
        </>
      )}
      {proximas.length > 0 && (
        <>
          <Typography variant="h6" sx={{ mt: 3, mb: 1 }}>
            Próximas (7 días)
          </Typography>
          <TablaDatos
            etiqueta="Próximas vacunaciones"
            columnas={columnas}
            filas={proximas}
            claveDeFila={(t) => t.id}
          />
        </>
      )}
      {notificacion.data && vencidasYHoy.length === 0 && proximas.length === 0 && (
        <Typography sx={{ mt: 1 }}>No hay vacunaciones pendientes ni próximas.</Typography>
      )}
      <CompletarTareaDialog
        tarea={completando}
        abierto={completando !== null}
        alCerrar={() => setCompletando(null)}
      />
      <CancelarTareaDialog
        tarea={cancelando}
        abierto={cancelando !== null}
        alCerrar={() => setCancelando(null)}
      />
    </Box>
  );
}
