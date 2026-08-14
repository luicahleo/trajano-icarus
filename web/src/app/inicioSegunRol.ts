import type { Rol } from '../lib/tipos';

// Destino de inicio según rol: Administrador ve clientes; Cliente ve sus
// trabajadores; Trabajador cae en el placeholder.
export function inicioSegunRol(rol: Rol): string {
  switch (rol) {
    case 'Administrador':
      return '/admin/clientes';
    case 'Cliente':
      return '/trabajadores';
    default:
      return '/inicio';
  }
}
