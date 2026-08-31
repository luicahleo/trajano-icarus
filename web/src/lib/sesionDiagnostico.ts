// Diagnóstico local por pestaña. Solo conserva metadatos sanitizados en memoria
// y permite exportarlos manualmente cuando se activa ?debug=1.
export interface EventoFlujo {
  seq: number;
  timestamp: string;
  eventName:
    | 'flow.navigation'
    | 'flow.app'
    | 'flow.api_call'
    | 'flow.offline'
    | 'flow.online'
    | 'flow.offline_queue'
    | 'flow.offline_sync'
    | 'flow.offline_cache';
  detail: string;
  correlationId?: string;
  traceId?: string;
  statusCode?: number;
  durationMs?: number;
}

const SESION_CLAVE = 'icarus.sesion';
const DEBUG_CLAVE = 'icarus.debug';
const EVENTOS_CLAVE = 'icarus.diagnostico.eventos';
const MAX_EVENTOS = 100;
const MAX_DETALLE = 120;
const PATRON_SESION = /^SES-[0-9A-F]{12}$/;
const PATRON_UUID = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

// El buffer se conserva en sessionStorage: sin esto, recargar la pestaña a mitad
// de una prueba offline borraba la traza justo antes de exportarla. Es por
// pestaña y se pierde al cerrarla, igual que la sesión de diagnóstico.
function cargarEventos(): EventoFlujo[] {
  try {
    const crudo = sessionStorage.getItem(EVENTOS_CLAVE);
    const datos: unknown = crudo ? JSON.parse(crudo) : [];
    return Array.isArray(datos) ? (datos as EventoFlujo[]) : [];
  } catch {
    return [];
  }
}

const eventos: EventoFlujo[] = cargarEventos();
let siguienteSeq = (eventos.at(-1)?.seq ?? 0) + 1;

interface EntornoDiagnostico {
  DEV: boolean;
  VITE_HABILITAR_DIAGNOSTICO_MANUAL?: string;
}

export function diagnosticoManualPermitido(entorno: EntornoDiagnostico = import.meta.env): boolean {
  return entorno.DEV || entorno.VITE_HABILITAR_DIAGNOSTICO_MANUAL === 'true';
}

export function obtenerSesionId(): string {
  const almacenada = sessionStorage.getItem(SESION_CLAVE);
  if (almacenada !== null && PATRON_SESION.test(almacenada)) return almacenada;

  const bytes = crypto.getRandomValues(new Uint8Array(6));
  const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('');
  const id = `SES-${hex.toUpperCase()}`;
  sessionStorage.setItem(SESION_CLAVE, id);
  return id;
}

export function modoDiagnosticoActivo(permitido = diagnosticoManualPermitido()): boolean {
  if (!permitido) {
    sessionStorage.removeItem(DEBUG_CLAVE);
    return false;
  }
  if (new URLSearchParams(window.location.search).get('debug') === '1') {
    sessionStorage.setItem(DEBUG_CLAVE, '1');
  }
  return sessionStorage.getItem(DEBUG_CLAVE) === '1';
}

export function sanitizarRuta(pathname: string): string {
  const sanitizada = pathname
    .split('/')
    .map((segmento) => (/^\d+$/.test(segmento) || PATRON_UUID.test(segmento) ? ':id' : segmento))
    .join('/');
  return sanitizada === '' ? '/' : sanitizada;
}

export function registrarEventoFlujo(evento: Omit<EventoFlujo, 'seq' | 'timestamp'>): void {
  eventos.push({
    ...evento,
    detail: evento.detail.slice(0, MAX_DETALLE),
    seq: siguienteSeq,
    timestamp: new Date().toISOString(),
  });
  siguienteSeq += 1;
  if (eventos.length > MAX_EVENTOS) eventos.splice(0, eventos.length - MAX_EVENTOS);
  try {
    sessionStorage.setItem(EVENTOS_CLAVE, JSON.stringify(eventos));
  } catch {
    // Cuota llena o almacenamiento bloqueado: se pierde la persistencia, no el evento.
  }
}

export function obtenerEventosRecientes(n: number): EventoFlujo[] {
  return eventos.slice(-n);
}

export function exportarDiagnostico(permitido = diagnosticoManualPermitido()): void {
  if (!permitido) return;

  const carga = JSON.stringify(
    {
      sessionId: obtenerSesionId(),
      build: __APP_BUILD__,
      generadoEn: new Date().toISOString(),
      eventos,
    },
    null,
    2,
  );
  const url = URL.createObjectURL(new Blob([carga], { type: 'application/json' }));
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = `diagnostico-${obtenerSesionId()}.json`;
  enlace.click();
  URL.revokeObjectURL(url);
}
