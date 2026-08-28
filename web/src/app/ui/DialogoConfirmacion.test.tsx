import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DialogoConfirmacion } from './DialogoConfirmacion';

describe('DialogoConfirmacion', () => {
  test('renderiza el diálogo con título, mensaje y botones por defecto', () => {
    render(
      <DialogoConfirmacion
        abierto
        titulo="Confirmar acción"
        mensaje="¿Suspender al cliente?"
        onCancelar={vi.fn()}
        onConfirmar={vi.fn()}
      />,
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Confirmar acción' })).toBeInTheDocument();
    expect(screen.getByText('¿Suspender al cliente?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirmar' })).toBeInTheDocument();
  });

  test('usa la etiqueta de confirmación y el color indicados', () => {
    render(
      <DialogoConfirmacion
        abierto
        titulo="Confirmar acción"
        mensaje="¿Reactivar al cliente?"
        etiquetaConfirmar="Reactivar"
        color="success"
        onCancelar={vi.fn()}
        onConfirmar={vi.fn()}
      />,
    );
    expect(screen.getByRole('button', { name: 'Reactivar' })).toBeInTheDocument();
  });

  test('deshabilita confirmar mientras la operación está pendiente', () => {
    render(
      <DialogoConfirmacion
        abierto
        titulo="Confirmar acción"
        mensaje="¿Eliminar?"
        pendiente
        onCancelar={vi.fn()}
        onConfirmar={vi.fn()}
      />,
    );
    expect(screen.getByRole('button', { name: 'Confirmar' })).toBeDisabled();
  });

  test('cancelar y confirmar invocan sus callbacks', async () => {
    const usuario = userEvent.setup();
    const onCancelar = vi.fn();
    const onConfirmar = vi.fn();
    render(
      <DialogoConfirmacion
        abierto
        titulo="Confirmar acción"
        mensaje="¿Eliminar?"
        onCancelar={onCancelar}
        onConfirmar={onConfirmar}
      />,
    );
    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }));
    expect(onCancelar).toHaveBeenCalledTimes(1);
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));
    expect(onConfirmar).toHaveBeenCalledTimes(1);
  });

  test('cerrado no renderiza el diálogo', () => {
    render(
      <DialogoConfirmacion
        abierto={false}
        titulo="Confirmar acción"
        mensaje="¿Eliminar?"
        onCancelar={vi.fn()}
        onConfirmar={vi.fn()}
      />,
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
