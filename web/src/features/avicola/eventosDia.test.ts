import { describe, expect, test } from 'vitest';
import type { OperacionPendiente } from '../../lib/offline/tipos';
import type { MortalidadRegistro, RecogidaResumen } from '../../lib/tipos';
import type { DatosBajas, DatosRecogida } from './api';
import { fusionarEventosDia } from './eventosDia';

const recogidaServidor: RecogidaResumen = {
  id: 'r1',
  fecha: '2026-08-31',
  hora: '09:00',
  cantidadMaples: 10,
  unidadesIncompletas: 5,
  maplesDescarte: 1,
  unidadesDescarte: 2,
  gallinasVivas: 100,
  totalVendible: 305,
  totalDescarte: 32,
};

const bajasServidor: MortalidadRegistro = {
  id: 'm1',
  fecha: '2026-08-31',
  hora: '07:30',
  cantidadMuertas: 2,
  gallinasVivas: 100,
};

function operacion(
  tipo: 'produccion.crear' | 'mortalidad.crear',
  cuerpo: unknown,
): OperacionPendiente {
  return {
    id: `op-${tipo}`,
    tipo,
    galponId: 'g1',
    cuerpo,
    estado: 'pendiente',
    intentos: 0,
    creadoEn: '2026-08-31T07:00:00.000Z',
    proximoIntentoEn: null,
  };
}

const recogidaPendiente: DatosRecogida = {
  hora: '08:15',
  cantidadMaples: 3,
  unidadesIncompletas: 1,
  maplesDescarte: 0,
  unidadesDescarte: 0,
  idempotencyKey: 'k1',
};

const bajasPendientes: DatosBajas = {
  hora: null,
  cantidadMuertas: 1,
  idempotencyKey: 'k2',
};

describe('fusionarEventosDia', () => {
  test('mezcla servidor y cola ordenados por hora, marcando los pendientes', () => {
    const eventos = fusionarEventosDia(
      [recogidaServidor],
      [bajasServidor],
      [
        operacion('produccion.crear', recogidaPendiente),
        operacion('mortalidad.crear', bajasPendientes),
      ],
    );

    expect(eventos.map((e) => `${e.tipo}:${e.hora}`)).toEqual([
      'bajas:', // hora null → '' queda primero
      'bajas:07:30',
      'recogida:08:15',
      'recogida:09:00',
    ]);
    expect(eventos[2].pendiente?.id).toBe('op-produccion.crear');
    expect(eventos[3].pendiente).toBeUndefined();
  });

  test('los pendientes conservan su cuerpo para editar y su id para eliminar', () => {
    const eventos = fusionarEventosDia([], [], [operacion('mortalidad.crear', bajasPendientes)]);
    const [evento] = eventos;
    expect(evento.tipo).toBe('bajas');
    expect(evento.pendiente?.id).toBe('op-mortalidad.crear');
    if (evento.tipo === 'bajas' && evento.pendiente) {
      expect(evento.datos.cantidadMuertas).toBe(1);
    }
  });
});
