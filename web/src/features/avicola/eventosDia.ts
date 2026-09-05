import type { OperacionPendiente } from '../../lib/offline/tipos';
import type { MortalidadRegistro, RecogidaResumen } from '../../lib/tipos';
import type { DatosBajas, DatosRecogida } from './api';

// Un evento del día es un registro confirmado (servidor o caché) o una
// operación aún en cola. Los pendientes se distinguen por `pendiente` y su
// `datos` es el cuerpo encolado (sin id de servidor ni totales calculados).
export type Evento =
  | { hora: string; tipo: 'recogida'; datos: RecogidaResumen; pendiente?: never }
  | { hora: string; tipo: 'bajas'; datos: MortalidadRegistro; pendiente?: never }
  | { hora: string; tipo: 'recogida'; datos: DatosRecogida; pendiente: OperacionPendiente }
  | { hora: string; tipo: 'bajas'; datos: DatosBajas; pendiente: OperacionPendiente };

// Mezcla los registros del día con las operaciones aún en cola, ordenados por
// hora. Los pendientes se muestran pero no suman a los totales del día.
export function fusionarEventosDia(
  recogidas: RecogidaResumen[],
  bajas: MortalidadRegistro[],
  pendientes: OperacionPendiente[],
): Evento[] {
  const eventos: Evento[] = [
    ...bajas.map((datos) => ({ hora: datos.hora, tipo: 'bajas' as const, datos })),
    ...recogidas.map((datos) => ({ hora: datos.hora ?? '', tipo: 'recogida' as const, datos })),
    ...pendientes.map((op): Evento => {
      if (op.tipo === 'produccion.crear') {
        const cuerpo = op.cuerpo as DatosRecogida;
        return { hora: cuerpo.hora ?? '', tipo: 'recogida', datos: cuerpo, pendiente: op };
      }
      const cuerpo = op.cuerpo as DatosBajas;
      return { hora: cuerpo.hora ?? '', tipo: 'bajas', datos: cuerpo, pendiente: op };
    }),
  ];
  return eventos.sort((a, b) => a.hora.localeCompare(b.hora));
}
