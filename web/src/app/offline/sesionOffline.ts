import { crearCacheLecturaIndexedDb } from '../../lib/offline/cacheLecturaIndexedDb';
import type { UsuarioActual } from '../../lib/tipos';

// Snapshot mínimo para abrir la PWA sin red (spec decisión 6). NUNCA guarda
// token ni correo (anti-PII). Se accede a IndexedDB directamente porque la
// restauración de sesión corre antes que el coordinador offline.
const CLAVE = 'sesion-offline';
// Caducidad de 12 h (spec decisión 6): obliga a login diario, y ese login con
// red es lo que vacía la cola (ciclo inicial del motor).
const VALIDEZ_MS = 12 * 60 * 60 * 1000;

interface SnapshotGuardado {
  guardadoEn: string; // ISO
  usuario: UsuarioActual;
}

export async function guardarSesionOffline(usuario: UsuarioActual): Promise<void> {
  const cache = crearCacheLecturaIndexedDb();
  if (usuario.rol !== 'Trabajador') {
    await cache.guardar(CLAVE, null); // otro rol → borra (dispositivo compartido)
    return;
  }
  const snapshot: UsuarioActual = {
    usuarioId: usuario.usuarioId,
    correo: null,
    rol: usuario.rol,
    clienteId: usuario.clienteId,
    trabajadorId: usuario.trabajadorId,
    modulos: usuario.modulos,
    funcionalidades: usuario.funcionalidades,
  };
  const guardado: SnapshotGuardado = { guardadoEn: new Date().toISOString(), usuario: snapshot };
  await cache.guardar(CLAVE, guardado);
}

export async function obtenerSesionOffline(ahora: Date = new Date()): Promise<UsuarioActual | null> {
  const valor = await crearCacheLecturaIndexedDb().obtener(CLAVE);
  if (!valor || typeof valor !== 'object') return null;
  const { guardadoEn, usuario } = valor as SnapshotGuardado;
  if (ahora.getTime() - new Date(guardadoEn).getTime() > VALIDEZ_MS) {
    await crearCacheLecturaIndexedDb().guardar(CLAVE, null); // expirado: se borra
    return null;
  }
  return usuario;
}

export async function borrarSesionOffline(): Promise<void> {
  await crearCacheLecturaIndexedDb().guardar(CLAVE, null);
}
