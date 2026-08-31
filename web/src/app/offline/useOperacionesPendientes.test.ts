import { renderHook, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import { descartarOperacion, encolarOperacion, iniciarCoordinadorOffline } from './coordinador';
import { useOperacionesPendientes } from './useOperacionesPendientes';

describe('useOperacionesPendientes', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => {
    limpiar?.();
    vi.restoreAllMocks();
  });

  test('lista solo las operaciones del galpón y reacciona a encolar y descartar', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
    const { result } = renderHook(() => useOperacionesPendientes('g1'));
    await waitFor(() => expect(result.current).toEqual([]));

    await encolarOperacion('produccion.crear', 'g1', {});
    await encolarOperacion('mortalidad.crear', 'g2', {});
    await waitFor(() => expect(result.current).toHaveLength(1));
    expect(result.current[0].tipo).toBe('produccion.crear');

    await descartarOperacion(result.current[0].id);
    await waitFor(() => expect(result.current).toEqual([]));
  });
});
