import { render, screen, waitFor } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import type { DiagnosticoFrontend } from '../lib/diagnosticos';
import { ErrorDiagnosticoPage } from './ErrorDiagnosticoPage';

function renderConError(reportero: (d: DiagnosticoFrontend) => Promise<void>, error: unknown) {
  const router = createMemoryRouter(
    [
      {
        path: '/',
        errorElement: <ErrorDiagnosticoPage reportero={reportero} />,
        Component: () => {
          throw error;
        },
      },
    ],
    { initialEntries: ['/'] },
  );
  return render(<RouterProvider router={router} />);
}

describe('ErrorDiagnosticoPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  test('muestra una referencia opaca y la reporta sin contenido sensible', async () => {
    const reportero = vi.fn().mockResolvedValue(undefined);
    renderConError(reportero, new Error('detalle interno de la base de datos'));

    expect(await screen.findByText(/ERR-[0-9A-F]{12}/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Recargar' })).toBeInTheDocument();

    expect(reportero).toHaveBeenCalledTimes(1);
    const reporte = reportero.mock.calls[0][0] as DiagnosticoFrontend;
    expect(reporte.errorId).toMatch(/^ERR-[0-9A-F]{12}$/);
    expect(reporte).toMatchObject({
      eventName: 'router.unexpected',
      category: 'unexpected',
      source: 'router',
    });
    expect(JSON.stringify(reporte)).not.toContain('detalle interno de la base de datos');
  });

  test('clasifica el fallo de carga de chunk sin enviar su mensaje', async () => {
    const reportero = vi.fn().mockResolvedValue(undefined);
    renderConError(
      reportero,
      new Error('Failed to fetch dynamically imported module: /assets/LoginPage-ab12.js'),
    );

    await waitFor(() => expect(reportero).toHaveBeenCalledTimes(1));
    const reporte = reportero.mock.calls[0][0] as DiagnosticoFrontend;
    expect(reporte).toMatchObject({
      eventName: 'chunk.load_failed',
      category: 'chunk',
      source: 'router',
    });
    expect(JSON.stringify(reporte)).not.toContain('dynamically imported');
  });
});
