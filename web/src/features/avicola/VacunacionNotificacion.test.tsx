import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../auth/AuthContext';
import type { Galpon, Rol } from '../../lib/tipos';
import { hoyIso } from './constantes';
import { VacunacionNotificacion } from './VacunacionNotificacion';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status, headers: { 'content-type': 'application/json' } });
}

const galpones: Galpon[] = [
  { id: 'ga1', numero: '1', capacidadMaxima: 5000, gallinasActuales: 4800, fechaNacimientoLote: '2026-08-01', descripcion: null },
];

function fetchConSesion(funcionalidades: string[], reglas: Record<string, Response>, rol: Rol = 'Trabajador') {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = init !== undefined ? new Request(String(input), init) : input instanceof Request ? input : new Request(String(input));
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    const fijas: Record<string, Response> = {
      'POST /api/identidad/sesion/renovar': respuesta(200, { accessToken: 't', expiraEnSegundos: 900 }),
      'GET /api/identidad/me': respuesta(200, { usuarioId: 'u1', rol, clienteId: 'cli1', trabajadorId: 'tr1', modulos: [], funcionalidades }),
    };
    return fijas[clave] ?? reglas[clave] ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function renderNotificacion() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><AuthProvider><VacunacionNotificacion galpones={galpones} /></AuthProvider></QueryClientProvider>);
}

describe('VacunacionNotificacion', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('con la funcionalidad muestra vencidas/hoy y próximas con el número de galpón', async () => {
    const ayer = new Date(Date.now() - 86400000);
    const isoAyer = `${ayer.getFullYear()}-${String(ayer.getMonth() + 1).padStart(2, '0')}-${String(ayer.getDate()).padStart(2, '0')}`;
    fetchConSesion(['Vacunacion'], {
      'GET /api/vacunacion/tareas': respuesta(200, {
        vencidasYHoy: [{ id: 't1', galponId: 'ga1', edadDia: 3, vacuna: 'BIO COCCIVET R', modoAplicacion: null, fechaProgramada: isoAyer, estado: 'Pendiente', fechaAplicacion: null, avesVacunadas: null, observacionesProgramadas: null, observacionesAplicacion: null, motivoCancelacion: null, programaNombre: 'PLAN CAISY 1000' }],
        proximas: [{ id: 't2', galponId: 'ga1', edadDia: 10, vacuna: 'HIPRAVIAR B1/H120', modoAplicacion: null, fechaProgramada: hoyIso(), estado: 'Pendiente', fechaAplicacion: null, avesVacunadas: null, observacionesProgramadas: null, observacionesAplicacion: null, motivoCancelacion: null, programaNombre: 'PLAN CAISY 1000' }],
      }),
    });
    renderNotificacion();

    expect(await screen.findByText(/BIO COCCIVET R/)).toBeInTheDocument();
    expect(screen.getByText(/HIPRAVIAR B1\/H120/)).toBeInTheDocument();
    expect(screen.getAllByText(/Galpón 1/).length).toBe(2);
  });

  test('sin la funcionalidad no consulta ni muestra nada', async () => {
    const fetchMock = fetchConSesion([], {});
    renderNotificacion();

    // Espera a que la sesión se resuelva; aun así no debe pedirse la notificación.
    await new Promise((resolve) => setTimeout(resolve, 50));
    const pidioTareas = fetchMock.mock.calls.some(([arg]) => {
      const req = arg instanceof Request ? arg : new Request(String(arg));
      return new URL(req.url).pathname === '/api/vacunacion/tareas';
    });
    expect(pidioTareas).toBe(false);
    expect(screen.queryByText(/Vacunación/)).not.toBeInTheDocument();
  });
});
