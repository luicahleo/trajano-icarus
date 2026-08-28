import { render, screen } from '@testing-library/react';
import { EstadoCarga } from './EstadoCarga';

describe('EstadoCarga', () => {
  test('muestra el indicador de carga cuando cargando', () => {
    render(
      <EstadoCarga cargando error={false}>
        contenido
      </EstadoCarga>,
    );
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    expect(screen.queryByText('contenido')).not.toBeInTheDocument();
  });

  test('muestra el error cuando falla', () => {
    render(
      <EstadoCarga cargando={false} error mensajeError="No se pudo cargar.">
        contenido
      </EstadoCarga>,
    );
    expect(screen.getByText('No se pudo cargar.')).toBeInTheDocument();
    expect(screen.queryByText('contenido')).not.toBeInTheDocument();
  });

  test('muestra el botón de reintento cuando se provee', () => {
    render(
      <EstadoCarga cargando={false} error mensajeError="No se pudo cargar." onReintentar={vi.fn()}>
        contenido
      </EstadoCarga>,
    );
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });

  test('muestra el contenido cuando no carga ni falla', () => {
    render(
      <EstadoCarga cargando={false} error={false}>
        contenido
      </EstadoCarga>,
    );
    expect(screen.getByText('contenido')).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });
});
