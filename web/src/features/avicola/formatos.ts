import { HUEVOS_POR_MAPLE } from './constantes';
export function totalHuevos(maples:number,sueltos:number):number{return maples*HUEVOS_POR_MAPLE+sueltos;}
export function formatearConteo(maples:number,sueltos:number):string{return `${maples} maples + ${sueltos} (= ${totalHuevos(maples,sueltos)})`;}
