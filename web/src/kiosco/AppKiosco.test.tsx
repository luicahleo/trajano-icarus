import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AppKiosco } from './AppKiosco';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

describe('AppKiosco', () => {
  afterEach(() => vi.restoreAllMocks());

  test('sin sesión muestra la activación y al activar pasa a marcación', async () => {
    const usuario = userEvent.setup();
    const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const req = input instanceof Request ? input : new Request(String(input), init);
      if (req.method === 'GET' && req.url.endsWith('/control-acceso/kiosco/sesion'))
        return respuesta(401);
      if (req.method === 'POST' && req.url.endsWith('/control-acceso/kiosco/sesion'))
        return respuesta(204);
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);

    render(<AppKiosco />);

    await usuario.type(await screen.findByLabelText('Correo'), 'cliente@icarus.test');
    await usuario.type(screen.getByLabelText('Contraseña'), 'Clave-Larga-123456');
    await usuario.click(screen.getByRole('button', { name: 'Activar' }));

    expect(await screen.findByRole('button', { name: 'Entrada' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Salida' })).toBeInTheDocument();
  });

  test('con sesión vigente entra directo a marcación', async () => {
    const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const req = input instanceof Request ? input : new Request(String(input), init);
      if (req.method === 'GET' && req.url.endsWith('/control-acceso/kiosco/sesion'))
        return respuesta(200, { expiraEnUtc: '2030-01-01T00:00:00+00:00' });
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);

    render(<AppKiosco />);

    expect(await screen.findByRole('button', { name: 'Entrada' })).toBeInTheDocument();
  });
});
