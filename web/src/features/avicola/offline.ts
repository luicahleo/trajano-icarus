import type { QueryClient } from '@tanstack/react-query';
import { encolarOperacion } from '../../app/offline/coordinador';
import { ApiError, esFalloDeConectividad } from '../../lib/http';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import {
  listarGalpones,
  listarGranjas,
  listarMortalidad,
  listarProduccion,
  obtenerGalpon,
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
// (4xx/5xx) es un rechazo del backend y se propaga al diálogo; la excepción
// son los códigos de gateway (502/503/504) y el timeout, que significan que la
// API no está alcanzable y sí deben encolarse.
async function conCola(
  tipo: 'produccion.crear' | 'mortalidad.crear',
  galponId: string,
  cuerpo: unknown,
  enviar: () => Promise<unknown>,
): Promise<boolean> {
  if (navigator.onLine) {
    try {
      await conPlazo(enviar(), TIEMPO_ESPERA_RESPUESTA_MS);
      return false;
    } catch (error) {
      if (error instanceof ApiError && !esFalloDeConectividad(error)) throw error;
    }
  }
  await encolarOperacion(tipo, galponId, cuerpo);
  return true;
}

// Sin este plazo, un backend inalcanzable con navigator.onLine en true (falso
// positivo) deja el fetch colgado y el formulario queda en isPending sin
// encolar ni cerrar (diagnóstico SES-BE7075EE1213). El envío original, si
// completa después, es absorbido por IdempotencyKey, así que encolar aquí es
// seguro.
const TIEMPO_ESPERA_RESPUESTA_MS = 4_000;

function conPlazo<T>(promesa: Promise<T>, ms: number): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const temporizador = setTimeout(() => reject(new TypeError('Tiempo de espera agotado.')), ms);
    promesa.then(
      (valor) => {
        clearTimeout(temporizador);
        resolve(valor);
      },
      (error) => {
        clearTimeout(temporizador);
        reject(error);
      },
    );
  });
}

export const guardarRecogida = (galponId: string, d: DatosRecogida): Promise<boolean> =>
  conCola('produccion.crear', galponId, d, () => registrarProduccion(galponId, d));

export const guardarBajas = (galponId: string, d: DatosBajas): Promise<boolean> =>
  conCola('mortalidad.crear', galponId, d, () => registrarMortalidad(galponId, d));

// Descarga los datos del día para operar sin red (spec decisión 5). Las
// funciones de api.ts ya escriben en la caché; aquí solo se recorren.
// Fallos individuales no abortan el precalentado.
export async function precalentarCacheAvicola(): Promise<void> {
  const granjas = await listarGranjas();
  for (const granja of granjas) {
    const galpones = await listarGalpones(granja.id);
    for (const galpon of galpones) {
      await Promise.all([
        obtenerGalpon(galpon.id).catch(() => undefined),
        listarProduccion(galpon.id).catch(() => undefined),
        listarMortalidad(galpon.id).catch(() => undefined),
      ]);
    }
  }
}
