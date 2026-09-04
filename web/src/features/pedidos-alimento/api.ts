import { peticion, peticionBlob } from '../../lib/http';

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

export interface LineaEntregaPedido {
  tipoAlimento: string;
  cantidadEntregada: number;
  equivalentes40Kg: number;
}

export interface DocumentoNota {
  id: string;
  nombreSeguro: string;
  mime: string;
  tamanoBytes: number;
  activo: boolean;
}

export interface EntregaPedido {
  numeroNota: string;
  fechaNota: string;
  fechaDespacho: string;
  totalNetoInformado: number | null;
  totalDespachado: number;
  lineas: LineaEntregaPedido[];
  documentos: DocumentoNota[];
}

export interface LineaRecepcionPedido {
  tipoAlimento: string;
  cantidadRecibida: number;
  equivalentes40Kg: number;
}

export interface DiferenciaRecepcion {
  tipoAlimento: string;
  cantidadRecibida: number;
  cantidadEntregada: number;
  diferencia: number;
}

export interface RecepcionPedido {
  fechaRecepcion: string;
  totalRecibido: number;
  lineas: LineaRecepcionPedido[];
  diferencias: DiferenciaRecepcion[];
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
  entrega: EntregaPedido | null;
  recepcion: RecepcionPedido | null;
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

export interface LineaRecepcionDatos {
  tipoAlimento: string;
  cantidadRecibida: number;
}

// Recepción por línea (spec SP8C): el tenant confirma desde Despachado la
// cantidad realmente recibida; el estado final lo decide el backend.
export const recibirPedido = (id: string, lineas: LineaRecepcionDatos[]) =>
  peticion<void>({ ruta: `/pedidos-alimento/${id}/recibir`, metodo: 'POST', cuerpo: { lineas } });

// Vista derivada de un respaldo (inline, sin metadatos) para mostrar en el
// detalle; el original se descarga como adjunto autorizado (spec SP8C).
export const obtenerVistaDocumentoNota = (pedidoId: string, documentoId: string) =>
  peticionBlob({ ruta: `/pedidos-alimento/${pedidoId}/nota/documentos/${documentoId}/vista` });

export const obtenerOriginalDocumentoNota = (pedidoId: string, documentoId: string) =>
  peticionBlob({ ruta: `/pedidos-alimento/${pedidoId}/nota/documentos/${documentoId}/original` });

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
