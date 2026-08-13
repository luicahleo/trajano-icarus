import { obtenerCorrelationId } from './correlation';
import { ApiError, peticion } from './http';
import { getAccessToken, setAccessToken } from './session';

function respuesta(status: number, cuerpo?: unknown, headers: Record<string, string> = {}) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json', ...headers },
  });
}

describe('peticion', () => {
  beforeEach(() => {
    setAccessToken(null);
    vi.restoreAllMocks();
  });

  test('inyecta correlation ID y Bearer en todas las peticiones', async () => {
    const fetchMock = vi.fn().mockResolvedValue(respuesta(200, { ok: true }));
    setAccessToken('tok');
    vi.stubGlobal('fetch', fetchMock);

    await peticion<{ ok: boolean }>({ ruta: '/clientes' });

    const [request] = fetchMock.mock.calls[0];
    expect(new Headers(request.headers).get('X-Correlation-ID')).toBe(obtenerCorrelationId());
    expect(new Headers(request.headers).get('Authorization')).toBe('Bearer tok');
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  test('401 en ruta de negocio renueva una vez y reintenta', async () => {
    setAccessToken('viejo');
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(respuesta(401, { title: 'No autorizado' }))
        .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' }))
        .mockResolvedValueOnce(respuesta(200, { id: 'c1' })),
    );

    const datos = await peticion<{ id: string }>({ ruta: '/clientes', metodo: 'POST', cuerpo: {} });

    expect(datos.id).toBe('c1');
    expect(getAccessToken()).toBe('nuevo');
  });

  test('401 sin renovación posible limpia el token y lanza ApiError', async () => {
    setAccessToken('viejo');
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(respuesta(401, { title: 'No autorizado' }))
        .mockResolvedValueOnce(respuesta(401)),
    );

    await expect(peticion({ ruta: '/clientes' })).rejects.toMatchObject({ status: 401 });
    expect(getAccessToken()).toBeNull();
  });

  test('las rutas de sesión nunca reintentan por 401', async () => {
    setAccessToken('viejo');
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(401, { title: 'No autorizado' })));

    await expect(peticion({ ruta: '/identidad/sesion', metodo: 'POST', cuerpo: {} })).rejects.toMatchObject({
      status: 401,
    });
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  test('204 devuelve undefined', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(204)));
    await expect(peticion({ ruta: '/clientes/1/suspender', metodo: 'POST' })).resolves.toBeUndefined();
  });

  test('el error expone title del ProblemDetails y correlation ID del header', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(respuesta(409, { title: 'Conflicto con el estado actual' }, { 'X-Correlation-ID': 'abc-123' })),
    );

    const error = (await peticion({ ruta: '/clientes', metodo: 'POST', cuerpo: {} }).catch((e) => e)) as ApiError;
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(409);
    expect(error.code).toBe('Conflicto con el estado actual');
    expect(error.correlationId).toBe('abc-123');
  });
});
