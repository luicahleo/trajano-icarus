import { abrirBaseDatosOffline, promesaDePedido } from './baseDatosOffline';
import type { AlmacenCola } from './almacenCola';
import type { OperacionPendiente } from './tipos';

async function conStore<T>(
  modo: IDBTransactionMode,
  usar: (store: IDBObjectStore) => Promise<T>,
): Promise<T> {
  const bd = await abrirBaseDatosOffline();
  try {
    const tx = bd.transaction('operaciones', modo);
    return await usar(tx.objectStore('operaciones'));
  } finally {
    bd.close();
  }
}

export function crearAlmacenColaIndexedDb(): AlmacenCola {
  return {
    agregar: (op) => conStore('readwrite', (s) => promesaDePedido(s.put(op)).then(() => {})),
    listarPendientes: async (ahoraIso, limite) => {
      const todas = await conStore('readonly', (s) =>
        promesaDePedido(s.getAll() as IDBRequest<OperacionPendiente[]>),
      );
      return todas
        .filter(
          (o) =>
            o.estado === 'pendiente' &&
            (o.proximoIntentoEn === null || o.proximoIntentoEn <= ahoraIso),
        )
        .slice(0, limite);
    },
    listarTodas: () =>
      conStore('readonly', (s) => promesaDePedido(s.getAll() as IDBRequest<OperacionPendiente[]>)),
    eliminar: (id) => conStore('readwrite', (s) => promesaDePedido(s.delete(id)).then(() => {})),
    actualizar: async (id, cambios) => {
      await conStore('readwrite', async (s) => {
        const actual = await promesaDePedido(
          s.get(id) as IDBRequest<OperacionPendiente | undefined>,
        );
        if (actual) await promesaDePedido(s.put({ ...actual, ...cambios }));
      });
    },
    contar: () => conStore('readonly', (s) => promesaDePedido(s.count())),
    rearmarPendientes: async () => {
      const todas = await conStore('readonly', (s) =>
        promesaDePedido(s.getAll() as IDBRequest<OperacionPendiente[]>),
      );
      const conBackoff = todas.filter(
        (o) => o.estado === 'pendiente' && o.proximoIntentoEn !== null,
      );
      for (const o of conBackoff) {
        await conStore('readwrite', (s) =>
          promesaDePedido(s.put({ ...o, proximoIntentoEn: null })).then(() => {}),
        );
      }
      return conBackoff.length;
    },
  };
}
