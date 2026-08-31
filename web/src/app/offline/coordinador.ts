import type { AlmacenCola } from '../../lib/offline/almacenCola';
import { crearAlmacenColaIndexedDb } from '../../lib/offline/almacenIndexedDb';
import type { CacheLectura } from '../../lib/offline/cacheLectura';
import { crearCacheLecturaIndexedDb } from '../../lib/offline/cacheLecturaIndexedDb';
import { crearMotorSincronizacion } from '../../lib/offline/motorSincronizacion';
import type { OperacionPendiente, TipoOperacionOffline } from '../../lib/offline/tipos';
import { registrarEventoFlujo } from '../../lib/sesionDiagnostico';

// Singleton: una cola y un motor por pestaña. Los datos son solo de negocio
// (anti-PII); el token nunca pasa por aquí.
let almacen: AlmacenCola | null = null;
let cache: CacheLectura | null = null;
let sincronizar: (() => Promise<void>) | null = null;
let conteo = 0;
const avisosPendientes = new Set<() => void>();
const avisosSnackbar = new Set<(mensaje: string) => void>();

function notificar(): void {
  avisosPendientes.forEach((a) => a());
}

async function refrescarConteo(): Promise<void> {
  if (!almacen) return;
  conteo = await almacen.contar();
  notificar();
}

export function iniciarCoordinadorOffline(deps: {
  despachar: (op: OperacionPendiente) => Promise<void>;
  almacen?: AlmacenCola;
  cache?: CacheLectura;
  intervaloMs?: number;
}): () => void {
  almacen = deps.almacen ?? crearAlmacenColaIndexedDb();
  cache = deps.cache ?? crearCacheLecturaIndexedDb();
  const motor = crearMotorSincronizacion({
    almacen,
    despachar: async (op) => {
      await deps.despachar(op);
    },
    conectado: () => navigator.onLine,
  });
  sincronizar = async () => {
    registrarEventoFlujo({ eventName: 'flow.offline_sync', detail: 'Sincronización iniciada' });
    await motor.sincronizar();
    await refrescarConteo();
    registrarEventoFlujo({
      eventName: 'flow.offline_sync',
      detail: `Sincronización completada (${conteo} pendientes)`,
    });
  };
  const alConectar = () => {
    registrarEventoFlujo({ eventName: 'flow.online', detail: 'Conexión restablecida' });
    void sincronizar?.();
  };
  const alDesconectar = () => {
    registrarEventoFlujo({ eventName: 'flow.offline', detail: 'Sin conexión de red' });
  };
  window.addEventListener('online', alConectar);
  window.addEventListener('offline', alDesconectar);
  const timer = window.setInterval(alConectar, deps.intervaloMs ?? 5 * 60_000);
  void refrescarConteo();
  void sincronizar(); // ciclo inicial: vacía la cola si quedó de otra sesión
  return () => {
    window.removeEventListener('online', alConectar);
    window.removeEventListener('offline', alDesconectar);
    window.clearInterval(timer);
    almacen = null;
    cache = null;
    sincronizar = null;
    conteo = 0;
  };
}

export function obtenerCacheLectura(): CacheLectura | null {
  return cache;
}

export async function encolarOperacion(
  tipo: TipoOperacionOffline,
  galponId: string,
  cuerpo: unknown,
): Promise<void> {
  if (!almacen) throw new Error('Coordinador offline no iniciado.');
  try {
    await almacen.agregar({
      id: crypto.randomUUID(),
      tipo,
      galponId,
      cuerpo,
      estado: 'pendiente',
      intentos: 0,
      creadoEn: new Date().toISOString(),
      proximoIntentoEn: null,
    });
  } catch (error) {
    // Sin este evento, un IndexedDB inaccesible dejaba el guardado colgado o
    // con error genérico y el diagnóstico no mostraba ningún rastro.
    registrarEventoFlujo({ eventName: 'flow.offline_queue', detail: `Fallo al encolar: ${tipo}` });
    throw error;
  }
  await refrescarConteo();
  registrarEventoFlujo({ eventName: 'flow.offline_queue', detail: `Operación encolada: ${tipo}` });
  avisosSnackbar.forEach((a) => a('Guardado sin conexión: se sincronizará al volver la red.'));
  if (navigator.onLine) void sincronizar?.(); // fire-and-forget
}

export function suscribirPendientes(aviso: () => void): () => void {
  avisosPendientes.add(aviso);
  return () => avisosPendientes.delete(aviso);
}

export function obtenerConteoPendientes(): number {
  return conteo;
}

export function suscribirAvisos(aviso: (mensaje: string) => void): () => void {
  avisosSnackbar.add(aviso);
  return () => avisosSnackbar.delete(aviso);
}

export async function listarOperaciones(): Promise<OperacionPendiente[]> {
  return almacen ? almacen.listarTodas() : [];
}

// Edición offline de una operación aún no sincronizada: solo cambia el cuerpo
// (misma idempotencyKey dentro de él); el estado y los reintentos no se tocan.
export async function actualizarContenidoOperacion(id: string, cuerpo: unknown): Promise<void> {
  await almacen?.actualizar(id, { cuerpo });
  await refrescarConteo();
  registrarEventoFlujo({
    eventName: 'flow.offline_queue',
    detail: 'Operación pendiente actualizada',
  });
}

export async function reintentarOperacion(id: string): Promise<void> {
  await almacen?.actualizar(id, { estado: 'pendiente', intentos: 0, proximoIntentoEn: null });
  await refrescarConteo();
  if (navigator.onLine) void sincronizar?.();
}

export async function descartarOperacion(id: string): Promise<void> {
  await almacen?.eliminar(id);
  await refrescarConteo();
}
