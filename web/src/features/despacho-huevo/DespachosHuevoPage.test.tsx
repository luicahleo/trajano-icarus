import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DespachosHuevoPage } from './DespachosHuevoPage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchSimulado(reglas: Record<string, Response>) {
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    return (
      reglas[`${req.method} ${new URL(req.url).pathname}`] ?? new Response('', { status: 404 })
    );
  });
}

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/despachos']}>
        <Routes>
          <Route path="/despachos" element={<DespachosHuevoPage />} />
          <Route path="/despachos/nuevo" element={<div>Formulario de despacho</div>} />
          <Route path="/despachos/:id" element={<div>Detalle del despacho</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('DespachosHuevoPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('muestra la bandeja con estados, fechas y totales', async () => {
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/despachos-huevo': respuesta(200, [
          {
            id: 'h1',
            estado: 'Despachado',
            fechaDespacho: '2026-09-05',
            totalAmarras: 5,
            totalHuevos: 930,
            totalBs: 744,
          },
          {
            id: 'h2',
            estado: 'Borrador',
            fechaDespacho: null,
            totalAmarras: 1,
            totalHuevos: 180,
            totalBs: null,
          },
        ]),
      }),
    );
    renderPagina();
    expect(await screen.findByText('Despachado')).toBeInTheDocument();
    expect(screen.getByText('Borrador')).toBeInTheDocument();
    expect(screen.getByText('05/09/2026')).toBeInTheDocument();
    expect(screen.getByText(/744,00/)).toBeInTheDocument();
  });

  test('navega al formulario de nuevo despacho', async () => {
    const usuario = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      fetchSimulado({ 'GET /api/despachos-huevo': respuesta(200, []) }),
    );
    renderPagina();
    await usuario.click(await screen.findByRole('link', { name: /nuevo despacho/i }));
    expect(await screen.findByText('Formulario de despacho')).toBeInTheDocument();
  });
});
