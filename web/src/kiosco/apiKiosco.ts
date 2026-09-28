import { ApiError } from '../lib/http';

// Transporte propio del kiosco: usa solo la cookie restringida (credentials
// include), exige el encabezado antiforgery y nunca adjunta el token
// administrativo ni intenta refrescarlo. Sin cola offline.
const CABECERA_KIOSCO = 'X-Icarus-Kiosco';

async function peticionKiosco<T>(
  ruta: string,
  opciones: { metodo?: 'GET' | 'POST' | 'DELETE'; cuerpo?: unknown } = {},
): Promise<T> {
  const { metodo = 'GET', cuerpo } = opciones;
  const url = new URL(`/api${ruta}`, window.location.origin).toString();
  const respuesta = await fetch(
    new Request(url, {
      method: metodo,
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        [CABECERA_KIOSCO]: '1',
      },
      body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
    }),
  );

  if (!respuesta.ok) {
    let code: string | undefined;
    try {
      code = ((await respuesta.json()) as { title?: string }).title;
    } catch {
      // respuesta sin cuerpo (401/204)
    }
    throw new ApiError({ status: respuesta.status, code });
  }

  if (respuesta.status === 204) return undefined as T;
  return (await respuesta.json()) as T;
}

export interface EstadoSesionKiosco {
  expiraEnUtc: string;
}

export async function estadoSesionKiosco(): Promise<EstadoSesionKiosco | null> {
  try {
    return await peticionKiosco<EstadoSesionKiosco>('/control-acceso/kiosco/sesion');
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null;
    throw error;
  }
}

export async function activarKiosco(datos: {
  email: string;
  contrasena: string;
}): Promise<void> {
  await peticionKiosco<void>('/control-acceso/kiosco/sesion', {
    metodo: 'POST',
    cuerpo: datos,
  });
}

export type EstadoResultadoMarcacion = 'Registrada' | 'PropuestaSalida' | 'Rechazada';

export interface ResultadoMarcacionKiosco {
  estado: EstadoResultadoMarcacion;
  trabajadorId: string | null;
  nombreCompleto: string | null;
  tipo: 'Entrada' | 'Salida' | null;
  instanteUtc: string | null;
  propuestaId: string | null;
  expiraPropuestaUtc: string | null;
  motivo: string | null;
}

export async function marcarKiosco(datos: {
  accion: 'Entrada' | 'Salida';
  muestraBase64: string;
  formato: string;
  claveIdempotencia: string;
}): Promise<ResultadoMarcacionKiosco> {
  return peticionKiosco<ResultadoMarcacionKiosco>('/control-acceso/kiosco/marcaciones', {
    metodo: 'POST',
    cuerpo: datos,
  });
}

export async function confirmarSalidaKiosco(
  propuestaId: string,
): Promise<ResultadoMarcacionKiosco> {
  return peticionKiosco<ResultadoMarcacionKiosco>(
    `/control-acceso/kiosco/marcaciones/${propuestaId}/confirmar`,
    { metodo: 'POST' },
  );
}
