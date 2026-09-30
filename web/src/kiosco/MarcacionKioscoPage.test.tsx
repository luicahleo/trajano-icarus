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
    value: { getUserMedia: vi.fn().mockReturnValue(stream) },
  });
}

async function avanzarCuentaRegresiva() {
  // 3 -> 2 -> 1 -> captura (tres intervalos de un segundo).
  for (let i = 0; i < 3; i++) {
    await act(async () => {
      vi.advanceTimersByTime(1_000);
    });
  }
  // La captura y el envío usan microtareas/efectos; dejar que se resuelvan.
  await act(async () => {
    vi.advanceTimersByTime(100);
  });
}

async function esperarCamaraLista() {
  const boton = await screen.findByRole('button', { name: 'Iniciar captura' });
  await act(async () => {
    await Promise.resolve();
  });
  await waitFor(() => expect(boton).toBeEnabled());
  return boton;
}

describe('MarcacionKioscoPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  test('confirma, inicia captura y muestra nombre y acción tras el éxito', async () => {
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
          flujoId: 'f1',
          intentos: 1,
          maximoIntentos: 3,
        });
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);

    render(<MarcacionKioscoPage />);

    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    const botonIniciar = await esperarCamaraLista();
    await usuario.click(botonIniciar);

    expect(screen.getByText('3')).toBeInTheDocument();
    await avanzarCuentaRegresiva();

    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();
    expect(screen.getByText('Entrada')).toBeInTheDocument();
    expect(fn).toHaveBeenCalledTimes(1);
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
          flujoId: 'f1',
          intentos: 1,
          maximoIntentos: 3,
        }),
      ),
    );

    render(<MarcacionKioscoPage />);
    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    const botonIniciar = await esperarCamaraLista();
    await usuario.click(botonIniciar);
    await avanzarCuentaRegresiva();
    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(6_000);
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
    expect(screen.getByText('Iniciar captura')).toBeDisabled();
    expect(fn).not.toHaveBeenCalled();
  });

  test('primer rechazo facial muestra intentos restantes y permite reintentar', async () => {
    const usuario = userEvent.setup();
    camaraSimulada();
    const fn = vi.fn(async (input: RequestInfo | URL) => {
      const req = input instanceof Request ? input : new Request(String(input));
      if (req.method === 'POST' && req.url.endsWith('/control-acceso/kiosco/marcaciones')) {
        const cuerpo = await req.clone().json();
        return respuesta(200, {
          estado: 'Rechazada',
          trabajadorId: null,
          nombreCompleto: null,
          tipo: null,
          instanteUtc: null,
          propuestaId: null,
          expiraPropuestaUtc: null,
          motivo: 'sin_coincidencia',
          flujoId: cuerpo.flujoId ?? 'f1',
          intentos: cuerpo.flujoId ? 2 : 1,
          maximoIntentos: 3,
        });
      }
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);

    render(<MarcacionKioscoPage />);
    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    const botonIniciar = await esperarCamaraLista();
    await usuario.click(botonIniciar);
    await avanzarCuentaRegresiva();

    expect(await screen.findByText(/quedan 2 intentos/i)).toBeInTheDocument();
    const reintentar = screen.getByRole('button', { name: 'Iniciar captura' });
    await usuario.click(reintentar);
    await avanzarCuentaRegresiva();

    expect(await screen.findByText(/último intento/i)).toBeInTheDocument();
    expect(fn).toHaveBeenCalledTimes(2);
    const segunda = fn.mock.calls[1][0] as Request;
    expect((await segunda.clone().json()).flujoId).toBe('f1');
  });

  test('tercer rechazo facial crea incidencia y libera la tablet', async () => {
    const usuario = userEvent.setup();
    camaraSimulada();
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
          flujoId: 'f1',
          intentos: intentos === 3 ? 3 : intentos,
          maximoIntentos: 3,
        });
      }
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);

    render(<MarcacionKioscoPage />);
    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    for (let i = 0; i < 3; i++) {
      const boton = await esperarCamaraLista();
      await usuario.click(boton);
      await avanzarCuentaRegresiva();
      if (i === 0) {
        expect(await screen.findByText(/quedan 2 intentos/i)).toBeInTheDocument();
      } else if (i === 1) {
        expect(await screen.findByText(/último intento/i)).toBeInTheDocument();
      }
    }

    expect(await screen.findByText(/incidencia/i)).toBeInTheDocument();
    expect(fn).toHaveBeenCalledTimes(3);

    act(() => {
      vi.advanceTimersByTime(4_000);
    });

    expect(screen.getByRole('button', { name: 'Entrada' })).toBeInTheDocument();
  });

  test('respuesta incierta no consume intento y permite reintentar', async () => {
    const usuario = userEvent.setup();
    camaraSimulada();
    let llamadas = 0;
    const fn = vi.fn(async (input: RequestInfo | URL) => {
      const req = input instanceof Request ? input : new Request(String(input));
      llamadas++;
      if (req.method === 'POST' && req.url.endsWith('/control-acceso/kiosco/marcaciones')) {
        if (llamadas === 1) throw new Error('red');
        return respuesta(200, {
          estado: 'Registrada',
          trabajadorId: 't1',
          nombreCompleto: 'Ana Quispe',
          tipo: 'Entrada',
          instanteUtc: '2026-09-27T12:00:00+00:00',
          propuestaId: null,
          expiraPropuestaUtc: null,
          motivo: null,
          flujoId: 'f1',
          intentos: 1,
          maximoIntentos: 3,
        });
      }
      return new Response('', { status: 404 });
    });
    vi.stubGlobal('fetch', fn);

    render(<MarcacionKioscoPage />);
    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    const botonIniciar = await esperarCamaraLista();
    await usuario.click(botonIniciar);
    await avanzarCuentaRegresiva();

    expect(await screen.findByText(/comprobando registro/i)).toBeInTheDocument();
    const reintentar = await screen.findByRole('button', { name: 'Iniciar captura' });
    await usuario.click(reintentar);
    await avanzarCuentaRegresiva();

    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();
  });

  test('doble toque en iniciar captura no dispara dos peticiones', async () => {
    const usuario = userEvent.setup();
    camaraSimulada();
    const fn = vi.fn(async () =>
      respuesta(200, {
        estado: 'Registrada',
        trabajadorId: 't1',
        nombreCompleto: 'Ana Quispe',
        tipo: 'Entrada',
        instanteUtc: '2026-09-27T12:00:00+00:00',
        propuestaId: null,
        expiraPropuestaUtc: null,
        motivo: null,
        flujoId: 'f1',
        intentos: 1,
        maximoIntentos: 3,
      }),
    );
    vi.stubGlobal('fetch', fn);

    render(<MarcacionKioscoPage />);
    await usuario.click(screen.getByRole('button', { name: 'Entrada' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    const botonIniciar = await esperarCamaraLista();
    await usuario.dblClick(botonIniciar);
    await avanzarCuentaRegresiva();

    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();
    expect(fn).toHaveBeenCalledTimes(1);
  });
});
