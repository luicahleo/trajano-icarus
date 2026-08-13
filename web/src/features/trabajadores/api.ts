import { peticion } from '../../lib/http';
import type { TrabajadorResumen } from '../../lib/tipos';

export async function listarTrabajadores(clienteId: string): Promise<TrabajadorResumen[]> {
  return peticion<TrabajadorResumen[]>({ ruta: `/clientes/${clienteId}/trabajadores` });
}

export async function crearTrabajador(
  clienteId: string,
  datos: { nombre: string; documentoIdentidad: string; cargo: string; fechaIngreso: string },
): Promise<{ id: string }> {
  return peticion<{ id: string }>({ ruta: `/clientes/${clienteId}/trabajadores`, metodo: 'POST', cuerpo: datos });
}

export async function cesarTrabajador(id: string, fechaCese: string): Promise<void> {
  return peticion<void>({ ruta: `/clientes/trabajadores/${id}/cese`, metodo: 'POST', cuerpo: { fechaCese } });
}

export async function desactivarTrabajador(id: string): Promise<void> {
  return peticion<void>({ ruta: `/clientes/trabajadores/${id}`, metodo: 'DELETE' });
}
