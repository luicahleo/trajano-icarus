import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './AuthContext';
import { RequiereFuncionalidad } from './RequiereFuncionalidad';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchConSesion(funcionalidades: string[]) {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    // Si input ya es un Request, usarlo tal cual: fetch(input, init) lo
    // consume sin alterar method/body. Reconstruirlo desde String(input)
    // perdería el POST; re-armarlo con body exigiría duplex.
    const req = input instanceof Request ? input : new Request(String(input), init);
    const ruta = new URL(req.url).pathname;
    if (ruta === '/api/identidad/sesion/renovar') {
      return respuesta(200, { accessToken: 't', expiraEnSegundos: 900 });
    }
    if (ruta === '/api/identidad/me') {
      return respuesta(200, {
        usuarioId: 'u1',
        rol: 'Trabajador',
        clienteId: 'c1',
        trabajadorId: 't1',
        modulos: [],
        funcionalidades,
      });
    }
    return new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function renderGuarda() {
  return render(
    <MemoryRouter initialEntries={['/protegida']}>
      <AuthProvider>
        <Routes>
          <Route
            path="/protegida"
            element={
              <RequiereFuncionalidad funcionalidades={['ProduccionHuevos']}>
                <div>Zona de recogida</div>
              </RequiereFuncionalidad>
            }
          />
          <Route path="/inicio" element={<div>Inicio</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('RequiereFuncionalidad', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('deja pasar cuando el usuario tiene la funcionalidad', async () => {
    fetchConSesion(['ProduccionHuevos', 'Mortalidad']);
    renderGuarda();
    expect(await screen.findByText('Zona de recogida')).toBeInTheDocument();
  });

  test('redirige al inicio cuando no la tiene', async () => {
    fetchConSesion(['Granjas']);
    renderGuarda();
    expect(await screen.findByText('Inicio')).toBeInTheDocument();
    expect(screen.queryByText('Zona de recogida')).not.toBeInTheDocument();
  });
});
