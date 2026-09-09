// Etiquetas y constantes de dominio (glosario avícola): una amarra son 180
// huevos (6 maples) y las unidades sueltas de una línea siempre son menos de
// una amarra (máximo 179).
export const ETIQUETAS_ESTADO: Record<string, string> = {
  Borrador: 'Borrador',
  Despachado: 'Despachado',
};

export const COLOR_ESTADO: Record<string, 'default' | 'info' | 'error' | 'success'> = {
  Borrador: 'default',
  Despachado: 'success',
};

export const ETIQUETAS_TAMANO: Record<string, string> = {
  Extra: 'Extra',
  Primera: 'Primera',
  Segunda: 'Segunda',
  Tercera: 'Tercera',
  Cuarta: 'Cuarta',
  Quinta: 'Quinta',
};

export const HUEVOS_POR_AMARRA = 180;
export const MAX_UNIDADES_SUELTAS = 179;

export function formatoFecha(iso: string): string {
  const [anio, mes, dia] = iso.split('-');
  return dia && mes && anio ? `${dia}/${mes}/${anio}` : iso;
}

export function formatoMoneda(valor: number): string {
  return new Intl.NumberFormat('es-BO', {
    style: 'currency',
    currency: 'BOB',
    maximumFractionDigits: 2,
  }).format(valor);
}
