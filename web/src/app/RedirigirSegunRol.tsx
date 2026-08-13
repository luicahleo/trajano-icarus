import { Navigate } from 'react-router-dom';
import { useAuth } from '../features/auth/AuthContext';
import { inicioSegunRol } from './inicioSegunRol';

export function RedirigirSegunRol() {
  const { rol } = useAuth();
  const destino = rol ? inicioSegunRol(rol) : '/login';
  return <Navigate to={destino} replace />;
}
