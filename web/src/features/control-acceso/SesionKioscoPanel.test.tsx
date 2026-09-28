import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { SesionKioscoPanel } from './SesionKioscoPanel';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

describe('SesionKioscoPanel', () => {
  afterEach(() => vi.restoreAllMocks());

  test('activa la sesión de kiosco con encabezado antiforgery', async () => {
    const usuario = userEvent.setup();
    const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const req = input instanceof Request ? input : new Request(String(input), init);
      if (req.url.endsWith('/identidad/sesion/renovar'))
        return respuesta(200, { accessToken: 't', expiraEnSegundos: 900 });
      if (req.url.endsWith('/identidad/me'))
        return respuesta(200, {
          usuarioId: 'u1',
          correo: 'cliente@icarus.test',
          rol: 'Cliente',
          clienteId: 'cli1',
          trabajadorId: null,
          modulos: ['ControlAcceso'],
          funcionalidades: [],
        });
      if (req.url.endsWith('/control-acceso/kiosco/sesion') && req.method === 'POST')
        return respuesta(204);
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);

    render(
      <MemoryRouter>
        <AuthProvider>
          <SesionKioscoPanel />
        </AuthProvider>
      </MemoryRouter>,
    );

    await usuario.type(await screen.findByLabelText('Contraseña'), 'Clave-Larga-123456');
    await usuario.click(screen.getByRole('button', { name: 'Activar kiosco' }));

    const llamada = await screen.findByText('Sesión de kiosco activada en este navegador.');
    expect(llamada).toBeInTheDocument();

    const peticion = fn.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/control-acceso/kiosco/sesion');
    });
    expect(peticion).toBeDefined();
    const request = peticion![0] as Request;
    expect(request.headers.get('X-Icarus-Kiosco')).toBe('1');
  });
});
