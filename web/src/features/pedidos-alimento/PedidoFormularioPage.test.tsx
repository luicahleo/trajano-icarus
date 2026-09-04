import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { PedidoFormularioPage } from './PedidoFormularioPage';

const pedidoBorrador = {
  id: 'p1',
  clienteId: 'c1',
  estado: 'Borrador',
  fechaPedido: null,
  fechaEntregaEstimada: null,
  totalSolicitado: null,
  lineas: [
    {
      id: 'l1',
      tipoAlimento: 'PosturaUno',
      presentacion: 'Bolsa',
      cantidadSolicitada: 100,
      equivalentes40Kg: 100,
      precioFinalPor40Kg: 176.5,
      subtotalSolicitado: 17650,
      notificacionPreciosAlimentosId: null,
    },
  ],
  historial: [],
};

const precios = {
  id: 'pub1',
  estado: 'Publicada',
  aporteCaisy: 1.2,
  fondo: 0.6,
  servicios: 0.75,
  detalles: [
    { tipoAlimento: 'PosturaUno', presentacion: 'Bolsa', precioFinalPor40Kg: 176.5, edadDesdeDias: 130, edadHastaDias: 500 },
    { tipoAlimento: 'Iniciador', presentacion: 'Bolsa', precioFinalPor40Kg: 180, edadDesdeDias: 21, edadHastaDias: 60 },
  ],
};

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchSimulado(reglas: Record<string, Response>) {
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    return reglas[`${req.method} ${new URL(req.url).pathname}`] ?? new Response('', { status: 404 });
  });
}

function renderPagina({ ruta = '/pedidos/nuevo' }: { ruta?: string } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[ruta]}>
        <Routes>
          <Route path="/pedidos/nuevo" element={<PedidoFormularioPage />} />
          <Route path="/pedidos/:id/editar" element={<PedidoFormularioPage />} />
          <Route path="/pedidos/:id" element={<div>Detalle del pedido</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PedidoFormularioPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('crea un borrador con líneas enteras y total estimado', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/pedidos-alimento/precios-vigentes': respuesta(200, precios),
      'GET /api/granjas': respuesta(200, []),
      'POST /api/pedidos-alimento': respuesta(201, { id: 'p99' }),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await screen.findByText(/Precio por 40 kg:/);
    await usuario.type(screen.getByLabelText('Bolsas'), '100');
    await usuario.click(screen.getByRole('button', { name: 'Crear borrador' }));
    const creacion = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/api/pedidos-alimento');
    });
    expect(creacion).toBeTruthy();
    const cuerpo = JSON.parse(await (creacion![0] as Request).clone().text());
    expect(cuerpo.detalles).toEqual([{ tipoAlimento: 'PosturaUno', presentacion: 'Bolsa', cantidad: 100 }]);
    expect(await screen.findByText('Detalle del pedido')).toBeInTheDocument();
  });

  test('rechaza granel por debajo de los mínimos sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/pedidos-alimento/precios-vigentes': respuesta(200, precios),
      'GET /api/granjas': respuesta(200, []),
      'POST /api/pedidos-alimento': respuesta(201, { id: 'p99' }),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await screen.findByText(/Precio por 40 kg:/);
    await usuario.click(screen.getByLabelText('Presentación'));
    await usuario.click(await screen.findByRole('option', { name: 'Granel (toneladas)' }));
    await usuario.type(await screen.findByLabelText('Toneladas'), '3');
    await usuario.click(screen.getByRole('button', { name: 'Crear borrador' }));
    expect(await screen.findByText(/mínimo 2 t por tipo y 6 t en total/i)).toBeInTheDocument();
    const llamadas = fetchMock.mock.calls.some(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/api/pedidos-alimento');
    });
    expect(llamadas).toBe(false);
  });

  test('rechaza cantidades no enteras', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/pedidos-alimento/precios-vigentes': respuesta(200, precios),
      'GET /api/granjas': respuesta(200, []),
      'POST /api/pedidos-alimento': respuesta(201, { id: 'nuevo' }),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await screen.findByText(/Precio por 40 kg:/);
    await usuario.type(screen.getByLabelText('Bolsas'), '100.5');
    await usuario.click(screen.getByRole('button', { name: 'Crear borrador' }));
    expect(await screen.findByText(/números enteros mayores que cero/i)).toBeInTheDocument();
  });

  test('al editar un borrador precarga sus líneas y guarda cambios', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/pedidos-alimento/p1': respuesta(200, pedidoBorrador),
      'GET /api/pedidos-alimento/precios-vigentes': respuesta(200, precios),
      'GET /api/granjas': respuesta(200, []),
      'PUT /api/pedidos-alimento/p1': respuesta(204),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina({ ruta: '/pedidos/p1/editar' });
    const bolsas = await screen.findByLabelText('Bolsas');
    expect((bolsas as HTMLInputElement).value).toBe('100');
    await usuario.clear(bolsas);
    await usuario.type(bolsas, '150');
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    const edicion = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'PUT' && req.url.endsWith('/api/pedidos-alimento/p1');
    });
    expect(edicion).toBeTruthy();
    expect(await screen.findByText('Detalle del pedido')).toBeInTheDocument();
  });

  test('recomienda tipos por la edad de los galpones sin obligar cantidad', async () => {
    const fechafutura = new Date(Date.now() - 40 * 86_400_000).toISOString().slice(0, 10);
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/pedidos-alimento/precios-vigentes': respuesta(200, precios),
        'GET /api/granjas': respuesta(200, [{ id: 'g1', nombre: 'Granja' }]),
        'GET /api/granjas/g1/galpones': respuesta(200, [
          {
            id: 'gal1',
            numero: '1',
            capacidadMaxima: 5000,
            gallinasActuales: 4800,
            fechaNacimientoLote: fechafutura,
            descripcion: null,
          },
        ]),
      }),
    );
    renderPagina();
    expect(await screen.findByText(/Galpón 1 \(40 días\): Iniciador/)).toBeInTheDocument();
  });
});
