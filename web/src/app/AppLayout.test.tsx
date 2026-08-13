import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Rol } from '../lib/tipos';
import { AuthProvider } from '../features/auth/AuthContext';
import { AppLayout } from './AppLayout';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function renderLayout(rol: Rol) {
  vi.stubGlobal(
    'fetch',
    vi
      .fn()
      .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' }))
      .mockResolvedValueOnce(respuesta(200, { usuarioId: 'u1', rol, clienteId: null })),
  );
  return render(
    <MemoryRouter initialEntries={['/']}>
      <AuthProvider>
        <Routes>
          <Route element={<AppLayout />}>
            <Route path="/" element={<div>contenido del layout</div>} />
          </Route>
          <Route path="/login" element={<div>pantalla de login</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('AppLayout', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('el administrador ve Clientes, Usuarios y Trabajadores', async () => {
    renderLayout('Administrador');
    expect(await screen.findByText('Clientes')).toBeInTheDocument();
    expect(screen.getByText('Usuarios')).toBeInTheDocument();
    expect(screen.getByText('Trabajadores')).toBeInTheDocument();
  });

  test('el cliente solo ve Trabajadores', async () => {
    renderLayout('Cliente');
    expect(await screen.findByText('Trabajadores')).toBeInTheDocument();
    expect(screen.queryByText('Clientes')).not.toBeInTheDocument();
    expect(screen.queryByText('Usuarios')).not.toBeInTheDocument();
  });

  test('cerrar sesión navega a /login y deja anónimo', async () => {
    const usuario = userEvent.setup();
    renderLayout('Administrador');
    await usuario.click(await screen.findByRole('button', { name: 'Cerrar sesión' }));
    expect(screen.getByText('pantalla de login')).toBeInTheDocument();
    expect(screen.queryByText('Clientes')).not.toBeInTheDocument();
  });
});
