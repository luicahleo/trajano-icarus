import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ClienteNuevoPage } from './ClienteNuevoPage';

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

function renderNuevo() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/admin/clientes/nuevo']}>
        <Routes>
          <Route path="/admin/clientes/nuevo" element={<ClienteNuevoPage />} />
          <Route path="/admin/clientes" element={<div>lista de clientes</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ClienteNuevoPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('envío vacío muestra los errores sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({});
    renderNuevo();

    await usuario.click(screen.getByRole('button', { name: 'Crear cliente' }));

    expect(await screen.findByText('La razón social es obligatoria.')).toBeInTheDocument();
    expect(screen.getByText('El NIT es obligatorio.')).toBeInTheDocument();
    expect(screen.getByText('El correo es obligatorio.')).toBeInTheDocument();
    expect(screen.getByText('La contraseña debe tener al menos 12 caracteres.')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([arg]) => (arg as Request).method === 'POST')).toBe(false);
  });

  test('envío válido crea el cliente y navega a la lista', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({ 'POST /api/clientes': respuesta(201, { id: 'c1' }) });
    renderNuevo();

    await usuario.type(screen.getByLabelText('Razón social'), 'Granja Demo S.A.C.');
    await usuario.type(screen.getByLabelText('NIT'), '20123456789');
    await usuario.type(screen.getByLabelText('Correo electrónico'), 'cliente@icarus.test');
    await usuario.type(screen.getByLabelText('Contraseña'), 'Clave-Larga-123456');
    await usuario.type(screen.getByLabelText('Confirmar contraseña'), 'Clave-Larga-123456');
    await usuario.click(screen.getByRole('button', { name: 'Crear cliente' }));

    expect(await screen.findByText('lista de clientes')).toBeInTheDocument();

    const llamada = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/clientes');
    });
    expect(llamada).toBeDefined();
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo).toEqual({
      razonSocial: 'Granja Demo S.A.C.',
      identificadorFiscal: '20123456789',
      email: 'cliente@icarus.test',
      contrasena: 'Clave-Larga-123456',
    });
  });

  test('un 409 muestra el title del problema', async () => {
    const usuario = userEvent.setup();
    fetchSimulado({ 'POST /api/clientes': respuesta(409, { title: 'Ya existe un cliente con ese identificador.' }) });
    renderNuevo();

    await usuario.type(screen.getByLabelText('Razón social'), 'Granja Demo S.A.C.');
    await usuario.type(screen.getByLabelText('NIT'), '20123456789');
    await usuario.type(screen.getByLabelText('Correo electrónico'), 'cliente@icarus.test');
    await usuario.type(screen.getByLabelText('Contraseña'), 'Clave-Larga-123456');
    await usuario.type(screen.getByLabelText('Confirmar contraseña'), 'Clave-Larga-123456');
    await usuario.click(screen.getByRole('button', { name: 'Crear cliente' }));

    expect(await screen.findByText(/Ya existe un cliente con ese identificador/)).toBeInTheDocument();
  });
});
