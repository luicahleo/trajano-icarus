import { describe, expect, test, vi } from 'vitest';
import { ApiError } from '../http';
import { crearAlmacenColaMemoria } from './almacenCola';
import { crearMotorSincronizacion } from './motorSincronizacion';
import type { OperacionPendiente } from './tipos';

const AHORA = new Date('2026-08-29T10:00:00.000Z');
const op = (id: string, extra: Partial<OperacionPendiente> = {}): OperacionPendiente => ({
  id,
  tipo: 'produccion.crear',
  galponId: 'g1',
  cuerpo: {},
  estado: 'pendiente',
  intentos: 0,
  creadoEn: AHORA.toISOString(),
  proximoIntentoEn: null,
  ...extra,
});

const motorCon = (
  almacen: ReturnType<typeof crearAlmacenColaMemoria>,
  despachar: (op: OperacionPendiente) => Promise<void>,
  conectado: () => boolean = () => true,
) => crearMotorSincronizacion({ almacen, despachar, conectado, ahora: () => AHORA });

describe('motor de sincronización', () => {
  test('despacha pendientes y los elimina de la cola', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    const despachar = vi.fn(async () => {});
    await motorCon(almacen, despachar).sincronizar();
    expect(despachar).toHaveBeenCalledTimes(1);
    expect(await almacen.contar()).toBe(0);
  });

  test('no bloqueante: un segundo ciclo simultáneo no re-despacha', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    let liberar!: () => void;
    const despachar = vi.fn(() => new Promise<void>((r) => (liberar = r)));
    const motor = motorCon(almacen, despachar);
    const ciclo1 = motor.sincronizar();
    await motor.sincronizar(); // retorna de inmediato, sin despachar
    expect(despachar).toHaveBeenCalledTimes(1);
    liberar();
    await ciclo1;
  });

  test('fallo de red: intento+1 y backoff de 2 minutos', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    const despachar = vi.fn(async () => {
      throw new TypeError('fetch failed');
    });
    await motorCon(almacen, despachar).sincronizar();
    const [r] = await almacen.listarTodas();
    expect(r.estado).toBe('pendiente');
    expect(r.intentos).toBe(1);
    expect(r.proximoIntentoEn).toBe('2026-08-29T10:02:00.000Z');
  });

  test('tercer intento fallido pasa a error terminal', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1', { intentos: 2 }));
    const despachar = vi.fn(async () => {
      throw new TypeError('fetch failed');
    });
    await motorCon(almacen, despachar).sincronizar();
    const [r] = await almacen.listarTodas();
    expect(r.estado).toBe('error');
    expect(r.intentos).toBe(3);
  });

  test('4xx pasa a error sin consumir reintentos', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    const despachar = vi.fn(async () => {
      throw new ApiError({ status: 422, code: 'Validacion' });
    });
    await motorCon(almacen, despachar).sincronizar();
    const [r] = await almacen.listarTodas();
    expect(r.estado).toBe('error');
    expect(r.intentos).toBe(0);
  });

  test('401 pausa el ciclo sin consumir intentos', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    await almacen.agregar(op('2'));
    const despachar = vi.fn(async () => {
      throw new ApiError({ status: 401 });
    });
    await motorCon(almacen, despachar).sincronizar();
    expect(despachar).toHaveBeenCalledTimes(1); // no sigue con la segunda
    const [r] = await almacen.listarTodas();
    expect(r.intentos).toBe(0);
    expect(r.estado).toBe('pendiente');
  });

  test('corta el ciclo si se pierde la conectividad', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1'));
    await almacen.agregar(op('2'));
    let online = true;
    const despachar = vi.fn(async () => {
      online = false; // la red cae tras el primer envío
    });
    await motorCon(almacen, despachar, () => online).sincronizar();
    expect(despachar).toHaveBeenCalledTimes(1);
    expect(await almacen.contar()).toBe(1);
  });

  test('no despacha operaciones en backoff futuro', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar(op('1', { proximoIntentoEn: '2026-08-29T11:00:00.000Z' }));
    const despachar = vi.fn(async () => {});
    await motorCon(almacen, despachar).sincronizar();
    expect(despachar).not.toHaveBeenCalled();
  });
});
