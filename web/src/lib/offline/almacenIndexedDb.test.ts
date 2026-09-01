import 'fake-indexeddb/auto';
import { describe, expect, test } from 'vitest';
import { crearAlmacenColaIndexedDb } from './almacenIndexedDb';
import type { OperacionPendiente } from './tipos';

const op = (id: string, extra: Partial<OperacionPendiente> = {}): OperacionPendiente => ({
  id,
  tipo: 'mortalidad.crear',
  galponId: 'g1',
  cuerpo: { cantidadMuertas: 2 },
  estado: 'pendiente',
  intentos: 0,
  creadoEn: '2026-08-29T10:00:00.000Z',
  proximoIntentoEn: null,
  ...extra,
});

describe('AlmacenCola IndexedDB', () => {
  test('mismo contrato que el almacén en memoria', async () => {
    const a = crearAlmacenColaIndexedDb();
    await a.agregar(op('lista'));
    await a.agregar(op('error', { estado: 'error' }));
    await a.agregar(op('futura', { proximoIntentoEn: '2026-08-29T12:00:00.000Z' }));
    expect(await a.contar()).toBe(3);
    const r = await a.listarPendientes('2026-08-29T11:00:00.000Z', 50);
    expect(r.map((x) => x.id)).toEqual(['lista']);
    await a.actualizar('lista', { intentos: 1 });
    expect((await a.listarTodas()).find((x) => x.id === 'lista')?.intentos).toBe(1);
    await a.eliminar('lista');
    expect(await a.contar()).toBe(2);
  });

  test('rearmarPendientes quita el backoff conservando intentos y estado', async () => {
    const a = crearAlmacenColaIndexedDb();
    await a.agregar(
      op('rearmar-backoff', { intentos: 1, proximoIntentoEn: '2026-08-29T12:00:00.000Z' }),
    );
    await a.agregar(op('rearmar-error', { estado: 'error' }));
    // La base se comparte entre tests del archivo: contar solo lo rearmable ahora.
    const rearmables = (await a.listarTodas()).filter(
      (o) => o.estado === 'pendiente' && o.proximoIntentoEn !== null,
    ).length;

    expect(await a.rearmarPendientes()).toBe(rearmables);

    const todas = await a.listarTodas();
    const rearmada = todas.find((x) => x.id === 'rearmar-backoff');
    expect(rearmada?.proximoIntentoEn).toBeNull();
    expect(rearmada?.intentos).toBe(1);
    expect(todas.find((x) => x.id === 'rearmar-error')?.estado).toBe('error');
    await a.eliminar('rearmar-backoff');
    await a.eliminar('rearmar-error');
  });
});
