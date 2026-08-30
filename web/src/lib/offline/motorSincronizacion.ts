import { ApiError } from '../http';
import type { AlmacenCola } from './almacenCola';
import type { OperacionPendiente } from './tipos';

const LOTE = 50;
const MAX_INTENTOS = 3;

interface DependenciasMotor {
  almacen: AlmacenCola;
  despachar: (op: OperacionPendiente) => Promise<void>;
  conectado: () => boolean;
  ahora?: () => Date; // inyectable para tests
}

// Motor genérico de la cola offline (spec sección 4). No conoce la API:
// el dispatcher lo aporta quien lo cablea. No bloqueante: un segundo ciclo
// simultáneo retorna de inmediato.
export function crearMotorSincronizacion(deps: DependenciasMotor): {
  sincronizar: () => Promise<void>;
} {
  const ahora = deps.ahora ?? (() => new Date());
  let enCurso = false;

  async function registrarFallo(op: OperacionPendiente): Promise<void> {
    const intentos = op.intentos + 1;
    if (intentos >= MAX_INTENTOS) {
      await deps.almacen.actualizar(op.id, { intentos, estado: 'error' });
      return;
    }
    const backoffMs = 2 ** intentos * 60_000;
    await deps.almacen.actualizar(op.id, {
      intentos,
      proximoIntentoEn: new Date(ahora().getTime() + backoffMs).toISOString(),
    });
  }

  async function sincronizar(): Promise<void> {
    if (enCurso) return;
    enCurso = true;
    try {
      const pendientes = await deps.almacen.listarPendientes(ahora().toISOString(), LOTE);
      for (const op of pendientes) {
        if (!deps.conectado()) break;
        try {
          await deps.despachar(op);
          await deps.almacen.eliminar(op.id);
        } catch (error) {
          if (error instanceof ApiError && error.status === 401) break; // sesión no renovable
          if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
            await deps.almacen.actualizar(op.id, { estado: 'error' }); // rechazo del backend
            continue;
          }
          await registrarFallo(op); // fallo de red o 5xx
        }
      }
    } finally {
      enCurso = false;
    }
  }

  return { sincronizar };
}
