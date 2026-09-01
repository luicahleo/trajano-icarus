import { useEffect, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../features/auth/AuthContext';
import { crearDespachadorAvicola } from '../features/avicola/offline';
import { apiAccesible } from '../lib/offline/sondaApi';
import { iniciarCoordinadorOffline } from './offline/coordinador';

const queryClient = new QueryClient();

export function AppProviders({ children }: { children: ReactNode }) {
  // El arranque del coordinador crea la cola IndexedDB, suscribe el evento
  // online, arranca el timer de respaldo y dispara el ciclo inicial que vacía
  // operaciones pendientes de otra sesión (requiere sesión válida; un 401 solo
  // pausa el ciclo). La sonda evita sincronizar contra un API inalcanzable:
  // navigator.onLine solo garantiza interfaz de red, no backend vivo.
  useEffect(
    () =>
      iniciarCoordinadorOffline({ despachar: crearDespachadorAvicola(queryClient), sonda: apiAccesible }),
    [],
  );
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>{children}</AuthProvider>
    </QueryClientProvider>
  );
}
