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

  test('muestra las novedades del despacho de huevo y permite marcarlas como leídas', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/despachos-huevo': respuesta(200, []),
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
      fetchSimulado({
        'GET /api/despachos-huevo': respuesta(200, []),
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
});
