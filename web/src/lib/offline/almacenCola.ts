import type { OperacionPendiente } from './tipos';

export interface AlmacenCola {
  agregar(op: OperacionPendiente): Promise<void>;
  // Pendientes listas para enviar: estado 'pendiente' y proximoIntentoEn vencido.
  listarPendientes(ahoraIso: string, limite: number): Promise<OperacionPendiente[]>;
  listarTodas(): Promise<OperacionPendiente[]>;
  eliminar(id: string): Promise<void>;
  actualizar(
    id: string,
    cambios: Partial<
      Pick<OperacionPendiente, 'estado' | 'intentos' | 'proximoIntentoEn' | 'cuerpo'>
    >,
  ): Promise<void>;
  contar(): Promise<number>;
  // Quita el backoff (proximoIntentoEn) de las pendientes conservando intentos
  // y estado: al recuperar la red, esperar el backoff no aporta nada. Las de
  // estado 'error' no se tocan (rechazo del backend o intentos agotados: piden
  // decisión manual). Devuelve cuántas se rearmaron.
  rearmarPendientes(): Promise<number>;
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
    async rearmarPendientes() {
      let rearmadas = 0;
      for (const o of ops.values()) {
        if (o.estado === 'pendiente' && o.proximoIntentoEn !== null) {
          ops.set(o.id, { ...o, proximoIntentoEn: null });
          rearmadas++;
        }
      }
      return rearmadas;
    },
  };
}
