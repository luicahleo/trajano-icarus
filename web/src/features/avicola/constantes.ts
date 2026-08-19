export const HUEVOS_POR_MAPLE = 30;
export function hoyIso(): string { const a = new Date(); const p=(n:number)=>String(n).padStart(2,'0'); return `${a.getFullYear()}-${p(a.getMonth()+1)}-${p(a.getDate())}`; }
