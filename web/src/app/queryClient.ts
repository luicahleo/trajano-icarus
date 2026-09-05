import { QueryClient } from '@tanstack/react-query';

export function crearQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Las lecturas avícolas tienen respaldo en IndexedDB (conCacheLectura).
        // Con el networkMode por defecto ('online'), navigator.onLine === false
        // pausaba las queries: el queryFn no corría y la caché offline nunca se
        // consultaba, dejando la app sin datos tras el login sin conexión
        // (diagnóstico SES-8B501C010EBD). 'offlineFirst' intenta la red siempre
        // y cae a la caché; en línea se comporta igual que 'online'.
        networkMode: 'offlineFirst',
      },
    },
  });
}
