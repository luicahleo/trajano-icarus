import { useSyncExternalStore } from 'react';

let estadoConexion = typeof navigator === 'undefined' ? true : navigator.onLine;

function suscribir(aviso: () => void): () => void {
  // Re-sincroniza al suscribir: si la red cambió mientras no había ningún
  // consumidor montado (p. ej. en /login, donde el banner no existe), el
  // estado quedaba obsoleto y el banner mostraba «sin conexión» habiendo red
  // (diagnóstico SES-4AF9D4EF3BC1).
  estadoConexion = navigator.onLine;
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
