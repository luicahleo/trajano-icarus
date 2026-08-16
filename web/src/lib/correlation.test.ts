import { crearCorrelationId } from './correlation';

describe('correlation', () => {
  test('genera un UUID diferente para cada petición', () => {
    const primero = crearCorrelationId();
    const segundo = crearCorrelationId();

    expect(primero).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
    );
    expect(segundo).not.toBe(primero);
  });
});
