import { peticion } from '../../lib/http';
import type {EficienciaGalpon,Galpon,Granja,MortalidadDia,ProduccionDia} from '../../lib/tipos';
export interface DatosGalpon{numero:string;capacidadMaxima:number;gallinasActuales:number;fechaNacimientoLote:string;descripcion:string|null}
export interface DatosRecogida{hora:string|null;cantidadMaples:number;unidadesIncompletas:number;maplesDescarte:number;unidadesDescarte:number;idempotencyKey:string}
export interface DatosBajas{hora:string|null;cantidadMuertas:number;idempotencyKey:string}
export const listarGranjas=()=>peticion<Granja[]>({ruta:'/granjas'});
export const crearGranja=(nombre:string)=>peticion<{id:string}>({ruta:'/granjas',metodo:'POST',cuerpo:{nombre}});
export const renombrarGranja=(id:string,nombre:string)=>peticion<void>({ruta:`/granjas/${id}`,metodo:'PUT',cuerpo:{nombre}});
export const listarGalpones=(id:string)=>peticion<Galpon[]>({ruta:`/granjas/${id}/galpones`});
export const crearGalpon=(id:string,d:DatosGalpon)=>peticion<{id:string}>({ruta:`/granjas/${id}/galpones`,metodo:'POST',cuerpo:d});
export const obtenerGalpon=(id:string)=>peticion<Galpon>({ruta:`/galpones/${id}`});
export const actualizarGalpon=(id:string,d:{numero:string;descripcion:string|null;capacidadMaxima:number})=>peticion<void>({ruta:`/galpones/${id}`,metodo:'PUT',cuerpo:d});
export const ajustarInventarioGalpon=(id:string,gallinasActuales:number)=>peticion<void>({ruta:`/galpones/${id}/inventario`,metodo:'PUT',cuerpo:{gallinasActuales}});
export const desactivarGalpon=(id:string)=>peticion<void>({ruta:`/galpones/${id}`,metodo:'DELETE'});
export const listarProduccion=(id:string,fecha?:string)=>peticion<ProduccionDia>({ruta:`/galpones/${id}/produccion${fecha?`?fecha=${fecha}`:''}`});
export const registrarProduccion=(id:string,d:DatosRecogida)=>peticion<{id:string}>({ruta:`/galpones/${id}/produccion`,metodo:'POST',cuerpo:d});
export const editarProduccion=(id:string,d:{hora:string;cantidadMaples:number;unidadesIncompletas:number;maplesDescarte:number;unidadesDescarte:number})=>peticion<void>({ruta:`/produccion/${id}`,metodo:'PUT',cuerpo:d});
export const desactivarProduccion=(id:string)=>peticion<void>({ruta:`/produccion/${id}`,metodo:'DELETE'});
export const listarMortalidad=(id:string,fecha?:string)=>peticion<MortalidadDia>({ruta:`/galpones/${id}/mortalidad${fecha?`?fecha=${fecha}`:''}`});
export const registrarMortalidad=(id:string,d:DatosBajas)=>peticion<{id:string}>({ruta:`/galpones/${id}/mortalidad`,metodo:'POST',cuerpo:d});
export const editarMortalidad=(id:string,d:{hora:string;cantidadMuertas:number})=>peticion<void>({ruta:`/mortalidad/${id}`,metodo:'PUT',cuerpo:d});
export const desactivarMortalidad=(id:string)=>peticion<void>({ruta:`/mortalidad/${id}`,metodo:'DELETE'});
export const obtenerEficiencia=(id:string,desde?:string,hasta?:string)=>{const p=new URLSearchParams();if(desde)p.set('desde',desde);if(hasta)p.set('hasta',hasta);const q=p.toString();return peticion<EficienciaGalpon>({ruta:`/galpones/${id}/eficiencia${q?`?${q}`:''}`});};
