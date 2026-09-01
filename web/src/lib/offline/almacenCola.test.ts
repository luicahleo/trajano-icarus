import { describe, expect, test } from 'vitest';
import { crearAlmacenColaMemoria } from './almacenCola';
import type { OperacionPendiente } from './tipos';

const op = (id: string, extra: Partial<OperacionPendiente> = {}): OperacionPendiente => ({
  id,
  tipo: 'produccion.crear',
  galponId: 'g1',
  cuerpo: { cantidadMaples: 1 },
  estado: 'pendiente',
  intentos: 0,
  creadoEn: '2026-08-29T10:00:00.000Z',
  proximoIntentoEn: null,
  ...extra,
});

describe('AlmacenCola en memoria', () => {
  test('agregar y contar', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('1'));
    await a.agregar(op('2'));
    expect(await a.contar()).toBe(2);
  });

  test('listarPendientes excluye error y respeta proximoIntentoEn', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('lista'));
    await a.agregar(op('error', { estado: 'error' }));
    await a.agregar(op('futura', { proximoIntentoEn: '2026-08-29T12:00:00.000Z' }));
    const r = await a.listarPendientes('2026-08-29T11:00:00.000Z', 50);
    expect(r.map((x) => x.id)).toEqual(['lista']);
  });

  test('listarPendientes respeta el límite', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('1'));
    await a.agregar(op('2'));
    await a.agregar(op('3'));
    expect((await a.listarPendientes('2026-08-29T11:00:00.000Z', 2)).length).toBe(2);
  });

  test('rearmarPendientes quita el backoff conservando intentos y estado', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('backoff', { intentos: 1, proximoIntentoEn: '2026-08-29T12:00:00.000Z' }));
    await a.agregar(op('lista'));
    await a.agregar(op('err', { estado: 'error', proximoIntentoEn: '2026-08-29T12:00:00.000Z' }));

    expect(await a.rearmarPendientes()).toBe(1);

    const todas = await a.listarTodas();
    expect(todas.find((x) => x.id === 'backoff')?.proximoIntentoEn).toBeNull();
    expect(todas.find((x) => x.id === 'backoff')?.intentos).toBe(1); // historial intacto
    expect(todas.find((x) => x.id === 'lista')?.proximoIntentoEn).toBeNull();
    // Las de estado 'error' (rechazo del backend o intentos agotados) no se tocan.
    expect(todas.find((x) => x.id === 'err')?.proximoIntentoEn).toBe('2026-08-29T12:00:00.000Z');
  });

  test('actualizar cambia estado, intentos y proximoIntentoEn', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('1'));
    await a.actualizar('1', { intentos: 1, proximoIntentoEn: '2026-08-29T11:02:00.000Z' });
    const [r] = await a.listarTodas();
    expect(r.intentos).toBe(1);
    expect(r.proximoIntentoEn).toBe('2026-08-29T11:02:00.000Z');
  });

  test('eliminar quita la operación', async () => {
    const a = crearAlmacenColaMemoria();
    await a.agregar(op('1'));
    await a.eliminar('1');
    expect(await a.contar()).toBe(0);
  });
});
