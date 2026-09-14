import { peticion } from '../../lib/http';
import { consultaPaginada } from '../../lib/paginacion';
import type { Pagina, PeticionPaginada } from '../../lib/paginacion';
import type { Granja } from '../../lib/tipos';

// SP9B: los despachos son deliberadamente online (spec), igual que los pedidos
// de alimento: sin cola offline, sin IndexedDB ni precalentado. Sin red la
// feature falla de forma explícita.

export interface LineaDespacho {
  tamano: string;
  cantidadAmarras: number;
  unidadesSueltas: number;
}

export interface DatosDespacho {
  lineas: LineaDespacho[];
}

export interface DespachoHuevoResumen {
  id: string;
  folio: string;
  numero: number;
  granjaId: string;
  creadoPorTrabajadorId: string | null;
  estado: string;
  fechaDespacho: string | null;
  totalAmarras: number;
  totalHuevos: number;
  totalBs: number | null;
}

export interface DetalleDespachoHuevo {
  id: string;
  tamano: string;
  cantidadAmarras: number;
  unidadesSueltas: number;
  cantidadHuevos: number;
  precioUnitarioCongelado: number | null;
  subtotal: number | null;
}

export interface DespachoHuevoDetalle {
  id: string;
  estado: string;
  fechaDespacho: string | null;
  totalAmarras: number;
  totalHuevos: number;
  totalBs: number | null;
  detalles: DetalleDespachoHuevo[];
}

export interface DetallePrecioHuevoVigente {
  id: string;
  tamano: string;
  precioAlProductor: number;
  precioActualDocumento: number | null;
  precioUnitario: number;
}

export interface PrecioHuevoVigente {
  id: string;
  fechaNotificacion: string;
  fechaVigencia: string;
  estado: string;
  servicio: number;
  detalles: DetallePrecioHuevoVigente[];
}

// Filtros del listado del tenant (spec 2026-09-14): sin presentación, que no
// existe en el despacho de huevo. El autor no viaja a CAISY.
export interface FiltrosDespachos {
  granjaId?: string;
  estado?: string;
  desde?: string;
  hasta?: string;
  creadoPorTrabajadorId?: string;
  numero?: string;
}

export const listarDespachos = (
  paginacion: PeticionPaginada,
  filtros: Record<string, string | undefined> = {},
) =>
  peticion<Pagina<DespachoHuevoResumen>>({
    ruta: `/despachos-huevo?${consultaPaginada(paginacion, filtros)}`,
  });

export const listarGranjas = () => peticion<Granja[]>({ ruta: '/granjas' });

export const obtenerDespacho = (id: string) =>
  peticion<DespachoHuevoDetalle>({ ruta: `/despachos-huevo/${id}` });

export const crearDespacho = (datos: DatosDespacho) =>
  peticion<{ id: string }>({ ruta: '/despachos-huevo', metodo: 'POST', cuerpo: datos });

export const editarDespacho = (id: string, datos: DatosDespacho) =>
  peticion<void>({ ruta: `/despachos-huevo/${id}`, metodo: 'PUT', cuerpo: datos });

export const borrarDespacho = (id: string) =>
  peticion<void>({ ruta: `/despachos-huevo/${id}`, metodo: 'DELETE' });

// Despachar (spec SP9B): foto obligatoria de la nota de entrega en la misma
// operación; el backend congela el precio unitario vigente (productor +
// servicio) por tamaño.
export const despacharDespacho = (id: string, archivo: File) => {
  const formData = new FormData();
  formData.append('archivo', archivo, archivo.name);
  return peticion<void>({
    ruta: `/despachos-huevo/${id}/despachar`,
    metodo: 'POST',
    cuerpo: formData,
  });
};

// El catálogo de precios lo administra CAISY (SP9A), pero el tenant necesita
// conocer el precio vigente antes de despachar: 404 cuando no hay vigente.
export const obtenerPrecioHuevoVigente = () =>
  peticion<PrecioHuevoVigente>({ ruta: '/despachos-huevo/precios-vigentes' });

export interface AjusteCreditoHuevo {
  id: string;
  monto: number;
  motivo: string;
  fecha: string; // yyyy-MM-dd
}

export interface BalanceCreditoHuevo {
  saldoDisponible: number;
  // Parte del saldo recibida dentro de la ventana de referencia. Informativo:
  // NO se resta del saldo (corrección 2026-09-14).
  recibidoReciente: number;
  // Lo manda el backend en vez de fijar 14 acá, para que el texto no mienta
  // si la constante cambia.
  diasReferencia: number;
  ajustes: AjusteCreditoHuevo[];
}

// Crédito disponible por despachos de huevo (spec SP9): informativo para
// decidir cuánto alimento pedir. El backend lo resuelve por ClienteId de la
// sesión (SP9).
export const obtenerBalanceCreditoHuevo = () =>
  peticion<BalanceCreditoHuevo>({ ruta: '/despachos-huevo/credito' });

// Bandeja de novedades del tenant (spec SP9F): los endpoints ya existían
// desde SP9C y hasta ahora ninguna pantalla los consumía. El backend filtra
// por rol qué tipos devuelve, así que acá no hay que decidir nada: se muestra
// lo que llega. `contador` viene del backend ya filtrado.
export interface NotificacionDespachoHuevo {
  id: string;
  tipo: string;
  despachoHuevoId: string | null;
  fechaUtc: string;
  leida: boolean;
  meta: string | null;
}

export const listarNotificacionesDespachoHuevo = () =>
  peticion<{ items: NotificacionDespachoHuevo[]; contador: number }>({
    ruta: '/despachos-huevo/notificaciones',
  });

export const marcarNotificacionDespachoHuevoLeida = (id: string) =>
  peticion<void>({
    ruta: `/despachos-huevo/notificaciones/${id}/marcar-leida`,
    metodo: 'POST',
  });
