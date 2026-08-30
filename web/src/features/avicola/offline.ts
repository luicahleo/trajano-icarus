import type { QueryClient } from '@tanstack/react-query';
import { encolarOperacion } from '../../app/offline/coordinador';
import { ApiError } from '../../lib/http';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import {
  registrarMortalidad,
  registrarProduccion,
  type DatosBajas,
  type DatosRecogida,
} from './api';

// Mapea la operación encolada a su endpoint y refresca la UI al sincronizar.
export function crearDespachadorAvicola(
  queryClient: QueryClient,
): (op: OperacionPendiente) => Promise<void> {
  return async (op) => {
    if (op.tipo === 'produccion.crear') {
      await registrarProduccion(op.galponId, op.cuerpo as DatosRecogida);
    } else {
      await registrarMortalidad(op.galponId, op.cuerpo as DatosBajas);
    }
    await queryClient.invalidateQueries({ queryKey: ['avicola'] });
  };
}

// Criterio del spec: encolar solo ante fallo de transporte. Un ApiError
// (4xx/5xx) es un rechazo del backend y se propaga al diálogo.
async function conCola(
  tipo: 'produccion.crear' | 'mortalidad.crear',
  galponId: string,
  cuerpo: unknown,
  enviar: () => Promise<unknown>,
): Promise<boolean> {
  if (navigator.onLine) {
    try {
      await enviar();
      return false;
    } catch (error) {
      if (error instanceof ApiError) throw error;
    }
  }
  await encolarOperacion(tipo, galponId, cuerpo);
  return true;
}

export const guardarRecogida = (galponId: string, d: DatosRecogida): Promise<boolean> =>
  conCola('produccion.crear', galponId, d, () => registrarProduccion(galponId, d));

export const guardarBajas = (galponId: string, d: DatosBajas): Promise<boolean> =>
  conCola('mortalidad.crear', galponId, d, () => registrarMortalidad(galponId, d));
