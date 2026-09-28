import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { HistorialAccesoPage } from './HistorialAccesoPage';

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

const jornada = {
  id: 'j1',
  trabajadorId: 't1',
  fechaBoliviana: '2026-09-27',
  estado: 'Completa',
  cantidadMarcaciones: 1,
  version: 1,
};

const detalle = {
  id: 'j1',
  trabajadorId: 't1',
  fechaBoliviana: '2026-09-27',
  estado: 'Completa',
  version: 1,
  marcaciones: [
    {
      id: 'm1',
      tipo: 'Entrada',
      origen: 'ManualCliente',
      instanteUtc: '2026-09-27T12:00:00+00:00',
      horaDeclaradaUtc: '2026-09-27T12:00:00+00:00',
      creadaEnUtc: '2026-09-27T12:05:00+00:00',
      autorId: 'u1',
      motivo: 'Falla de reconocimiento',
    },
  ],
  revisiones: [],
  valoresEfectivos: [{ tipo: 'Entrada', instanteUtc: '2026-09-27T12:00:00+00:00' }],
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
    ...reglas,
  });
}

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AuthProvider>
          <HistorialAccesoPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('HistorialAccesoPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('lista las jornadas del tenant con el nombre del trabajador', async () => {
    baseFetch({
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'GET /api/control-acceso/jornadas': respuesta(200, {
        items: [jornada],
        total: 1,
        numeroPagina: 1,
        tamanoPagina: 20,
      }),
    });
    renderPagina();

    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();
    expect(screen.getByText('Completa')).toBeInTheDocument();
  });

  test('la corrección exige motivo y no envía sin él', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'GET /api/control-acceso/jornadas': respuesta(200, {
        items: [jornada],
        total: 1,
        numeroPagina: 1,
        tamanoPagina: 20,
      }),
      'GET /api/control-acceso/jornadas/j1': respuesta(200, detalle),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Corregir' }));
    await usuario.click(await screen.findByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/motivo de la corrección es obligatorio/i)).toBeInTheDocument();
    const envios = fetchMock.mock.calls.filter(([arg]) => {
      const req = arg as Request;
      return req.method === 'POST' && req.url.endsWith('/control-acceso/jornadas/j1/correcciones');
    });
    expect(envios).toHaveLength(0);
  });

  test('un 409 avisa del conflicto y recarga la jornada sin sobrescribir', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'GET /api/control-acceso/jornadas': respuesta(200, {
        items: [jornada],
        total: 1,
        numeroPagina: 1,
        tamanoPagina: 20,
      }),
      'GET /api/control-acceso/jornadas/j1': [respuesta(200, detalle), respuesta(200, detalle)],
      'POST /api/control-acceso/jornadas/j1/correcciones': respuesta(409, {
        title: 'Conflicto con el estado actual',
      }),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Corregir' }));
    await usuario.type(await screen.findByLabelText('Motivo'), 'Ajuste autorizado');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/La jornada cambió mientras editabas/i)).toBeInTheDocument();
    await waitFor(() => {
      const lecturas = fetchMock.mock.calls.filter(([arg]) => {
        const req = arg as Request;
        return req.method === 'GET' && req.url.endsWith('/control-acceso/jornadas/j1');
      });
      expect(lecturas.length).toBeGreaterThanOrEqual(2);
    });
  });

  test('el registro manual exige motivo', async () => {
    const usuario = userEvent.setup();
    baseFetch({
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'GET /api/control-acceso/jornadas': respuesta(200, {
        items: [],
        total: 0,
        numeroPagina: 1,
        tamanoPagina: 20,
      }),
    });
    renderPagina();

    await usuario.click(
      await screen.findByRole('button', { name: 'Registrar marcación manual' }),
    );
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/Selecciona un trabajador/i)).toBeInTheDocument();
  });
});
