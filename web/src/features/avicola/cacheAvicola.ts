import { obtenerCacheLectura } from '../../app/offline/coordinador';
import { ApiError } from '../../lib/http';
import { registrarEventoFlujo, sanitizarRuta } from '../../lib/sesionDiagnostico';

// Lectura con respaldo offline: éxito → actualiza la caché; fallo de red →
// sirve la caché si existe. ApiError (4xx/5xx) siempre se propaga.
export async function conCacheLectura<T>(
  clave: string,
  obtenerDatos: () => Promise<T>,
): Promise<T> {
  const cache = obtenerCacheLectura();
  if (!cache) return obtenerDatos();
  try {
    const valor = await obtenerDatos();
    await cache.guardar(clave, valor);
    registrarEventoFlujo({
      eventName: 'flow.offline_cache',
      detail: `Caché actualizada: ${sanitizarRuta(`/${clave}`).slice(1)}`,
    });
    return valor;
  } catch (error) {
    if (error instanceof ApiError) throw error;
    const cacheado = await cache.obtener(clave);
    if (cacheado !== undefined) return cacheado as T;
    throw error;
  }
}
