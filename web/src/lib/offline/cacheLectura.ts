// Caché de lectura offline: solo datos de negocio (anti-PII), nunca tokens.
export interface CacheLectura {
  obtener(clave: string): Promise<unknown>;
  guardar(clave: string, valor: unknown): Promise<void>;
}

export function crearCacheLecturaMemoria(): CacheLectura {
  const datos = new Map<string, unknown>();
  return {
    async obtener(clave) {
      return datos.get(clave);
    },
    async guardar(clave, valor) {
      datos.set(clave, valor);
    },
  };
}
