import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ClientesListaPage } from './ClientesListaPage';

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

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/admin/clientes']}>
        <Routes>
          <Route path="/admin/clientes" element={<ClientesListaPage />} />
          <Route path="/admin/clientes/nuevo" element={<div>nuevo cliente</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const clienteActivo = {
  id: 'c1',
  razonSocial: 'Granja Uno S.A.C.',
  identificadorFiscal: '20100102030',
  estaActivo: true,
  modulos: ['GestionAvicola'],
};
const clienteSuspendido = {
  id: 'c2',
  razonSocial: 'Avícola Dos S.R.L.',
  identificadorFiscal: '20511223344',
  estaActivo: false,
  modulos: ['ControlAcceso'],
};

describe('ClientesListaPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('lista los clientes con estado y módulos', async () => {
    fetchSimulado({ 'GET /api/clientes': respuesta(200, [clienteActivo, clienteSuspendido]) });
    renderPagina();

    expect(await screen.findByText('Granja Uno S.A.C.')).toBeInTheDocument();
    expect(screen.getByText('Avícola Dos S.R.L.')).toBeInTheDocument();
    expect(screen.getByText('20100102030')).toBeInTheDocument();
    expect(screen.getByText('Activo')).toBeInTheDocument();
    expect(screen.getByText('Suspendido')).toBeInTheDocument();
    expect(screen.getByText('GestionAvicola')).toBeInTheDocument();
    expect(screen.getByText('ControlAcceso')).toBeInTheDocument();
  });

  test('suspender pide confirmación y llama al endpoint', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/clientes': respuesta(200, [clienteActivo, clienteSuspendido]),
      'POST /api/clientes/c1/suspender': respuesta(204),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Suspender' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(
      fetchMock.mock.calls.some(([arg]) => {
        const req = arg as Request;
        return req.method === 'POST' && req.url.endsWith('/clientes/c1/suspender');
      }),
    ).toBe(true);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  test('el botón de nuevo cliente navega al alta', async () => {
    const usuario = userEvent.setup();
    fetchSimulado({ 'GET /api/clientes': respuesta(200, [clienteActivo]) });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Nuevo cliente' }));

    expect(await screen.findByText('nuevo cliente')).toBeInTheDocument();
  });
});
