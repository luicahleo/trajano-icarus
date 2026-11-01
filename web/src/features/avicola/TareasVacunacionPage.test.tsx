import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { isoDesplazado, tareaVacunacion } from '../../test/vacunacion';
import { TareasVacunacionPage } from './TareasVacunacionPage';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

const galpon = (id: string, numero: string) => ({
  id,
  numero,
  capacidadMaxima: 1000,
  gallinasActuales: 900,
  fechaNacimientoLote: '2026-01-01',
  fechaDescarteLote: null,
  descripcion: null,
});

function prepararFetch(vencidasYHoy: unknown[] | 'error') {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = input instanceof Request ? input : new Request(String(input), init);
    const clave = `${req.method} ${new URL(req.url).pathname}`;
    switch (clave) {
      case 'POST /api/identidad/sesion/renovar':
        return respuesta(200, { accessToken: 't', expiraEnSegundos: 900 });
      case 'GET /api/identidad/me':
        return respuesta(200, {
          usuarioId: 'u1',
          rol: 'Trabajador',
          clienteId: 'cli1',
          trabajadorId: 'tr1',
          modulos: ['GestionAvicola'],
          funcionalidades: ['Vacunacion'],
        });
      case 'GET /api/granjas':
        return respuesta(200, [{ id: 'gr1', nombre: 'Granja' }]);
      case 'GET /api/granjas/gr1/galpones':
        return respuesta(200, [galpon('g1', '1'), galpon('g2', '2')]);
      case 'GET /api/vacunacion/tareas':
        return vencidasYHoy === 'error'
          ? respuesta(500, { title: 'Error' })
          : respuesta(200, { vencidasYHoy, proximas: [] });
      default:
        return new Response('', { status: 404 });
    }
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function renderPagina() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AuthProvider>
          <TareasVacunacionPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('TareasVacunacionPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('agrupa por galpón, pone primero las vencidas y omite las futuras', async () => {
    prepararFetch([
      tareaVacunacion('t1', 'g2', isoDesplazado(0), { vacuna: 'NEWCASTLE' }),
      tareaVacunacion('t2', 'g1', isoDesplazado(0), { vacuna: 'GUMBORO' }),
      tareaVacunacion('t3', 'g1', isoDesplazado(-3), { vacuna: 'BRONQUITIS', edadDia: 14 }),
      tareaVacunacion('t4', 'g2', isoDesplazado(1), { vacuna: 'MAÑANA' }),
    ]);
    renderPagina();

    const grupo1 = await screen.findByRole('list', { name: 'Galpón 1 · 2 pendientes' });
    const grupo2 = screen.getByRole('list', { name: 'Galpón 2 · 1 pendiente' });
    expect(
      grupo1.compareDocumentPosition(grupo2) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();

    const [primera, segunda] = within(grupo1).getAllByRole('listitem');
    expect(within(primera).getByText('BRONQUITIS')).toBeInTheDocument();
    expect(within(primera).getByText('Vencida')).toBeInTheDocument();
    expect(
      within(primera).getByText(`Día 14 · Programada ${isoDesplazado(-3)}`),
    ).toBeInTheDocument();
    expect(within(segunda).getByText('Hoy')).toBeInTheDocument();
    expect(within(primera).getByRole('button', { name: 'Completar' })).toBeInTheDocument();

    expect(screen.queryByText('MAÑANA')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancelar' })).not.toBeInTheDocument();
  });

  test('Completar abre el diálogo de la tarea', async () => {
    prepararFetch([tareaVacunacion('t1', 'g1', isoDesplazado(0), { vacuna: 'GUMBORO' })]);
    renderPagina();

    await userEvent.click(await screen.findByRole('button', { name: 'Completar' }));
    expect(
      await screen.findByRole('heading', { name: 'Marcar como aplicada — GUMBORO' }),
    ).toBeInTheDocument();
  });

  test('recorta el texto largo y lo despliega con Ver más', async () => {
    const largo = 'Aplicar en agua de bebida '.repeat(5).trim();
    prepararFetch([tareaVacunacion('t1', 'g1', isoDesplazado(0), { vacuna: largo })]);
    renderPagina();

    const boton = await screen.findByRole('button', { name: 'Ver más' });
    expect(boton).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(boton);
    expect(screen.getByRole('button', { name: 'Ver menos' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
  });

  test('un texto corto no ofrece Ver más', async () => {
    prepararFetch([tareaVacunacion('t1', 'g1', isoDesplazado(0), { vacuna: 'GUMBORO' })]);
    renderPagina();
    await screen.findByText('GUMBORO');
    expect(screen.queryByRole('button', { name: 'Ver más' })).not.toBeInTheDocument();
  });

  test('sin pendientes muestra el mensaje vacío', async () => {
    prepararFetch([tareaVacunacion('t1', 'g1', isoDesplazado(2))]);
    renderPagina();
    expect(
      await screen.findByText('No hay tareas de vacunación pendientes.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  test('un error de carga muestra un mensaje genérico con Reintentar', async () => {
    prepararFetch('error');
    renderPagina();
    expect(
      await screen.findByText('No se pudieron cargar las tareas de vacunación.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });
});
