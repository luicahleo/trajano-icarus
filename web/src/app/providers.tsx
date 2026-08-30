import { useEffect, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../features/auth/AuthContext';
import { crearDespachadorAvicola } from '../features/avicola/offline';
import { iniciarCoordinadorOffline } from './offline/coordinador';

const queryClient = new QueryClient();

export function AppProviders({ children }: { children: ReactNode }) {
  // El arranque del coordinador crea la cola IndexedDB, suscribe el evento
  // online, arranca el timer de respaldo y dispara el ciclo inicial que vacía
  // operaciones pendientes de otra sesión (requiere sesión válida; un 401 solo
  // pausa el ciclo).
  useEffect(() => iniciarCoordinadorOffline({ despachar: crearDespachadorAvicola(queryClient) }), []);
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>{children}</AuthProvider>
    </QueryClientProvider>
  );
}
