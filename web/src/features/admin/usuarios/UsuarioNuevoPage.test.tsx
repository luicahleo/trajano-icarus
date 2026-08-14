import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { UsuarioNuevoPage } from './UsuarioNuevoPage';

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
      <MemoryRouter initialEntries={['/admin/usuarios/nuevo']}>
        <Routes>
          <Route path="/admin/usuarios/nuevo" element={<UsuarioNuevoPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function elegirOpcion(usuario: ReturnType<typeof userEvent.setup>, nombreCombobox: string, opcion: string) {
  await usuario.click(screen.getByRole('combobox', { name: nombreCombobox }));
  await usuario.click(await screen.findByRole('option', { name: opcion }));
}

const cliente = {
  id: 'cli1',
  razonSocial: 'Granja Uno S.A.C.',
  identificadorFiscal: '20100102030',
  estaActivo: true,
  modulos: ['GestionAvicola'],
};
const trabajador = {
  id: 't1',
  nombre: 'Ana Quispe',
  documentoIdentidad: 'DNI-00000001',
  cargo: 'Criadora',
  fechaIngreso: '2025-01-01',
  fechaCese: null,
};

describe('UsuarioNuevoPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('sin rol, correo o contraseña corta muestra errores sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({});
    renderNuevo();

    await usuario.type(screen.getByLabelText('Contraseña'), 'corta');
    await usuario.click(screen.getByRole('button', { name: 'Crear cuenta' }));

    expect(await screen.findByText('El correo es obligatorio.')).toBeInTheDocument();
    expect(screen.getByText('La contraseña debe tener al menos 12 caracteres.')).toBeInTheDocument();
    expect(screen.getByText('Selecciona un rol.')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([arg]) => (arg as Request).method === 'POST')).toBe(false);
  });

  test('el rol Administrador no muestra selectores de cliente ni trabajador', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({});
    renderNuevo();

    await elegirOpcion(usuario, 'Rol', 'Administrador');

    expect(screen.queryByRole('combobox', { name: 'Cliente' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Trabajador' })).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([arg]) => new URL(String((arg as Request).url)).pathname === '/clientes')).toBe(
      false,
    );
  });

  test('el rol Cliente muestra el selector de cliente y no el de trabajador', async () => {
    const usuario = userEvent.setup();
    fetchSimulado({ 'GET /clientes': respuesta(200, [cliente]) });
    renderNuevo();

    await elegirOpcion(usuario, 'Rol', 'Cliente');

    expect(await screen.findByRole('combobox', { name: 'Cliente' })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Trabajador' })).not.toBeInTheDocument();
  });

  test('el rol Trabajador carga los trabajadores del cliente elegido', async () => {
    const usuario = userEvent.setup();
    fetchSimulado({
      'GET /clientes': respuesta(200, [cliente]),
      'GET /clientes/cli1/trabajadores': respuesta(200, [trabajador]),
    });
    renderNuevo();

    await elegirOpcion(usuario, 'Rol', 'Trabajador');
    await elegirOpcion(usuario, 'Cliente', 'Granja Uno S.A.C.');

    const selectorTrabajador = await screen.findByRole('combobox', { name: 'Trabajador' });
    expect(selectorTrabajador).toBeInTheDocument();
    await usuario.click(selectorTrabajador);
    expect(await screen.findByRole('option', { name: 'Ana Quispe' })).toBeInTheDocument();
  });

  test('envío válido crea la cuenta y muestra el éxito', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /clientes': respuesta(200, [cliente]),
      'POST /identidad/usuarios': respuesta(201, { id: 'u1' }),
    });
    renderNuevo();

    await usuario.type(screen.getByLabelText('Correo electrónico'), 'cuenta@icarus.test');
    await usuario.type(screen.getByLabelText('Contraseña'), 'Clave-Larga-123456');
    await elegirOpcion(usuario, 'Rol', 'Cliente');
    await elegirOpcion(usuario, 'Cliente', 'Granja Uno S.A.C.');
    await usuario.click(screen.getByRole('button', { name: 'Crear cuenta' }));

    expect(await screen.findByText(/Cuenta creada correctamente/)).toBeInTheDocument();

    const llamada = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/identidad/usuarios');
    });
    expect(llamada).toBeDefined();
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo).toEqual({
      email: 'cuenta@icarus.test',
      contrasena: 'Clave-Larga-123456',
      rol: 'Cliente',
      clienteId: 'cli1',
      trabajadorId: null,
    });
  });

  test('un 409 muestra el title del problema', async () => {
    const usuario = userEvent.setup();
    fetchSimulado({
      'GET /clientes': respuesta(200, [cliente]),
      'POST /identidad/usuarios': respuesta(409, { title: 'Ya existe una cuenta con ese correo.' }),
    });
    renderNuevo();

    await usuario.type(screen.getByLabelText('Correo electrónico'), 'duplicado@icarus.test');
    await usuario.type(screen.getByLabelText('Contraseña'), 'Clave-Larga-123456');
    await elegirOpcion(usuario, 'Rol', 'Cliente');
    await elegirOpcion(usuario, 'Cliente', 'Granja Uno S.A.C.');
    await usuario.click(screen.getByRole('button', { name: 'Crear cuenta' }));

    expect(await screen.findByText(/Ya existe una cuenta con ese correo/)).toBeInTheDocument();
  });
});
