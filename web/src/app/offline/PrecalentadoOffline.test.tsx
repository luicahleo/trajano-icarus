import { render, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import { crearCacheLecturaMemoria } from '../../lib/offline/cacheLectura';
import { iniciarCoordinadorOffline } from './coordinador';
import { PrecalentadoOffline } from './PrecalentadoOffline';

// AuthContext real es pesado para este test: se mockea useAuth.
const authMock = vi.fn();
vi.mock('../../features/auth/AuthContext', () => ({
  useAuth: () => authMock(),
}));

const respuesta = (cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  });

describe('PrecalentadoOffline', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => {
    limpiar?.();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  test('con rol Trabajador descarga y cachea los datos del día', async () => {
    const cache = crearCacheLecturaMemoria();
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
      cache,
      intervaloMs: 60_000,
    });
    authMock.mockReturnValue({ rol: 'Trabajador', estaAutenticado: true });
    const fetchMock = vi.fn(async (input: Request) => {
      const url = typeof input === 'string' ? input : input.url;
      if (url.includes('/galpones/g1/produccion')) return respuesta({ recogidas: [] });
      if (url.includes('/galpones/g1/mortalidad')) return respuesta({ registros: [] });
      if (url.endsWith('/galpones/g1')) return respuesta({ id: 'g1' });
      if (url.includes('/granjas/f1/galpones')) return respuesta([{ id: 'g1' }]);
      return respuesta([{ id: 'f1' }]); // /granjas
    });
    vi.stubGlobal('fetch', fetchMock);
    render(<PrecalentadoOffline />);
    await waitFor(async () =>
      expect(await cache.obtener('galpones/g1/produccion/hoy')).toBeDefined(),
    );
    expect(await cache.obtener('granjas')).toBeDefined();
    expect(await cache.obtener('granjas/f1/galpones')).toBeDefined();
    expect(await cache.obtener('galpones/g1/mortalidad/hoy')).toBeDefined();
  });

  test('con otro rol no precalienta', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
      cache: crearCacheLecturaMemoria(),
      intervaloMs: 60_000,
    });
    authMock.mockReturnValue({ rol: 'Cliente', estaAutenticado: true });
    render(<PrecalentadoOffline />);
    await new Promise((r) => setTimeout(r, 50));
    const llamadas = fetchMock.mock.calls.filter((c) =>
      String(typeof c[0] === 'string' ? c[0] : (c[0] as Request).url).includes('/granjas'),
    );
    expect(llamadas).toEqual([]);
  });
});
