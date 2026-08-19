import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './AuthContext';
import { LoginPage } from './LoginPage';

function respuesta(status: number, cuerpo?: unknown, headers: Record<string, string> = {}) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json', ...headers },
  });
}

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/admin/clientes" element={<div>clientes de administrador</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

function esLlamadaA(urlFinal: string) {
  return (arg: RequestInfo | URL) =>
    arg instanceof Request ? arg.url.endsWith(urlFinal) : String(arg).endsWith(urlFinal);
}

describe('LoginPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(401)));
  });

  test('muestra el título, los campos y el botón', async () => {
    renderLogin();
    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument();
    expect(screen.getByLabelText('Correo electrónico')).toBeInTheDocument();
    expect(screen.getByLabelText('Contraseña')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Iniciar sesión' })).toBeInTheDocument();
  });

  test('envío válido autentica y navega al inicio del rol', async () => {
    const usuario = userEvent.setup();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(respuesta(401)) // restauración de la sesión al montar
      .mockResolvedValueOnce(respuesta(200, { accessToken: 'tok', expiraEnSegundos: 900 }))
      .mockImplementation(async () => respuesta(200, { usuarioId: 'u1', rol: 'Administrador', clienteId: null, trabajadorId: null, modulos: [], funcionalidades: [] }));

    vi.stubGlobal('fetch', fetchMock);
    renderLogin();

    await usuario.type(screen.getByLabelText('Correo electrónico'), 'admin@icarus.test');
    await usuario.type(screen.getByLabelText('Contraseña'), 'Secreto-123');
    await usuario.click(screen.getByRole('button', { name: 'Iniciar sesión' }));

    expect(await screen.findByText('clientes de administrador')).toBeInTheDocument();

    const llamada = fetchMock.mock.calls.find(([arg]) => esLlamadaA('/api/identidad/sesion')(arg));
    expect(llamada).toBeDefined();
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo).toEqual({ email: 'admin@icarus.test', contrasena: 'Secreto-123' });
  });

  test('envío vacío muestra los errores de campo sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = vi.fn().mockResolvedValue(respuesta(401));
    vi.stubGlobal('fetch', fetchMock);

    renderLogin();
    await usuario.click(screen.getByRole('button', { name: 'Iniciar sesión' }));

    expect(await screen.findByText('El correo es obligatorio.')).toBeInTheDocument();
    expect(screen.getByText('La contraseña es obligatoria.')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([arg]) => esLlamadaA('/api/identidad/sesion')(arg))).toBe(false);
  });

  test('ApiError 401 muestra el title genérico y el correlation ID sin credenciales', async () => {
    const usuario = userEvent.setup();
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(respuesta(401))
      .mockResolvedValueOnce(respuesta(401, { title: 'No autorizado' }, { 'X-Correlation-ID': 'abc-123' }))
      .mockImplementation(async () => respuesta(401));
    vi.stubGlobal('fetch', fetchMock);

    renderLogin();
    await usuario.type(screen.getByLabelText('Correo electrónico'), 'admin@icarus.test');
    await usuario.type(screen.getByLabelText('Contraseña'), 'Secreto-123');
    await usuario.click(screen.getByRole('button', { name: 'Iniciar sesión' }));

    expect(await screen.findByText(/No autorizado/)).toBeInTheDocument();
    expect(screen.getByText(/abc-123/)).toBeInTheDocument();
    expect(screen.queryByText(/admin@icarus\.test/)).not.toBeInTheDocument();
  });
});
