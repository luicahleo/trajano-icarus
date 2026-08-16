import { peticion } from '../../lib/http';
import { setAccessToken } from '../../lib/session';
import type { SesionInfo, UsuarioActual } from '../../lib/tipos';

export interface Credenciales {
  email: string;
  contrasena: string;
}

export async function iniciarSesion(cred: Credenciales): Promise<SesionInfo> {
  const datos = await peticion<SesionInfo>({ ruta: '/identidad/sesion', metodo: 'POST', cuerpo: cred });
  setAccessToken(datos.accessToken);
  return datos;
}

export async function obtenerMe(): Promise<UsuarioActual> {
  return peticion<UsuarioActual>({ ruta: '/identidad/me' });
}
