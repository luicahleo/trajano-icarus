import { afterEach, describe, expect, test, vi } from 'vitest';
import { avisarActividadApi } from '../../lib/offline/actividadApi';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import { obtenerEventosRecientes } from '../../lib/sesionDiagnostico';
import {
  actualizarContenidoOperacion,
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

  test('si el almacén falla al encolar, registra el fallo en el diagnóstico y propaga', async () => {
    const almacen = {
      ...crearAlmacenColaMemoria(),
      agregar: () => Promise.reject(new Error('IndexedDB no disponible')),
    };
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen,
      intervaloMs: 60_000,
    });

    await expect(encolarOperacion('produccion.crear', 'g1', {})).rejects.toThrow(
      'IndexedDB no disponible',
    );
    const eventos = obtenerEventosRecientes(10);
    expect(
      eventos.some(
        (e) => e.eventName === 'flow.offline_queue' && e.detail.includes('Fallo al encolar'),
      ),
    ).toBe(true);
  });

  test('actualizarContenidoOperacion cambia el cuerpo y registra el evento', async () => {
    const almacen = crearAlmacenColaMemoria();
    const onlineSpy = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen,
      intervaloMs: 60_000,
    });
    await encolarOperacion('produccion.crear', 'g1', { cantidadMaples: 1 });
    const [op] = await listarOperaciones();

    await actualizarContenidoOperacion(op.id, { cantidadMaples: 5 });

    const [actualizada] = await listarOperaciones();
    expect((actualizada.cuerpo as { cantidadMaples: number }).cantidadMaples).toBe(5);
    expect(
      obtenerEventosRecientes(10).some(
        (e) => e.eventName === 'flow.offline_queue' && e.detail.includes('actualizada'),
      ),
    ).toBe(true);
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

  test('si la sonda no alcanza el API, el ciclo inicial no quema intentos', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar({
      id: 'sin-api',
      tipo: 'produccion.crear',
      galponId: 'g1',
      cuerpo: {},
      estado: 'pendiente',
      intentos: 0,
      creadoEn: '2026-08-29T09:00:00.000Z',
      proximoIntentoEn: null,
    });
    const despachar = vi.fn(async () => {});
    limpiar = iniciarCoordinadorOffline({
      despachar,
      almacen,
      intervaloMs: 60_000,
      sonda: async () => false,
    });

    await vi.waitFor(() => {
      expect(
        obtenerEventosRecientes(10).some(
          (e) => e.eventName === 'flow.offline_sync' && e.detail.includes('pospuesta'),
        ),
      ).toBe(true);
    });
    expect(despachar).not.toHaveBeenCalled();
    const [op] = await almacen.listarTodas();
    expect(op.intentos).toBe(0); // sin intento quemado contra una red caída
  });

  test('al volver la red (evento online) reactiva operaciones en backoff', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar({
      id: 'en-backoff',
      tipo: 'produccion.crear',
      galponId: 'g1',
      cuerpo: {},
      estado: 'pendiente',
      intentos: 1,
      creadoEn: '2026-08-29T09:00:00.000Z',
      proximoIntentoEn: '2999-01-01T00:00:00.000Z', // backoff lejano
    });
    let apiAccesible = false; // arranca con el API caído: el ciclo inicial no debe tocarla
    const despachar = vi.fn(async () => {});
    limpiar = iniciarCoordinadorOffline({
      despachar,
      almacen,
      intervaloMs: 60_000,
      sonda: async () => apiAccesible,
    });
    await vi.waitFor(() => {
      expect(
        obtenerEventosRecientes(10).some(
          (e) => e.eventName === 'flow.offline_sync' && e.detail.includes('pospuesta'),
        ),
      ).toBe(true);
    });
    expect(despachar).not.toHaveBeenCalled();

    apiAccesible = true;
    window.dispatchEvent(new Event('online'));

    await vi.waitFor(() => expect(despachar).toHaveBeenCalledTimes(1));
    expect(await almacen.contar()).toBe(0);
    expect(
      obtenerEventosRecientes(20).some(
        (e) => e.eventName === 'flow.offline_sync' && e.detail.includes('Reactivada'),
      ),
    ).toBe(true);
  });

  test('si el despacho falla, el cierre de la sync informa de la espera por reintentos', async () => {
    const despachar = vi.fn(async () => {
      throw new TypeError('sin red');
    });
    arrancar(despachar);
    await encolarOperacion('produccion.crear', 'g1', { cantidadMaples: 1 });

    await vi.waitFor(() => {
      expect(
        obtenerEventosRecientes(20).some(
          (e) => e.eventName === 'flow.offline_sync' && e.detail.includes('en espera por reintentos'),
        ),
      ).toBe(true);
    });
  });

  test('con pendientes y sonda caída, reintenta la sonda solo hasta recuperar el API', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar({
      id: 'reintento-sonda',
      tipo: 'produccion.crear',
      galponId: 'g1',
      cuerpo: {},
      estado: 'pendiente',
      intentos: 0,
      creadoEn: '2026-08-29T09:00:00.000Z',
      proximoIntentoEn: null,
    });
    let apiVivo = false;
    const despachar = vi.fn(async () => {});
    limpiar = iniciarCoordinadorOffline({
      despachar,
      almacen,
      intervaloMs: 60_000,
      sonda: async () => apiVivo,
      reintentoSondaMs: 50,
    });
    await vi.waitFor(() => {
      expect(
        obtenerEventosRecientes(10).some(
          (e) => e.eventName === 'flow.offline_sync' && e.detail.includes('pospuesta'),
        ),
      ).toBe(true);
    });
    expect(despachar).not.toHaveBeenCalled();

    // El API se recupera sin evento online (WiFi viva, backend caído): el
    // reintento programado de la sonda lo descubre y vacía la cola.
    apiVivo = true;
    await vi.waitFor(() => expect(despachar).toHaveBeenCalledTimes(1));
    expect(await almacen.contar()).toBe(0);
  });

  test('sin pendientes, una sonda caída no programa reintentos', async () => {
    const sonda = vi.fn(async () => false);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
      sonda,
      reintentoSondaMs: 50,
    });

    await new Promise((r) => setTimeout(r, 200)); // ~4 ventanas de reintento
    expect(sonda).toHaveBeenCalledTimes(1); // solo la del ciclo inicial
  });

  test('una respuesta real del API adelanta la sync pospuesta sin esperar al reintento', async () => {
    const almacen = crearAlmacenColaMemoria();
    await almacen.agregar({
      id: 'adelantar-sync',
      tipo: 'produccion.crear',
      galponId: 'g1',
      cuerpo: {},
      estado: 'pendiente',
      intentos: 0,
      creadoEn: '2026-08-29T09:00:00.000Z',
      proximoIntentoEn: null,
    });
    let apiVivo = false;
    const despachar = vi.fn(async () => {});
    limpiar = iniciarCoordinadorOffline({
      despachar,
      almacen,
      intervaloMs: 60_000,
      sonda: async () => apiVivo,
      reintentoSondaMs: 60_000, // largo: solo el aviso puede adelantar la sync
    });
    await vi.waitFor(() => {
      expect(
        // «reintento en 60 s» distingue la pospuesta de ESTE ciclo: el buffer
        // de eventos es compartido entre tests y una pospuesta vieja resolvería
        // la espera antes de que el reintento de este ciclo quede armado.
        obtenerEventosRecientes(10).some(
          (e) =>
            e.eventName === 'flow.offline_sync' &&
            e.detail.includes('pospuesta') &&
            e.detail.includes('60 s'),
        ),
      ).toBe(true);
    });
    expect(despachar).not.toHaveBeenCalled();

    // Diagnóstico SES-C54A1A220B07: el API volvió entre sondas (la UI ya hacía
    // GET 200) y la cola esperó al timer ciego de 15 s. Una respuesta real es
    // prueba de conectividad: la sync se adelanta sin esperar al reintento.
    apiVivo = true;
    avisarActividadApi();
    await vi.waitFor(() => expect(despachar).toHaveBeenCalledTimes(1));
    expect(await almacen.contar()).toBe(0);
  });

  test('sin sync pospuesta, el aviso de actividad del API no dispara sondas extra', async () => {
    const sonda = vi.fn(async () => true);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
      sonda,
    });
    await vi.waitFor(() => expect(sonda).toHaveBeenCalledTimes(1)); // ciclo inicial

    avisarActividadApi();
    await new Promise((r) => setTimeout(r, 100));
    expect(sonda).toHaveBeenCalledTimes(1);
  });
});
