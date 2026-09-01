import { afterEach, describe, expect, test, vi } from 'vitest';
import { apiAccesible } from './sondaApi';

describe('sonda de conectividad con el API', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  test('un 401 prueba que el backend responde aunque no haya sesión', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 401 })));
    expect(await apiAccesible()).toBe(true);
  });

  test('un código de gateway sin backend cuenta como inalcanzable', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 503 })));
    expect(await apiAccesible()).toBe(false);
  });

  test('un fallo de red cuenta como inalcanzable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('fetch failed');
      }),
    );
    expect(await apiAccesible()).toBe(false);
  });

  test('agota el tiempo de espera y devuelve inalcanzable', async () => {
    vi.useFakeTimers();
    vi.stubGlobal(
      'fetch',
      vi.fn(
        (_url: unknown, init?: RequestInit) =>
          new Promise<Response>((_resolve, reject) => {
            init?.signal?.addEventListener('abort', () =>
              reject(new DOMException('abortado', 'AbortError')),
            );
          }),
      ),
    );
    const promesa = apiAccesible();
    await vi.advanceTimersByTimeAsync(10_000);
    await expect(promesa).resolves.toBe(false);
  });
});
