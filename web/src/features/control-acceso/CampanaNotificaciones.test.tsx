import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { CampanaNotificaciones } from './CampanaNotificaciones';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchSimulado(reglas: Record<string, Response | Response[]>) {
  const colas = new Map(
    Object.entries(reglas).map(([clave, valor]) => [
      clave,
      Array.isArray(valor) ? [...valor] : [valor],
    ]),
  );
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    const cola = colas.get(clave);
    const valor = cola?.shift();
    return valor ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function RutaActual() {
  const { pathname } = useLocation();
  return <span data-testid="ruta-actual">{pathname}</span>;
}

function renderCampana(reglas: Record<string, Response | Response[]>) {
  const fetchMock = fetchSimulado(reglas);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/inicio']}>
        <Routes>
          <Route path="*" element={<CampanaNotificaciones />} />
        </Routes>
        <RutaActual />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return fetchMock;
}

describe('CampanaNotificaciones', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('muestra el contador de notificaciones sin leer', async () => {
    renderCampana({
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [
          { id: 'n1', incidenciaId: 'i1', tipo: 'IncidenciaAcceso', fechaUtc: '2026-09-27T12:00:00Z', leida: false },
        ],
        contador: 1,
      }),
    });

    expect(await screen.findByText('1')).toBeInTheDocument();
  });

  test('abre el menú y navega a incidencias al seleccionar una notificación', async () => {
    const usuario = userEvent.setup();
    const fetchMock = renderCampana({
      'GET /api/control-acceso/notificaciones': respuesta(200, {
        items: [
          { id: 'n1', incidenciaId: 'i1', tipo: 'IncidenciaAcceso', fechaUtc: '2026-09-27T12:00:00Z', leida: false },
        ],
        contador: 1,
      }),
      'POST /api/control-acceso/notificaciones/n1/marcar-leida': respuesta(204),
    });

    await usuario.click(await screen.findByRole('button', { name: /notificaciones/i }));
    await usuario.click(await screen.findByRole('menuitem'));

    expect(await screen.findByTestId('ruta-actual')).toHaveTextContent('/control-acceso/incidencias');
    const marcarLeida = fetchMock.mock.calls.find(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.includes('/control-acceso/notificaciones/n1/marcar-leida');
    });
    expect(marcarLeida).toBeTruthy();
  });
});
