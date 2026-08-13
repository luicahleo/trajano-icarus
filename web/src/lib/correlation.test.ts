import { obtenerCorrelationId, renovarCorrelationId } from './correlation';

describe('correlation', () => {
  beforeEach(() => sessionStorage.clear());

  test('genera y reutiliza el mismo id dentro de la pestaña', () => {
    const a = obtenerCorrelationId();
    expect(obtenerCorrelationId()).toBe(a);
  });

  test('renovarCorrelationId cambia el id', () => {
    const a = obtenerCorrelationId();
    expect(renovarCorrelationId()).not.toBe(a);
  });
});
