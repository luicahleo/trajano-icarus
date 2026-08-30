import { afterEach, describe, expect, test, vi } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import {
  descartarOperacion,
  encolarOperacion,
  iniciarCoordinadorOffline,
  listarOperaciones,
  obtenerConteoPendientes,
  reintentarOperacion,
  suscribirAvisos,
  suscribirPendientes,
} from './coordinador';

describe('coordinador offline', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => limpiar?.());

  const arrancar = (despachar: (op: OperacionPendiente) => Promise<void>) => {
    limpiar = iniciarCoordinadorOffline({
      despachar,
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
  };

  test('encolar notifica suscriptores, avisa y dispara sync si hay red', async () => {
    const despachar = vi.fn(async () => {});
    arrancar(despachar);
    const avisoPendientes = vi.fn();
    const avisoSnackbar = vi.fn();
    suscribirPendientes(avisoPendientes);
    suscribirAvisos(avisoSnackbar);
    await encolarOperacion('produccion.crear', 'g1', { cantidadMaples: 1 });
    expect(avisoPendientes).toHaveBeenCalled();
    expect(avisoSnackbar).toHaveBeenCalledWith(
      'Guardado sin conexión: se sincronizará al volver la red.',
    );
    await vi.waitFor(() => expect(despachar).toHaveBeenCalledTimes(1));
    await vi.waitFor(() => expect(obtenerConteoPendientes()).toBe(0));
  });

  test('reintentar resetea intentos y descartar elimina', async () => {
    const despachar = vi.fn(async () => {
      throw new TypeError('sin red');
    });
    arrancar(despachar);
    await encolarOperacion('mortalidad.crear', 'g1', { cantidadMuertas: 2 });
    await vi.waitFor(async () => {
      const [op] = await listarOperaciones();
      expect(op.intentos).toBe(1);
    });
    // Sin red simulada: reintentar no re-dispara el sync y el resultado es determinista.
    const onlineSpy = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    const [encolada] = await listarOperaciones();
    await reintentarOperacion(encolada.id);
    const [op] = await listarOperaciones();
    expect(op.intentos).toBe(0);
    expect(op.estado).toBe('pendiente');
    await descartarOperacion(op.id);
    expect(obtenerConteoPendientes()).toBe(0);
    onlineSpy.mockRestore();
  });

  test('al iniciar con cola previa y red, vacía la cola (ciclo inicial)', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar({
      id: 'previa',
      tipo: 'produccion.crear',
      galponId: 'g1',
      cuerpo: {},
      estado: 'pendiente',
      intentos: 0,
      creadoEn: '2026-08-29T09:00:00.000Z',
      proximoIntentoEn: null,
    });
    const despachar = vi.fn(async () => {});
    limpiar = iniciarCoordinadorOffline({ despachar, almacen, intervaloMs: 60_000 });
    await vi.waitFor(() => expect(despachar).toHaveBeenCalledTimes(1));
    expect(await almacen.contar()).toBe(0);
  });
});
