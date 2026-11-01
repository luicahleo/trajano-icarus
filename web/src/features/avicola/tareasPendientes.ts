import type { Galpon, TareaVacunacionResumen } from '../../lib/tipos';
import { hoyIso } from './constantes';

// Umbral fijo para recortar textos a dos líneas con «Ver más». Es fijo y no
// medido para que la regla sea comprobable en jsdom.
export const LIMITE_TEXTO_LARGO = 80;

export interface GrupoTareasGalpon {
  galponId: string;
  numero: string | null;
  tareas: TareaVacunacionResumen[];
}

const porFecha = (a: TareaVacunacionResumen, b: TareaVacunacionResumen) =>
  a.fechaProgramada.localeCompare(b.fechaProgramada);

// Regla de visibilidad del trabajador: pendientes con fecha programada anterior
// o igual a la fecha local. El servidor calcula «hoy» en UTC, y desde las 20:00
// de Bolivia ya incluye las tareas de mañana. Este filtro las descarta.
export function pendientesHastaHoy(
  tareas: TareaVacunacionResumen[],
  hoy: string = hoyIso(),
): TareaVacunacionResumen[] {
  return tareas
    .filter((t) => t.estado === 'Pendiente' && t.fechaProgramada <= hoy)
    .sort(porFecha);
}

// Grupos por galpón. Primero va el que tiene la tarea más antigua, de modo que
// las vencidas quedan arriba. El empate se resuelve por número de galpón, con
// orden numérico.
export function agruparPorGalpon(
  tareas: TareaVacunacionResumen[],
  galpones: Galpon[],
): GrupoTareasGalpon[] {
  const porGalpon = new Map<string, TareaVacunacionResumen[]>();
  for (const tarea of [...tareas].sort(porFecha)) {
    const lista = porGalpon.get(tarea.galponId) ?? [];
    lista.push(tarea);
    porGalpon.set(tarea.galponId, lista);
  }
  return [...porGalpon.entries()]
    .map(([galponId, lista]) => ({
      galponId,
      numero: galpones.find((g) => g.id === galponId)?.numero ?? null,
      tareas: lista,
    }))
    .sort(
      (a, b) =>
        porFecha(a.tareas[0], b.tareas[0]) ||
        (a.numero ?? '').localeCompare(b.numero ?? '', 'es', { numeric: true }),
    );
}

export function textoTituloGrupo(grupo: GrupoTareasGalpon): string {
  const cantidad = grupo.tareas.length;
  return `Galpón ${grupo.numero ?? '—'} · ${cantidad} ${cantidad === 1 ? 'pendiente' : 'pendientes'}`;
}

export function textoPendientesGalpon(cantidad: number): string {
  return cantidad === 1
    ? '1 tarea pendiente en este galpón'
    : `${cantidad} tareas pendientes en este galpón`;
}

// Texto de la única entrada de la sección «Vacunación» de la campana general.
export function textoResumenVacunacion(cantidad: number, vencidas: number): string {
  const base = cantidad === 1 ? '1 tarea pendiente' : `${cantidad} tareas pendientes`;
  if (vencidas === 0) return base;
  return `${base} (${vencidas} ${vencidas === 1 ? 'vencida' : 'vencidas'})`;
}
