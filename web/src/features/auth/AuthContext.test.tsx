import { render, screen } from '@testing-library/react';
import { AuthProvider, useAuth } from './AuthContext';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function Consumidor() {
  const { estaAutenticado, cargando, rol, usuario } = useAuth();
  return (
    <div>
      <span data-testid="autenticado">{String(estaAutenticado)}</span>
      <span data-testid="cargando">{String(cargando)}</span>
      <span data-testid="rol">{rol ?? 'sin-rol'}</span>
      <span data-testid="usuario">{usuario ? usuario.usuarioId : 'anónimo'}</span>
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
        .mockResolvedValueOnce(respuesta(200, { usuarioId: 'u1', rol: 'Cliente', clienteId: 'c1' })),
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
