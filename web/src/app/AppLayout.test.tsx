import 'fake-indexeddb/auto';
import html from '../../index.html?raw';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { isoDesplazado, tareaVacunacion } from '../test/vacunacion';
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

function renderLayout(
  rol: Rol,
  rutaInicial = '/',
  funcionalidades: Funcionalidad[] = [],
  tareas: unknown[] = [],
) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    if (clave === 'POST /api/identidad/sesion/renovar') {
      return respuesta(200, { accessToken: 'nuevo' });
    }
    if (clave === 'GET /api/identidad/me') {
      return respuesta(200, {
        usuarioId: 'u1',
        correo: 'persona@icarus.test',
        rol,
        clienteId: null,
        trabajadorId: null,
        modulos: [],
        funcionalidades,
      });
    }
    if (clave === 'GET /api/vacunacion/tareas') {
      return respuesta(200, { vencidasYHoy: tareas, proximas: [] });
    }
    return new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fetchMock);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const resultado = render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <MemoryRouter initialEntries={[rutaInicial]}>
          <AuthProvider>
            <Routes>
              <Route element={<AppLayout />}>
                <Route path="/" element={<div>contenido del layout</div>} />
                <Route path="/admin/clientes" element={<div>listado de clientes</div>} />
                <Route path="/avicola/tareas" element={<div>página de tareas</div>} />
              </Route>
              <Route path="/login" element={<div>pantalla de login</div>} />
            </Routes>
          </AuthProvider>
        </MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  );
  return { ...resultado, fetchMock };
}

describe('AppLayout', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('index.html enlaza el favicon SVG y su fallback .ico', () => {
    expect(html).toContain('rel="icon"');
    expect(html).toContain('/favicon.svg');
    expect(html).toContain('/favicon.ico');
  });

  test('presenta la marca Icarus junto al nombre de la aplicación', async () => {
    renderLayout('Administrador');

    const marca = await screen.findByTestId('marca-icarus');
    expect(marca).toHaveAttribute('viewBox', '0 0 800 800');
    expect(marca).toHaveAttribute('aria-hidden', 'true');
  });

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

  test('la campana está siempre en la cabecera, aunque no haya módulos ni notificaciones', async () => {
    const usuario = userEvent.setup();
    renderLayout('Administrador');

    const campana = await screen.findByRole('button', { name: 'Notificaciones sin pendientes' });
    await usuario.click(campana);
    expect(await screen.findByText('No hay notificaciones')).toBeInTheDocument();
  });

  test('el trabajador con Vacunacion ve la insignia sin las futuras y llega a sus tareas', async () => {
    const usuario = userEvent.setup();
    renderLayout(
      'Trabajador',
      '/',
      ['Vacunacion'],
      [
        tareaVacunacion('t1', 'g1', isoDesplazado(-2)),
        tareaVacunacion('t2', 'g1', isoDesplazado(0)),
        tareaVacunacion('t3', 'g2', isoDesplazado(1)),
      ],
    );

    const campana = await screen.findByRole('button', { name: 'Notificaciones: 2 pendientes' });
    expect(within(campana).getByText('2')).toBeInTheDocument();

    await usuario.click(campana);
    await usuario.click(
      screen.getByRole('menuitem', { name: '2 tareas pendientes (1 vencida)' }),
    );
    expect(await screen.findByText('página de tareas')).toBeInTheDocument();
  });

  test('cerrar sesión navega a /login y deja anónimo', async () => {
    const usuario = userEvent.setup();
    renderLayout('Administrador');
    await usuario.click(await screen.findByRole('button', { name: 'Cerrar sesión' }));
    expect(screen.getByText('pantalla de login')).toBeInTheDocument();
    expect(screen.queryByText('Clientes')).not.toBeInTheDocument();
  });
});
