import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
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
    const req =
      init !== undefined
        ? new Request(String(input), init)
        : input instanceof Request
          ? input
          : new Request(String(input));
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

  test('sin conexión el botón de guardar queda deshabilitado', async () => {
    baseFetchAvicola({});
    renderDialog();

    act(() => window.dispatchEvent(new Event('offline')));
    expect(await screen.findByRole('button', { name: 'Guardar' })).toBeDisabled();
    act(() => window.dispatchEvent(new Event('online')));
  });
});
