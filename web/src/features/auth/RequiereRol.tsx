import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import type { Rol } from '../../lib/tipos';
import { useAuth } from './AuthContext';

export function RequiereRol({ roles, children }: { roles: Rol[]; children: ReactNode }) {
  const { tieneRol } = useAuth();

  if (!tieneRol(...roles)) return <Navigate to="/" replace />;
  return <>{children}</>;
}
