import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MarcacionKioscoPage } from './MarcacionKioscoPage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

const NOMBRE_CENTINELA = 'Ana Quispe Centinela';
const ID_CENTINELA = 't-centinela';

function volcadoSession(): string {
  return Array.from({ length: sessionStorage.length }, (_, i) => {
    const clave = sessionStorage.key(i);
    return clave ? `${clave}=${sessionStorage.getItem(clave)}` : '';
  }).join('|');
}

describe('privacidad del kiosco', () => {
  afterEach(() => vi.restoreAllMocks());

  test('no persiste ni registra el nombre ni el identificador tras el éxito', async () => {
    const usuario = userEvent.setup();
    const stop = vi.fn();
    const stream = { getTracks: () => [{ stop }] } as unknown as MediaStream;
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn().mockResolvedValue(stream) },
    });
    const registro = vi.spyOn(console, 'log').mockImplementation(() => undefined);
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const fn = vi.fn(async (input: RequestInfo | URL) => {
      const req = input instanceof Request ? input : new Request(String(input));
      if (req.method === 'POST' && req.url.endsWith('/control-acceso/kiosco/marcaciones'))
        return respuesta(200, {
          estado: 'Registrada',
          trabajadorId: ID_CENTINELA,
          nombreCompleto: NOMBRE_CENTINELA,
          tipo: 'Entrada',
          instanteUtc: '2026-09-27T12:00:00+00:00',
          propuestaId: null,
          expiraPropuestaUtc: null,
          motivo: null,
        });
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);
    localStorage.clear();
    sessionStorage.clear();

    render(<MarcacionKioscoPage />);

    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    const botonCapturar = await screen.findByRole('button', { name: 'Capturar' });
    await waitFor(() => expect(botonCapturar).toBeEnabled());
    await usuario.click(botonCapturar);

    expect(await screen.findByText(NOMBRE_CENTINELA)).toBeInTheDocument();

    expect(localStorage.length).toBe(0);
    expect(volcadoSession()).not.toContain(NOMBRE_CENTINELA);
    expect(volcadoSession()).not.toContain(ID_CENTINELA);
    expect(sessionStorage.getItem('icarus.diagnostico.eventos')).toBeNull();
    expect(JSON.stringify(registro.mock.calls)).not.toContain(NOMBRE_CENTINELA);
    expect(JSON.stringify(error.mock.calls)).not.toContain(NOMBRE_CENTINELA);
  });
});
