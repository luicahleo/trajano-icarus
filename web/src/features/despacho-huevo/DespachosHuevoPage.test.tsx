import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { INTERVALO_SONDEO_MS } from '../../lib/sondeo';
import { DespachosHuevoPage } from './DespachosHuevoPage';

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
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    return colas.get(`${req.method} ${new URL(req.url).pathname}`)?.shift() ?? new Response('', { status: 404 });
  });
}

function baseFetch(reglas: Record<string, Response | Response[]>) {
  return fetchSimulado({
    'POST /api/identidad/sesion/renovar': respuesta(200, {
      accessToken: 't',
      expiraEnSegundos: 900,
    }),
    'GET /api/identidad/me': respuesta(200, {
      usuarioId: 'u1',
      rol: 'Cliente',
      clienteId: 'cli1',
      trabajadorId: null,
      modulos: [],
      funcionalidades: [],
    }),
    'GET /api/granjas': respuesta(200, [{ id: 'g1', nombre: 'Granja Uno' }]),
    'GET /api/clientes/cli1/trabajadores': respuesta(200, [{ id: 't1', nombre: 'Ana Quispe' }]),
    ...reglas,
  });
}

function pagina<T>(items: T[], total: number, numeroPagina = 1, tamanoPagina = 20) {
  return { items, total, numeroPagina, tamanoPagina };
}

const DESPACHO = {
  id: 'h1',
  folio: 'D-000001',
  numero: 1,
  granjaId: 'g1',
  creadoPorTrabajadorId: 't1',
  estado: 'Despachado',
  fechaDespacho: '2026-09-05',
  totalAmarras: 5,
  totalHuevos: 930,
  totalBs: 744,
};

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/despachos']}>
        <AuthProvider>
          <Routes>
            <Route path="/despachos" element={<DespachosHuevoPage />} />
            <Route path="/despachos/nuevo" element={<div>Formulario de despacho</div>} />
            <Route path="/despachos/:id" element={<div>Detalle del despacho</div>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function urlsDe(fetchMock: ReturnType<typeof vi.fn>) {
  return fetchMock.mock.calls
    .map(([arg]) => {
      if (arg instanceof Request) return new URL(arg.url);
      try {
        return new URL(String(arg), 'http://localhost');
      } catch {
        return null;
      }
    })
    .filter((url): url is URL => url !== null);
}

describe('DespachosHuevoPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('muestra la bandeja con folio, autor, fechas y totales', async () => {
    vi.stubGlobal(
      'fetch',
      baseFetch({ 'GET /api/despachos-huevo': respuesta(200, pagina([DESPACHO], 1)) }),
    );
    renderPagina();
    expect(await screen.findByText('D-000001')).toBeInTheDocument();
    expect(screen.getByText('Despachado')).toBeInTheDocument();
    expect(screen.getByText('05/09/2026')).toBeInTheDocument();
    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();
    expect(screen.getByText(/744,00/)).toBeInTheDocument();
  });

  test('muestra «Cliente» cuando el despacho no lo creó un trabajador', async () => {
    vi.stubGlobal(
      'fetch',
      baseFetch({
        'GET /api/despachos-huevo': respuesta(
          200,
          pagina([{ ...DESPACHO, creadoPorTrabajadorId: null }], 1),
        ),
      }),
    );
    renderPagina();
    expect(await screen.findByText('D-000001')).toBeInTheDocument();
    expect(screen.getByText('Cliente')).toBeInTheDocument();
  });

  test('pide la primera página con el tamaño por defecto', async () => {
    const fetchMock = baseFetch({
      'GET /api/despachos-huevo': respuesta(200, pagina([DESPACHO], 1)),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await screen.findByText('D-000001');

    const url = urlsDe(fetchMock).find((u) => u.pathname === '/api/despachos-huevo')!;
    expect(url.searchParams.get('pagina')).toBe('1');
    expect(url.searchParams.get('tamanoPagina')).toBe('20');
  });

  test('al elegir una granja vuelve a pedir con granjaId', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/despachos-huevo': [
        respuesta(200, pagina([DESPACHO], 1)),
        respuesta(200, pagina([DESPACHO], 1)),
      ],
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await screen.findByText('D-000001');

    await usuario.click(screen.getByLabelText('Granja'));
    await usuario.click(await screen.findByRole('option', { name: 'Granja Uno' }));
    await usuario.click(screen.getByRole('button', { name: 'Aplicar filtros' }));

    await waitFor(() =>
      expect(
        urlsDe(fetchMock).some((u) => u.searchParams.get('granjaId') === 'g1'),
      ).toBe(true),
    );
  });

  test('navega al formulario de nuevo despacho', async () => {
    const usuario = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      baseFetch({ 'GET /api/despachos-huevo': respuesta(200, pagina([], 0)) }),
    );
    renderPagina();
    await usuario.click(await screen.findByRole('link', { name: /nuevo despacho/i }));
    expect(await screen.findByText('Formulario de despacho')).toBeInTheDocument();
  });

  test('muestra las novedades del despacho de huevo y permite marcarlas como leídas', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/despachos-huevo': respuesta(200, pagina([], 0)),
      'GET /api/despachos-huevo/notificaciones': respuesta(200, {
        items: [
          {
            id: 'n1',
            tipo: 'AjusteCredito',
            despachoHuevoId: 'h1',
            fechaUtc: '2026-09-10T15:00:00Z',
            leida: false,
            meta: '27.0000 Bs — Precio mal digitado.',
          },
        ],
        contador: 1,
      }),
      'POST /api/despachos-huevo/notificaciones/n1/marcar-leida': respuesta(204),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();

    expect(await screen.findByText(/Se ajustó tu crédito de huevo/i)).toBeInTheDocument();
    expect(screen.getByText(/27,0000|27\.0000/)).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Marcar como leída' }));
    const marco = fetchMock.mock.calls.some(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/notificaciones/n1/marcar-leida');
    });
    expect(marco).toBe(true);
  });

  test('no muestra el bloque de novedades cuando no hay sin leer', async () => {
    vi.stubGlobal(
      'fetch',
      baseFetch({
        'GET /api/despachos-huevo': respuesta(200, pagina([], 0)),
        'GET /api/despachos-huevo/notificaciones': respuesta(200, {
          items: [
            {
              id: 'n1',
              tipo: 'DespachoRecibido',
              despachoHuevoId: 'h1',
              fechaUtc: '2026-09-10T15:00:00Z',
              leida: true,
              meta: null,
            },
          ],
          contador: 0,
        }),
      }),
    );
    renderPagina();

    expect(await screen.findByText('No hay despachos todavía. Creá el primero.')).toBeInTheDocument();
    expect(screen.queryByText(/Novedades del despacho de huevo/i)).not.toBeInTheDocument();
  });

  test('actualiza el contador de novedades sin recargar la página', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      vi.stubGlobal(
        'fetch',
        baseFetch({
          'GET /api/despachos-huevo': [
            respuesta(200, pagina([DESPACHO], 1)),
            respuesta(200, pagina([DESPACHO], 1)),
          ],
          'GET /api/despachos-huevo/notificaciones': [
            respuesta(200, { items: [], contador: 0 }),
            respuesta(200, {
              items: [
                {
                  id: 'n1',
                  tipo: 'DespachoRecibido',
                  despachoHuevoId: 'h1',
                  fechaUtc: '2026-09-14T10:00:00Z',
                  leida: false,
                  meta: null,
                },
              ],
              contador: 1,
            }),
          ],
        }),
      );
      renderPagina();
      expect(await screen.findByText('D-000001')).toBeInTheDocument();

      await vi.advanceTimersByTimeAsync(INTERVALO_SONDEO_MS + 100);

      await waitFor(() =>
        expect(
          screen.getByText(/Novedades del despacho de huevo \(1\)/),
        ).toBeInTheDocument(),
      );
    } finally {
      vi.useRealTimers();
    }
  });
});
