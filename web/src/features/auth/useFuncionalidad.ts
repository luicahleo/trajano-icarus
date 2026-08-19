import type { Funcionalidad } from '../../lib/tipos';
import { useAuth } from './AuthContext';

// Para ocultar acciones puntuales dentro de una página. Semántica ANY, igual que la guarda.
export function useFuncionalidad(...funcionalidades: Funcionalidad[]): boolean {
  const { tieneFuncionalidad } = useAuth();
  return tieneFuncionalidad(...funcionalidades);
}
