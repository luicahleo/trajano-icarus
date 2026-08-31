import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import {
  encolarOperacion,
  iniciarCoordinadorOffline,
  listarOperaciones,
} from '../../app/offline/coordinador';
import type { DatosBajas } from './api';
import { RegistrarBajasDialog } from './RegistrarBajasDialog';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function baseFetchAvicola(reglas: Record<string, Response | Response[]>) {
  const colas = new Map(
    Object.entries(reglas).map(([clave, valor]) => [
      clave,
      Array.isArray(valor) ? [...valor] : [valor],
    ]),
  );
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    // Si input ya es un Request, usarlo tal cual: fetch(input, init) lo
    // consume sin alterar method/body. Reconstruirlo desde String(input)
    // perdería el POST; re-armarlo con body exigiría duplex.
    const req = input instanceof Request ? input : new Request(String(input), init);
    return (
      colas.get(`${req.method} ${new URL(req.url).pathname}`)?.shift() ??
      new Response('', { status: 404 })
    );
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function renderDialog() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <RegistrarBajasDialog galponId="ga1" abierto alCerrar={vi.fn()} />
    </QueryClientProvider>,
  );
}

function llamadaCon(fetchMock: ReturnType<typeof vi.fn>, metodo: string, sufijo: string) {
  return fetchMock.mock.calls.some(([arg]) => {
    const req = arg as Request;
    return req.method === metodo && req.url.endsWith(sufijo);
  });
}

describe('RegistrarBajasDialog', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('registra bajas solas con idempotencyKey', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({
      'POST /api/galpones/ga1/mortalidad': respuesta(201, { id: 'm1' }),
    });
    renderDialog();

    await usuario.type(screen.getByLabelText('Gallinas muertas'), '10');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'POST');
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo.cantidadMuertas).toBe(10);
    expect(cuerpo.idempotencyKey).toBeTruthy();
    expect(cuerpo).not.toHaveProperty('fecha');
  });

  test('rechaza cero muertas sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({});
    renderDialog();

    await usuario.type(screen.getByLabelText('Gallinas muertas'), '0');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/mayor que cero/i)).toBeInTheDocument();
    expect(llamadaCon(fetchMock, 'POST', '/mortalidad')).toBe(false);
  });

  test('encola aunque TanStack Query marque la app sin conexión (evento offline real)', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    vi.stubGlobal('fetch', vi.fn());
    const limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    try {
      const alCerrar = vi.fn();
      const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={queryClient}>
          <RegistrarBajasDialog galponId="ga1" abierto alCerrar={alCerrar} />
        </QueryClientProvider>,
      );
      // El listener del onlineManager lo instala QueryClient.mount() (efecto
      // del provider): la red se corta DESPUÉS de renderizar, como en la app.
      window.dispatchEvent(new Event('offline'));
      const usuario = userEvent.setup();
      await usuario.type(screen.getByLabelText('Gallinas muertas'), '2');
      await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
      await waitFor(() => expect(alCerrar).toHaveBeenCalled());
      const ops = await listarOperaciones();
      expect(ops.length).toBe(1);
      expect(ops[0].tipo).toBe('mortalidad.crear');
    } finally {
      limpiar();
      window.dispatchEvent(new Event('online'));
      vi.restoreAllMocks();
      vi.unstubAllGlobals();
    }
  });

  test('edita una baja pendiente: precarga los datos y actualiza la cola', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    vi.stubGlobal('fetch', vi.fn());
    const limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    try {
      await encolarOperacion('mortalidad.crear', 'ga1', {
        hora: '07:45',
        cantidadMuertas: 2,
        idempotencyKey: 'k9',
      });
      const [op] = await listarOperaciones();
      const alCerrar = vi.fn();
      const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={queryClient}>
          <RegistrarBajasDialog galponId="ga1" abierto pendiente={op} alCerrar={alCerrar} />
        </QueryClientProvider>,
      );

      expect(screen.getByRole('heading', { name: 'Editar bajas' })).toBeInTheDocument();
      expect(screen.getByLabelText('Gallinas muertas')).toHaveValue('2');
      const usuario = userEvent.setup();
      await usuario.clear(screen.getByLabelText('Gallinas muertas'));
      await usuario.type(screen.getByLabelText('Gallinas muertas'), '5');
      await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

      await waitFor(() => expect(alCerrar).toHaveBeenCalled());
      const ops = await listarOperaciones();
      expect(ops).toHaveLength(1);
      expect((ops[0].cuerpo as DatosBajas).cantidadMuertas).toBe(5);
      expect((ops[0].cuerpo as DatosBajas).idempotencyKey).toBe('k9');
    } finally {
      limpiar();
      vi.restoreAllMocks();
      vi.unstubAllGlobals();
    }
  });

  test('guardar habilitado sin conexión y encola la baja', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    const limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    try {
      const alCerrar = vi.fn();
      const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={queryClient}>
          <RegistrarBajasDialog galponId="ga1" abierto alCerrar={alCerrar} />
        </QueryClientProvider>,
      );
      const usuario = userEvent.setup();
      await usuario.type(screen.getByLabelText('Gallinas muertas'), '2');
      await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
      await waitFor(() => expect(alCerrar).toHaveBeenCalled());
      expect(fetchMock).not.toHaveBeenCalled();
      const ops = await listarOperaciones();
      expect(ops.length).toBe(1);
      expect(ops[0].tipo).toBe('mortalidad.crear');
    } finally {
      limpiar();
      vi.restoreAllMocks();
      vi.unstubAllGlobals();
    }
  });
});
