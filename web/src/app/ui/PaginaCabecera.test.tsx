import { render, screen } from '@testing-library/react';
import { PaginaCabecera } from './PaginaCabecera';

describe('PaginaCabecera', () => {
  test('muestra el título como encabezado', () => {
    render(<PaginaCabecera titulo="Clientes" />);
    expect(screen.getByRole('heading', { name: 'Clientes' })).toBeInTheDocument();
  });

  test('muestra el subtítulo cuando se pasa', () => {
    render(<PaginaCabecera titulo="Clientes" subtitulo="Gestión de cuentas" />);
    expect(screen.getByText('Gestión de cuentas')).toBeInTheDocument();
  });

  test('muestra las acciones cuando se pasan', () => {
    render(<PaginaCabecera titulo="Clientes" acciones={<button>Nuevo</button>} />);
    expect(screen.getByRole('button', { name: 'Nuevo' })).toBeInTheDocument();
  });

  test('respeta la variante de tipografía indicada', () => {
    const { container } = render(<PaginaCabecera titulo="Eficiencia" variante="h6" />);
    expect(container.querySelector('h6')).toBeInTheDocument();
  });
});
