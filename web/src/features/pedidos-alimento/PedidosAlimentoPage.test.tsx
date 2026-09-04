import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
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

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/pedidos']}>
        <Routes>
          <Route path="/pedidos" element={<PedidosAlimentoPage />} />
          <Route path="/pedidos/nuevo" element={<div>Formulario de pedido</div>} />
          <Route path="/pedidos/:id" element={<div>Detalle del pedido</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PedidosAlimentoPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('muestra la bandeja con cupo, estados y totales', async () => {
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/pedidos-alimento': respuesta(200, [
          {
            id: 'p1',
            estado: 'Solicitado',
            presentacion: 'Bolsa',
            fechaPedido: '2026-09-01',
            fechaEntregaEstimada: null,
            totalSolicitado: 17650,
            cantidadLineas: 1,
          },
        ]),
        'GET /api/pedidos-alimento/cupo': respuesta(200, {
          enviados: 1,
          maximo: 3,
          desde: '2026-08-31',
          hasta: '2026-09-06',
        }),
        'GET /api/pedidos-alimento/notificaciones': respuesta(200, { items: [], contador: 0 }),
      }),
    );
    renderPagina();
    expect(await screen.findByText('Cupo semanal: 1 de 3 pedidos enviados.')).toBeInTheDocument();
    expect(screen.getByText('Solicitado')).toBeInTheDocument();
    expect(screen.getByText(/17\.650/)).toBeInTheDocument();
  });

  test('muestra novedades de CAISY y permite marcarlas como leídas', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/pedidos-alimento': respuesta(200, []),
      'GET /api/pedidos-alimento/cupo': respuesta(200, { enviados: 0, maximo: 3, desde: '2026-08-31', hasta: '2026-09-06' }),
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
      fetchSimulado({
        'GET /api/pedidos-alimento': respuesta(200, []),
        'GET /api/pedidos-alimento/cupo': respuesta(200, { enviados: 0, maximo: 3, desde: '2026-08-31', hasta: '2026-09-06' }),
        'GET /api/pedidos-alimento/notificaciones': respuesta(200, { items: [], contador: 0 }),
      }),
    );
    renderPagina();
    await usuario.click(await screen.findByRole('link', { name: /nuevo pedido/i }));
    expect(await screen.findByText('Formulario de pedido')).toBeInTheDocument();
  });
});
