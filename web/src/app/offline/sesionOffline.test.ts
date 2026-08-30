import 'fake-indexeddb/auto';
import { describe, expect, test } from 'vitest';
import { crearCacheLecturaIndexedDb } from '../../lib/offline/cacheLecturaIndexedDb';
import type { UsuarioActual } from '../../lib/tipos';
import {
  borrarSesionOffline,
  guardarSesionOffline,
  obtenerSesionOffline,
} from './sesionOffline';

const trabajador: UsuarioActual = {
  usuarioId: 'u1',
  correo: 'campo@example.com',
  rol: 'Trabajador',
  clienteId: 'c1',
  trabajadorId: 't1',
  modulos: ['GestionAvicola'],
  funcionalidades: ['ProduccionHuevos', 'Mortalidad'],
};

describe('sesión offline', () => {
  test('guarda snapshot del trabajador sin correo ni token', async () => {
    await guardarSesionOffline(trabajador);
    const snap = await obtenerSesionOffline();
    expect(snap?.rol).toBe('Trabajador');
    expect(snap?.funcionalidades).toEqual(['ProduccionHuevos', 'Mortalidad']);
    expect(snap?.correo).toBeNull();
    expect(JSON.stringify(snap)).not.toContain('campo@example.com');
  });

  test('guarda con otro rol borra el snapshot', async () => {
    await guardarSesionOffline(trabajador);
    await guardarSesionOffline({ ...trabajador, rol: 'Cliente', clienteId: 'c1' });
    expect(await obtenerSesionOffline()).toBeNull();
  });

  test('borrarSesionOffline lo elimina', async () => {
    await guardarSesionOffline(trabajador);
    await borrarSesionOffline();
    expect(await obtenerSesionOffline()).toBeNull();
  });

  test('snapshot con más de 12 horas no se restaura y se borra', async () => {
    // Sembrar un snapshot expirado escribiendo el wrapper crudo directamente.
    await crearCacheLecturaIndexedDb().guardar('sesion-offline', {
      guardadoEn: new Date('2026-08-28T20:00:00.000Z').toISOString(),
      usuario: { ...trabajador, correo: null },
    });
    const ahora = new Date('2026-08-29T10:00:00.000Z');
    expect(await obtenerSesionOffline(ahora)).toBeNull();
    expect(await crearCacheLecturaIndexedDb().obtener('sesion-offline')).toBeNull();
  });

  test('snapshot fresco se restaura', async () => {
    await guardarSesionOffline(trabajador);
    const snap = await obtenerSesionOffline(new Date('2026-08-29T10:00:00.000Z'));
    expect(snap?.rol).toBe('Trabajador');
  });
});
