import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ControlesPaginacion } from './ControlesPaginacion';

describe('ControlesPaginacion', () => {
  test('deshabilita «anterior» en la primera página y «siguiente» en la última', () => {
    const { rerender } = render(
      <ControlesPaginacion numeroPagina={1} total={40} tamanoPagina={20} onCambiarPagina={() => {}} />,
    );

    expect(screen.getByText('Página 1 de 2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Página anterior' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Página siguiente' })).toBeEnabled();

    rerender(
      <ControlesPaginacion numeroPagina={2} total={40} tamanoPagina={20} onCambiarPagina={() => {}} />,
    );

    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Página anterior' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Página siguiente' })).toBeDisabled();
  });

  test('navega a la página anterior y a la siguiente', async () => {
    const usuario = userEvent.setup();
    const alCambiar = vi.fn();
    render(
      <ControlesPaginacion numeroPagina={3} total={100} tamanoPagina={20} onCambiarPagina={alCambiar} />,
    );

    await usuario.click(screen.getByRole('button', { name: 'Página anterior' }));
    await usuario.click(screen.getByRole('button', { name: 'Página siguiente' }));

    expect(alCambiar).toHaveBeenNthCalledWith(1, 2);
    expect(alCambiar).toHaveBeenNthCalledWith(2, 4);
  });

  test('con una sola página informa el total y deshabilita ambos controles', () => {
    render(
      <ControlesPaginacion numeroPagina={1} total={3} tamanoPagina={20} onCambiarPagina={() => {}} />,
    );

    expect(screen.getByText('Página 1 de 1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Página anterior' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Página siguiente' })).toBeDisabled();
  });
});
