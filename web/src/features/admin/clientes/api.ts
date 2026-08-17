import { peticion } from '../../../lib/http';
import type { ClienteResumen, Modulo } from '../../../lib/tipos';

export async function listarClientes(): Promise<ClienteResumen[]> {
  return peticion<ClienteResumen[]>({ ruta: '/clientes' });
}

export async function crearCliente(datos: {
  razonSocial: string;
  identificadorFiscal: string;
  email: string;
  contrasena: string;
  confirmacionContrasena: string;
}): Promise<{ id: string }> {
  const cuerpo = {
    razonSocial: datos.razonSocial,
    identificadorFiscal: datos.identificadorFiscal,
    email: datos.email,
    contrasena: datos.contrasena,
  };
  return peticion<{ id: string }>({ ruta: '/clientes', metodo: 'POST', cuerpo });
}

export async function suspenderCliente(id: string): Promise<void> {
  return peticion<void>({ ruta: `/clientes/${id}/suspender`, metodo: 'POST' });
}

export async function reactivarCliente(id: string): Promise<void> {
  return peticion<void>({ ruta: `/clientes/${id}/reactivar`, metodo: 'POST' });
}

export async function definirModulos(id: string, modulos: Modulo[]): Promise<void> {
  return peticion<void>({ ruta: `/clientes/${id}/modulos`, metodo: 'PUT', cuerpo: { modulos } });
}
