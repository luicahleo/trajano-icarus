import { render, screen } from '@testing-library/react';
import { BotonDiagnostico } from './BotonDiagnostico';

describe('BotonDiagnostico', () => {
  beforeEach(() => {
    sessionStorage.clear();
    window.history.replaceState(null, '', '/');
  });

  test('solo es visible en modo diagnóstico', () => {
    const { rerender } = render(<BotonDiagnostico />);
    expect(screen.queryByRole('button', { name: 'Descargar diagnóstico' })).not.toBeInTheDocument();

    window.history.replaceState(null, '', '/?debug=1');
    rerender(<BotonDiagnostico />);
    expect(screen.getByRole('button', { name: 'Descargar diagnóstico' })).toBeInTheDocument();
  });

  test('permanece oculto si la build no permite diagnóstico manual', () => {
    window.history.replaceState(null, '', '/?debug=1');

    render(<BotonDiagnostico permitido={false} />);

    expect(
      screen.queryByRole('button', { name: 'Descargar diagnóstico' }),
    ).not.toBeInTheDocument();
  });
});
