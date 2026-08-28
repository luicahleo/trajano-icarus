import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CampoContrasena } from './CampoContrasena';

describe('CampoContrasena', () => {
  test('renderiza un campo de contraseña con su etiqueta', () => {
    render(<CampoContrasena label="Contraseña" />);
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('type', 'password');
  });

  test('el toggle muestra y oculta la contraseña', async () => {
    const usuario = userEvent.setup();
    render(<CampoContrasena label="Contraseña" />);
    const campo = screen.getByLabelText('Contraseña');
    await usuario.click(screen.getByRole('button', { name: 'Mostrar contraseña' }));
    expect(campo).toHaveAttribute('type', 'text');
    await usuario.click(screen.getByRole('button', { name: 'Ocultar contraseña' }));
    expect(campo).toHaveAttribute('type', 'password');
  });

  test('propaga los props del formulario', () => {
    render(
      <CampoContrasena label="Contraseña" name="contrasena" error helperText="Debe ser segura" />,
    );
    expect(screen.getByText('Debe ser segura')).toBeInTheDocument();
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('name', 'contrasena');
  });
});
