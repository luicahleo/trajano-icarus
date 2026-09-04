import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { PedidoAlimentoDetallePage } from './PedidoAlimentoDetallePage';

const pedidoBorradorDevuelto = {
  id: 'p1',
  clienteId: 'c1',
  estado: 'Borrador',
  fechaPedido: '2026-09-01',
  fechaEntregaEstimada: null,
  totalSolicitado: 26475,
  lineas: [
    {
      id: 'l1',
      tipoAlimento: 'PosturaUno',
      presentacion: 'Bolsa',
      cantidadSolicitada: 150,
      equivalentes40Kg: 150,
      precioFinalPor40Kg: 176.5,
      subtotalSolicitado: 26475,
      notificacionPreciosAlimentosId: 'pub1',
    },
  ],
  historial: [
    { estadoOrigen: 'Borrador', estadoDestino: 'Solicitado', fechaUtc: '2026-09-01T15:00:00Z', motivo: null, fechaEntregaEstimada: null },
    {
      estadoOrigen: 'Solicitado',
      estadoDestino: 'Borrador',
      fechaUtc: '2026-09-02T16:00:00Z',
      motivo: 'Revise las cantidades',
      fechaEntregaEstimada: null,
    },
  ],
};

const pedidoAceptado = {
  ...pedidoBorradorDevuelto,
  estado: 'Aceptado',
  fechaEntregaEstimada: '2026-09-10',
  historial: [
    { estadoOrigen: 'Borrador', estadoDestino: 'Solicitado', fechaUtc: '2026-09-01T15:00:00Z', motivo: null, fechaEntregaEstimada: null },
    { estadoOrigen: 'Solicitado', estadoDestino: 'Aceptado', fechaUtc: '2026-09-02T16:00:00Z', motivo: null, fechaEntregaEstimada: '2026-09-10' },
  ],
};

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchSimulado(reglas: Record<string, Response>) {
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    return reglas[`${req.method} ${new URL(req.url).pathname}`] ?? new Response('', { status: 404 });
  });
}

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/pedidos/p1']}>
        <Routes>
          <Route path="/pedidos/:id" element={<PedidoAlimentoDetallePage />} />
          <Route path="/pedidos/:id/editar" element={<div>Editar pedido</div>} />
          <Route path="/pedidos" element={<div>Bandeja</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PedidoAlimentoDetallePage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('un borrador devuelto muestra el motivo y permite corregirlo', async () => {
    vi.stubGlobal(
      'fetch',
      fetchSimulado({ 'GET /api/pedidos-alimento/p1': respuesta(200, pedidoBorradorDevuelto) }),
    );
    renderPagina();
    expect(await screen.findByText(/CAISY devolvió este pedido: Revise las cantidades/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Editar' })).toBeInTheDocument();
    expect(screen.getByText('Solicitado → Borrador')).toBeInTheDocument();
  });

  test('un pedido aceptado muestra la entrega estimada sin acciones de borrador', async () => {
    vi.stubGlobal(
      'fetch',
      fetchSimulado({ 'GET /api/pedidos-alimento/p1': respuesta(200, pedidoAceptado) }),
    );
    renderPagina();
    expect(await screen.findByText('10/09/2026')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enviar a CAISY' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Borrar borrador' })).not.toBeInTheDocument();
  });

  test('enviar pide confirmación con el total y reintenta sin duplicar', async () => {
    const usuario = userEvent.setup();
    let envios = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const req = input instanceof Request ? input : new Request(String(input), init);
      const ruta = `${req.method} ${new URL(req.url).pathname}`;
      if (ruta === 'POST /api/pedidos-alimento/p1/enviar') {
        envios += 1;
        return envios === 1 ? respuesta(204) : respuesta(409);
      }
      if (ruta === 'GET /api/pedidos-alimento/p1') return respuesta(200, pedidoBorradorDevuelto);
      return respuesta(404);
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await usuario.click(await screen.findByRole('button', { name: 'Enviar a CAISY' }));
    expect(screen.getByText(/consume el cupo semanal/i)).toBeInTheDocument();
    expect((await screen.findAllByText(/26\.475/)).length).toBeGreaterThan(0);
    await usuario.click(screen.getByRole('button', { name: 'Confirmar envío' }));
    expect(envios).toBe(1);
  });

  test('borrar borrador pide confirmación y vuelve a la bandeja', async () => {
    const usuario = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/pedidos-alimento/p1': respuesta(200, pedidoBorradorDevuelto),
        'DELETE /api/pedidos-alimento/p1': respuesta(204),
      }),
    );
    renderPagina();
    await usuario.click(await screen.findByRole('button', { name: 'Borrar borrador' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(await screen.findByText('Bandeja')).toBeInTheDocument();
  });
});
