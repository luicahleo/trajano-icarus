import { useSyncExternalStore } from 'react';

let estadoConexion = typeof navigator === 'undefined' ? true : navigator.onLine;

function suscribir(aviso: () => void): () => void {
  const alConectar = () => {
    estadoConexion = true;
    aviso();
  };
  const alDesconectar = () => {
    estadoConexion = false;
    aviso();
  };
  window.addEventListener('online', alConectar);
  window.addEventListener('offline', alDesconectar);
  return () => {
    window.removeEventListener('online', alConectar);
    window.removeEventListener('offline', alDesconectar);
  };
}

// true = hay conexión. Fuente: navigator.onLine + eventos online/offline.
export function useConexion(): boolean {
  return useSyncExternalStore(
    suscribir,
    () => estadoConexion,
    () => true,
  );
}
