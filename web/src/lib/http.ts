import { crearCorrelationId } from './correlation';
import { crearErrorId, reportarDiagnostico } from './diagnosticos';
import { avisarActividadApi } from './offline/actividadApi';
import { clearAccessToken, getAccessToken, setAccessToken } from './session';
import { obtenerSesionId, registrarEventoFlujo, sanitizarRuta } from './sesionDiagnostico';
import type { SesionInfo } from './tipos';

// ApiError transporta solo referencias técnicas seguras. Nunca cuerpos,
// documentos, identificadores fiscales, credenciales ni tokens (anti-PII).
export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly correlationId?: string;
  readonly traceId?: string;
  readonly errorId?: string;
  readonly erroresValidacion?: Readonly<Record<string, readonly string[]>>;

  constructor(o: {
    status: number;
    code?: string;
    correlationId?: string;
    traceId?: string;
    errorId?: string;
    erroresValidacion?: Readonly<Record<string, readonly string[]>>;
  }) {
    super(o.code ?? `Error de servidor (${o.status})`);
    this.name = 'ApiError';
    this.status = o.status;
    this.code = o.code;
    this.correlationId = o.correlationId;
    this.traceId = o.traceId;
    this.errorId = o.errorId;
    this.erroresValidacion = o.erroresValidacion;
  }
}

function urlCompleta(ruta: string): string {
  // El backend sirve sin prefijo y el proxy de Vite / gateway lo enrutan bajo
  // /api (spec): toda llamada a la API lleva la base, aunque la ruta se escriba
  // sin ella.
  const conBase = ruta.startsWith('/api') ? ruta : `/api${ruta}`;
  return new URL(conBase, window.location.origin).toString();
}

function esRutaDeSesion(ruta: string): boolean {
  return ruta.startsWith('/identidad/sesion');
}

function esRutaDeDiagnostico(ruta: string): boolean {
  return ruta.startsWith('/diagnosticos/frontend');
}

function reportarFalloDeRed(ruta: string): void {
  if (esRutaDeDiagnostico(ruta)) return;
  void reportarDiagnostico({
    errorId: crearErrorId(),
    eventName: 'http.network_failed',
    category: 'network',
    source: 'http',
  });
}

// Sin timeout, un backend inalcanzable deja la petición colgada (p. ej. WiFi
// arriba sin internet) y el modo offline no puede encolar. El abort se traduce
// a un error de transporte, no a un ApiError.
const TIEMPO_ESPERA_FETCH_MS = 15_000;

async function fetchConTiempo(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const controlador = new AbortController();
  const temporizador = setTimeout(() => controlador.abort(), TIEMPO_ESPERA_FETCH_MS);
  try {
    return await fetch(input, { ...init, signal: controlador.signal });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new TypeError('Tiempo de espera agotado.', { cause: error });
    }
    throw error;
  } finally {
    clearTimeout(temporizador);
  }
}

// Códigos de gateway sin backend: no son un rechazo de negocio, sino falta de
// conectividad con la API (el modo offline debe encolar, no propagar).
export function esFalloDeConectividad(error: ApiError): boolean {
  return (
    error.status === 408 || error.status === 502 || error.status === 503 || error.status === 504
  );
}

function conHeaders(init: RequestInit, cuerpo?: unknown): RequestInit {
  const cabeceras = new Headers(init.headers);
  cabeceras.set('X-Correlation-ID', crearCorrelationId());
  cabeceras.set('X-Session-Id', obtenerSesionId());
  const token = getAccessToken();
  if (token) cabeceras.set('Authorization', `Bearer ${token}`);
  // FormData (subida del Excel de vacunación): el navegador fija el boundary;
  // nunca forzar Content-Type ni serializar como JSON.
  const esFormData = cuerpo instanceof FormData;
  if (cuerpo !== undefined && !esFormData) cabeceras.set('Content-Type', 'application/json');
  return {
    ...init,
    headers: cabeceras,
    body:
      cuerpo === undefined || esFormData
        ? (cuerpo as BodyInit | undefined)
        : JSON.stringify(cuerpo),
  };
}

let renovacionEnCurso: Promise<boolean> | null = null;
async function renovarSesionInterna(): Promise<boolean> {
  renovacionEnCurso ??= (async () => {
    const r = await fetch(urlCompleta('/identidad/sesion/renovar'), {
      method: 'POST',
      credentials: 'include',
      headers: {
        'X-Correlation-ID': crearCorrelationId(),
        'X-Session-Id': obtenerSesionId(),
      },
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
  let correlationId: string | undefined;
  let traceId: string | undefined;
  let errorId: string | undefined;
  let erroresValidacion: Record<string, string[]> | undefined;
  try {
    const cuerpo = (await respuesta.json()) as {
      title?: string;
      correlationId?: string;
      traceId?: string;
      errorId?: string;
      errors?: Record<string, string[]>;
    };
    code = cuerpo.title;
    correlationId = cuerpo.correlationId;
    traceId = cuerpo.traceId;
    errorId = cuerpo.errorId;
    erroresValidacion = cuerpo.errors;
  } catch {
    // el cuerpo puede no ser JSON (204 o 401 vacíos)
  }
  return new ApiError({
    status: respuesta.status,
    code,
    correlationId: respuesta.headers.get('X-Correlation-ID') ?? correlationId ?? undefined,
    traceId: respuesta.headers.get('X-Trace-Id') ?? traceId ?? undefined,
    errorId,
    erroresValidacion,
  });
}

function registrarLlamadaApi(request: Request, inicio: number, respuesta?: Response): void {
  registrarEventoFlujo({
    eventName: 'flow.api_call',
    detail: `${request.method} ${sanitizarRuta(new URL(request.url).pathname)}`,
    statusCode: respuesta?.status,
    durationMs: Math.round(performance.now() - inicio),
    correlationId: respuesta?.headers.get('X-Correlation-ID') ?? undefined,
    traceId: respuesta?.headers.get('X-Trace-Id') ?? undefined,
  });
}

export async function peticion<T>(o: {
  ruta: string;
  metodo?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  cuerpo?: unknown;
}): Promise<T> {
  const { ruta, metodo = 'GET', cuerpo } = o;
  const crearRequest = () =>
    new Request(urlCompleta(ruta), conHeaders({ method: metodo, credentials: 'include' }, cuerpo));
  const original = crearRequest();
  const reintentable = !esRutaDeSesion(ruta);
  const inicio = performance.now();
  let respuesta: Response;
  try {
    respuesta = await fetchConTiempo(original);
  } catch (error) {
    registrarLlamadaApi(original, inicio);
    reportarFalloDeRed(ruta);
    throw error;
  }
  registrarLlamadaApi(original, inicio, respuesta);
  // Cualquier respuesta (aun un 401 o 500) prueba que el API responde: el
  // coordinador offline adelanta una sincronización pospuesta si la hay.
  avisarActividadApi();

  if (respuesta.status === 401 && reintentable && (await renovarSesionInterna())) {
    const reintento = crearRequest();
    const inicioReintento = performance.now();
    try {
      respuesta = await fetchConTiempo(reintento);
    } catch (error) {
      registrarLlamadaApi(reintento, inicioReintento);
      reportarFalloDeRed(ruta);
      throw error;
    }
    registrarLlamadaApi(reintento, inicioReintento, respuesta);
    avisarActividadApi();
  } else if (respuesta.status === 401 && reintentable) {
    clearAccessToken();
  }

  if (!respuesta.ok) {
    const error = await errorDesde(respuesta);
    if (respuesta.status >= 500) {
      void reportarDiagnostico({
        errorId: error.errorId ?? crearErrorId(),
        eventName: 'http.server_failed',
        category: 'server',
        source: 'http',
        correlationId: error.correlationId,
        traceId: error.traceId,
        statusCode: respuesta.status,
      });
    }
    throw error;
  }
  if (respuesta.status === 204) return undefined as T;
  return (await respuesta.json()) as T;
}

// Descarga binaria autenticada (respaldos de la nota, spec SP8C): devuelve el
// blob con su tipo de contenido para mostrarlo inline o descargarlo como
// adjunto. Nunca registra contenido, solo la ruta sanitizada.
export async function peticionBlob(o: { ruta: string }): Promise<{ blob: Blob; tipo: string }> {
  const { ruta } = o;
  const crearRequest = () =>
    new Request(urlCompleta(ruta), conHeaders({ method: 'GET', credentials: 'include' }));
  const inicio = performance.now();
  let respuesta: Response;
  try {
    respuesta = await fetchConTiempo(crearRequest());
  } catch (error) {
    registrarLlamadaApi(crearRequest(), inicio);
    reportarFalloDeRed(ruta);
    throw error;
  }
  registrarLlamadaApi(crearRequest(), inicio, respuesta);
  avisarActividadApi();
  if (respuesta.status === 401 && (await renovarSesionInterna())) {
    respuesta = await fetchConTiempo(crearRequest());
    registrarLlamadaApi(crearRequest(), inicio, respuesta);
    avisarActividadApi();
  } else if (respuesta.status === 401) {
    clearAccessToken();
  }
  if (!respuesta.ok) throw await errorDesde(respuesta);
  return { blob: await respuesta.blob(), tipo: respuesta.headers.get('content-type') ?? 'application/octet-stream' };
}
