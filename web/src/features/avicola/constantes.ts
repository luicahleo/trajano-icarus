export const HUEVOS_POR_MAPLE = 30;
export const CLAVE_GRANJAS = ['avicola', 'granjas'] as const;
export const CLAVE_PROGRAMAS_VACUNACION = ['vacunacion', 'programas'] as const;
export const CLAVE_NOTIFICACION_VACUNACION = ['vacunacion', 'notificacion'] as const;
export const CLAVE_TAREAS_VACUNACION = ['vacunacion', 'tareas'] as const;
export function hoyIso(): string { const a = new Date(); const p=(n:number)=>String(n).padStart(2,'0'); return `${a.getFullYear()}-${p(a.getMonth()+1)}-${p(a.getDate())}`; }
