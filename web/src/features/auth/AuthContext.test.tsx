import 'fake-indexeddb/auto';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  guardarSesionOffline,
  obtenerSesionOffline,
} from '../../app/offline/sesionOffline';
import type { UsuarioActual } from '../../lib/tipos';
import { AuthProvider, useAuth } from './AuthContext';

const snapshotTrabajador: UsuarioActual = {
  usuarioId: 'u1',
  correo: null,
  rol: 'Trabajador',
  clienteId: 'c1',
  trabajadorId: 't1',
  modulos: ['GestionAvicola'],
  funcionalidades: ['ProduccionHuevos'],
};

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function Consumidor() {
  const { estaAutenticado, cargando, rol, usuario, modulos, funcionalidades, tieneFuncionalidad } =
    useAuth();
  return (
    <div>
      <span data-testid="autenticado">{String(estaAutenticado)}</span>
      <span data-testid="cargando">{String(cargando)}</span>
      <span data-testid="rol">{rol ?? 'sin-rol'}</span>
      <span data-testid="usuario">{usuario ? usuario.usuarioId : 'anónimo'}</span>
      <span data-testid="modulos">{modulos.join(',')}</span>
      <span data-testid="funcionalidades">{funcionalidades.join(',')}</span>
      <span data-testid="tiene-produccion">{String(tieneFuncionalidad('ProduccionHuevos'))}</span>
      <span data-testid="tiene-granjas">{String(tieneFuncionalidad('Granjas'))}</span>
      <span data-testid="tiene-cualquiera">
        {String(tieneFuncionalidad('Granjas', 'Mortalidad'))}
      </span>
    </div>
  );
}

function ConsumidorAcciones() {
  const { rol, cerrarSesion, entrarSinConexion } = useAuth();
  return (
    <div>
      <span data-testid="rol">{rol ?? 'sin-rol'}</span>
      <button onClick={() => cerrarSesion()}>salir</button>
      <button onClick={() => void entrarSinConexion()}>entrar-offline</button>
    </div>
  );
}

describe('AuthProvider', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  test('restaura la sesión por cookie y expone rol y cliente', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' }))
        .mockResolvedValueOnce(
          respuesta(200, {
            usuarioId: 'u1',
            rol: 'Cliente',
            clienteId: 'c1',
            trabajadorId: null,
            modulos: [],
            funcionalidades: [],
          }),
        ),
    );

    render(
      <AuthProvider>
        <Consumidor />
      </AuthProvider>,
    );

    expect(screen.getByTestId('cargando').textContent).toBe('true');

    expect(await screen.findByTestId('autenticado')).toHaveTextContent('true');
    await waitFor(() => expect(screen.getByTestId('cargando').textContent).toBe('false'));
    expect(screen.getByTestId('rol').textContent).toBe('Cliente');
    expect(screen.getByTestId('usuario').textContent).toBe('u1');
  });

  test('expone modulos y funcionalidades y evalua tieneFuncionalidad', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' }))
        .mockResolvedValueOnce(
          respuesta(200, {
            usuarioId: 'u1',
            rol: 'Trabajador',
            clienteId: 'c1',
            trabajadorId: 't1',
            modulos: [],
            funcionalidades: ['ProduccionHuevos', 'Mortalidad'],
          }),
        ),
    );

    render(
      <AuthProvider>
        <Consumidor />
      </AuthProvider>,
    );

    await waitFor(() =>
      expect(screen.getByTestId('funcionalidades')).toHaveTextContent(
        'ProduccionHuevos,Mortalidad',
      ),
    );
    expect(screen.getByTestId('modulos')).toHaveTextContent('');
    expect(screen.getByTestId('tiene-produccion')).toHaveTextContent('true');
    expect(screen.getByTestId('tiene-granjas')).toHaveTextContent('false');
    expect(screen.getByTestId('tiene-cualquiera')).toHaveTextContent('true');
  });

  test('sin renovación posible queda anónimo', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(401)));

    render(
      <AuthProvider>
        <Consumidor />
      </AuthProvider>,
    );

    expect(await screen.findByTestId('autenticado')).toHaveTextContent('false');
    await waitFor(() => expect(screen.getByTestId('cargando').textContent).toBe('false'));
    expect(screen.getByTestId('rol').textContent).toBe('sin-rol');
  });

  test('sin red restaura desde el snapshot del trabajador', async () => {
    const snapshotTrabajador: UsuarioActual = {
      usuarioId: 'u1',
      correo: null,
      rol: 'Trabajador',
      clienteId: 'c1',
      trabajadorId: 't1',
      modulos: ['GestionAvicola'],
      funcionalidades: ['ProduccionHuevos'],
    };
    await guardarSesionOffline(snapshotTrabajador);
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('fetch failed');
      }),
    );

    render(
      <AuthProvider>
        <Consumidor />
      </AuthProvider>,
    );

    await waitFor(() => expect(screen.getByTestId('rol')).toHaveTextContent('Trabajador'));
  });

  test('rechazo del backend (no red) NO usa el snapshot', async () => {
    const snapshotTrabajador: UsuarioActual = {
      usuarioId: 'u1',
      correo: null,
      rol: 'Trabajador',
      clienteId: 'c1',
      trabajadorId: 't1',
      modulos: ['GestionAvicola'],
      funcionalidades: ['ProduccionHuevos'],
    };
    await guardarSesionOffline(snapshotTrabajador);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(401)));

    render(
      <AuthProvider>
        <Consumidor />
      </AuthProvider>,
    );

    expect(await screen.findByTestId('rol')).toHaveTextContent('sin-rol');
  });

  test('cerrar sesión sin red conserva el snapshot y permite reentrar offline', async () => {
    await guardarSesionOffline(snapshotTrabajador);
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('fetch failed');
      }),
    );
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    const usuario = userEvent.setup();

    render(
      <AuthProvider>
        <ConsumidorAcciones />
      </AuthProvider>,
    );
    await waitFor(() => expect(screen.getByTestId('rol')).toHaveTextContent('Trabajador'));

    await usuario.click(screen.getByText('salir'));
    expect(screen.getByTestId('rol')).toHaveTextContent('sin-rol');
    // Sin red el snapshot se conserva (caduca a las 12 h igualmente).
    expect(await obtenerSesionOffline()).not.toBeNull();

    await usuario.click(screen.getByText('entrar-offline'));
    await waitFor(() => expect(screen.getByTestId('rol')).toHaveTextContent('Trabajador'));
  });

  test('cerrar sesión con red borra el snapshot (dispositivo compartido)', async () => {
    await guardarSesionOffline(snapshotTrabajador);
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' }))
        .mockResolvedValueOnce(respuesta(200, snapshotTrabajador)),
    );
    const usuario = userEvent.setup();

    render(
      <AuthProvider>
        <ConsumidorAcciones />
      </AuthProvider>,
    );
    await waitFor(() => expect(screen.getByTestId('rol')).toHaveTextContent('Trabajador'));

    await usuario.click(screen.getByText('salir'));
    expect(await obtenerSesionOffline()).toBeNull();
  });
});
