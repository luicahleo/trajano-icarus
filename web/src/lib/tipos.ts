export type Rol = 'Administrador' | 'SoporteTecnico' | 'Cliente' | 'Trabajador';
export type Modulo = 'GestionAvicola' | 'ControlAcceso';

export interface SesionInfo {
  accessToken: string;
  expiraEnSegundos: number;
}

export interface UsuarioActual {
  usuarioId: string;
  rol: Rol;
  clienteId: string | null;
}

export interface ClienteResumen {
  id: string;
  razonSocial: string;
  identificadorFiscal: string;
  estaActivo: boolean;
  modulos: Modulo[];
}

export interface TrabajadorResumen {
  id: string;
  nombre: string;
  documentoIdentidad: string;
  cargo: string;
  fechaIngreso: string;
  fechaCese: string | null;
}
