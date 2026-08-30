import { useSyncExternalStore } from 'react';
import { obtenerConteoPendientes, suscribirPendientes } from './coordinador';

// Número de operaciones en la cola offline (pendientes + en error).
export function usePendientesOffline(): number {
  return useSyncExternalStore(suscribirPendientes, obtenerConteoPendientes, () => 0);
}
