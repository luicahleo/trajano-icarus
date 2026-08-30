import type { OperacionPendiente } from './tipos';

export interface AlmacenCola {
  agregar(op: OperacionPendiente): Promise<void>;
  // Pendientes listas para enviar: estado 'pendiente' y proximoIntentoEn vencido.
  listarPendientes(ahoraIso: string, limite: number): Promise<OperacionPendiente[]>;
  listarTodas(): Promise<OperacionPendiente[]>;
  eliminar(id: string): Promise<void>;
  actualizar(
    id: string,
    cambios: Partial<Pick<OperacionPendiente, 'estado' | 'intentos' | 'proximoIntentoEn'>>,
  ): Promise<void>;
  contar(): Promise<number>;
}

export function crearAlmacenColaMemoria(): AlmacenCola {
  const ops = new Map<string, OperacionPendiente>();
  return {
    async agregar(op) {
      ops.set(op.id, op);
    },
    async listarPendientes(ahoraIso, limite) {
      return [...ops.values()]
        .filter(
          (o) =>
            o.estado === 'pendiente' &&
            (o.proximoIntentoEn === null || o.proximoIntentoEn <= ahoraIso),
        )
        .slice(0, limite);
    },
    async listarTodas() {
      return [...ops.values()];
    },
    async eliminar(id) {
      ops.delete(id);
    },
    async actualizar(id, cambios) {
      const actual = ops.get(id);
      if (actual) ops.set(id, { ...actual, ...cambios });
    },
    async contar() {
      return ops.size;
    },
  };
}
