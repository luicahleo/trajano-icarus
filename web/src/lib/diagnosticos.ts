import { crearCorrelationId } from './correlation';
import { getAccessToken } from './session';
import { obtenerEventosRecientes, obtenerSesionId } from './sesionDiagnostico';

type EventoDiagnostico =
  | 'router.unexpected'
  | 'window.unexpected'
  | 'promise.unhandled'
  | 'http.network_failed'
  | 'http.server_failed'
  | 'chunk.load_failed';

type CategoriaDiagnostico = 'unexpected' | 'network' | 'server' | 'chunk';
type FuenteDiagnostico = 'router' | 'window' | 'promise' | 'http';

export interface DiagnosticoFrontend {
  errorId?: string;
  eventName: EventoDiagnostico;
  category: CategoriaDiagnostico;
  source: FuenteDiagnostico;
  correlationId?: string;
  traceId?: string;
  statusCode?: number;
  asset?: string;
  lineNumber?: number;
  columnNumber?: number;
}

type ReporteroDiagnostico = (diagnostico: DiagnosticoFrontend) => Promise<void>;

const VENTANA_DEDUPLICACION_MS = 60_000;
const deduplicados = new Map<string, number>();

function claveDeduplicacion(diagnostico: DiagnosticoFrontend): string {
  return [
    diagnostico.eventName,
    diagnostico.source,
    diagnostico.statusCode ?? '',
    diagnostico.asset ?? '',
    diagnostico.lineNumber ?? '',
    diagnostico.columnNumber ?? '',
  ].join('|');
}

function esDuplicado(diagnostico: DiagnosticoFrontend): boolean {
  const clave = claveDeduplicacion(diagnostico);
  const ahora = Date.now();
  for (const [existente, caduca] of deduplicados) {
    if (caduca < ahora) deduplicados.delete(existente);
  }
  if (deduplicados.has(clave)) return true;
  deduplicados.set(clave, ahora + VENTANA_DEDUPLICACION_MS);
  return false;
}

export function limpiarDeduplicacionReportes(): void {
  deduplicados.clear();
}

export { type ReporteroDiagnostico };

export function crearErrorId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(6));
  const hex = Array.from(bytes, (valor) => valor.toString(16).padStart(2, '0')).join('');
  return `ERR-${hex.toUpperCase()}`;
}

export function sanitizarAsset(valor?: string): string | undefined {
  if (!valor) return undefined;

  let nombre: string;
  try {
    const url = new URL(valor, window.location.origin);
    if (url.origin !== window.location.origin) return undefined;
    nombre = url.pathname.split('/').at(-1) ?? '';
  } catch {
    return undefined;
  }

  return /^[A-Za-z0-9._-]{1,120}$/.test(nombre) ? nombre : undefined;
}

export async function reportarDiagnostico(diagnostico: DiagnosticoFrontend): Promise<void> {
  if (esDuplicado(diagnostico)) return;

  const sessionId = obtenerSesionId();
  const cuerpo = {
    ...diagnostico,
    errorId: diagnostico.errorId ?? crearErrorId(),
    sessionId,
    release: import.meta.env.VITE_RELEASE || 'development',
    asset: sanitizarAsset(diagnostico.asset),
    flowEvents: obtenerEventosRecientes(30),
  };
  const headers = new Headers({
    'Content-Type': 'application/json',
    'X-Correlation-ID': crearCorrelationId(),
    'X-Session-Id': sessionId,
  });
  const token = getAccessToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  try {
    await fetch('/api/diagnosticos/frontend', {
      method: 'POST',
      headers,
      body: JSON.stringify(cuerpo),
      credentials: 'include',
      keepalive: true,
    });
  } catch {
    // Best effort: un fallo del propio canal de diagnóstico no genera otro reporte.
  }
}

export function instalarCapturaGlobal(
  reportero: ReporteroDiagnostico = reportarDiagnostico,
): () => void {
  const alError = (evento: Event) => {
    const error = evento as ErrorEvent;
    void reportero({
      errorId: crearErrorId(),
      eventName: 'window.unexpected',
      category: 'unexpected',
      source: 'window',
      asset: sanitizarAsset(error.filename),
      lineNumber: error.lineno || undefined,
      columnNumber: error.colno || undefined,
    });
  };
  const aPromesaNoManejada = () => {
    void reportero({
      errorId: crearErrorId(),
      eventName: 'promise.unhandled',
      category: 'unexpected',
      source: 'promise',
    });
  };

  window.addEventListener('error', alError);
  window.addEventListener('unhandledrejection', aPromesaNoManejada);
  return () => {
    window.removeEventListener('error', alError);
    window.removeEventListener('unhandledrejection', aPromesaNoManejada);
  };
}
