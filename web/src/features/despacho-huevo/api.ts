import { peticion } from '../../lib/http';

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
  precioProductorCongelado: number | null;
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

export const listarDespachos = () => peticion<DespachoHuevoResumen[]>({ ruta: '/despachos-huevo' });

export const obtenerDespacho = (id: string) =>
  peticion<DespachoHuevoDetalle>({ ruta: `/despachos-huevo/${id}` });

export const crearDespacho = (datos: DatosDespacho) =>
  peticion<{ id: string }>({ ruta: '/despachos-huevo', metodo: 'POST', cuerpo: datos });

export const editarDespacho = (id: string, datos: DatosDespacho) =>
  peticion<void>({ ruta: `/despachos-huevo/${id}`, metodo: 'PUT', cuerpo: datos });

export const borrarDespacho = (id: string) =>
  peticion<void>({ ruta: `/despachos-huevo/${id}`, metodo: 'DELETE' });

// Despachar (spec SP9B): foto obligatoria de la nota de entrega en la misma
// operación; el backend congela el precio al productor vigente por tamaño.
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
