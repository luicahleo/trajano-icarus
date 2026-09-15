import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { INTERVALO_SONDEO_MS } from '../../lib/sondeo';
import { PedidosAlimentoPage } from './PedidosAlimentoPage';

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
    const valor = colas.get(`${req.method} ${new URL(req.url).pathname}`)?.shift();
    return valor ?? new Response('', { status: 404 });
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
    'GET /api/pedidos-alimento/cupo': respuesta(200, {
      enviados: 1,
      maximo: 3,
      desde: '2026-08-31',
      hasta: '2026-09-06',
    }),
    'GET /api/pedidos-alimento/notificaciones': respuesta(200, { items: [], contador: 0 }),
    'GET /api/granjas': respuesta(200, [{ id: 'g1', nombre: 'Granja Uno' }]),
    'GET /api/clientes/cli1/trabajadores': respuesta(200, [{ id: 't1', nombre: 'Ana Quispe' }]),
    ...reglas,
  });
}

function pagina<T>(items: T[], total: number, numeroPagina = 1, tamanoPagina = 20) {
  return { items, total, numeroPagina, tamanoPagina };
}

const PEDIDO = {
  id: 'p1',
  folio: 'P-000001',
  numero: 1,
  granjaId: 'g1',
  creadoPorTrabajadorId: 't1',
  estado: 'Solicitado',
  presentacion: 'Bolsa',
  fechaPedido: '2026-09-01',
  fechaEntregaEstimada: null,
  totalSolicitado: 17650,
  cantidadLineas: 1,
};

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/pedidos']}>
        <AuthProvider>
          <Routes>
            <Route path="/pedidos" element={<PedidosAlimentoPage />} />
            <Route path="/pedidos/nuevo" element={<div>Formulario de pedido</div>} />
            <Route path="/pedidos/:id" element={<div>Detalle del pedido</div>} />
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

describe('PedidosAlimentoPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('muestra la bandeja con cupo, folio, autor y totales', async () => {
    vi.stubGlobal(
      'fetch',
      baseFetch({ 'GET /api/pedidos-alimento': respuesta(200, pagina([PEDIDO], 1)) }),
    );
    renderPagina();
    expect(await screen.findByText('Cupo semanal: 1 de 3 pedidos enviados.')).toBeInTheDocument();
    expect(await screen.findByText('P-000001')).toBeInTheDocument();
    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();
    expect(screen.getByText(/17\.650/)).toBeInTheDocument();
  });

  test('muestra «Cliente» cuando el pedido no lo creó un trabajador', async () => {
    vi.stubGlobal(
      'fetch',
      baseFetch({
        'GET /api/pedidos-alimento': respuesta(
          200,
          pagina([{ ...PEDIDO, creadoPorTrabajadorId: null }], 1),
        ),
      }),
    );
    renderPagina();
    expect(await screen.findByText('P-000001')).toBeInTheDocument();
    expect(screen.getByText('Cliente')).toBeInTheDocument();
  });

  test('pide la primera página con el tamaño por defecto', async () => {
    const fetchMock = baseFetch({
      'GET /api/pedidos-alimento': respuesta(200, pagina([PEDIDO], 1)),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await screen.findByText('P-000001');

    const url = urlsDe(fetchMock).find((u) => u.pathname === '/api/pedidos-alimento')!;
    expect(url.searchParams.get('pagina')).toBe('1');
    expect(url.searchParams.get('tamanoPagina')).toBe('20');
  });

  test('al elegir una granja vuelve a pedir con granjaId', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/pedidos-alimento': [
        respuesta(200, pagina([PEDIDO], 1)),
        respuesta(200, pagina([PEDIDO], 1)),
      ],
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await screen.findByText('P-000001');

    await usuario.click(screen.getByLabelText('Granja'));
    await usuario.click(await screen.findByRole('option', { name: 'Granja Uno' }));
    await usuario.click(screen.getByRole('button', { name: 'Aplicar filtros' }));

    await waitFor(() =>
      expect(
        urlsDe(fetchMock).some((u) => u.searchParams.get('granjaId') === 'g1'),
      ).toBe(true),
    );
  });

  test('muestra novedades de CAISY y permite marcarlas como leídas', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/pedidos-alimento': respuesta(200, pagina([], 0)),
      'GET /api/pedidos-alimento/notificaciones': respuesta(200, {
        items: [
          {
            id: 'n1',
            tipo: 'PedidoDevuelto',
            pedidoId: 'p9',
            fechaUtc: '2026-09-03T15:00:00Z',
            leida: false,
            meta: null,
          },
        ],
        contador: 1,
      }),
      'POST /api/pedidos-alimento/notificaciones/n1/marcar-leida': respuesta(204),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    expect(await screen.findByText(/CAISY devolvió un pedido para corrección/i)).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Marcar como leída' }));
    const marco = fetchMock.mock.calls.some(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/notificaciones/n1/marcar-leida');
    });
    expect(marco).toBe(true);
  });

  test('navega al formulario de nuevo pedido', async () => {
    const usuario = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      baseFetch({ 'GET /api/pedidos-alimento': respuesta(200, pagina([], 0)) }),
    );
    renderPagina();
    await usuario.click(await screen.findByRole('link', { name: /nuevo pedido/i }));
    expect(await screen.findByText('Formulario de pedido')).toBeInTheDocument();
  });

  test('actualiza el contador de novedades sin recargar la página', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      vi.stubGlobal(
        'fetch',
        baseFetch({
          'GET /api/pedidos-alimento': [
            respuesta(200, pagina([PEDIDO], 1)),
            respuesta(200, pagina([PEDIDO], 1)),
          ],
          'GET /api/pedidos-alimento/notificaciones': [
            respuesta(200, { items: [], contador: 0 }),
            respuesta(200, {
              items: [
                {
                  id: 'n1',
                  tipo: 'PedidoAceptado',
                  pedidoId: 'p1',
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
      expect(await screen.findByText('P-000001')).toBeInTheDocument();
      expect(screen.queryByText(/Novedades de CAISY/)).not.toBeInTheDocument();

      await vi.advanceTimersByTimeAsync(INTERVALO_SONDEO_MS + 100);

      await waitFor(() =>
        expect(screen.getByText(/Novedades de CAISY \(1\)/)).toBeInTheDocument(),
      );
    } finally {
      vi.useRealTimers();
    }
  });
});
