import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, test } from 'vitest';
import { crearAlmacenColaMemoria } from '../../lib/offline/almacenCola';
import { encolarOperacion, iniciarCoordinadorOffline, listarOperaciones } from './coordinador';
import { PendientesOffline } from './PendientesOffline';

describe('PendientesOffline', () => {
  let limpiar: (() => void) | undefined;
  afterEach(() => limpiar?.());

  test('sin operaciones no muestra el chip', () => {
    limpiar = iniciarCoordinadorOffline({
      despachar: async () => {},
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
    render(<PendientesOffline />);
    expect(screen.queryByRole('button', { name: /pendiente/i })).not.toBeInTheDocument();
  });

  test('muestra el contador, lista y permite descartar', async () => {
    limpiar = iniciarCoordinadorOffline({
      despachar: async () => {
        throw new TypeError('sin red');
      },
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
    render(<PendientesOffline />);
    await encolarOperacion('produccion.crear', 'g1', {});
    expect(await screen.findByRole('button', { name: /1 pendiente/i })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /1 pendiente/i }));
    expect(await screen.findByText('Recogida')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Descartar' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Confirmar' }));
    await waitFor(async () => expect(await listarOperaciones()).toEqual([]));
  });

  test('muestra snackbar al encolar', async () => {
    limpiar = iniciarCoordinadorOffline({
      despachar: async () => {},
      almacen: crearAlmacenColaMemoria(),
      intervaloMs: 60_000,
    });
    render(<PendientesOffline />);
    await encolarOperacion('mortalidad.crear', 'g1', {});
    expect(await screen.findByText(/Guardado sin conexión/)).toBeInTheDocument();
  });
});
