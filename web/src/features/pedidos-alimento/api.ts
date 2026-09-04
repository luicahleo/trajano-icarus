import { peticion } from '../../lib/http';

// SP8B: los pedidos son deliberadamente online (spec) — sin cola offline, sin
// IndexedDB ni precalentado. Sin red la feature falla de forma explícita.

export interface LineaPedido {
  tipoAlimento: string;
  presentacion: 'Bolsa' | 'Granel';
  cantidad: number;
}

export interface DatosPedido {
  detalles: LineaPedido[];
}

export interface PedidoResumen {
  id: string;
  estado: string;
  presentacion: string;
  fechaPedido: string | null;
  fechaEntregaEstimada: string | null;
  totalSolicitado: number | null;
  cantidadLineas: number;
}

export interface LineaPedidoDetalle {
  id: string;
  tipoAlimento: string;
  presentacion: string;
  cantidadSolicitada: number;
  equivalentes40Kg: number;
  precioFinalPor40Kg: number | null;
  subtotalSolicitado: number | null;
  notificacionPreciosAlimentosId: string | null;
}

export interface TransicionPedido {
  estadoOrigen: string;
  estadoDestino: string;
  fechaUtc: string;
  motivo: string | null;
  fechaEntregaEstimada: string | null;
}

export interface PedidoDetalle {
  id: string;
  clienteId: string;
  estado: string;
  fechaPedido: string | null;
  fechaEntregaEstimada: string | null;
  totalSolicitado: number | null;
  lineas: LineaPedidoDetalle[];
  historial: TransicionPedido[];
}

export const listarPedidos = () =>
  peticion<PedidoResumen[]>({ ruta: '/pedidos-alimento' });

export const obtenerPedido = (id: string) =>
  peticion<PedidoDetalle>({ ruta: `/pedidos-alimento/${id}` });

export const crearPedido = (datos: DatosPedido) =>
  peticion<{ id: string }>({ ruta: '/pedidos-alimento', metodo: 'POST', cuerpo: datos });

export const editarPedido = (id: string, datos: DatosPedido) =>
  peticion<void>({ ruta: `/pedidos-alimento/${id}`, metodo: 'PUT', cuerpo: datos });

export const borrarPedido = (id: string) =>
  peticion<void>({ ruta: `/pedidos-alimento/${id}`, metodo: 'DELETE' });

export const enviarPedido = (id: string) =>
  peticion<void>({ ruta: `/pedidos-alimento/${id}/enviar`, metodo: 'POST' });

export interface DetallePrecioVigente {
  tipoAlimento: string;
  presentacion: string;
  precioFinalPor40Kg: number;
  edadDesdeDias: number | null;
  edadHastaDias: number | null;
}

export interface PublicacionVigente {
  id: string;
  estado: string;
  aporteCaisy: number;
  fondo: number;
  servicios: number;
  detalles: DetallePrecioVigente[];
}

export const obtenerPrecioVigente = () =>
  peticion<PublicacionVigente>({ ruta: '/pedidos-alimento/precios-vigentes' });

export interface CupoPedidos {
  enviados: number;
  maximo: number;
  desde: string;
  hasta: string;
}

export const obtenerCupo = () => peticion<CupoPedidos>({ ruta: '/pedidos-alimento/cupo' });

export interface NotificacionPedido {
  id: string;
  tipo: string;
  pedidoId: string;
  fechaUtc: string;
  leida: boolean;
  meta: string | null;
}

export const listarNotificaciones = () =>
  peticion<{ items: NotificacionPedido[]; contador: number }>({
    ruta: '/pedidos-alimento/notificaciones',
  });

export const marcarNotificacionLeida = (id: string) =>
  peticion<void>({ ruta: `/pedidos-alimento/notificaciones/${id}/marcar-leida`, metodo: 'POST' });
