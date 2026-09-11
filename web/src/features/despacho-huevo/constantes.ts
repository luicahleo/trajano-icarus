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

// Mensajes de la bandeja de novedades (spec SP9F). Mismo patrón que
// mensajeNotificacion en pedidos-alimento/constantes.ts. El detalle del
// ajuste viaja en `meta` como texto plano y la página lo muestra crudo: no
// hace falta parsearlo.
export function mensajeNotificacionDespachoHuevo(tipo: string): string {
  switch (tipo) {
    case 'DespachoRecibido':
      return 'CAISY confirmó la recepción de un despacho de huevo.';
    case 'AjusteCredito':
      return 'Se ajustó tu crédito de huevo por una corrección de precio.';
    case 'CreditoInsuficiente':
      return 'Se envió un pedido de alimento con crédito de huevo insuficiente.';
    default:
      return 'Hubo una novedad en un despacho de huevo.';
  }
}

// Los formatos de dinero viven en lib/formatos.ts, compartidos con
// pedidos-alimento. El precio por unidad de huevo conserva su nombre de
// dominio: son los 4 decimales exactos de la publicación de CAISY.
export { formatoMoneda, formatoMonedaExacta as formatoPrecioUnitario } from '../../lib/formatos';
