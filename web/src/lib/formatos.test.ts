import { describe, expect, test } from 'vitest';
import { formatoMoneda, formatoMonedaExacta } from './formatos';

// Intl separa el símbolo del importe con un espacio duro, invisible en el
// código y normalizado por el DOM. Acá se compara la cadena cruda, así que
// unificamos los espacios antes de comparar.
const legible = (valor: string) => valor.replace(/\s/g, ' ');

describe('formatoMoneda', () => {
  test('muestra un monto en bolivianos con dos decimales', () => {
    expect(legible(formatoMoneda(1234.5678))).toBe('Bs 1.234,57');
  });

  test('deja el signo negativo delante del símbolo', () => {
    expect(legible(formatoMoneda(-45.5))).toBe('-Bs 45,50');
  });
});

describe('formatoMonedaExacta', () => {
  test('muestra cuatro decimales para que el cliente rehaga la cuenta a mano', () => {
    expect(legible(formatoMonedaExacta(1234.5678))).toBe('Bs 1.234,5678');
  });

  test('completa los decimales que faltan en lugar de recortarlos', () => {
    expect(legible(formatoMonedaExacta(-45.5))).toBe('-Bs 45,5000');
  });
});
