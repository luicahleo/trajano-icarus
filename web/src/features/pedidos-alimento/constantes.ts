import type { NotificacionPedido } from './api';

// Etiquetas y mensajes compuestos en la UI (spec SP8): la notificación solo
// conserva datos técnicos y aquí se traduce a texto en español.
export const ETIQUETAS_ESTADO: Record<string, string> = {
  Borrador: 'Borrador',
  Solicitado: 'Solicitado',
  Rechazado: 'Rechazado',
  Aceptado: 'Aceptado',
  Despachado: 'Despachado',
  RecibidoConforme: 'Recibido conforme',
  RecibidoConDiferencias: 'Recibido con diferencias',
};

export const COLOR_ESTADO: Record<string, 'default' | 'info' | 'error' | 'success'> = {
  Borrador: 'default',
  Solicitado: 'info',
  Rechazado: 'error',
  Aceptado: 'success',
  Despachado: 'info',
  RecibidoConforme: 'success',
  RecibidoConDiferencias: 'default',
};

export const ETIQUETAS_TIPO_ALIMENTO: Record<string, string> = {
  Preiniciador: 'Preiniciador',
  Iniciador: 'Iniciador',
  Crecimiento: 'Crecimiento',
  Finalizador: 'Finalizador',
  PosturaUno: 'Postura 1',
  PosturaDos: 'Postura 2',
};

export function mensajeNotificacion(notificacion: NotificacionPedido): string {
  const fechaMeta = notificacion.meta
    ? (/"fechaEntregaEstimada":\s*"([^"]+)"/.exec(notificacion.meta)?.[1] ?? null)
    : null;
  const fecha = fechaMeta ? ` al ${formatoFecha(fechaMeta)}` : '';
  switch (notificacion.tipo) {
    case 'PedidoSolicitado':
      return 'Se envió un pedido a CAISY.';
    case 'PedidoReenviado':
      return 'Se reenvió un pedido corregido a CAISY.';
    case 'PedidoDevuelto':
      return 'CAISY devolvió un pedido para corrección. Revisá el motivo en el detalle.';
    case 'PedidoRechazado':
      return 'CAISY rechazó un pedido. Consultá el motivo en el detalle.';
    case 'PedidoAceptado':
      return 'CAISY aceptó un pedido.';
    case 'EntregaEstimadaActualizada':
      return `CAISY actualizó la entrega estimada de un pedido${fecha}.`;
    default:
      return 'Hubo una novedad en un pedido.';
  }
}

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
