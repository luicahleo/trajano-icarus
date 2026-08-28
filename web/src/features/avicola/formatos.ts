import { HUEVOS_POR_MAPLE, hoyIso } from './constantes';
export function totalHuevos(maples: number, sueltos: number): number {
  return maples * HUEVOS_POR_MAPLE + sueltos;
}
export function formatearConteo(maples: number, sueltos: number): string {
  return `${maples} maples + ${sueltos} (= ${totalHuevos(maples, sueltos)})`;
}
export type ClasificacionTarea = 'Vencida' | 'Hoy' | 'Próxima';
export function clasificarTarea(
  fechaProgramada: string,
  hoy: string = hoyIso(),
): ClasificacionTarea {
  if (fechaProgramada < hoy) return 'Vencida';
  if (fechaProgramada === hoy) return 'Hoy';
  return 'Próxima';
}
