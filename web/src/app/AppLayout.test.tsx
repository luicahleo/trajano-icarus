import 'fake-indexeddb/auto';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ThemeProvider } from '@mui/material';
import { theme } from './theme';
import type { Funcionalidad, Rol } from '../lib/tipos';
import { AuthProvider } from '../features/auth/AuthContext';
import { AppLayout } from './AppLayout';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function renderLayout(rol: Rol, rutaInicial = '/', funcionalidades: Funcionalidad[] = []) {
  vi.stubGlobal(
    'fetch',
    vi
      .fn()
      .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' }))
      .mockResolvedValueOnce(
        respuesta(200, {
          usuarioId: 'u1',
          correo: 'persona@icarus.test',
          rol,
          clienteId: null,
          trabajadorId: null,
          modulos: [],
          funcionalidades,
        }),
      ),
  );
  return render(
    <ThemeProvider theme={theme}>
      <MemoryRouter initialEntries={[rutaInicial]}>
        <AuthProvider>
          <Routes>
            <Route element={<AppLayout />}>
              <Route path="/" element={<div>contenido del layout</div>} />
              <Route path="/admin/clientes" element={<div>listado de clientes</div>} />
            </Route>
            <Route path="/login" element={<div>pantalla de login</div>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </ThemeProvider>,
  );
}

describe('AppLayout', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('presenta la plantilla maestra y su navegación accesible', async () => {
    renderLayout('Administrador');

    expect(await screen.findByText('Trajano Icarus')).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Navegación principal' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Saltar al contenido' })).toHaveAttribute(
      'href',
      '#contenido-principal',
    );
    expect(screen.getByRole('main')).toHaveAttribute('id', 'contenido-principal');
  });

  test('el administrador ve sus módulos y destaca la ruta activa', async () => {
    renderLayout('Administrador', '/admin/clientes');

    const clientes = await screen.findByRole('link', { name: 'Clientes' });
    expect(clientes).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('heading', { name: 'Clientes', level: 1 })).toBeInTheDocument();
    expect(screen.queryByText('Usuarios')).not.toBeInTheDocument();
    expect(screen.queryByText('Trabajadores')).not.toBeInTheDocument();
  });

  test('el cliente solo ve sus módulos', async () => {
    renderLayout('Cliente');
    expect(await screen.findByText('Trabajadores')).toBeInTheDocument();
    expect(screen.getByText('Gestión Avícola')).toBeInTheDocument();
    expect(screen.queryByText('Clientes')).not.toBeInTheDocument();
    expect(screen.queryByText('Usuarios')).not.toBeInTheDocument();
  });

  test('el trabajador solo ve gestión avícola si tiene una funcionalidad operativa', async () => {
    const { unmount } = renderLayout('Trabajador');
    await screen.findByText('Trajano Icarus');
    expect(screen.queryByText('Gestión Avícola')).not.toBeInTheDocument();
    unmount();

    renderLayout('Trabajador', '/', ['Vacunacion']);
    expect(await screen.findByText('Gestión Avícola')).toBeInTheDocument();
  });

  test('muestra el correo de la sesión en la barra', async () => {
    renderLayout('Cliente');
    expect(await screen.findByText('persona@icarus.test')).toBeInTheDocument();
  });

  test('cerrar sesión navega a /login y deja anónimo', async () => {
    const usuario = userEvent.setup();
    renderLayout('Administrador');
    await usuario.click(await screen.findByRole('button', { name: 'Cerrar sesión' }));
    expect(screen.getByText('pantalla de login')).toBeInTheDocument();
    expect(screen.queryByText('Clientes')).not.toBeInTheDocument();
  });
});
