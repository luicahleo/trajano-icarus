import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Rol } from '../../lib/tipos';
import { AuthProvider } from '../auth/AuthContext';
import { TrabajadoresPage } from './TrabajadoresPage';

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

function baseFetch(rol: Rol, clienteId: string | null, reglas: Record<string, Response>) {
  return fetchSimulado({
    'POST /api/identidad/sesion/renovar': respuesta(200, { accessToken: 't', expiraEnSegundos: 900 }),
    'GET /api/identidad/me': respuesta(200, { usuarioId: 'u1', rol, clienteId }),
    ...reglas,
  });
}

function renderPagina(rutaInicial: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[rutaInicial]}>
        <AuthProvider>
          <Routes>
            <Route path="/trabajadores" element={<TrabajadoresPage />} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function llamadaCon(fetchMock: ReturnType<typeof vi.fn>, metodo: string, sufijo: string) {
  return fetchMock.mock.calls.some(([arg]) => {
    const req = arg as Request;
    return req.method === metodo && req.url.endsWith(sufijo);
  });
}

const trabajador = {
  id: 't1',
  nombre: 'Ana Quispe',
  documentoIdentidad: 'DNI-00000001',
  cargo: 'Criadora',
  fechaIngreso: '2025-01-01',
  fechaCese: null,
  funcionalidades: ['Granjas'],
};
const trabajador2 = {
  id: 't2',
  nombre: 'Roberto Mamani',
  documentoIdentidad: 'DNI-00000002',
  cargo: 'Veterinario',
  fechaIngreso: '2025-03-01',
  fechaCese: '2026-02-01',
  funcionalidades: [],
};

describe('TrabajadoresPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('un cliente ve los trabajadores de su propia empresa', async () => {
    const fetchMock = baseFetch('Cliente', 'cli1', {
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador, trabajador2]),
    });
    renderPagina('/trabajadores');

    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();
    expect(screen.getByText('Roberto Mamani')).toBeInTheDocument();
    expect(screen.getByText('DNI-00000001')).toBeInTheDocument();
    expect(llamadaCon(fetchMock, 'GET', '/clientes/cli1/trabajadores')).toBe(true);
  });

  test('el alta crea un trabajador y refresca la lista', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch('Cliente', 'cli1', {
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'POST /api/clientes/cli1/trabajadores': respuesta(201, { id: 't9' }),
    });
    renderPagina('/trabajadores');

    await usuario.click(await screen.findByRole('button', { name: 'Nuevo trabajador' }));

    await usuario.type(screen.getByLabelText('Nombre completo'), 'Nuevo Colaborador');
    await usuario.type(screen.getByLabelText('Documento de identidad'), 'DNI-00000099');
    await usuario.type(screen.getByLabelText('Cargo'), 'Supervisor');
    fireEvent.change(screen.getByLabelText('Fecha de ingreso'), { target: { value: '2026-01-01' } });
    await usuario.type(screen.getByLabelText('Correo electrónico'), 'nuevo@icarus.test');
    await usuario.type(screen.getByLabelText('Contraseña'), 'Clave-Larga-123456');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(llamadaCon(fetchMock, 'POST', '/clientes/cli1/trabajadores')).toBe(true);
    const llamada = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/clientes/cli1/trabajadores');
    });
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo).toEqual({
      nombre: 'Nuevo Colaborador',
      documentoIdentidad: 'DNI-00000099',
      cargo: 'Supervisor',
      fechaIngreso: '2026-01-01',
      email: 'nuevo@icarus.test',
      contrasena: 'Clave-Larga-123456',
    });
  });

  test('cesar rechaza una fecha futura y llama al endpoint con una válida', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch('Cliente', 'cli1', {
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'POST /api/clientes/trabajadores/t1/cese': respuesta(204),
    });
    renderPagina('/trabajadores');

    await usuario.click(await screen.findByRole('button', { name: 'Cesar' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Fecha de cese'), { target: { value: '2030-01-01' } });
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(await screen.findByText('La fecha de cese no puede ser futura.')).toBeInTheDocument();
    expect(llamadaCon(fetchMock, 'POST', '/clientes/trabajadores/t1/cese')).toBe(false);

    fireEvent.change(screen.getByLabelText('Fecha de cese'), { target: { value: '2026-01-01' } });
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(llamadaCon(fetchMock, 'POST', '/clientes/trabajadores/t1/cese')).toBe(true);
    const llamada = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/clientes/trabajadores/t1/cese');
    });
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo).toEqual({ fechaCese: '2026-01-01' });
  });

  test('desactivar pide confirmación y borra el trabajador', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch('Cliente', 'cli1', {
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'DELETE /api/clientes/trabajadores/t1': respuesta(204),
    });
    renderPagina('/trabajadores');

    await usuario.click(await screen.findByRole('button', { name: 'Desactivar' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(llamadaCon(fetchMock, 'DELETE', '/clientes/trabajadores/t1')).toBe(true);
  });

  test('un 409 en el alta muestra el title sin revelar el documento', async () => {
    const usuario = userEvent.setup();
    baseFetch('Cliente', 'cli1', {
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'POST /api/clientes/cli1/trabajadores': respuesta(409, { title: 'El documento de identidad ya está registrado.' }),
    });
    renderPagina('/trabajadores');

    await usuario.click(await screen.findByRole('button', { name: 'Nuevo trabajador' }));
    await usuario.type(screen.getByLabelText('Nombre completo'), 'Nuevo Colaborador');
    await usuario.type(screen.getByLabelText('Documento de identidad'), 'DNI-00000099');
    await usuario.type(screen.getByLabelText('Cargo'), 'Supervisor');
    fireEvent.change(screen.getByLabelText('Fecha de ingreso'), { target: { value: '2026-01-01' } });
    await usuario.type(screen.getByLabelText('Correo electrónico'), 'nuevo@icarus.test');
    await usuario.type(screen.getByLabelText('Contraseña'), 'Clave-Larga-123456');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/El documento de identidad ya está registrado/)).toBeInTheDocument();
    expect(screen.queryByText(/DNI-00000099/)).not.toBeInTheDocument();
  });
});
