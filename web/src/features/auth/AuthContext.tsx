import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { renovarSesion } from '../../lib/http';
import { clearAccessToken } from '../../lib/session';
import type { Rol, UsuarioActual } from '../../lib/tipos';
import { iniciarSesion, obtenerMe, type Credenciales } from './api';

export interface EstadoAuth {
  usuario: UsuarioActual | null;
  estaAutenticado: boolean;
  cargando: boolean;
  rol: Rol | null;
  clienteId: string | null;
  tieneRol: (...roles: Rol[]) => boolean;
  iniciarSesion: (cred: Credenciales) => Promise<void>;
  cerrarSesion: () => void;
}

const AuthContext = createContext<EstadoAuth | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<UsuarioActual | null>(null);
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    let activo = true;
    void (async () => {
      try {
        const restaurada = await renovarSesion();
        if (!restaurada || !activo) return;
        const me = await obtenerMe();
        if (activo) setUsuario(me);
      } catch {
        // restauración fallida: sesión anónima
      } finally {
        if (activo) setCargando(false);
      }
    })();
    return () => {
      activo = false;
    };
  }, []);

  const iniciarSesionFn = useCallback(async (cred: Credenciales) => {
    await iniciarSesion(cred);
    const me = await obtenerMe();
    setUsuario(me);
  }, []);

  const cerrarSesionFn = useCallback(() => {
    clearAccessToken();
    setUsuario(null);
  }, []);

  const tieneRol = useCallback(
    (...roles: Rol[]) => (usuario ? roles.includes(usuario.rol) : false),
    [usuario],
  );

  const estado = useMemo<EstadoAuth>(
    () => ({
      usuario,
      estaAutenticado: usuario !== null,
      cargando,
      rol: usuario?.rol ?? null,
      clienteId: usuario?.clienteId ?? null,
      tieneRol,
      iniciarSesion: iniciarSesionFn,
      cerrarSesion: cerrarSesionFn,
    }),
    [usuario, cargando, tieneRol, iniciarSesionFn, cerrarSesionFn],
  );

  return <AuthContext.Provider value={estado}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): EstadoAuth {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}
