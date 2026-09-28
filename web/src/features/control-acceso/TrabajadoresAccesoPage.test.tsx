import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { TrabajadoresAccesoPage } from './TrabajadoresAccesoPage';

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

function baseFetch(reglas: Record<string, Response | Response[]>) {
  return fetchSimulado({
    'POST /api/identidad/sesion/renovar': respuesta(200, {
      accessToken: 't',
      expiraEnSegundos: 900,
    }),
    'GET /api/identidad/me': respuesta(200, {
      usuarioId: 'u1',
      correo: 'cliente@icarus.test',
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
          <TrabajadoresAccesoPage />
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

describe('TrabajadoresAccesoPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('muestra el estado de acceso y habilita al trabajador', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'GET /api/control-acceso/trabajadores/acceso': respuesta(200, [
        { trabajadorId: 't1', habilitado: false, enrolamiento: 'SinEnrolar', versionEnrolamiento: 0 },
      ]),
      'PUT /api/control-acceso/trabajadores/t1/habilitacion': respuesta(204),
    });
    renderPagina();

    expect(await screen.findByText('Ana Quispe')).toBeInTheDocument();
    expect(screen.getByText('Deshabilitado')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Habilitar' }));
    expect(llamadaCon(fetchMock, 'PUT', '/control-acceso/trabajadores/t1/habilitacion')).toBe(true);
  });

  test('revocar pide confirmación y llama al endpoint', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetch({
      'GET /api/clientes/cli1/trabajadores': respuesta(200, [trabajador]),
      'GET /api/control-acceso/trabajadores/acceso': respuesta(200, [
        { trabajadorId: 't1', habilitado: true, enrolamiento: 'Vigente', versionEnrolamiento: 1 },
      ]),
      'POST /api/control-acceso/trabajadores/t1/revocacion': respuesta(204),
    });
    renderPagina();

    await usuario.click(await screen.findByRole('button', { name: 'Revocar' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(llamadaCon(fetchMock, 'POST', '/control-acceso/trabajadores/t1/revocacion')).toBe(true);
  });
});
