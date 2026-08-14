import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ClienteDetallePage } from './ClienteDetallePage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchSimulado(reglas: Record<string, Response>) {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req =
      init !== undefined ? new Request(String(input), init) : input instanceof Request ? input : new Request(String(input));
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    return reglas[clave] ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function renderDetalle() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/admin/clientes/c1']}>
        <Routes>
          <Route path="/admin/clientes/:id" element={<ClienteDetallePage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const clienteConGA = {
  id: 'c1',
  razonSocial: 'Granja Uno S.A.C.',
  identificadorFiscal: '20100102030',
  estaActivo: true,
  modulos: ['GestionAvicola'],
};

describe('ClienteDetallePage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('muestra los datos del cliente encontrado por :id', async () => {
    fetchSimulado({ 'GET /clientes': respuesta(200, [clienteConGA]) });
    renderDetalle();

    expect(await screen.findByText('Granja Uno S.A.C.')).toBeInTheDocument();
    expect(screen.getByText('20100102030')).toBeInTheDocument();
    expect(screen.getByText('Activo')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'GestionAvicola' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'ControlAcceso' })).not.toBeChecked();
  });

  test('alternar un módulo guarda la lista completa nueva', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /clientes': respuesta(200, [clienteConGA]),
      'PUT /clientes/c1/modulos': respuesta(204),
    });
    renderDetalle();

    await usuario.click(await screen.findByRole('checkbox', { name: 'ControlAcceso' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'PUT' && req.url.endsWith('/clientes/c1/modulos');
    });
    expect(llamada).toBeDefined();
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo).toEqual({ modulos: ['GestionAvicola', 'ControlAcceso'] });
  });

  test('suspender pide confirmación y llama a su endpoint', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /clientes': respuesta(200, [clienteConGA]),
      'POST /clientes/c1/suspender': respuesta(204),
    });
    renderDetalle();

    await usuario.click(await screen.findByRole('button', { name: 'Suspender' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(
      fetchMock.mock.calls.some(([arg]) => {
        const req = arg as Request;
        return req.method === 'POST' && req.url.endsWith('/clientes/c1/suspender');
      }),
    ).toBe(true);
  });
});
