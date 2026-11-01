import { etiquetaCampana, totalPendientes, type FuenteNotificaciones } from './fuentes';

const fuente = (id: string, visible: boolean, conteo: number): FuenteNotificaciones => ({
  id,
  seccion: id,
  visible,
  conteo,
  entradas: [],
  error: false,
});

describe('fuentes de notificaciones', () => {
  test('la insignia suma solo las fuentes visibles', () => {
    expect(
      totalPendientes([fuente('a', true, 2), fuente('b', true, 1), fuente('c', false, 5)]),
    ).toBe(3);
    expect(totalPendientes([])).toBe(0);
  });

  test('nombre accesible de la campana', () => {
    expect(etiquetaCampana(0)).toBe('Notificaciones sin pendientes');
    expect(etiquetaCampana(1)).toBe('Notificaciones: 1 pendiente');
    expect(etiquetaCampana(4)).toBe('Notificaciones: 4 pendientes');
  });
});
