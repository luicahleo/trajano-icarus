import { renovarCorrelationId } from '../../lib/correlation';
import { peticion } from '../../lib/http';
import { setAccessToken } from '../../lib/session';
import type { Rol, SesionInfo, UsuarioActual } from '../../lib/tipos';

export interface Credenciales {
  email: string;
  contrasena: string;
}

export async function iniciarSesion(cred: Credenciales): Promise<SesionInfo> {
  const datos = await peticion<SesionInfo>({ ruta: '/identidad/sesion', metodo: 'POST', cuerpo: cred });
  renovarCorrelationId();
  setAccessToken(datos.accessToken);
  return datos;
}

export async function obtenerMe(): Promise<UsuarioActual> {
  return peticion<UsuarioActual>({ ruta: '/identidad/me' });
}

export interface DatosNuevoUsuario {
  email: string;
  contrasena: string;
  rol: Rol;
  clienteId?: string | null;
  trabajadorId?: string | null;
}

export async function crearUsuario(datos: DatosNuevoUsuario): Promise<{ id: string }> {
  return peticion<{ id: string }>({ ruta: '/identidad/usuarios', metodo: 'POST', cuerpo: datos });
}
