import { ApiError, peticion } from './http';
import { getAccessToken, setAccessToken } from './session';
import { obtenerSesionId } from './sesionDiagnostico';

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
    const fetchMock = vi
      .fn()
      .mockImplementation(() => Promise.resolve(respuesta(200, { ok: true })));
    setAccessToken('tok');
    vi.stubGlobal('fetch', fetchMock);

    await peticion<{ ok: boolean }>({ ruta: '/clientes' });

    const [request] = fetchMock.mock.calls[0];
    expect(new Headers(request.headers).get('X-Correlation-ID')).toMatch(/^[0-9a-f-]{36}$/i);
    expect(new Headers(request.headers).get('X-Session-Id')).toBe(obtenerSesionId());
    expect(new Headers(request.headers).get('Authorization')).toBe('Bearer tok');
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  test('usa un correlation ID distinto y el mismo session ID en cada petición', async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(() => Promise.resolve(respuesta(200, { ok: true })));
    vi.stubGlobal('fetch', fetchMock);

    await peticion({ ruta: '/clientes' });
    await peticion({ ruta: '/clientes' });

    const primera = fetchMock.mock.calls[0][0] as Request;
    const segunda = fetchMock.mock.calls[1][0] as Request;
    expect(primera.headers.get('X-Correlation-ID')).not.toBe(
      segunda.headers.get('X-Correlation-ID'),
    );
    expect(primera.headers.get('X-Session-Id')).toBe(segunda.headers.get('X-Session-Id'));
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

    await expect(
      peticion({ ruta: '/identidad/sesion', metodo: 'POST', cuerpo: {} }),
    ).rejects.toMatchObject({
      status: 401,
    });
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  test('204 devuelve undefined', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(204)));
    await expect(
      peticion({ ruta: '/clientes/1/suspender', metodo: 'POST' }),
    ).resolves.toBeUndefined();
  });

  test('el error expone title del ProblemDetails y correlation ID del header', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          respuesta(
            409,
            { title: 'Conflicto con el estado actual' },
            { 'X-Correlation-ID': 'abc-123' },
          ),
        ),
    );

    const error = (await peticion({ ruta: '/clientes', metodo: 'POST', cuerpo: {} }).catch(
      (e) => e,
    )) as ApiError;
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(409);
    expect(error.code).toBe('Conflicto con el estado actual');
    expect(error.correlationId).toBe('abc-123');
  });

  test('el error 500 expone referencias técnicas seguras del backend', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValueOnce(
          respuesta(
            500,
            {
              title: 'Error interno',
              errorId: 'ERR-0123456789AB',
              correlationId: '20cc2ea2-2f71-45bb-a667-25f1700431bb',
              traceId: '0123456789abcdef0123456789abcdef',
            },
            {
              'X-Correlation-ID': '20cc2ea2-2f71-45bb-a667-25f1700431bb',
              'X-Trace-Id': '0123456789abcdef0123456789abcdef',
            },
          ),
        )
        .mockResolvedValue(new Response(null, { status: 202 })),
    );

    const error = (await peticion({ ruta: '/clientes' }).catch((e) => e)) as ApiError;

    expect(error).toMatchObject({
      status: 500,
      errorId: 'ERR-0123456789AB',
      correlationId: '20cc2ea2-2f71-45bb-a667-25f1700431bb',
      traceId: '0123456789abcdef0123456789abcdef',
    });
  });

  test('el fallo de red se reporta como incidente técnico', async () => {
    const fetchMock = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    vi.stubGlobal('fetch', fetchMock);

    await expect(peticion({ ruta: '/clientes' })).rejects.toThrow('Failed to fetch');

    const reporte = fetchMock.mock.calls.filter(([ruta]) => ruta === '/api/diagnosticos/frontend');
    expect(reporte).toHaveLength(1);
    const cuerpo = JSON.parse(String(reporte[0][1].body)) as Record<string, unknown>;
    expect(cuerpo).toMatchObject({
      eventName: 'http.network_failed',
      category: 'network',
      source: 'http',
    });
    expect(cuerpo).not.toHaveProperty('message');
    expect(cuerpo).not.toHaveProperty('stack');
  });

  test('los 4xx esperados no generan reporte técnico', async () => {
    const fetchMock = vi.fn().mockResolvedValue(respuesta(404, { title: 'No encontrado' }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(peticion({ ruta: '/clientes/abc' })).rejects.toMatchObject({ status: 404 });

    const reportes = fetchMock.mock.calls.filter(([ruta]) => ruta === '/api/diagnosticos/frontend');
    expect(reportes).toHaveLength(0);
  });

  test('el endpoint de diagnóstico nunca se reporta a sí mismo', async () => {
    const fetchMock = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    vi.stubGlobal('fetch', fetchMock);

    await expect(
      peticion({ ruta: '/diagnosticos/frontend', metodo: 'POST', cuerpo: {} }),
    ).rejects.toThrow();

    expect(fetchMock.mock.calls).toHaveLength(1);
  });
});
