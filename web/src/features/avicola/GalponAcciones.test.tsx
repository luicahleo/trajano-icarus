import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../auth/AuthContext';
import type { Rol } from '../../lib/tipos';
import { GalponAcciones } from './GalponAcciones';

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
    // Si input ya es un Request, usarlo tal cual: fetch(input, init) lo
    // consume sin alterar method/body. Reconstruirlo desde String(input)
    // perdería el POST; re-armarlo con body exigiría duplex.
    const req = input instanceof Request ? input : new Request(String(input), init);
    return (
      colas.get(`${req.method} ${new URL(req.url).pathname}`)?.shift() ??
      new Response('', { status: 404 })
    );
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function baseFetchAvicolaConFuncionalidades(
  funcionalidades: string[],
  reglas: Record<string, Response | Response[]>,
  rol: Rol = 'Cliente',
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

function renderConGalpon() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <GalponAcciones
          galpon={{
            id: 'ga1',
            numero: '1',
            capacidadMaxima: 5000,
            gallinasActuales: 4800,
            fechaNacimientoLote: '2026-01-15',
            descripcion: null,
          }}
        />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

function llamadaCon(fetchMock: ReturnType<typeof vi.fn>, metodo: string, sufijo: string) {
  return fetchMock.mock.calls.some(([arg]) => {
    const req = arg as Request;
    return req.method === metodo && req.url.endsWith(sufijo);
  });
}

describe('GalponAcciones', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('editar manda numero, descripcion y capacidad', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicolaConFuncionalidades(['Granjas', 'Galpones'], {
      'PUT /api/galpones/ga1': respuesta(204),
    });
    renderConGalpon();
    await usuario.click(await screen.findByRole('button', { name: 'Editar' }));
    await usuario.clear(screen.getByLabelText('Número'));
    await usuario.type(screen.getByLabelText('Número'), '2');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'PUT');
    expect(JSON.parse(await (llamada![0] as Request).clone().text())).toEqual({
      numero: '2',
      descripcion: null,
      capacidadMaxima: 5000,
    });
  });

  test('ajustar inventario manda el total absoluto', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicolaConFuncionalidades(['Granjas', 'Galpones'], {
      'PUT /api/galpones/ga1/inventario': respuesta(204),
    });
    renderConGalpon();
    await usuario.click(await screen.findByRole('button', { name: 'Inventario' }));
    await usuario.clear(screen.getByLabelText('Gallinas actuales'));
    await usuario.type(screen.getByLabelText('Gallinas actuales'), '4750');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(llamadaCon(fetchMock, 'PUT', '/galpones/ga1/inventario')).toBe(true);
  });

  test('desactivar pide confirmación y llama al DELETE', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicolaConFuncionalidades(['Granjas', 'Galpones'], {
      'DELETE /api/galpones/ga1': respuesta(204),
    });
    renderConGalpon();
    await usuario.click(await screen.findByRole('button', { name: 'Desactivar' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(llamadaCon(fetchMock, 'DELETE', '/galpones/ga1')).toBe(true);
  });

  test('sin funcionalidad Galpones no muestra acciones', async () => {
    baseFetchAvicolaConFuncionalidades(['ProduccionHuevos'], {});
    renderConGalpon();
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(screen.queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument();
  });
});
