import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { TareaVacunacionResumen } from '../../lib/tipos';
import { hoyIso } from './constantes';
import { CompletarTareaDialog } from './CompletarTareaDialog';

const tarea: TareaVacunacionResumen = {
  id: 't1', galponId: 'ga1', edadDia: 3, vacuna: 'BIO COCCIVET R', modoAplicacion: 'Vía oral',
  fechaProgramada: hoyIso(), estado: 'Pendiente', fechaAplicacion: null, avesVacunadas: null,
  observacionesProgramadas: null, observacionesAplicacion: null, motivoCancelacion: null,
};

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), { status, headers: { 'content-type': 'application/json' } });
}

function renderDialog() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><CompletarTareaDialog tarea={tarea} abierto alCerrar={vi.fn()} /></QueryClientProvider>);
}

describe('CompletarTareaDialog', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('la fecha de aplicación viene prellenada con hoy y se envía con las aves', async () => {
    const usuario = userEvent.setup();
    const fetchMock = vi.fn(async () => respuesta(204));
    vi.stubGlobal('fetch', fetchMock);
    renderDialog();

    expect(screen.getByLabelText('Fecha de aplicación')).toHaveValue(hoyIso());
    await usuario.type(screen.getByLabelText('Aves vacunadas'), '4800');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamadas = fetchMock.mock.calls as unknown as Array<[Request, RequestInit?]>;
    const req = llamadas.find(([arg]) => arg.method === 'POST')?.[0];
    expect(req).toBeDefined();
    expect(new URL(req!.url).pathname).toBe('/api/vacunacion/tareas/t1/completar');
    expect(JSON.parse(await req!.clone().text())).toEqual({ fechaAplicacion: hoyIso(), avesVacunadas: 4800, observaciones: null });
  });

  test('rechaza una fecha futura sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = vi.fn(async () => respuesta(204));
    vi.stubGlobal('fetch', fetchMock);
    renderDialog();

    const futura = new Date(Date.now() + 86400000 * 2);
    const iso = `${futura.getFullYear()}-${String(futura.getMonth() + 1).padStart(2, '0')}-${String(futura.getDate()).padStart(2, '0')}`;
    await usuario.clear(screen.getByLabelText('Fecha de aplicación'));
    await usuario.type(screen.getByLabelText('Fecha de aplicación'), iso);
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/no puede ser futura/i)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
