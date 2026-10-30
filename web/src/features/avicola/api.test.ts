import { describe, expect, test, vi, beforeEach } from 'vitest';
import {
  listarGalpones,
  registrarProduccion,
  registrarMortalidad,
  obtenerEficiencia,
  obtenerNotificacionVacunacion,
  listarProgramasVacunacion,
  completarTareaVacunacion,
} from './api';
const r = (s: number, c: unknown) =>
  new Response(JSON.stringify(c), { status: s, headers: { 'content-type': 'application/json' } });
const solicitud = (f: ReturnType<typeof vi.fn>) => f.mock.calls.at(0)?.[0] as unknown as Request;
describe('api avícola', () => {
  beforeEach(() => vi.restoreAllMocks());
  test('galpones', async () => {
    const f = vi.fn(async () => r(200, []));
    vi.stubGlobal('fetch', f);
    await listarGalpones('g1');
    const q = solicitud(f);
    expect(q.url).toContain('/api/granjas/g1/galpones');
  });
  test('produccion', async () => {
    const f = vi.fn(async () => r(201, { id: 'p' }));
    vi.stubGlobal('fetch', f);
    await registrarProduccion('g', {
      hora: '10:30',
      cantidadMaples: 1,
      unidadesIncompletas: 2,
      maplesDescarte: 0,
      unidadesDescarte: 0,
      idempotencyKey: 'k',
    });
    const q = solicitud(f);
    expect(JSON.parse(await q.clone().text())).not.toHaveProperty('fecha');
  });
  test('mortalidad', async () => {
    const f = vi.fn(async () => r(201, { id: 'm' }));
    vi.stubGlobal('fetch', f);
    await registrarMortalidad('g', { hora: '06:15', cantidadMuertas: 2, idempotencyKey: 'k' });
    const q = solicitud(f);
    expect(q.url).toContain('/mortalidad');
  });
  test('eficiencia', async () => {
    const f = vi.fn(async () => r(200, { dias: [] }));
    vi.stubGlobal('fetch', f);
    await obtenerEficiencia('g', '2026-08-01', '2026-08-18');
    const q = solicitud(f);
    expect(q.url).toContain('desde=2026-08-01');
  });
});

describe('api vacunación', () => {
  beforeEach(() => vi.restoreAllMocks());
  const solicitudVacunacion = (f: ReturnType<typeof vi.fn>) =>
    f.mock.calls.at(0)?.[0] as unknown as Request;

  test('obtenerNotificacionVacunacion hace GET /api/vacunacion/tareas', async () => {
    const cuerpo = { vencidasYHoy: [], proximas: [] };
    const fetchMock = vi.fn(
      async () =>
        new Response(JSON.stringify(cuerpo), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const resultado = await obtenerNotificacionVacunacion();

    expect(resultado.vencidasYHoy).toEqual([]);
    const req = solicitudVacunacion(fetchMock);
    expect(req.method).toBe('GET');
    expect(new URL(req.url).pathname).toBe('/api/vacunacion/tareas');
  });

  test('listarProgramasVacunacion hace GET /api/vacunacion/programas sin parámetros', async () => {
    const fetchMock = vi.fn(
      async () =>
        new Response(JSON.stringify([]), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        }),
    );
    vi.stubGlobal('fetch', fetchMock);

    await listarProgramasVacunacion();

    const req = solicitudVacunacion(fetchMock);
    expect(req.method).toBe('GET');
    const url = new URL(req.url);
    expect(url.pathname).toBe('/api/vacunacion/programas');
    expect(url.search).toBe('');
  });

  test('el módulo avícola ya no expone la escritura del catálogo', async () => {
    const api = await import('./api');
    expect(api).not.toHaveProperty('crearProgramaVacunacion');
    expect(api).not.toHaveProperty('actualizarProgramaVacunacion');
    expect(api).not.toHaveProperty('desactivarProgramaVacunacion');
    expect(api).not.toHaveProperty('importarCronogramaExcel');
  });

  test('completarTareaVacunacion envía fecha, aves y observaciones', async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    await completarTareaVacunacion('t1', {
      fechaAplicacion: '2026-08-18',
      avesVacunadas: 4800,
      observaciones: null,
    });

    const req = solicitudVacunacion(fetchMock);
    expect(new URL(req.url).pathname).toBe('/api/vacunacion/tareas/t1/completar');
    expect(JSON.parse(await req.clone().text())).toEqual({
      fechaAplicacion: '2026-08-18',
      avesVacunadas: 4800,
      observaciones: null,
    });
  });
});
