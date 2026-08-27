import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AsignarPlanDialog } from './AsignarPlanDialog';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status, headers: { 'content-type': 'application/json' } });
}

function renderDialog(reglas: Record<string, Response>) {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = init !== undefined ? new Request(String(input), init) : input instanceof Request ? input : new Request(String(input));
    return reglas[`${req.method} ${new URL(req.url).pathname}`] ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={queryClient}><AsignarPlanDialog galponId="ga1" abierto alCerrar={vi.fn()} /></QueryClientProvider>);
  return fn;
}

describe('AsignarPlanDialog', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('asigna el programa elegido al galpón', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderDialog({
      'GET /api/vacunacion/programas': respuesta(200, [
        { id: 'p1', nombre: 'Plan CAISY 2026', fechaEmision: '2026-01-15', cantidadAves: 1000, observaciones: null, estaActivo: true },
      ]),
      'POST /api/galpones/ga1/plan-vacunacion': respuesta(204),
    });

    await usuario.click(await screen.findByLabelText('Plan CAISY 2026'));
    await usuario.click(screen.getByRole('button', { name: 'Asignar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'POST');
    expect(JSON.parse(await (llamada![0] as Request).clone().text())).toEqual({ programaId: 'p1' });
  });

  test('advierte que las pendientes del plan anterior se desactivan', async () => {
    renderDialog({
      'GET /api/vacunacion/programas': respuesta(200, [
        { id: 'p1', nombre: 'Plan CAISY 2026', fechaEmision: '2026-01-15', cantidadAves: 1000, observaciones: null, estaActivo: true },
      ]),
    });

    expect(await screen.findByText(/pendientes del plan anterior se desactivan/i)).toBeInTheDocument();
  });
});
