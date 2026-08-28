import { describe, expect, test } from 'vitest';
import { clasificarTarea, formatearConteo, totalHuevos } from './formatos';
import { HUEVOS_POR_MAPLE, hoyIso } from './constantes';
describe('formatos avícola', () => {
  test('maple', () => expect(HUEVOS_POR_MAPLE).toBe(30));
  test('total', () => expect(totalHuevos(10, 5)).toBe(305));
  test('texto', () => expect(formatearConteo(10, 5)).toBe('10 maples + 5 (= 305)'));
  test('fecha', () => expect(hoyIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/));
});
describe('clasificación de tarea', () => {
  const hoy = '2026-08-27';
  test('pasada es Vencida', () => expect(clasificarTarea('2026-08-06', hoy)).toBe('Vencida'));
  test('de hoy es Hoy', () => expect(clasificarTarea(hoy, hoy)).toBe('Hoy'));
  test('futura es Próxima', () => expect(clasificarTarea('2026-09-03', hoy)).toBe('Próxima'));
});
