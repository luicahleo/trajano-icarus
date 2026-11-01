import { useQuery, useQueryClient } from '@tanstack/react-query';
import MarkEmailReadRoundedIcon from '@mui/icons-material/MarkEmailReadRounded';
import type { FuenteNotificaciones } from '../../app/notificaciones/fuentes';
import { useAuth } from '../auth/AuthContext';
import {
  listarNotificacionesAcceso,
  marcarNotificacionAccesoLeida,
  type NotificacionAccesoResumen,
} from './api';

export const CLAVE_NOTIFICACIONES_ACCESO = ['control-acceso', 'notificaciones'] as const;
const INTERVALO_REFRESCO_MS = 60_000;

function textoNotificacion(notificacion: NotificacionAccesoResumen): string {
  if (notificacion.tipo === 'IncidenciaAcceso') return 'Hay una incidencia de marcación por revisar';
  return 'Nueva notificación';
}

function horaResumida(fechaUtc: string): string {
  return new Intl.DateTimeFormat('es-BO', {
    timeZone: 'America/La_Paz',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(fechaUtc));
}

// Mismo comportamiento que la campana anterior: solo con el módulo
// ControlAcceso, contador de sin leer y marcar leída al elegir.
export function useFuenteNotificacionesAcceso(): FuenteNotificaciones {
  const { modulos } = useAuth();
  const queryClient = useQueryClient();
  const visible = modulos.includes('ControlAcceso');
  const { data, isError } = useQuery({
    queryKey: CLAVE_NOTIFICACIONES_ACCESO,
    queryFn: listarNotificacionesAcceso,
    enabled: visible,
    refetchInterval: INTERVALO_REFRESCO_MS,
  });
  const items = visible ? (data?.items ?? []) : [];
  return {
    id: 'control-acceso',
    seccion: 'Control de acceso',
    visible,
    conteo: visible ? (data?.contador ?? 0) : 0,
    entradas: items.map((n) => ({
      id: n.id,
      texto: textoNotificacion(n),
      detalle: horaResumida(n.fechaUtc),
      icono: <MarkEmailReadRoundedIcon fontSize="small" />,
      destino: '/control-acceso/incidencias',
      alSeleccionar: async () => {
        await marcarNotificacionAccesoLeida(n.id);
        void queryClient.invalidateQueries({ queryKey: CLAVE_NOTIFICACIONES_ACCESO });
      },
    })),
    error: visible && isError && data === undefined,
  };
}
