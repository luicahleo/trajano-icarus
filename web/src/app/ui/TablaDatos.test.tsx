import { render, screen } from '@testing-library/react';
import { TablaDatos } from './TablaDatos';
import type { Columna } from './TablaDatos';

interface Fila {
  id: string;
  nombre: string;
  valor: number;
}

const columnas: Columna<Fila>[] = [
  { clave: 'nombre', encabezado: 'Nombre', render: (f) => f.nombre },
  { clave: 'valor', encabezado: 'Valor', alinear: 'right', render: (f) => f.valor },
];

describe('TablaDatos', () => {
  test('muestra los encabezados y las filas', () => {
    render(
      <TablaDatos
        columnas={columnas}
        filas={[
          { id: '1', nombre: 'Ana', valor: 5 },
          { id: '2', nombre: 'Beto', valor: 8 },
        ]}
        claveDeFila={(f) => f.id}
      />,
    );
    expect(screen.getByText('Nombre')).toBeInTheDocument();
    expect(screen.getByText('Valor')).toBeInTheDocument();
    expect(screen.getByText('Ana')).toBeInTheDocument();
    expect(screen.getByText('Beto')).toBeInTheDocument();
  });

  test('muestra el mensaje de vacío cuando no hay filas', () => {
    render(
      <TablaDatos
        columnas={columnas}
        filas={[]}
        claveDeFila={(f) => f.id}
        mensajeVacio="No hay registros."
      />,
    );
    expect(screen.getByText('No hay registros.')).toBeInTheDocument();
  });

  test('usa el mensaje de vacío por defecto cuando no se pasa', () => {
    render(<TablaDatos columnas={columnas} filas={[]} claveDeFila={(f) => f.id} />);
    expect(screen.getByText('No hay datos para mostrar.')).toBeInTheDocument();
  });

  test('renderiza las celdas con el render de cada columna', () => {
    render(
      <TablaDatos
        columnas={[
          { clave: 'saludo', encabezado: 'Saludo', render: (f: Fila) => `Hola ${f.nombre}` },
        ]}
        filas={[{ id: '1', nombre: 'Ana', valor: 5 }]}
        claveDeFila={(f) => f.id}
      />,
    );
    expect(screen.getByText('Hola Ana')).toBeInTheDocument();
  });
});
