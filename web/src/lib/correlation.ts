const CLAVE = 'icarus-correlation-id';

export function obtenerCorrelationId(): string {
  const actual = sessionStorage.getItem(CLAVE);
  if (actual) return actual;
  const nuevo = crypto.randomUUID();
  sessionStorage.setItem(CLAVE, nuevo);
  return nuevo;
}

export function renovarCorrelationId(): string {
  const nuevo = crypto.randomUUID();
  sessionStorage.setItem(CLAVE, nuevo);
  return nuevo;
}
