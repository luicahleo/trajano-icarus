export function crearCorrelationId(): string {
  return crypto.randomUUID();
}
