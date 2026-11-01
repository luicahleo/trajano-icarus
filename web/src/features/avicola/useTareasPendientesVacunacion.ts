import { useQuery } from '@tanstack/react-query';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { obtenerNotificacionVacunacion } from './api';
import { CLAVE_NOTIFICACION_VACUNACION } from './constantes';
import { pendientesHastaHoy } from './tareasPendientes';

export const INTERVALO_REFRESCO_VACUNACION_MS = 60_000;

// Comparte la clave con VacunacionNotificacion y con los diálogos de completar,
// cancelar y asignar, que ya la invalidan. Así la insignia baja al instante.
// Sin red, React Query pausa la consulta y conserva el último dato.
export function useTareasPendientesVacunacion(habilitado = true) {
  const puede = useFuncionalidad('Vacunacion');
  const consulta = useQuery({
    queryKey: CLAVE_NOTIFICACION_VACUNACION,
    queryFn: obtenerNotificacionVacunacion,
    enabled: puede && habilitado,
    refetchInterval: INTERVALO_REFRESCO_VACUNACION_MS,
  });
  // Se calcula en cada render, sin memo: la fecha local puede cambiar aunque
  // los datos no cambien.
  const pendientes = pendientesHastaHoy(consulta.data?.vencidasYHoy ?? []);
  return {
    pendientes,
    cargando: consulta.isLoading,
    // Error solo si no hay datos previos: un refresco fallido conserva lo último.
    error: consulta.isError && consulta.data === undefined,
    reintentar: () => void consulta.refetch(),
  };
}
