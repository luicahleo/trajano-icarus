import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import type { Funcionalidad } from '../../lib/tipos';
import { useAuth } from './AuthContext';

// Guarda de ruta por funcionalidad: pasa si el usuario tiene alguna de las listadas.
// La autorización real la hace el backend (403).
export function RequiereFuncionalidad({
  funcionalidades,
  children,
}: {
  funcionalidades: Funcionalidad[];
  children: ReactNode;
}) {
  const { tieneFuncionalidad, cargando } = useAuth();

  if (cargando) return null;
  if (!tieneFuncionalidad(...funcionalidades)) return <Navigate to="/inicio" replace />;
  return <>{children}</>;
}
