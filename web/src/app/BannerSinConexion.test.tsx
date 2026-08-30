import { act, render, screen } from '@testing-library/react';
import { crearAlmacenColaMemoria } from '../lib/offline/almacenCola';
import { encolarOperacion, iniciarCoordinadorOffline } from './offline/coordinador';
import { BannerSinConexion } from './BannerSinConexion';
describe('BannerSinConexion', () => {
  test('online no muestra', () => {
    render(<BannerSinConexion />);
    expect(screen.queryByText(/sin conexión/i)).not.toBeInTheDocument();
  });
  test('offline aparece y vuelve', () => {
    render(<BannerSinConexion />);
    act(() => window.dispatchEvent(new Event('offline')));
    expect(screen.getByText(/sin conexión/i)).toBeInTheDocument();
    act(() => window.dispatchEvent(new Event('online')));
    expect(screen.queryByText(/sin conexión/i)).not.toBeInTheDocument();
  });
  test('offline muestra el nuevo texto y el contador de pendientes', async () => {
    // despachar rechaza: la operación queda en la cola y el contador se mantiene en 1.
    const limpiar = iniciarCoordinadorOffline({
      despachar: async () => {
        throw new TypeError('sin red');
      },
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
    try {
      render(<BannerSinConexion />);
      act(() => window.dispatchEvent(new Event('offline')));
      expect(
        screen.getByText(/los registros se guardan en este dispositivo/i),
      ).toBeInTheDocument();
      await encolarOperacion('produccion.crear', 'g1', {});
      expect(await screen.findByText(/1 registro pendiente/i)).toBeInTheDocument();
    } finally {
      limpiar();
      act(() => window.dispatchEvent(new Event('online')));
    }
  });
});
