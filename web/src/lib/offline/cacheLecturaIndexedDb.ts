import { abrirBaseDatosOffline, promesaDePedido } from './baseDatosOffline';
import type { CacheLectura } from './cacheLectura';

interface EntradaCache {
  clave: string;
  valor: unknown;
}

export function crearCacheLecturaIndexedDb(): CacheLectura {
  return {
    async obtener(clave) {
      const bd = await abrirBaseDatosOffline();
      try {
        const tx = bd.transaction('cache-lectura', 'readonly');
        const entrada = await promesaDePedido(
          tx.objectStore('cache-lectura').get(clave) as IDBRequest<EntradaCache | undefined>,
        );
        return entrada?.valor;
      } finally {
        bd.close();
      }
    },
    async guardar(clave, valor) {
      const bd = await abrirBaseDatosOffline();
      try {
        const tx = bd.transaction('cache-lectura', 'readwrite');
        await promesaDePedido(tx.objectStore('cache-lectura').put({ clave, valor }));
      } finally {
        bd.close();
      }
    },
  };
}
