import { useFuenteNotificacionesVacunacion } from '../../features/avicola/useFuenteNotificacionesVacunacion';
import { useFuenteNotificacionesAcceso } from '../../features/control-acceso/useFuenteNotificacionesAcceso';
import type { FuenteNotificaciones } from './fuentes';

// Orden de las secciones del menú. Cada hook se llama siempre y en el mismo
// orden; la visibilidad va dentro de la fuente (visible + enabled), nunca en un
// montaje condicional. Fuente nueva: una línea más aquí.
export function useFuentesNotificaciones(): FuenteNotificaciones[] {
  return [useFuenteNotificacionesVacunacion(), useFuenteNotificacionesAcceso()];
}
