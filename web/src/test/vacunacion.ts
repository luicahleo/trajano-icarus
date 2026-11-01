import type { TareaVacunacionResumen } from '../lib/tipos';

// Fecha local AAAA-MM-DD desplazada en días respecto de hoy.
export function isoDesplazado(dias: number): string {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() + dias);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${fecha.getFullYear()}-${p(fecha.getMonth() + 1)}-${p(fecha.getDate())}`;
}

export function tareaVacunacion(
  id: string,
  galponId: string,
  fechaProgramada: string,
  extra: Partial<TareaVacunacionResumen> = {},
): TareaVacunacionResumen {
  return {
    id,
    galponId,
    edadDia: 7,
    vacuna: `Vacuna ${id}`,
    modoAplicacion: null,
    fechaProgramada,
    estado: 'Pendiente',
    fechaAplicacion: null,
    avesVacunadas: null,
    observacionesProgramadas: null,
    observacionesAplicacion: null,
    motivoCancelacion: null,
    programaNombre: 'PLAN CAISY 1000',
    ...extra,
  };
}
