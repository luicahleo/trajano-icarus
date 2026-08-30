import { useEffect, useRef } from 'react';
import { useAuth } from '../../features/auth/AuthContext';
import { precalentarCacheAvicola } from '../../features/avicola/offline';
import { useConexion } from '../useConexion';

// Efecto sin UI: precalienta la caché del día para el rol Trabajador (spec
// decisión 5). Se reintenta en cada reconexión mientras dure la sesión.
export function PrecalentadoOffline() {
  const { rol, estaAutenticado } = useAuth();
  const online = useConexion();
  const ultimaVez = useRef<string | null>(null);
  useEffect(() => {
    if (!estaAutenticado || rol !== 'Trabajador' || !online) return;
    const hoy = new Date().toDateString();
    if (ultimaVez.current === hoy) return;
    ultimaVez.current = hoy;
    void precalentarCacheAvicola().catch(() => {
      ultimaVez.current = null; // permite reintentar si falló a medias
    });
  }, [estaAutenticado, rol, online]);
  return null;
}
