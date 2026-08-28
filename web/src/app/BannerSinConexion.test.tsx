import { act, render, screen } from '@testing-library/react';
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
});
