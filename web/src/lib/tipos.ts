export type Rol = 'Administrador' | 'Cliente' | 'Trabajador';
export type Modulo = 'GestionAvicola' | 'ControlAcceso';
export type Funcionalidad =
  | 'Granjas'
  | 'Galpones'
  | 'ProduccionHuevos'
  | 'Mortalidad'
  | 'Vacunacion'
  | 'Alimentacion'
  | 'Despachos'
  | 'Precios';
export type FuncionalidadOperativaTrabajador = 'ProduccionHuevos' | 'Mortalidad';

export interface SesionInfo {
  accessToken: string;
  expiraEnSegundos: number;
}

export interface UsuarioActual {
  usuarioId: string;
  rol: Rol;
  clienteId: string | null;
  trabajadorId: string | null;
  modulos: Modulo[];
  funcionalidades: Funcionalidad[];
}

export interface Granja {
  id: string;
  nombre: string;
}

export interface Galpon {
  id: string;
  numero: string;
  capacidadMaxima: number;
  gallinasActuales: number;
  fechaNacimientoLote: string;
  descripcion: string | null;
}

export interface RecogidaResumen {
  id: string;
  fecha: string;
  hora: string;
  cantidadMaples: number;
  unidadesIncompletas: number;
  maplesDescarte: number;
  unidadesDescarte: number;
  gallinasVivas: number;
  totalVendible: number;
  totalDescarte: number;
}

export interface ProduccionDia {
  galponId: string;
  fecha: string;
  recogidas: RecogidaResumen[];
  totalMaples: number;
  totalUnidadesIncompletas: number;
  totalVendible: number;
  totalMaplesDescarte: number;
  totalUnidadesDescarte: number;
  totalDescarte: number;
}

export interface MortalidadRegistro {
  id: string;
  fecha: string;
  hora: string;
  cantidadMuertas: number;
  gallinasVivas: number;
}

export interface MortalidadDia {
  galponId: string;
  fecha: string;
  registros: MortalidadRegistro[];
  totalMuertas: number;
}

export interface EficienciaDia {
  fecha: string;
  totalMaples: number;
  totalUnidadesIncompletas: number;
  totalVendible: number;
  totalMaplesDescarte: number;
  totalUnidadesDescarte: number;
  totalDescarte: number;
  gallinasVivas: number;
  eficiencia: number;
  bajoUmbral: boolean;
}

export interface EficienciaGalpon {
  galponId: string;
  desde: string;
  hasta: string;
  dias: EficienciaDia[];
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
  funcionalidades: Funcionalidad[];
}
