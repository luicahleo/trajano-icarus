import { peticion } from '../../lib/http';
import { conCacheLectura } from './cacheAvicola';
import type {
  EficienciaGalpon,
  Galpon,
  Granja,
  MortalidadDia,
  NotificacionVacunacion,
  ProduccionDia,
  ProgramaVacunacionDetalle,
  ProgramaVacunacionResumen,
  TareaVacunacionResumen,
} from '../../lib/tipos';
export interface DatosGalpon {
  numero: string;
  capacidadMaxima: number;
  gallinasActuales: number;
  fechaNacimientoLote: string;
  descripcion: string | null;
}
export interface DatosRecogida {
  hora: string | null;
  cantidadMaples: number;
  unidadesIncompletas: number;
  maplesDescarte: number;
  unidadesDescarte: number;
  idempotencyKey: string;
}
export interface DatosBajas {
  hora: string | null;
  cantidadMuertas: number;
  idempotencyKey: string;
}
export const listarGranjas = () =>
  conCacheLectura('granjas', () => peticion<Granja[]>({ ruta: '/granjas' }));
export const crearGranja = (nombre: string) =>
  peticion<{ id: string }>({ ruta: '/granjas', metodo: 'POST', cuerpo: { nombre } });
export const renombrarGranja = (id: string, nombre: string) =>
  peticion<void>({ ruta: `/granjas/${id}`, metodo: 'PUT', cuerpo: { nombre } });
export const listarGalpones = (id: string) =>
  conCacheLectura(`granjas/${id}/galpones`, () =>
    peticion<Galpon[]>({ ruta: `/granjas/${id}/galpones` }),
  );
export const crearGalpon = (id: string, d: DatosGalpon) =>
  peticion<{ id: string }>({ ruta: `/granjas/${id}/galpones`, metodo: 'POST', cuerpo: d });
export const obtenerGalpon = (id: string) =>
  conCacheLectura(`galpones/${id}`, () => peticion<Galpon>({ ruta: `/galpones/${id}` }));
export const actualizarGalpon = (
  id: string,
  d: { numero: string; descripcion: string | null; capacidadMaxima: number },
) => peticion<void>({ ruta: `/galpones/${id}`, metodo: 'PUT', cuerpo: d });
export const ajustarInventarioGalpon = (id: string, gallinasActuales: number) =>
  peticion<void>({
    ruta: `/galpones/${id}/inventario`,
    metodo: 'PUT',
    cuerpo: { gallinasActuales },
  });
export const desactivarGalpon = (id: string) =>
  peticion<void>({ ruta: `/galpones/${id}`, metodo: 'DELETE' });
export const listarProduccion = (id: string, fecha?: string) =>
  conCacheLectura(`galpones/${id}/produccion/${fecha ?? 'hoy'}`, () =>
    peticion<ProduccionDia>({ ruta: `/galpones/${id}/produccion${fecha ? `?fecha=${fecha}` : ''}` }),
  );
export const registrarProduccion = (id: string, d: DatosRecogida) =>
  peticion<{ id: string }>({ ruta: `/galpones/${id}/produccion`, metodo: 'POST', cuerpo: d });
export const editarProduccion = (
  id: string,
  d: {
    hora: string;
    cantidadMaples: number;
    unidadesIncompletas: number;
    maplesDescarte: number;
    unidadesDescarte: number;
  },
) => peticion<void>({ ruta: `/produccion/${id}`, metodo: 'PUT', cuerpo: d });
export const desactivarProduccion = (id: string) =>
  peticion<void>({ ruta: `/produccion/${id}`, metodo: 'DELETE' });
export const listarMortalidad = (id: string, fecha?: string) =>
  conCacheLectura(`galpones/${id}/mortalidad/${fecha ?? 'hoy'}`, () =>
    peticion<MortalidadDia>({ ruta: `/galpones/${id}/mortalidad${fecha ? `?fecha=${fecha}` : ''}` }),
  );
export const registrarMortalidad = (id: string, d: DatosBajas) =>
  peticion<{ id: string }>({ ruta: `/galpones/${id}/mortalidad`, metodo: 'POST', cuerpo: d });
export const editarMortalidad = (id: string, d: { hora: string; cantidadMuertas: number }) =>
  peticion<void>({ ruta: `/mortalidad/${id}`, metodo: 'PUT', cuerpo: d });
export const desactivarMortalidad = (id: string) =>
  peticion<void>({ ruta: `/mortalidad/${id}`, metodo: 'DELETE' });
export const obtenerEficiencia = (id: string, desde?: string, hasta?: string) => {
  const p = new URLSearchParams();
  if (desde) p.set('desde', desde);
  if (hasta) p.set('hasta', hasta);
  const q = p.toString();
  return peticion<EficienciaGalpon>({ ruta: `/galpones/${id}/eficiencia${q ? `?${q}` : ''}` });
};
export interface DatosProgramaVacunacion {
  nombre: string;
  cantidadAves: number;
  observaciones: string | null;
}
export const listarProgramasVacunacion = (incluirInactivos = false) =>
  peticion<ProgramaVacunacionResumen[]>({
    ruta: `/vacunacion/programas${incluirInactivos ? '?incluirInactivos=true' : ''}`,
  });
export const obtenerProgramaVacunacion = (id: string) =>
  peticion<ProgramaVacunacionDetalle>({ ruta: `/vacunacion/programas/${id}` });
export const crearProgramaVacunacion = (d: DatosProgramaVacunacion) =>
  peticion<{ id: string }>({ ruta: '/vacunacion/programas', metodo: 'POST', cuerpo: d });
export const actualizarProgramaVacunacion = (id: string, d: DatosProgramaVacunacion) =>
  peticion<void>({ ruta: `/vacunacion/programas/${id}`, metodo: 'PUT', cuerpo: d });
export const desactivarProgramaVacunacion = (id: string) =>
  peticion<void>({ ruta: `/vacunacion/programas/${id}`, metodo: 'DELETE' });
export const importarCronogramaExcel = (id: string, archivo: File) => {
  const form = new FormData();
  form.append('archivo', archivo);
  return peticion<{ itemsImportados: number }>({
    ruta: `/vacunacion/programas/${id}/cronograma-excel`,
    metodo: 'POST',
    cuerpo: form,
  });
};
export const asignarPlanVacunacion = (galponId: string, programaId: string) =>
  peticion<void>({
    ruta: `/galpones/${galponId}/plan-vacunacion`,
    metodo: 'POST',
    cuerpo: { programaId },
  });
export const quitarPlanVacunacion = (galponId: string) =>
  peticion<void>({ ruta: `/galpones/${galponId}/plan-vacunacion`, metodo: 'DELETE' });
export const listarTareasVacunacion = (galponId: string) =>
  peticion<TareaVacunacionResumen[]>({ ruta: `/galpones/${galponId}/vacunacion/tareas` });
export const obtenerNotificacionVacunacion = () =>
  peticion<NotificacionVacunacion>({ ruta: '/vacunacion/tareas' });
export const completarTareaVacunacion = (
  id: string,
  d: { fechaAplicacion: string; avesVacunadas: number | null; observaciones: string | null },
) => peticion<void>({ ruta: `/vacunacion/tareas/${id}/completar`, metodo: 'POST', cuerpo: d });
export const cancelarTareaVacunacion = (id: string, motivo: string | null) =>
  peticion<void>({ ruta: `/vacunacion/tareas/${id}/cancelar`, metodo: 'POST', cuerpo: { motivo } });
