import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeProvider } from '@mui/material';
import { theme } from './theme';
import { SelectorTema } from './SelectorTema';

function renderSelector() {
  return render(
    <ThemeProvider theme={theme} defaultMode="light">
      <SelectorTema />
    </ThemeProvider>,
  );
}

describe('SelectorTema', () => {
  test('ofrece pasar al modo oscuro cuando el tema es claro', async () => {
    renderSelector();
    expect(screen.getByRole('button', { name: 'Cambiar a modo oscuro' })).toBeInTheDocument();
  });

  test('alterna el modo al pulsarlo', async () => {
    const usuario = userEvent.setup();
    renderSelector();
    await usuario.click(screen.getByRole('button', { name: 'Cambiar a modo oscuro' }));
    expect(await screen.findByRole('button', { name: 'Cambiar a modo claro' })).toBeInTheDocument();
  });
});
