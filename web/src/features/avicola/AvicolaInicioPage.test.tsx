import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Rol } from '../../lib/tipos';
import { AuthProvider } from '../auth/AuthContext';
import { AvicolaInicioPage } from './AvicolaInicioPage';

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
    const req =
      init !== undefined
        ? new Request(String(input), init)
        : input instanceof Request
          ? input
          : new Request(String(input));
    const valor = colas.get(`${req.method} ${new URL(req.url).pathname}`)?.shift();
    return valor ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function baseFetch(
  rol: Rol,
  funcionalidades: string[],
  reglas: Record<string, Response | Response[]>,
) {
  return fetchSimulado({
    'POST /api/identidad/sesion/renovar': respuesta(200, {
      accessToken: 't',
      expiraEnSegundos: 900,
    }),
    'GET /api/identidad/me': respuesta(200, {
      usuarioId: 'u1',
      rol,
      clienteId: 'cli1',
      trabajadorId: null,
      modulos: ['GestionAvicola'],
      funcionalidades,
    }),
    ...reglas,
  });
}

function renderPagina(rutaInicial: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[rutaInicial]}>
        <AuthProvider>
          <Routes>
            <Route path="/avicola" element={<AvicolaInicioPage />} />
            <Route path="/avicola/galpones" element={<div>Lista de galpones</div>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function llamadaCon(fetchMock: ReturnType<typeof vi.fn>, metodo: string, sufijo: string) {
  return fetchMock.mock.calls.some(([arg]) => {
    const req = arg as Request;
    return req.method === metodo && req.url.endsWith(sufijo);
  });
}

describe('AvicolaInicioPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('con granja existente redirige a la lista de galpones', async () => {
    baseFetch('Cliente', ['Granjas', 'Galpones'], {
      'GET /api/granjas': respuesta(200, [{ id: 'gr1', nombre: 'Granja Norte' }]),
    });
    renderPagina('/avicola');
    expect(await screen.findByText('Lista de galpones')).toBeInTheDocument();
  });

  test('sin granja muestra el alta y crea la primera granja', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch('Cliente', ['Granjas', 'Galpones'], {
      'GET /api/granjas': [
        respuesta(200, []),
        respuesta(200, [{ id: 'gr1', nombre: 'Granja Nueva' }]),
      ],
      'POST /api/granjas': respuesta(201, { id: 'gr1' }),
    });
    renderPagina('/avicola');
    expect(await screen.findByText('Creá tu granja')).toBeInTheDocument();
    await usuario.type(screen.getByLabelText('Nombre de la granja'), 'Granja Nueva');
    await usuario.click(screen.getByRole('button', { name: 'Crear granja' }));
    expect(llamadaCon(fetchMock, 'POST', '/granjas')).toBe(true);
    expect(await screen.findByText('Lista de galpones')).toBeInTheDocument();
  });

  test('sin granja y sin funcionalidad Granjas muestra aviso sin formulario', async () => {
    baseFetch('Trabajador', ['ProduccionHuevos'], { 'GET /api/granjas': respuesta(200, []) });
    renderPagina('/avicola');
    expect(await screen.findByText(/no tiene una granja configurada/i)).toBeInTheDocument();
    expect(screen.queryByLabelText('Nombre de la granja')).not.toBeInTheDocument();
  });
});
