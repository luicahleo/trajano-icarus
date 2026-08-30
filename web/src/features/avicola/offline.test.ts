import { QueryClient } from '@tanstack/react-query';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { ApiError } from '../../lib/http';
import { iniciarCoordinadorOffline, listarOperaciones } from '../../app/offline/coordinador';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import { crearDespachadorAvicola, guardarBajas, guardarRecogida } from './offline';

const recogida = {
  hora: '10:30',
  cantidadMaples: 1,
  unidadesIncompletas: 2,
  maplesDescarte: 0,
  unidadesDescarte: 0,
  idempotencyKey: 'k1',
};
const bajas = { hora: '06:15', cantidadMuertas: 2, idempotencyKey: 'k2' };

describe('offline avícola', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => {
    limpiar?.();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  test('guardarRecogida online envía directo y no encola', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(JSON.stringify({ id: 'p' }), { status: 201 })),
    );
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    expect(await guardarRecogida('g1', recogida)).toBe(false);
    expect(await listarOperaciones()).toEqual([]);
  });

  test('fallo de red encola; 4xx no encola y propaga', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('fetch failed');
      }),
    );
    // El despachador rechaza (sin red): el sync automático tras encolar falla y
    // la operación permanece en la cola, que es lo que verifica la aserción.
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {
        throw new TypeError('sin red');
      }),
      almacen: crearAlmacenColaMemoria(),
    });
    expect(await guardarBajas('g1', bajas)).toBe(true);
    expect((await listarOperaciones()).length).toBe(1);

    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ title: 'Validacion' }), {
            status: 422,
            headers: { 'content-type': 'application/json' },
          }),
      ),
    );
    await expect(guardarRecogida('g1', recogida)).rejects.toBeInstanceOf(ApiError);
    expect((await listarOperaciones()).length).toBe(1); // no encoló el 4xx
  });

  test('offline encola sin llamar a la API', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    const onlineSpy = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    expect(await guardarRecogida('g1', recogida)).toBe(true);
    expect(fetchMock).not.toHaveBeenCalled();
    onlineSpy.mockRestore();
  });

  test('despachador llama al endpoint correcto e invalida queries', async () => {
    const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>(
      async () => new Response(JSON.stringify({ id: 'p' }), { status: 201 }),
    );
    vi.stubGlobal('fetch', fetchMock);
    const qc = new QueryClient();
    const invalidar = vi.spyOn(qc, 'invalidateQueries');
    const despachar = crearDespachadorAvicola(qc);
    await despachar({
      id: 'op1',
      tipo: 'produccion.crear',
      galponId: 'g1',
      cuerpo: recogida,
      estado: 'pendiente',
      intentos: 0,
      creadoEn: '2026-08-29T10:00:00.000Z',
      proximoIntentoEn: null,
    });
    const req = fetchMock.mock.calls.at(0)?.[0] as unknown as Request;
    expect(req.url).toContain('/api/galpones/g1/produccion');
    expect(invalidar).toHaveBeenCalledWith({ queryKey: ['avicola'] });
  });
});
