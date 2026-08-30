import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import { iniciarCoordinadorOffline, listarOperaciones } from '../../app/offline/coordinador';
import { RegistrarRecogidaDialog } from './RegistrarRecogidaDialog';

const envolver = (ui: React.ReactElement) => (
  <QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>
);

describe('RegistrarRecogidaDialog offline', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => {
    limpiar?.();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  test('guardar habilitado sin conexión y encola', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    const alCerrar = vi.fn();
    render(envolver(<RegistrarRecogidaDialog galponId="g1" abierto alCerrar={alCerrar} />));
    await userEvent.type(screen.getByLabelText('Maples'), '3');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(alCerrar).toHaveBeenCalled());
    expect(fetchMock).not.toHaveBeenCalled();
    const ops = await listarOperaciones();
    expect(ops.length).toBe(1);
    expect(ops[0].tipo).toBe('produccion.crear');
  });

  test('fallo de red durante el guardado encola y cierra', async () => {
    // Despachador que rechaza: el sync automático tras encolar falla y la
    // operación permanece en la cola, que es lo que verifica el test.
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {
        throw new TypeError('sin red');
      }),
      almacen: crearAlmacenColaMemoria(),
    });
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('fetch failed');
      }),
    );
    const alCerrar = vi.fn();
    render(envolver(<RegistrarRecogidaDialog galponId="g1" abierto alCerrar={alCerrar} />));
    await userEvent.type(screen.getByLabelText('Maples'), '3');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(alCerrar).toHaveBeenCalled());
    expect((await listarOperaciones()).length).toBe(1);
  });

  test('el rechazo de negocio muestra el error y mantiene el formulario abierto', async () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true);
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ title: 'Solicitud inválida' }), {
            status: 400,
            headers: { 'content-type': 'application/json' },
          }),
      ),
    );
    limpiar = iniciarCoordinadorOffline({
      despachar: vi.fn(async () => {}),
      almacen: crearAlmacenColaMemoria(),
    });
    const alCerrar = vi.fn();
    render(envolver(<RegistrarRecogidaDialog galponId="g1" abierto alCerrar={alCerrar} />));
    await userEvent.type(screen.getByLabelText('Maples'), '3');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(await screen.findByText('Solicitud inválida')).toBeInTheDocument();
    expect(alCerrar).not.toHaveBeenCalled();
  });
});
