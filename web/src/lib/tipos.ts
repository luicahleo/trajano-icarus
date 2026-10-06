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
  | 'Precios'
  | 'PedidoAlimento'
  | 'DespachoHuevo';
export type FuncionalidadOperativaTrabajador =
  | 'ProduccionHuevos'
  | 'Mortalidad'
  | 'Vacunacion'
  | 'PedidoAlimento'
  | 'DespachoHuevo';

export interface SesionInfo {
  accessToken: string;
  expiraEnSegundos: number;
}

export interface UsuarioActual {
  usuarioId: string;
  correo: string | null;
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

export interface TrabajadorNombreResumen {
  id: string;
  nombre: string;
}

export type EstadoTareaVacunacion = 'Pendiente' | 'Completada' | 'Cancelada';

export interface ProgramaVacunacionResumen {
  id: string;
  nombre: string;
  fechaEmision: string | null;
  cantidadAves: number;
  observaciones: string | null;
  estaActivo: boolean;
}

export interface ItemPlanVacunacionResumen {
  id: string;
  edadDia: number;
  vacuna: string;
  modoAplicacion: string | null;
  observaciones: string | null;
  fecha: string | null;
}

export interface ProgramaVacunacionDetalle extends ProgramaVacunacionResumen {
  items: ItemPlanVacunacionResumen[];
}

export interface TareaVacunacionResumen {
  id: string;
  galponId: string;
  edadDia: number;
  vacuna: string;
  modoAplicacion: string | null;
  fechaProgramada: string;
  estado: EstadoTareaVacunacion;
  fechaAplicacion: string | null;
  avesVacunadas: number | null;
  observacionesProgramadas: string | null;
  observacionesAplicacion: string | null;
  motivoCancelacion: string | null;
  programaNombre: string | null;
}

export interface NotificacionVacunacion {
  vencidasYHoy: TareaVacunacionResumen[];
  proximas: TareaVacunacionResumen[];
}

export type EstadoJornadaAcceso = 'Abierta' | 'Completa' | 'Incompleta';
export type TipoMarcacion = 'Entrada' | 'Salida';
export type OrigenMarcacion = 'Kiosco' | 'ManualCliente';

export interface JornadaAccesoResumen {
  id: string;
  trabajadorId: string;
  fechaBoliviana: string;
  estado: EstadoJornadaAcceso;
  cantidadMarcaciones: number;
  version: number;
}

export interface ValorCorregido {
  tipo: TipoMarcacion;
  instanteUtc: string;
}

export interface MarcacionAccesoDetalle {
  id: string;
  tipo: TipoMarcacion;
  origen: OrigenMarcacion;
  instanteUtc: string;
  horaDeclaradaUtc: string;
  creadaEnUtc: string;
  autorId: string | null;
  motivo: string | null;
}

export interface RevisionJornadaDetalle {
  id: string;
  instanteCorreccionUtc: string;
  hastaSecuenciaOriginal: number;
  motivo: string;
  autorId: string;
  valores: ValorCorregido[];
}

export interface JornadaAccesoDetalle {
  id: string;
  trabajadorId: string;
  fechaBoliviana: string;
  estado: EstadoJornadaAcceso;
  version: number;
  marcaciones: MarcacionAccesoDetalle[];
  revisiones: RevisionJornadaDetalle[];
  valoresEfectivos: ValorCorregido[];
}

export interface ResultadoEnrolamientoAcceso {
  exitoso: boolean;
  versionEnrolamiento: number;
  motivo: string | null;
}
