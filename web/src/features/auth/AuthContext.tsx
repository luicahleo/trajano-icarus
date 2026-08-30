import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { renovarSesion } from '../../lib/http';
import { clearAccessToken } from '../../lib/session';
import type { Funcionalidad, Modulo, Rol, UsuarioActual } from '../../lib/tipos';
import {
  borrarSesionOffline,
  guardarSesionOffline,
  obtenerSesionOffline,
} from '../../app/offline/sesionOffline';
import { iniciarSesion, obtenerMe, type Credenciales } from './api';

export interface EstadoAuth {
  usuario: UsuarioActual | null;
  estaAutenticado: boolean;
  cargando: boolean;
  rol: Rol | null;
  correo: string | null;
  clienteId: string | null;
  modulos: Modulo[];
  funcionalidades: Funcionalidad[];
  tieneRol: (...roles: Rol[]) => boolean;
  tieneFuncionalidad: (...funcionalidades: Funcionalidad[]) => boolean;
  iniciarSesion: (cred: Credenciales) => Promise<void>;
  cerrarSesion: () => void;
}

const AuthContext = createContext<EstadoAuth | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<UsuarioActual | null>(null);
  const [cargando, setCargando] = useState(true);
  // true cuando la sesión proviene del snapshot offline (sin token); dispara
  // la revalidación al reconectar.
  const [esSnapshot, setEsSnapshot] = useState(false);

  useEffect(() => {
    let activo = true;
    void (async () => {
      try {
        const restaurada = await renovarSesion();
        if (!restaurada || !activo) return; // rechazo del backend: sin fallback
        const me = await obtenerMe();
        if (activo) setUsuario(me);
        await guardarSesionOffline(me).catch(() => undefined); // trabajador → snapshot; otro rol → borra
      } catch {
        // fallo de red: restaurar sesión offline del trabajador si hay snapshot
        const snapshot = await obtenerSesionOffline().catch(() => null);
        if (activo && snapshot) {
          setUsuario(snapshot);
          setEsSnapshot(true);
        }
      } finally {
        if (activo) setCargando(false);
      }
    })();
    return () => {
      activo = false;
    };
  }, []);

  // Revalidación al reconectar si la sesión viene del snapshot offline.
  useEffect(() => {
    if (!esSnapshot) return;
    const revalidar = () => {
      void (async () => {
        try {
          if (await renovarSesion()) {
            const me = await obtenerMe();
            setUsuario(me);
            setEsSnapshot(false);
            await guardarSesionOffline(me).catch(() => undefined);
          }
        } catch {
          // sigue sin red real; se reintenta en el próximo evento online
        }
      })();
    };
    window.addEventListener('online', revalidar);
    return () => window.removeEventListener('online', revalidar);
  }, [esSnapshot]);

  const iniciarSesionFn = useCallback(async (cred: Credenciales) => {
    await iniciarSesion(cred);
    const me = await obtenerMe();
    setUsuario(me);
    setEsSnapshot(false);
    await guardarSesionOffline(me).catch(() => undefined);
  }, []);

  const cerrarSesionFn = useCallback(() => {
    clearAccessToken();
    setUsuario(null);
    setEsSnapshot(false);
    void borrarSesionOffline();
  }, []);

  const tieneRol = useCallback(
    (...roles: Rol[]) => (usuario ? roles.includes(usuario.rol) : false),
    [usuario],
  );

  const tieneFuncionalidad = useCallback(
    (...funcionalidades: Funcionalidad[]) =>
      usuario ? funcionalidades.some((f) => usuario.funcionalidades.includes(f)) : false,
    [usuario],
  );

  const estado = useMemo<EstadoAuth>(
    () => ({
      usuario,
      estaAutenticado: usuario !== null,
      cargando,
      rol: usuario?.rol ?? null,
      correo: usuario?.correo ?? null,
      clienteId: usuario?.clienteId ?? null,
      modulos: usuario?.modulos ?? [],
      funcionalidades: usuario?.funcionalidades ?? [],
      tieneRol,
      tieneFuncionalidad,
      iniciarSesion: iniciarSesionFn,
      cerrarSesion: cerrarSesionFn,
    }),
    [usuario, cargando, tieneRol, tieneFuncionalidad, iniciarSesionFn, cerrarSesionFn],
  );

  return <AuthContext.Provider value={estado}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): EstadoAuth {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}
