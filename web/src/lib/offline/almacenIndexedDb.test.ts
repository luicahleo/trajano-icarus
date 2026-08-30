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
});
