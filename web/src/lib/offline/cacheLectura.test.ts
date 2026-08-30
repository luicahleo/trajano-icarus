import 'fake-indexeddb/auto';
import { describe, expect, test } from 'vitest';
import { crearCacheLecturaMemoria } from './cacheLectura';
import { crearCacheLecturaIndexedDb } from './cacheLecturaIndexedDb';

const contrato = (nombre: string, crear: () => import('./cacheLectura').CacheLectura) =>
  describe(nombre, () => {
    test('guarda y recupera; clave ausente da undefined', async () => {
      const c = crear();
      expect(await c.obtener('nada')).toBeUndefined();
      await c.guardar('granjas', [{ id: 'g1' }]);
      expect(await c.obtener('granjas')).toEqual([{ id: 'g1' }]);
      await c.guardar('granjas', [{ id: 'g2' }]); // sobrescribe
      expect(await c.obtener('granjas')).toEqual([{ id: 'g2' }]);
    });
  });

contrato('memoria', crearCacheLecturaMemoria);
contrato('indexeddb', crearCacheLecturaIndexedDb);
