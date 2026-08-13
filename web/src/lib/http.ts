import { obtenerCorrelationId } from './correlation';
import { clearAccessToken, getAccessToken, setAccessToken } from './session';
import type { SesionInfo } from './tipos';

// ApiError transporta solo status, code (title del ProblemDetails) y correlation
// ID. Nunca cuerpos, documentos, identificadores fiscales, credenciales ni
// tokens (anti-PII).
export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly correlationId?: string;

  constructor(o: { status: number; code?: string; correlationId?: string }) {
    super(o.code ?? `Error de servidor (${o.status})`);
    this.name = 'ApiError';
    this.status = o.status;
    this.code = o.code;
    this.correlationId = o.correlationId;
  }
}

function urlCompleta(ruta: string): string {
  return new URL(ruta, window.location.origin).toString();
}

function esRutaDeSesion(ruta: string): boolean {
  return ruta.startsWith('/identidad/sesion');
}

function conHeaders(init: RequestInit, cuerpo?: unknown): RequestInit {
  const cabeceras = new Headers(init.headers);
  cabeceras.set('X-Correlation-ID', obtenerCorrelationId());
  const token = getAccessToken();
  if (token) cabeceras.set('Authorization', `Bearer ${token}`);
  if (cuerpo !== undefined) cabeceras.set('Content-Type', 'application/json');
  return {
    ...init,
    headers: cabeceras,
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  };
}

let renovacionEnCurso: Promise<boolean> | null = null;
async function renovarSesionInterna(): Promise<boolean> {
  renovacionEnCurso ??= (async () => {
    const r = await fetch(urlCompleta('/identidad/sesion/renovar'), {
      method: 'POST',
      credentials: 'include',
      headers: { 'X-Correlation-ID': obtenerCorrelationId() },
    });
    if (!r.ok) return false;
    try {
      const datos = (await r.json()) as SesionInfo;
      setAccessToken(datos.accessToken);
      return true;
    } catch {
      return false;
    }
  })().finally(() => {
    renovacionEnCurso = null;
  });
  return renovacionEnCurso;
}

export async function renovarSesion(): Promise<boolean> {
  return renovarSesionInterna();
}

async function errorDesde(respuesta: Response): Promise<ApiError> {
  let code: string | undefined;
  try {
    const cuerpo = (await respuesta.json()) as { title?: string };
    code = cuerpo.title;
  } catch {
    // el cuerpo puede no ser JSON (204 o 401 vacíos)
  }
  return new ApiError({
    status: respuesta.status,
    code,
    correlationId: respuesta.headers.get('X-Correlation-ID') ?? undefined,
  });
}

export async function peticion<T>(o: {
  ruta: string;
  metodo?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  cuerpo?: unknown;
}): Promise<T> {
  const { ruta, metodo = 'GET', cuerpo } = o;
  const original = new Request(urlCompleta(ruta), conHeaders({ method: metodo, credentials: 'include' }, cuerpo));
  const reintentable = !esRutaDeSesion(ruta);
  let respuesta = await fetch(original);

  if (respuesta.status === 401 && reintentable && (await renovarSesionInterna())) {
    respuesta = await fetch(original.clone());
  } else if (respuesta.status === 401 && reintentable) {
    clearAccessToken();
  }

  if (!respuesta.ok) throw await errorDesde(respuesta);
  if (respuesta.status === 204) return undefined as T;
  return (await respuesta.json()) as T;
}
