import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BarraFiltros, type DeclaracionFiltro } from './BarraFiltros';

const FILTROS: DeclaracionFiltro[] = [
  {
    clave: 'granjaId',
    etiqueta: 'Granja',
    tipo: 'seleccion',
    opciones: [
      { valor: 'g1', etiqueta: 'Granja Uno' },
      { valor: 'g2', etiqueta: 'Granja Dos' },
    ],
  },
  { clave: 'numero', etiqueta: 'Folio', tipo: 'texto' },
  { clave: 'desde', etiqueta: 'Desde', tipo: 'fecha' },
];

describe('BarraFiltros', () => {
  test('emite el cambio con los valores elegidos al aplicar', async () => {
    const usuario = userEvent.setup();
    const alAplicar = vi.fn();
    render(<BarraFiltros filtros={FILTROS} onAplicar={alAplicar} />);

    await usuario.click(screen.getByLabelText('Granja'));
    await usuario.click(await screen.findByRole('option', { name: 'Granja Uno' }));
    await usuario.type(screen.getByLabelText('Folio'), 'P-000002');
    await usuario.click(screen.getByRole('button', { name: 'Aplicar filtros' }));

    expect(alAplicar).toHaveBeenCalledWith({
      granjaId: 'g1',
      numero: 'P-000002',
      desde: '',
    });
  });

  test('limpia todos los campos al restablecer', async () => {
    const usuario = userEvent.setup();
    const alRestablecer = vi.fn();
    render(
      <BarraFiltros
        filtros={FILTROS}
        valores={{ granjaId: 'g2', numero: 'P-000009', desde: '2026-09-01' }}
        onAplicar={() => {}}
        onRestablecer={alRestablecer}
      />,
    );

    expect(screen.getByLabelText('Folio')).toHaveValue('P-000009');
    await usuario.click(screen.getByRole('button', { name: 'Restablecer' }));

    expect(alRestablecer).toHaveBeenCalled();
    expect(screen.getByLabelText('Folio')).toHaveValue('');
    expect(screen.getByLabelText('Desde')).toHaveValue('');
  });

  test('expone cada control con su etiqueta accesible', () => {
    render(<BarraFiltros filtros={FILTROS} onAplicar={() => {}} />);

    expect(screen.getByLabelText('Granja')).toBeInTheDocument();
    expect(screen.getByLabelText('Folio')).toBeInTheDocument();
    expect(screen.getByLabelText('Desde')).toBeInTheDocument();
  });
});
