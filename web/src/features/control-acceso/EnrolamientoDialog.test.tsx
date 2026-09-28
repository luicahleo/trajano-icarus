import { render, screen, waitFor } from '@testing-library/react';
import { EnrolamientoDialog } from './EnrolamientoDialog';

const trabajador = {
  id: 't1',
  nombre: 'Ana Quispe',
  documentoIdentidad: 'DNI-00000001',
  cargo: 'Criadora',
  fechaIngreso: '2025-01-01',
  fechaCese: null,
  funcionalidades: [],
};

describe('EnrolamientoDialog', () => {
  afterEach(() => vi.restoreAllMocks());

  test('detiene la cámara al cerrar el diálogo', async () => {
    const stop = vi.fn();
    const stream = { getTracks: () => [{ stop }] } as unknown as MediaStream;
    const getUserMedia = vi.fn().mockResolvedValue(stream);
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia },
    });

    const { rerender } = render(
      <EnrolamientoDialog
        abierto
        trabajador={trabajador}
        pendiente={false}
        resultado={null}
        error={null}
        onCancelar={() => undefined}
        onCapturar={() => undefined}
      />,
    );

    await waitFor(() => expect(getUserMedia).toHaveBeenCalled());

    rerender(
      <EnrolamientoDialog
        abierto={false}
        trabajador={trabajador}
        pendiente={false}
        resultado={null}
        error={null}
        onCancelar={() => undefined}
        onCapturar={() => undefined}
      />,
    );

    await waitFor(() => expect(stop).toHaveBeenCalled());
  });

  test('avisa cuando el dispositivo no tiene cámara', async () => {
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: undefined });

    render(
      <EnrolamientoDialog
        abierto
        trabajador={trabajador}
        pendiente={false}
        resultado={null}
        error={null}
        onCancelar={() => undefined}
        onCapturar={() => undefined}
      />,
    );

    expect(await screen.findByText(/No hay cámara disponible/i)).toBeInTheDocument();
  });
});
