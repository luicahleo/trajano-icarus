import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AdminVacunacionPage } from './AdminVacunacionPage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function baseFetch(reglas: Record<string, Response | Response[]>) {
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

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AdminVacunacionPage />
    </QueryClientProvider>,
  );
}

describe('AdminVacunacionPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('lista los programas del catálogo', async () => {
    baseFetch({
      'GET /api/vacunacion/programas': respuesta(200, [
        {
          id: 'p1',
          nombre: 'Plan CAISY 2026',
          fechaEmision: '2026-01-15',
          cantidadAves: 1000,
          observaciones: null,
          estaActivo: true,
        },
      ]),
    });
    renderPagina();

    expect(await screen.findByText('Plan CAISY 2026')).toBeInTheDocument();
    expect(screen.getByText(/1000 aves/)).toBeInTheDocument();
  });

  test('crea un programa con sus datos básicos', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/vacunacion/programas': respuesta(200, []),
      'POST /api/vacunacion/programas': respuesta(201, { id: 'p2' }),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Nuevo programa' }));
    await usuario.type(screen.getByLabelText('Nombre'), 'Plan nuevo');
    await usuario.type(screen.getByLabelText('Cantidad de aves'), '1000');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'POST');
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo).toMatchObject({ nombre: 'Plan nuevo', cantidadAves: 1000 });
  });

  test('muestra los errores por fila cuando el Excel es inválido', async () => {
    const usuario = userEvent.setup();
    baseFetch({
      'GET /api/vacunacion/programas': respuesta(200, [
        {
          id: 'p1',
          nombre: 'Plan CAISY 2026',
          fechaEmision: '2026-01-15',
          cantidadAves: 1000,
          observaciones: null,
          estaActivo: true,
        },
      ]),
      'GET /api/vacunacion/programas/p1': respuesta(200, {
        id: 'p1',
        nombre: 'Plan CAISY 2026',
        fechaEmision: '2026-01-15',
        cantidadAves: 1000,
        observaciones: null,
        estaActivo: true,
        items: [],
      }),
      'POST /api/vacunacion/programas/p1/cronograma-excel': respuesta(400, {
        title: 'Error de validación',
        errors: { Contenido: ['Fila 4: La edad debe ser un número entero mayor que cero.'] },
      }),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Subir Excel' }));
    const input = screen.getByLabelText('Archivo Excel');
    await usuario.upload(input, new File(['x'], 'plan.xlsx'));

    expect(await screen.findByText(/Fila 4/)).toBeInTheDocument();
  });
});
