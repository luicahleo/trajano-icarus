import {
  crearErrorId,
  instalarCapturaGlobal,
  reportarDiagnostico,
  sanitizarAsset,
} from './diagnosticos';
import { registrarEventoFlujo } from './sesionDiagnostico';
import { setAccessToken } from './session';

describe('diagnosticos', () => {
  beforeEach(() => {
    sessionStorage.clear();
    setAccessToken(null);
    vi.restoreAllMocks();
  });

  test('genera referencias de error opacas', () => {
    expect(crearErrorId()).toMatch(/^ERR-[0-9A-F]{12}$/);
  });

  test('reporta metadata segura y solo los últimos 30 eventos', async () => {
    for (let i = 0; i < 31; i += 1) {
      registrarEventoFlujo({ eventName: 'flow.navigation', detail: `/ruta/${i}` });
    }
    setAccessToken('token-en-memoria');
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 202 }));
    vi.stubGlobal('fetch', fetchMock);

    await reportarDiagnostico({
      errorId: 'ERR-0123456789AB',
      eventName: 'window.unexpected',
      category: 'unexpected',
      source: 'window',
      asset: 'index-A1b2.js',
      lineNumber: 12,
      columnNumber: 4,
    });

    const [ruta, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(ruta).toBe('/api/diagnosticos/frontend');
    expect(init.keepalive).toBe(true);
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer token-en-memoria');
    const cuerpo = JSON.parse(String(init.body)) as Record<string, unknown>;
    expect(cuerpo.flowEvents).toHaveLength(30);
    expect(cuerpo).not.toHaveProperty('message');
    expect(cuerpo).not.toHaveProperty('stack');
    expect(JSON.stringify(cuerpo)).not.toContain('token-en-memoria');
  });

  test('solo acepta el nombre de asset del mismo origen', () => {
    expect(sanitizarAsset(`${window.location.origin}/assets/index-Ab12.js?secreto=1`)).toBe(
      'index-Ab12.js',
    );
    expect(sanitizarAsset('https://externo.test/assets/index.js')).toBeUndefined();
  });

  test('captura errores globales y promesas sin enviar su contenido', async () => {
    const reporter = vi.fn().mockResolvedValue(undefined);
    const desinstalar = instalarCapturaGlobal(reporter);

    window.dispatchEvent(new Event('error'));
    window.dispatchEvent(new Event('unhandledrejection'));

    expect(reporter).toHaveBeenCalledTimes(2);
    expect(reporter.mock.calls[0][0]).toMatchObject({
      eventName: 'window.unexpected',
      source: 'window',
    });
    expect(reporter.mock.calls[1][0]).toMatchObject({
      eventName: 'promise.unhandled',
      source: 'promise',
    });
    expect(reporter.mock.calls.flat()).not.toContain('contenido sensible');
    desinstalar();
  });
});
