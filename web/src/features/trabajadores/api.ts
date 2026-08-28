import { peticion } from '../../lib/http';
import type { FuncionalidadOperativaTrabajador, TrabajadorResumen } from '../../lib/tipos';

export async function listarTrabajadores(clienteId: string): Promise<TrabajadorResumen[]> {
  return peticion<TrabajadorResumen[]>({ ruta: `/clientes/${clienteId}/trabajadores` });
}

export async function crearTrabajador(
  clienteId: string,
  datos: {
    nombre: string;
    documentoIdentidad: string;
    cargo: string;
    fechaIngreso: string;
    email: string;
    contrasena: string;
    confirmacionContrasena: string;
  },
): Promise<{ id: string }> {
  const cuerpo = {
    nombre: datos.nombre,
    documentoIdentidad: datos.documentoIdentidad,
    cargo: datos.cargo,
    fechaIngreso: datos.fechaIngreso,
    email: datos.email,
    contrasena: datos.contrasena,
  };
  return peticion<{ id: string }>({
    ruta: `/clientes/${clienteId}/trabajadores`,
    metodo: 'POST',
    cuerpo,
  });
}

export async function cesarTrabajador(id: string, fechaCese: string): Promise<void> {
  return peticion<void>({
    ruta: `/clientes/trabajadores/${id}/cese`,
    metodo: 'POST',
    cuerpo: { fechaCese },
  });
}

export async function desactivarTrabajador(id: string): Promise<void> {
  return peticion<void>({ ruta: `/clientes/trabajadores/${id}`, metodo: 'DELETE' });
}

export async function definirFuncionalidades(
  clienteId: string,
  trabajadorId: string,
  funcionalidades: FuncionalidadOperativaTrabajador[],
): Promise<void> {
  return peticion<void>({
    ruta: `/clientes/${clienteId}/trabajadores/${trabajadorId}/funcionalidades`,
    metodo: 'PUT',
    cuerpo: { funcionalidades },
  });
}
