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
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

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
          flujoId: 'f1',
          intentos: 1,
          maximoIntentos: 3,
        });
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);
    localStorage.clear();
    sessionStorage.clear();

    render(<MarcacionKioscoPage />);

    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    const botonIniciar = await screen.findByRole('button', { name: 'Iniciar captura' });
    await waitFor(() => expect(botonIniciar).toBeEnabled());
    await usuario.click(botonIniciar);
    await vi.advanceTimersByTimeAsync(3_100);

    expect(await screen.findByText(NOMBRE_CENTINELA)).toBeInTheDocument();

    expect(localStorage.length).toBe(0);
    expect(volcadoSession()).not.toContain(NOMBRE_CENTINELA);
    expect(volcadoSession()).not.toContain(ID_CENTINELA);
    expect(sessionStorage.getItem('icarus.diagnostico.eventos')).toBeNull();
    expect(JSON.stringify(registro.mock.calls)).not.toContain(NOMBRE_CENTINELA);
    expect(JSON.stringify(error.mock.calls)).not.toContain(NOMBRE_CENTINELA);
  });

  test('no persiste ni registra datos de la incidencia en el kiosco', async () => {
    const usuario = userEvent.setup();
    const stop = vi.fn();
    const stream = { getTracks: () => [{ stop }] } as unknown as MediaStream;
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn().mockResolvedValue(stream) },
    });
    const registro = vi.spyOn(console, 'log').mockImplementation(() => undefined);
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    let intentos = 0;
    const fn = vi.fn(async (input: RequestInfo | URL) => {
      const req = input instanceof Request ? input : new Request(String(input));
      if (req.method === 'POST' && req.url.endsWith('/control-acceso/kiosco/marcaciones')) {
        intentos++;
        return respuesta(200, {
          estado: intentos === 3 ? 'Incidencia' : 'Rechazada',
          trabajadorId: null,
          nombreCompleto: null,
          tipo: null,
          instanteUtc: null,
          propuestaId: null,
          expiraPropuestaUtc: null,
          motivo: intentos === 3 ? null : 'sin_coincidencia',
          flujoId: 'flujo-centinela',
          intentos,
          maximoIntentos: 3,
        });
      }
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);
    localStorage.clear();
    sessionStorage.clear();

    render(<MarcacionKioscoPage />);

    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    for (let i = 0; i < 3; i++) {
      const boton = await screen.findByRole('button', { name: 'Iniciar captura' });
      await waitFor(() => expect(boton).toBeEnabled());
      await usuario.click(boton);
      await vi.advanceTimersByTimeAsync(3_100);
      if (i < 2) {
        expect(await screen.findByText(/quedan \d intentos?|último intento/i)).toBeInTheDocument();
      }
    }

    expect(await screen.findByText(/incidencia/i)).toBeInTheDocument();

    expect(localStorage.length).toBe(0);
    expect(volcadoSession()).not.toContain('flujo-centinela');
    expect(volcadoSession()).not.toContain(NOMBRE_CENTINELA);
    expect(volcadoSession()).not.toContain(ID_CENTINELA);
    expect(sessionStorage.getItem('icarus.diagnostico.eventos')).toBeNull();
    expect(JSON.stringify(registro.mock.calls)).not.toContain('flujo-centinela');
    expect(JSON.stringify(error.mock.calls)).not.toContain('flujo-centinela');
  });
});
