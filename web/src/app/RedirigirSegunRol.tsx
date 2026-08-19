import { Navigate } from 'react-router-dom';
import { useAuth } from '../features/auth/AuthContext';
import { inicioSegunRol } from './inicioSegunRol';

export function RedirigirSegunRol() {
  const { rol, funcionalidades } = useAuth();
  const destino = rol ? inicioSegunRol(rol, funcionalidades) : '/login';
  return <Navigate to={destino} replace />;
}
