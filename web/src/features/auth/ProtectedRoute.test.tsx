import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './AuthContext';
import { ProtectedRoute } from './ProtectedRoute';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function renderConRutas(fetchMock: () => Promise<Response>) {
  vi.stubGlobal('fetch', fetchMock);
  return render(
    <MemoryRouter initialEntries={['/']}>
      <AuthProvider>
        <Routes>
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <div>contenido protegido</div>
              </ProtectedRoute>
            }
          />
          <Route path="/login" element={<div>pantalla de login</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('sin sesión redirige a /login', async () => {
    renderConRutas(() => Promise.resolve(respuesta(401)));
    expect(await screen.findByText('pantalla de login')).toBeInTheDocument();
    expect(screen.queryByText('contenido protegido')).not.toBeInTheDocument();
  });

  test('con sesión muestra el contenido protegido', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' }))
      .mockResolvedValueOnce(respuesta(200, { usuarioId: 'u1', rol: 'Administrador', clienteId: null }));
    renderConRutas(fetchMock);
    expect(await screen.findByText('contenido protegido')).toBeInTheDocument();
  });

  test('mientras carga muestra un indicador y no redirige', async () => {
    renderConRutas(() => new Promise(() => {}));
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    expect(screen.queryByText('contenido protegido')).not.toBeInTheDocument();
    expect(screen.queryByText('pantalla de login')).not.toBeInTheDocument();
  });
});
