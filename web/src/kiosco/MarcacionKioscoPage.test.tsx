import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MarcacionKioscoPage } from './MarcacionKioscoPage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function camaraSimulada() {
  const stop = vi.fn();
  const stream = { getTracks: () => [{ stop }] } as unknown as MediaStream;
  Object.defineProperty(navigator, 'mediaDevices', {
    configurable: true,
    value: { getUserMedia: vi.fn().mockResolvedValue(stream) },
  });
}

describe('MarcacionKioscoPage', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  test('confirma y muestra nombre y acción tras el éxito', async () => {
    const usuario = userEvent.setup();
    camaraSimulada();
    const fn = vi.fn(async (input: RequestInfo | URL) => {
      const req = input instanceof Request ? input : new Request(String(input));
      if (req.method === 'POST' && req.url.endsWith('/control-acceso/kiosco/marcaciones'))
        return respuesta(200, {
          estado: 'Registrada',
          trabajadorId: 't1',
          nombreCompleto: 'Ana Quispe',
          tipo: 'Entrada',
          instanteUtc: '2026-09-27T12:00:00+00:00',
          propuestaId: null,
          expiraPropuestaUtc: null,
          motivo: null,
        });
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);

    render(<MarcacionKioscoPage />);

    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    const botonCapturar = await screen.findByRole('button', { name: 'Capturar' });
    await waitFor(() => expect(botonCapturar).toBeEnabled());
    await usuario.click(botonCapturar);

    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();
    expect(screen.getByText('Entrada')).toBeInTheDocument();
  });

  test('cancelar la confirmación no envía ninguna marcación', async () => {
    const usuario = userEvent.setup();
    camaraSimulada();
    const fn = vi.fn(async () => new Response('', { status: 404 }));
    vi.stubGlobal('fetch', fn);

    render(<MarcacionKioscoPage />);

    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }));

    expect(fn).not.toHaveBeenCalled();
    expect(await screen.findByRole('button', { name: 'Entrada' })).toBeInTheDocument();
  });

  test('limpia el nombre y vuelve al inicio después del resultado exitoso', async () => {
    const usuario = userEvent.setup();
    const programarLimpieza = vi.spyOn(globalThis, 'setTimeout');
    camaraSimulada();
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        respuesta(200, {
          estado: 'Registrada',
          trabajadorId: 't1',
          nombreCompleto: 'Ana Quispe',
          tipo: 'Entrada',
          instanteUtc: '2026-09-27T12:00:00+00:00',
          propuestaId: null,
          expiraPropuestaUtc: null,
          motivo: null,
        }),
      ),
    );

    render(<MarcacionKioscoPage />);
    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    const botonCapturar = await screen.findByRole('button', { name: 'Capturar' });
    await waitFor(() => expect(botonCapturar).toBeEnabled());
    await usuario.click(botonCapturar);
    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();

    await waitFor(() => expect(programarLimpieza).toHaveBeenCalledWith(expect.any(Function), 6_000));
    const limpiar = programarLimpieza.mock.calls.find(([, espera]) => espera === 6_000)?.[0];
    await act(async () => {
      (limpiar as () => void)();
    });

    expect(screen.queryByText('Ana Quispe')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Entrada' })).toBeInTheDocument();
  });

  test('un rechazo de cámara no envía una marcación', async () => {
    const usuario = userEvent.setup();
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn().mockRejectedValue(new Error('denegada')) },
    });
    const fn = vi.fn(async () => new Response('', { status: 404 }));
    vi.stubGlobal('fetch', fn);

    render(<MarcacionKioscoPage />);
    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(await screen.findByText('No se pudo acceder a la cámara.')).toBeInTheDocument();
    expect(screen.getByText('Capturar')).toBeDisabled();
    expect(fn).not.toHaveBeenCalled();
  });
});
