import { render, screen, waitFor } from '@testing-library/react';
import { AuthProvider, useAuth } from './AuthContext';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function Consumidor() {
  const { estaAutenticado, cargando, rol, usuario, modulos, funcionalidades, tieneFuncionalidad } = useAuth();
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
      <span data-testid="tiene-cualquiera">{String(tieneFuncionalidad('Granjas', 'Mortalidad'))}</span>
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
        .mockResolvedValueOnce(respuesta(200, { usuarioId: 'u1', rol: 'Cliente', clienteId: 'c1', trabajadorId: null, modulos: [], funcionalidades: [] })),
    );

    render(
      <AuthProvider>
        <Consumidor />
      </AuthProvider>,
    );

    expect(screen.getByTestId('cargando').textContent).toBe('true');

    expect(await screen.findByTestId('autenticado')).toHaveTextContent('true');
    expect(screen.getByTestId('cargando').textContent).toBe('false');
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

    await waitFor(() => expect(screen.getByTestId('funcionalidades')).toHaveTextContent('ProduccionHuevos,Mortalidad'));
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
    expect(screen.getByTestId('cargando').textContent).toBe('false');
    expect(screen.getByTestId('rol').textContent).toBe('sin-rol');
  });
});
