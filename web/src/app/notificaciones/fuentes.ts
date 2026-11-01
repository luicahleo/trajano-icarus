import type { ReactNode } from 'react';

// Contrato de una fuente de la campana general. Para añadir una fuente: crear
// su hook junto a su feature y sumarlo en useFuentesNotificaciones. La campana
// no cambia.
export interface EntradaNotificacion {
  id: string;
  texto: string;
  detalle?: string;
  icono?: ReactNode;
  destino: string;
  // Acción previa a navegar, por ejemplo marcar como leída. Si falla, no se navega.
  alSeleccionar?: () => Promise<void>;
}

export interface FuenteNotificaciones {
  id: string;
  seccion: string;
  // false: el usuario no puede ver la sección y su consulta no se ejecuta.
  visible: boolean;
  conteo: number;
  entradas: EntradaNotificacion[];
  // La consulta falló y no hay datos previos.
  error: boolean;
}

export const MENSAJE_ERROR_FUENTE = 'No se pudieron cargar estas notificaciones.';

export function totalPendientes(fuentes: FuenteNotificaciones[]): number {
  return fuentes.filter((f) => f.visible).reduce((suma, f) => suma + f.conteo, 0);
}

export function etiquetaCampana(total: number): string {
  if (total === 0) return 'Notificaciones sin pendientes';
  return `Notificaciones: ${total} ${total === 1 ? 'pendiente' : 'pendientes'}`;
}
