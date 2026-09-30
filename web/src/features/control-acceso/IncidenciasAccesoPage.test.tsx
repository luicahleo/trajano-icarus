import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { IncidenciasAccesoPage } from './IncidenciasAccesoPage';

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

const trabajador = {
  id: 't1',
  nombre: 'Ana Quispe',
  documentoIdentidad: 'DNI-00000001',
  cargo: 'Criadora',
  fechaIngreso: '2025-01-01',
  fechaCese: null,
  funcionalidades: [],
};

const incidenciaPendiente = {
  id: 'i1',
  flujoMarcacionId: 'f1',
  accion: 'Entrada',
  primerRechazoUtc: '2026-09-27T12:00:00+00:00',
  tercerRechazoUtc: '2026-09-27T12:00:05+00:00',
  estado: 'Pendiente',
  version: 1,
};

function baseFetch(reglas: Record<string, Response | Response[]>) {
  return fetchSimulado({
    'POST /api/identidad/sesion/renovar': respuesta(200, {
      accessToken: 't',
      expiraEnSegundos: 900,
    }),
    'GET /api/identidad/me': respuesta(200, {
      usuarioId: 'u1',
      rol: 'Cliente',
      clienteId: 'cli1',
      trabajadorId: null,
      modulos: ['ControlAcceso'],
      funcionalidades: [],
    }),
    'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
    ...reglas,
  });
}

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AuthProvider>
          <IncidenciasAccesoPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('IncidenciasAccesoPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('lista incidencias con acción y estado', async () => {
    baseFetch({
      'GET /api/control-acceso/incidencias': respuesta(200, {
        items: [incidenciaPendiente],
        total: 1,
        numeroPagina: 1,
        tamanoPagina: 25,
      }),
    });
    renderPagina();

    expect(await screen.findByText('Entrada')).toBeInTheDocument();
    expect(screen.getByText('Pendiente')).toBeInTheDocument();
  });

  test('resolver exige trabajador, hora declarada y motivo', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/control-acceso/incidencias': respuesta(200, {
        items: [incidenciaPendiente],
        total: 1,
        numeroPagina: 1,
        tamanoPagina: 25,
      }),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Resolver' }));
    await usuario.click(await screen.findByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/selecciona un trabajador/i)).toBeInTheDocument();
    const envios = fetchMock.mock.calls.filter(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.includes('/control-acceso/incidencias/i1/resolucion');
    });
    expect(envios).toHaveLength(0);
  });

  test('resolver envía los datos y refresca la lista', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/control-acceso/incidencias': [
        respuesta(200, {
          items: [incidenciaPendiente],
          total: 1,
          numeroPagina: 1,
          tamanoPagina: 25,
        }),
        respuesta(200, {
          items: [{ ...incidenciaPendiente, estado: 'Resuelta' }],
          total: 1,
          numeroPagina: 1,
          tamanoPagina: 25,
        }),
      ],
      'POST /api/control-acceso/incidencias/i1/resolucion': respuesta(204),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Resolver' }));
    await usuario.click(screen.getByLabelText('Trabajador'));
    await usuario.click(await screen.findByRole('option', { name: 'Ana Quispe' }));
    await usuario.type(screen.getByLabelText('Fecha y hora declarada'), '2026-09-27T08:00');
    await usuario.type(screen.getByLabelText('Motivo'), 'Reconocimiento manual');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => {
      const envio = fetchMock.mock.calls.find(([arg]) => {
        const req = arg as Request;
        return (
          req.method === 'POST' && req.url.includes('/control-acceso/incidencias/i1/resolucion')
        );
      });
      expect(envio).toBeTruthy();
    });
  });

  test('descartar exige motivo y llama al endpoint de descarte', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/control-acceso/incidencias': respuesta(200, {
        items: [incidenciaPendiente],
        total: 1,
        numeroPagina: 1,
        tamanoPagina: 25,
      }),
      'POST /api/control-acceso/incidencias/i1/descarte': respuesta(204),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Descartar' }));
    await usuario.type(await screen.findByLabelText('Motivo'), 'No corresponde');
    await usuario.click(screen.getByRole('button', { name: 'Descartar' }));

    await waitFor(() => {
      const envio = fetchMock.mock.calls.find(([arg]) => {
        const req = arg as Request;
        return req.method === 'POST' && req.url.includes('/control-acceso/incidencias/i1/descarte');
      });
      expect(envio).toBeTruthy();
    });
  });
});
