// Base única de la app para offline. Los dos stores se crean en la versión 1
// para no necesitar migraciones: operaciones (cola) y cache-lectura.
const NOMBRE_BD = 'icarus-offline';

export function abrirBaseDatosOffline(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const pedido = indexedDB.open(NOMBRE_BD, 1);
    pedido.onupgradeneeded = () => {
      const bd = pedido.result;
      if (!bd.objectStoreNames.contains('operaciones')) {
        bd.createObjectStore('operaciones', { keyPath: 'id' });
      }
      if (!bd.objectStoreNames.contains('cache-lectura')) {
        bd.createObjectStore('cache-lectura', { keyPath: 'clave' });
      }
    };
    pedido.onsuccess = () => resolve(pedido.result);
    pedido.onerror = () => reject(pedido.error);
  });
}

export function promesaDePedido<T>(pedido: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    pedido.onsuccess = () => resolve(pedido.result);
    pedido.onerror = () => reject(pedido.error);
  });
}
