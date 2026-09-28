import { peticion } from '../../lib/http';
import { consultaPaginada } from '../../lib/paginacion';
import type { Pagina, PeticionPaginada } from '../../lib/paginacion';
import type {
  JornadaAccesoDetalle,
  JornadaAccesoResumen,
  ResultadoEnrolamientoAcceso,
  TipoMarcacion,
  TrabajadorResumen,
  ValorCorregido,
} from '../../lib/tipos';

// El alta y el listado de trabajadores viven en el módulo Clientes; esta
// feature consume el endpoint sin montar páginas de la otra.
export async function listarTrabajadoresDeAcceso(clienteId: string): Promise<TrabajadorResumen[]> {
  return peticion<TrabajadorResumen[]>({ ruta: `/clientes/${clienteId}/trabajadores` });
}

export interface FiltrosHistorial {
  trabajadorId?: string;
  desde?: string;
  hasta?: string;
}

export async function listarJornadas(
  filtros: FiltrosHistorial,
  peticionPaginada: PeticionPaginada,
): Promise<Pagina<JornadaAccesoResumen>> {
  const qs = consultaPaginada(peticionPaginada, {
    trabajadorId: filtros.trabajadorId,
    desde: filtros.desde,
    hasta: filtros.hasta,
  });
  return peticion<Pagina<JornadaAccesoResumen>>({
    ruta: `/control-acceso/jornadas?${qs}`,
  });
}

export async function obtenerJornada(id: string): Promise<JornadaAccesoDetalle> {
  return peticion<JornadaAccesoDetalle>({ ruta: `/control-acceso/jornadas/${id}` });
}

export async function corregirJornada(
  id: string,
  datos: { versionEsperada: number; motivo: string; valores: ValorCorregido[] },
): Promise<void> {
  return peticion<void>({
    ruta: `/control-acceso/jornadas/${id}/correcciones`,
    metodo: 'POST',
    cuerpo: datos,
  });
}

export async function registrarMarcacionManual(datos: {
  trabajadorId: string;
  tipo: TipoMarcacion;
  horaDeclaradaUtc: string;
  motivo: string;
  claveIdempotencia: string;
}): Promise<{ jornadaId: string }> {
  return peticion<{ jornadaId: string }>({
    ruta: '/control-acceso/marcaciones/manuales',
    metodo: 'POST',
    cuerpo: datos,
  });
}

export async function definirHabilitacionAcceso(
  trabajadorId: string,
  habilitado: boolean,
): Promise<void> {
  return peticion<void>({
    ruta: `/control-acceso/trabajadores/${trabajadorId}/habilitacion`,
    metodo: 'PUT',
    cuerpo: { habilitado },
  });
}

export interface AccesoTrabajadorResumen {
  trabajadorId: string;
  habilitado: boolean;
  enrolamiento: 'SinEnrolar' | 'Vigente' | 'Revocado';
  versionEnrolamiento: number;
}

export async function listarAccesoTrabajadores(): Promise<AccesoTrabajadorResumen[]> {
  return peticion<AccesoTrabajadorResumen[]>({ ruta: '/control-acceso/trabajadores/acceso' });
}

export async function enrolarTrabajador(
  trabajadorId: string,
  datos: { muestraBase64: string; formato: string; claveIdempotencia: string },
): Promise<ResultadoEnrolamientoAcceso> {
  return peticion<ResultadoEnrolamientoAcceso>({
    ruta: `/control-acceso/trabajadores/${trabajadorId}/enrolamiento`,
    metodo: 'POST',
    cuerpo: datos,
  });
}

export async function revocarRostro(trabajadorId: string): Promise<void> {
  return peticion<void>({
    ruta: `/control-acceso/trabajadores/${trabajadorId}/revocacion`,
    metodo: 'POST',
  });
}

// Cabecera antiforgery del origen del kiosco (spec): obligatoria en la
// activación y la salida.
const CABECERA_KIOSCO = 'X-Icarus-Kiosco';

export async function activarSesionKiosco(datos: {
  email: string;
  contrasena: string;
}): Promise<void> {
  return peticion<void>({
    ruta: '/control-acceso/kiosco/sesion',
    metodo: 'POST',
    cuerpo: datos,
    cabeceras: { [CABECERA_KIOSCO]: '1' },
  });
}

export async function cerrarSesionKiosco(datos: {
  email: string;
  contrasena: string;
}): Promise<void> {
  return peticion<void>({
    ruta: '/control-acceso/kiosco/sesion',
    metodo: 'DELETE',
    cuerpo: datos,
    cabeceras: { [CABECERA_KIOSCO]: '1' },
  });
}
