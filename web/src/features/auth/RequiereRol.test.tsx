import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Rol } from '../../lib/tipos';
import { AuthProvider } from './AuthContext';
import { ProtectedRoute } from './ProtectedRoute';
import { RequiereRol } from './RequiereRol';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function sesionConRol(rol: string) {
  return vi
    .fn()
    .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' }))
    .mockResolvedValueOnce(
      respuesta(200, {
        usuarioId: 'u1',
        rol,
        clienteId: null,
        trabajadorId: null,
        modulos: [],
        funcionalidades: [],
      }),
    );
}

function renderEnRutas(roles: Rol[], fetchMock: () => Promise<Response>) {
  vi.stubGlobal('fetch', fetchMock);
  return render(
    <MemoryRouter initialEntries={['/protegida']}>
      <AuthProvider>
        <Routes>
          <Route path="/" element={<div>inicio</div>} />
          <Route
            path="/protegida"
            element={
              <ProtectedRoute>
                <RequiereRol roles={roles}>
                  <div>panel protegido</div>
                </RequiereRol>
              </ProtectedRoute>
            }
          />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('RequiereRol', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('con el rol permitido muestra el contenido', async () => {
    renderEnRutas(['Cliente'], sesionConRol('Cliente'));
    expect(await screen.findByText('panel protegido')).toBeInTheDocument();
  });

  test('con rol ajeno redirige a la raíz', async () => {
    renderEnRutas(['Administrador'], sesionConRol('Cliente'));
    expect(await screen.findByText('inicio')).toBeInTheDocument();
    expect(screen.queryByText('panel protegido')).not.toBeInTheDocument();
  });
});
