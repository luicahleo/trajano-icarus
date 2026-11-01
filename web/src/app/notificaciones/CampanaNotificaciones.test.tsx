import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { AuthProvider, useAuth } from '../../features/auth/AuthContext';
import type { Funcionalidad, Modulo, Rol } from '../../lib/tipos';
import { isoDesplazado, tareaVacunacion } from '../../test/vacunacion';
import { CampanaNotificaciones } from './CampanaNotificaciones';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchSimulado(reglas: Record<string, Response | Response[]>) {
  const colas = new Map(
    Object.entries(reglas).map(([clave, valor]) => [
      clave,
      Array.isArray(valor) ? [...valor] : [valor],
    ]),
  );
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    const cola = colas.get(clave);
    const valor = cola?.shift();
    return valor ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function sesion(rol: Rol, modulos: Modulo[], funcionalidades: Funcionalidad[] = []) {
  return {
    'POST /api/identidad/sesion/renovar': respuesta(200, {
      accessToken: 't',
      expiraEnSegundos: 900,
    }),
    'GET /api/identidad/me': respuesta(200, {
      usuarioId: 'u1',
      correo: 'persona@icarus.test',
      rol,
      clienteId: 'cli1',
      trabajadorId: null,
      modulos,
      funcionalidades,
    }),
  };
}

const INCIDENCIA = {
  id: 'n1',
  incidenciaId: 'i1',
  tipo: 'IncidenciaAcceso',
  fechaUtc: '2026-09-27T12:00:00Z',
  leida: false,
};

function RutaActual() {
  const { pathname } = useLocation();
  return <span data-testid="ruta-actual">{pathname}</span>;
}

// Permite esperar a que la sesión cargue antes de afirmar que algo no se pidió.
function RolActual() {
  const { rol } = useAuth();
  return <span data-testid="rol">{rol ?? ''}</span>;
}

function renderCampana(reglas: Record<string, Response | Response[]>) {
  const fetchMock = fetchSimulado(reglas);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/inicio']}>
        <AuthProvider>
          <Routes>
            <Route path="*" element={<CampanaNotificaciones />} />
          </Routes>
          <RutaActual />
          <RolActual />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return fetchMock;
}

const pidio = (fetchMock: ReturnType<typeof vi.fn>, metodo: string, ruta: string) =>
  fetchMock.mock.calls.some(([arg, init]) => {
    const req = arg instanceof Request ? arg : new Request(String(arg), init as RequestInit);
    return req.method === metodo && new URL(req.url).pathname === ruta;
  });

async function esperarSesion(rol: Rol) {
  await waitFor(() => expect(screen.getByTestId('rol')).toHaveTextContent(rol));
  await new Promise((resolver) => setTimeout(resolver, 50));
}

describe('CampanaNotificaciones', () => {
  beforeEach(() => vi.restoreAllMocks());
  afterEach(() => vi.unstubAllGlobals());

  test('muestra el contador de notificaciones sin leer', async () => {
    renderCampana({
      ...sesion('Cliente', ['ControlAcceso']),
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [INCIDENCIA],
        contador: 1,
      }),
    });

    expect(await screen.findByText('1')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Notificaciones: 1 pendiente' }),
    ).toBeInTheDocument();
  });

  test('abre el menú y navega a incidencias al seleccionar una notificación', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderCampana({
      ...sesion('Cliente', ['ControlAcceso']),
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [INCIDENCIA],
        contador: 1,
      }),
      'POST /api/control-acceso/notificaciones/n1/marcar-leida': respuesta(204),
    });

    await usuario.click(
      await screen.findByRole('button', { name: 'Notificaciones: 1 pendiente' }),
    );
    expect(screen.getByText('Control de acceso')).toBeInTheDocument();
    await usuario.click(
      await screen.findByRole('menuitem', { name: /incidencia de marcación/ }),
    );

    await waitFor(() =>
      expect(screen.getByTestId('ruta-actual')).toHaveTextContent('/control-acceso/incidencias'),
    );
    expect(
      pidio(fetchMock, 'POST', '/api/control-acceso/notificaciones/n1/marcar-leida'),
    ).toBe(true);
  });

  test('sin fuentes visibles se ve sin insignia y dice que no hay notificaciones', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderCampana(sesion('Administrador', []));
    await esperarSesion('Administrador');

    const campana = screen.getByRole('button', { name: 'Notificaciones sin pendientes' });
    expect(within(campana).queryByText('0')).not.toBeInTheDocument();
    await usuario.click(campana);
    expect(await screen.findByText('No hay notificaciones')).toBeInTheDocument();
    expect(pidio(fetchMock, 'GET', '/api/control-acceso/notificaciones')).toBe(false);
    expect(pidio(fetchMock, 'GET', '/api/vacunacion/tareas')).toBe(false);
  });

  test('el trabajador con Vacunacion y ControlAcceso ve la suma y las dos secciones', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderCampana({
      ...sesion('Trabajador', ['GestionAvicola', 'ControlAcceso'], ['Vacunacion']),
      'GET /api/vacunacion/tareas': respuesta(200, {
        vencidasYHoy: [
          tareaVacunacion('t1', 'g1', isoDesplazado(-2)),
          tareaVacunacion('t2', 'g2', isoDesplazado(0)),
          tareaVacunacion('t3', 'g2', isoDesplazado(1)),
        ],
        proximas: [],
      }),
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [INCIDENCIA],
        contador: 1,
      }),
    });

    const campana = await screen.findByRole('button', { name: 'Notificaciones: 3 pendientes' });
    expect(within(campana).getByText('3')).toBeInTheDocument();
    await usuario.click(campana);

    const vacunacion = screen.getByText('Vacunación');
    const acceso = screen.getByText('Control de acceso');
    expect(
      vacunacion.compareDocumentPosition(acceso) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();

    await usuario.click(
      screen.getByRole('menuitem', { name: '2 tareas pendientes (1 vencida)' }),
    );
    await waitFor(() =>
      expect(screen.getByTestId('ruta-actual')).toHaveTextContent('/avicola/tareas'),
    );
    // La entrada de vacunación no se marca como leída.
    expect(
      pidio(fetchMock, 'POST', '/api/control-acceso/notificaciones/n1/marcar-leida'),
    ).toBe(false);
  });

  test('si falla una fuente, la otra sigue funcionando', async () => {
    const usuario = userEvent.setup();
    renderCampana({
      ...sesion('Trabajador', ['GestionAvicola', 'ControlAcceso'], ['Vacunacion']),
      'GET /api/vacunacion/tareas': respuesta(500, { title: 'Error' }),
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [INCIDENCIA],
        contador: 1,
      }),
    });

    await usuario.click(
      await screen.findByRole('button', { name: 'Notificaciones: 1 pendiente' }),
    );
    expect(
      await screen.findByText('No se pudieron cargar estas notificaciones.'),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('menuitem', { name: /incidencia de marcación/ }),
    ).toBeInTheDocument();
  });

  test('el cliente con Vacunacion no consulta ni ve la sección de vacunación', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderCampana(sesion('Cliente', ['GestionAvicola'], ['Vacunacion']));
    await esperarSesion('Cliente');

    expect(pidio(fetchMock, 'GET', '/api/vacunacion/tareas')).toBe(false);
    await usuario.click(screen.getByRole('button', { name: 'Notificaciones sin pendientes' }));
    expect(screen.queryByText('Vacunación')).not.toBeInTheDocument();
    expect(screen.getByText('No hay notificaciones')).toBeInTheDocument();
  });
});
