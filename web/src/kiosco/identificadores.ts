// UUID si el navegador lo expone; respaldo para entornos sin crypto.
export function nuevoId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') return globalThis.crypto.randomUUID();
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
