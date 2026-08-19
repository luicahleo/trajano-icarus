import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Rol } from '../../lib/tipos';
import { AuthProvider } from '../auth/AuthContext';
import { GalponesPage } from './GalponesPage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status, headers: { 'content-type': 'application/json' } });
}
function fetchSimulado(reglas: Record<string, Response | Response[]>) {
  const colas = new Map(Object.entries(reglas).map(([clave, valor]) => [clave, Array.isArray(valor) ? [...valor] : [valor]]));
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = init !== undefined ? new Request(String(input), init) : input instanceof Request ? input : new Request(String(input));
    return colas.get(`${req.method} ${new URL(req.url).pathname}`)?.shift() ?? new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}
function baseFetchAvicolaConFuncionalidades(funcionalidades: string[], reglas: Record<string, Response | Response[]>, rol: Rol = 'Cliente') {
  return fetchSimulado({
    'POST /api/identidad/sesion/renovar': respuesta(200, { accessToken: 't', expiraEnSegundos: 900 }),
    'GET /api/identidad/me': respuesta(200, { usuarioId: 'u1', rol, clienteId: 'cli1', trabajadorId: null, modulos: ['GestionAvicola'], funcionalidades }),
    ...reglas,
  });
}
function baseFetchAvicola(reglas: Record<string, Response | Response[]>) { return baseFetchAvicolaConFuncionalidades(['Granjas', 'Galpones', 'ProduccionHuevos'], reglas); }
function renderPagina(rutaInicial: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><MemoryRouter initialEntries={[rutaInicial]}><AuthProvider><Routes><Route path="/avicola/galpones" element={<GalponesPage />} /></Routes></AuthProvider></MemoryRouter></QueryClientProvider>);
}
function llamadaCon(fetchMock: ReturnType<typeof vi.fn>, metodo: string, sufijo: string) { return fetchMock.mock.calls.some(([arg]) => { const req = arg as Request; return req.method === metodo && req.url.endsWith(sufijo); }); }

const granja = { id: 'gr1', nombre: 'Granja Norte' };
const galpon = { id: 'ga1', numero: '1', capacidadMaxima: 5000, gallinasActuales: 4800, fechaNacimientoLote: '2026-01-15', descripcion: null };

describe('GalponesPage', () => {
  beforeEach(() => vi.restoreAllMocks());
  test('muestra granja, tarjeta y eficiencia', async () => {
    baseFetchAvicola({ 'GET /api/granjas': respuesta(200, [granja]), 'GET /api/granjas/gr1/galpones': respuesta(200, [galpon]), 'GET /api/galpones/ga1/eficiencia': respuesta(200, { galponId: 'ga1', desde: '2026-08-19', hasta: '2026-08-19', dias: [{ fecha: '2026-08-19', eficiencia: 62.5, bajoUmbral: true }] }) });
    renderPagina('/avicola/galpones');
    expect(await screen.findByText('Granja Norte')).toBeInTheDocument();
    expect(await screen.findByText('Galpón 1')).toBeInTheDocument();
    expect(screen.getByText(/4[.\u00a0]?800 \/ 5[.\u00a0]?000 gallinas/)).toBeInTheDocument();
    expect(await screen.findByText(/62.5 %|62,5 %/)).toBeInTheDocument();
    expect(await screen.findByText(/bajo umbral/i)).toBeInTheDocument();
  });
  test('estado vacío invita a crear', async () => { baseFetchAvicola({ 'GET /api/granjas': respuesta(200, [granja]), 'GET /api/granjas/gr1/galpones': respuesta(200, []) }); renderPagina('/avicola/galpones'); expect(await screen.findByText(/todavía no hay galpones/i)).toBeInTheDocument(); expect(screen.getByRole('button', { name: /crear el primero/i })).toBeInTheDocument(); });
  test('alta crea galpón', async () => {
    const usuario = userEvent.setup(); const fetchMock = baseFetchAvicola({ 'GET /api/granjas': respuesta(200, [granja]), 'GET /api/granjas/gr1/galpones': [respuesta(200, []), respuesta(200, [galpon])], 'POST /api/granjas/gr1/galpones': respuesta(201, { id: 'ga1' }) });
    renderPagina('/avicola/galpones'); await usuario.click(await screen.findByRole('button', { name: /nuevo galpón|crear el primero/i })); await usuario.type(screen.getByLabelText('Número'), '1'); await usuario.type(screen.getByLabelText('Capacidad máxima'), '5000'); await usuario.type(screen.getByLabelText('Gallinas actuales'), '4800'); fireEvent.change(screen.getByLabelText('Fecha de poblado del lote'), { target: { value: '2026-01-15' } }); await usuario.click(screen.getByRole('button', { name: 'Guardar' })); expect(llamadaCon(fetchMock, 'POST', '/granjas/gr1/galpones')).toBe(true); expect(await screen.findByText('Galpón 1')).toBeInTheDocument();
  });
  test('renombra la granja', async () => { const usuario = userEvent.setup(); const fetchMock = baseFetchAvicola({ 'GET /api/granjas': [respuesta(200, [granja]), respuesta(200, [{ ...granja, nombre: 'Granja Sur' }])], 'GET /api/granjas/gr1/galpones': respuesta(200, []), 'PUT /api/granjas/gr1': respuesta(204) }); renderPagina('/avicola/galpones'); await usuario.click(await screen.findByRole('button', { name: /renombrar/i })); await usuario.clear(screen.getByLabelText('Nombre de la granja')); await usuario.type(screen.getByLabelText('Nombre de la granja'), 'Granja Sur'); await usuario.click(screen.getByRole('button', { name: 'Guardar' })); expect(llamadaCon(fetchMock, 'PUT', '/granjas/gr1')).toBe(true); expect(await screen.findByText('Granja Sur')).toBeInTheDocument(); });
  test('sin funcionalidad Granjas oculta renombrar y alta', async () => { baseFetchAvicolaConFuncionalidades(['Galpones', 'ProduccionHuevos'], { 'GET /api/granjas': respuesta(200, [granja]), 'GET /api/granjas/gr1/galpones': respuesta(200, []) }, 'Trabajador'); renderPagina('/avicola/galpones'); expect(await screen.findByText(/todavía no hay galpones/i)).toBeInTheDocument(); expect(screen.queryByRole('button', { name: /renombrar/i })).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: /nuevo galpón/i })).not.toBeInTheDocument(); });
});
