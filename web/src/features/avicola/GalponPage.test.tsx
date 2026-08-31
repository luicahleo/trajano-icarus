import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import {
  encolarOperacion,
  iniciarCoordinadorOffline,
  listarOperaciones,
} from '../../app/offline/coordinador';
import { AuthProvider } from '../auth/AuthContext';
import { GalponPage } from './GalponPage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchSimulado(reglas: Record<string, Response | Response[]>) {
  const colas = new Map(
    Object.entries(reglas).map(([clave, valor]) => [
      clave,
      Array.isArray(valor) ? [...valor] : [valor],
    ]),
  );
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    return (
      colas.get(`${req.method} ${new URL(req.url).pathname}`)?.shift() ??
      new Response('', { status: 404 })
    );
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function prepararFetch() {
  return fetchSimulado({
    'POST /api/identidad/sesion/renovar': respuesta(200, {
      accessToken: 't',
      expiraEnSegundos: 900,
    }),
    'GET /api/identidad/me': respuesta(200, {
      usuarioId: 'u1',
      rol: 'Trabajador',
      clienteId: 'cli1',
      trabajadorId: 't1',
      modulos: ['GestionAvicola'],
      funcionalidades: ['ProduccionHuevos', 'Mortalidad'],
    }),
    'GET /api/galpones/g1': respuesta(200, {
      id: 'g1',
      numero: 3,
      gallinasActuales: 100,
      capacidadMaxima: 200,
    }),
    'GET /api/galpones/g1/produccion': [
      respuesta(200, {
        galponId: 'g1',
        fecha: '2026-08-31',
        recogidas: [],
        totalMaples: 0,
        totalUnidadesIncompletas: 0,
        totalVendible: 0,
        totalMaplesDescarte: 0,
        totalUnidadesDescarte: 0,
        totalDescarte: 0,
      }),
    ],
    'GET /api/galpones/g1/mortalidad': [
      respuesta(200, { galponId: 'g1', fecha: '2026-08-31', registros: [], totalMuertas: 0 }),
    ],
    'GET /api/galpones/g1/eficiencia': [respuesta(200, { dias: [] })],
  });
}

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/avicola/galpones/g1']}>
        <AuthProvider>
          <Routes>
            <Route path="/avicola/galpones/:galponId" element={<GalponPage />} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('GalponPage con registros pendientes', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => {
    limpiar?.();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    window.dispatchEvent(new Event('online'));
  });

  test('muestra la operación encolada con la etiqueta Pendiente y permite editarla', async () => {
    // Sin red para que la cola no se vacíe sola durante el test.
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    prepararFetch();
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    await encolarOperacion('produccion.crear', 'g1', {
      hora: '08:15',
      cantidadMaples: 3,
      unidadesIncompletas: 1,
      maplesDescarte: 0,
      unidadesDescarte: 0,
      idempotencyKey: 'k1',
    });

    renderPagina();

    expect(await screen.findByText('Pendiente')).toBeInTheDocument();
    expect(screen.getByText('3 maples + 1 (= 91)')).toBeInTheDocument();
    // Los pendientes no suman al total del día hasta sincronizarse.
    expect(screen.getByText(/0 huevos vendibles/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Editar' }));
    expect(await screen.findByRole('heading', { name: 'Editar recogida' })).toBeInTheDocument();
    expect(screen.getByLabelText('Maples')).toHaveValue('3');
  });

  test('eliminar un pendiente lo descarta de la cola, sin llamar a la API', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    const fetchMock = prepararFetch();
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    await encolarOperacion('mortalidad.crear', 'g1', {
      hora: '07:45',
      cantidadMuertas: 2,
      idempotencyKey: 'k2',
    });

    renderPagina();
    expect(await screen.findByText('2 bajas')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Eliminar' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Confirmar' }));

    await waitFor(async () => expect((await listarOperaciones()).length).toBe(0));
    await waitFor(() => expect(screen.queryByText('2 bajas')).not.toBeInTheDocument());
    const huboDelete = fetchMock.mock.calls.some(([arg]) => (arg as Request).method === 'DELETE');
    expect(huboDelete).toBe(false);
  });
});
