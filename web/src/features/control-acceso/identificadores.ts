// Identificador para claves de idempotencia: UUID si el navegador lo expone,
// con respaldo determinista para entornos sin crypto (jsdom antiguo).
export function nuevoId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') return globalThis.crypto.randomUUID();
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
