import type { Funcionalidad, Rol } from '../lib/tipos';

// Destino de inicio según rol: Administrador ve clientes; Cliente ve sus
// trabajadores; Trabajador cae en el placeholder.
export function inicioSegunRol(rol: Rol, funcionalidades: Funcionalidad[] = []): string {
  switch (rol) {
    case 'Administrador':
      return '/admin/clientes';
    case 'Cliente':
      return '/avicola';
    case 'Trabajador':
      return funcionalidades.includes('ProduccionHuevos') ||
        funcionalidades.includes('Mortalidad') ||
        funcionalidades.includes('Vacunacion')
        ? '/avicola'
        : '/inicio';
    default:
      return '/inicio';
  }
}
