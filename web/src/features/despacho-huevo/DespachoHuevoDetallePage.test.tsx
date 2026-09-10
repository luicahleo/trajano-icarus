import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DespachoHuevoDetallePage } from './DespachoHuevoDetallePage';

const despachoBorrador = {
  id: 'h1',
  estado: 'Borrador',
  fechaDespacho: null,
  totalAmarras: 5,
  totalHuevos: 930,
  totalBs: null,
  detalles: [
    {
      id: 'l1',
      tamano: 'Extra',
      cantidadAmarras: 5,
      unidadesSueltas: 30,
      cantidadHuevos: 930,
      precioUnitarioCongelado: null,
      subtotal: null,
    },
  ],
};

const despachoDespachado = {
  id: 'h1',
  estado: 'Despachado',
  fechaDespacho: '2026-09-05',
  totalAmarras: 5,
  totalHuevos: 930,
  totalBs: 790.5,
  detalles: [
    {
      id: 'l1',
      tamano: 'Extra',
      cantidadAmarras: 5,
      unidadesSueltas: 30,
      cantidadHuevos: 930,
      precioUnitarioCongelado: 0.85,
      subtotal: 790.5,
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

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/despachos/h1']}>
        <Routes>
          <Route path="/despachos/:id" element={<DespachoHuevoDetallePage />} />
          <Route path="/despachos/:id/editar" element={<div>Editar despacho</div>} />
          <Route path="/despachos" element={<div>Bandeja</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('DespachoHuevoDetallePage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('un borrador muestra sus cantidades y exige la foto para despachar', async () => {
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/despachos-huevo/h1': respuesta(200, despachoBorrador),
        'GET /api/despachos-huevo/precios-vigentes': respuesta(200, precioVigente),
      }),
    );
    renderPagina();
    expect((await screen.findAllByText('930')).length).toBeGreaterThan(0);
    // El precio solo se muestra tras despachar.
    expect(screen.getAllByText('—').length).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: 'Editar' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Borrar borrador' })).toBeInTheDocument();
    const boton = screen.getByRole('button', { name: 'Despachar a CAISY' });
    expect(boton).toBeDisabled();
  });

  test('despachar exige la foto, confirma con el total estimado y cierra el despacho', async () => {
    const usuario = userEvent.setup();
    let despachado = false;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const req = input instanceof Request ? input : new Request(String(input), init);
      const ruta = `${req.method} ${new URL(req.url).pathname}`;
      if (ruta === 'POST /api/despachos-huevo/h1/despachar') return respuesta(204);
      if (ruta === 'GET /api/despachos-huevo/h1') {
        return respuesta(200, despachado ? despachoDespachado : despachoBorrador);
      }
      if (ruta === 'GET /api/despachos-huevo/precios-vigentes') return respuesta(200, precioVigente);
      return respuesta(404);
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    const boton = await screen.findByRole('button', { name: 'Despachar a CAISY' });
    expect(boton).toBeDisabled();
    const foto = new File(['imagen'], 'nota-entrega.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText('Elegir foto de la nota de entrega'), {
      target: { files: [foto] },
    });
    expect(await screen.findByText('Foto elegida: nota-entrega.jpg')).toBeInTheDocument();
    expect(boton).toBeEnabled();
    await usuario.click(boton);
    // La confirmación muestra el total estimado con el precio vigente.
    expect(screen.getAllByText(/no se puede revertir/i).length).toBeGreaterThan(0);
    expect((await screen.findAllByText(/790,50/)).length).toBeGreaterThan(0);
    // El GET vuelve con el estado despachado después de la confirmación.
    despachado = true;
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    // Tras despachar se ven amarras, huevos y Bs, y ya no se puede editar.
    expect((await screen.findAllByText('Bs 790,50')).length).toBeGreaterThan(0);
    expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Borrar borrador' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Despachar a CAISY' })).not.toBeInTheDocument();
    expect(screen.getByText('Bs 0,85')).toBeInTheDocument();
    const envio = fetchMock.mock.calls.some(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/api/despachos-huevo/h1/despachar');
    });
    expect(envio).toBe(true);
  });

  test('sin precio vigente no permite confirmar el despacho', async () => {
    const fetchMock = fetchSimulado({
      'GET /api/despachos-huevo/h1': respuesta(200, despachoBorrador),
      'GET /api/despachos-huevo/precios-vigentes': respuesta(404),
      'POST /api/despachos-huevo/h1/despachar': respuesta(204),
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    expect(
      await screen.findByText(/No hay publicación de precios de huevo vigente/i),
    ).toBeInTheDocument();
    const boton = screen.getByRole('button', { name: 'Despachar a CAISY' });
    expect(boton).toBeDisabled();
    const foto = new File(['imagen'], 'nota-entrega.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText('Elegir foto de la nota de entrega'), {
      target: { files: [foto] },
    });
    // Con foto pero sin precios, el despacho sigue bloqueado.
    expect(boton).toBeDisabled();
    const llamadas = fetchMock.mock.calls.some(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/api/despachos-huevo/h1/despachar');
    });
    expect(llamadas).toBe(false);
  });

  test('borrar borrador pide confirmación y vuelve a la bandeja', async () => {
    const usuario = userEvent.setup();
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/despachos-huevo/h1': respuesta(200, despachoBorrador),
        'GET /api/despachos-huevo/precios-vigentes': respuesta(200, precioVigente),
        'DELETE /api/despachos-huevo/h1': respuesta(204),
      }),
    );
    renderPagina();
    await usuario.click(await screen.findByRole('button', { name: 'Borrar borrador' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(await screen.findByText('Bandeja')).toBeInTheDocument();
  });

  test('un despachado muestra amarras, huevos, Bs y el precio congelado', async () => {
    vi.stubGlobal(
      'fetch',
      fetchSimulado({
        'GET /api/despachos-huevo/h1': respuesta(200, despachoDespachado),
        'GET /api/despachos-huevo/precios-vigentes': respuesta(200, precioVigente),
      }),
    );
    renderPagina();
    expect(await screen.findByText('Despachado')).toBeInTheDocument();
    expect(screen.getByText('05/09/2026')).toBeInTheDocument();
    expect(screen.getAllByText('Bs 790,50').length).toBeGreaterThan(0);
    expect(screen.getByText('Bs 0,85')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument();
  });
});
