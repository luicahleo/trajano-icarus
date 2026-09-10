import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DespachoHuevoFormularioPage } from './DespachoHuevoFormularioPage';

const despachoBorrador = {
  id: 'h1',
  estado: 'Borrador',
  fechaDespacho: null,
  totalAmarras: 2,
  totalHuevos: 390,
  totalBs: null,
  detalles: [
    {
      id: 'l1',
      tamano: 'Extra',
      cantidadAmarras: 2,
      unidadesSueltas: 30,
      cantidadHuevos: 390,
      precioUnitarioCongelado: null,
      subtotal: null,
    },
  ],
};

const precioVigente = {
  id: 'pub1',
  fechaNotificacion: '2026-09-01',
  fechaVigencia: '2026-09-01',
  estado: 'Publicada',
  servicio: 0.05,
  detalles: [
    {
      id: 'd1',
      tamano: 'Extra',
      precioAlProductor: 0.8,
      precioActualDocumento: null,
      precioUnitario: 0.85,
    },
    {
      id: 'd2',
      tamano: 'Primera',
      precioAlProductor: 0.72,
      precioActualDocumento: null,
      precioUnitario: 0.77,
    },
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
    return (
      reglas[`${req.method} ${new URL(req.url).pathname}`] ?? new Response('', { status: 404 })
    );
  });
}

function renderPagina({ ruta = '/despachos/nuevo' }: { ruta?: string } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[ruta]}>
        <Routes>
          <Route path="/despachos/nuevo" element={<DespachoHuevoFormularioPage />} />
          <Route path="/despachos/:id/editar" element={<DespachoHuevoFormularioPage />} />
          <Route path="/despachos/:id" element={<div>Detalle del despacho</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('DespachoHuevoFormularioPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('crea un borrador con líneas por tamaño y resumen en vivo', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/despachos-huevo/precios-vigentes': respuesta(200, precioVigente),
      'POST /api/despachos-huevo': respuesta(201, { id: 'h99' }),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    // Resumen en vivo: 2 amarras × 180 + 30 sueltas = 390 huevos × Bs 0,85
    // (precio unitario: productor + servicio).
    const amarras = await screen.findByLabelText('Amarras');
    await usuario.type(amarras, '2');
    await usuario.type(screen.getByLabelText('Unidades sueltas'), '30');
    expect(screen.getByText('Total amarras: 2')).toBeInTheDocument();
    expect(screen.getByText('Total huevos: 390')).toBeInTheDocument();
    expect(screen.getByText(/331,50/)).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Crear borrador' }));
    const creacion = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/api/despachos-huevo');
    });
    expect(creacion).toBeTruthy();
    const cuerpo = JSON.parse(await (creacion![0] as Request).clone().text());
    expect(cuerpo.lineas).toEqual([{ tamano: 'Extra', cantidadAmarras: 2, unidadesSueltas: 30 }]);
    expect(await screen.findByText('Detalle del despacho')).toBeInTheDocument();
  });

  test('rechaza unidades sueltas fuera de rango sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/despachos-huevo/precios-vigentes': respuesta(200, precioVigente),
      'POST /api/despachos-huevo': respuesta(201, { id: 'h99' }),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await screen.findByLabelText('Amarras');
    await usuario.type(screen.getByLabelText('Unidades sueltas'), '180');
    await usuario.click(screen.getByRole('button', { name: 'Crear borrador' }));
    expect(await screen.findByText(/entre 0 y 179/i)).toBeInTheDocument();
    const llamadas = fetchMock.mock.calls.some(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/api/despachos-huevo');
    });
    expect(llamadas).toBe(false);
  });

  test('rechaza una línea vacía', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/despachos-huevo/precios-vigentes': respuesta(200, precioVigente),
      'POST /api/despachos-huevo': respuesta(201, { id: 'h99' }),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await screen.findByLabelText('Amarras');
    await usuario.click(screen.getByRole('button', { name: 'Crear borrador' }));
    expect(await screen.findByText(/amarras o unidades sueltas/i)).toBeInTheDocument();
    const llamadas = fetchMock.mock.calls.some(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/api/despachos-huevo');
    });
    expect(llamadas).toBe(false);
  });

  test('rechaza tamaños duplicados', async () => {
    const usuario = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/despachos-huevo/precios-vigentes': respuesta(200, precioVigente),
        'POST /api/despachos-huevo': respuesta(201, { id: 'h99' }),
      }),
    );
    renderPagina();
    await usuario.type(await screen.findByLabelText('Amarras'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Agregar tamaño' }));
    const selects = screen.getAllByLabelText('Tamaño de huevo');
    await usuario.click(selects[1]);
    await usuario.click(await screen.findByRole('option', { name: 'Extra' }));
    await usuario.type(screen.getAllByLabelText('Amarras')[1], '1');
    await usuario.click(screen.getByRole('button', { name: 'Crear borrador' }));
    expect(await screen.findByText(/solo puede aparecer una vez/i)).toBeInTheDocument();
  });

  test('sin precio vigente avisa pero permite guardar el borrador', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/despachos-huevo/precios-vigentes': respuesta(404),
      'POST /api/despachos-huevo': respuesta(201, { id: 'h99' }),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    expect(
      await screen.findByText(/No hay publicación de precios de huevo vigente/i),
    ).toBeInTheDocument();
    await usuario.type(screen.getByLabelText('Amarras'), '1');
    await usuario.click(screen.getByRole('button', { name: 'Crear borrador' }));
    const creacion = fetchMock.mock.calls.some(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/api/despachos-huevo');
    });
    expect(creacion).toBe(true);
  });

  test('al editar un borrador precarga sus líneas y guarda cambios', async () => {
    const usuario = userEvent.setup();
    const fetchMock = fetchSimulado({
      'GET /api/despachos-huevo/h1': respuesta(200, despachoBorrador),
      'GET /api/despachos-huevo/precios-vigentes': respuesta(200, precioVigente),
      'PUT /api/despachos-huevo/h1': respuesta(204),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina({ ruta: '/despachos/h1/editar' });
    const amarras = await screen.findByLabelText('Amarras');
    expect((amarras as HTMLInputElement).value).toBe('2');
    expect((screen.getByLabelText('Unidades sueltas') as HTMLInputElement).value).toBe('30');
    await usuario.clear(amarras);
    await usuario.type(amarras, '3');
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    const edicion = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'PUT' && req.url.endsWith('/api/despachos-huevo/h1');
    });
    expect(edicion).toBeTruthy();
    const cuerpo = JSON.parse(await (edicion![0] as Request).clone().text());
    expect(cuerpo.lineas).toEqual([{ tamano: 'Extra', cantidadAmarras: 3, unidadesSueltas: 30 }]);
    expect(await screen.findByText('Detalle del despacho')).toBeInTheDocument();
  });

  test('muestra el precio vigente por tamaño en el combo', async () => {
    const usuario = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/despachos-huevo/precios-vigentes': respuesta(200, precioVigente),
      }),
    );
    renderPagina();
    expect(
      await screen.findByText(/vigente desde el 01\/09\/2026 \(notificada el 01\/09\/2026\)/),
    ).toBeInTheDocument();
    await usuario.click(screen.getByLabelText('Tamaño de huevo'));
    expect(await screen.findByRole('option', { name: 'Primera' })).toBeInTheDocument();
  });
});
