// Sonda ligera de conectividad real con el API. navigator.onLine solo dice que
// hay interfaz de red levantada, no que el backend responda (en PC1 el evento
// online saltaba con el API inalcanzable y la sync quemaba reintentos en
// timeouts de 15 s). Cualquier respuesta HTTP cuenta como alcanzable —un 401
// sin sesión también prueba que el backend vive— salvo los códigos de gateway
// sin backend (mismos criterios que `esFalloDeConectividad` en lib/http.ts).
// Sin token ni datos de negocio (anti-PII). El timeout propio, mucho menor que
// el de http.ts, evita que la sonda cuelgue la sincronización.
const TIEMPO_ESPERA_SONDA_MS = 4_000;
const CODIGOS_GATEWAY_SIN_BACKEND = new Set([408, 502, 503, 504]);

export async function apiAccesible(): Promise<boolean> {
  const controlador = new AbortController();
  const temporizador = setTimeout(() => controlador.abort(), TIEMPO_ESPERA_SONDA_MS);
  try {
    const respuesta = await fetch('/api/identidad/me', {
      method: 'GET',
      credentials: 'include',
      signal: controlador.signal,
    });
    return !CODIGOS_GATEWAY_SIN_BACKEND.has(respuesta.status);
  } catch {
    return false;
  } finally {
    clearTimeout(temporizador);
  }
}
