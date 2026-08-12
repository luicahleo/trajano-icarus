import { test } from 'node:test';
import assert from 'node:assert/strict';
import { GATES, ejecutarGates } from '../verify.mjs';

test('el primer gate es el de los tests de la propia puerta', () => {
  assert.equal(GATES[0].nombre, 'Tests de la puerta');
});

test('están declarados los gates de mojibake y de enlaces', () => {
  const nombres = GATES.map((g) => g.nombre);
  assert.ok(nombres.includes('Mojibake'));
  assert.ok(nombres.includes('Enlaces'));
});

test('todos los gates se invocan con node', () => {
  for (const gate of GATES) {
    assert.equal(gate.comando, 'node');
    assert.ok(Array.isArray(gate.args) && gate.args.length > 0);
  }
});

test('con todos los gates en verde devuelve ok y los ejecuta todos', () => {
  const gates = [{ nombre: 'A' }, { nombre: 'B' }, { nombre: 'C' }];
  const resultado = ejecutarGates(gates, () => ({ codigo: 0 }));
  assert.equal(resultado.ok, true);
  assert.equal(resultado.ejecutados.length, 3);
});

test('corta en el primer fallo y no ejecuta los gates siguientes', () => {
  const gates = [{ nombre: 'A' }, { nombre: 'B' }, { nombre: 'C' }];
  const resultado = ejecutarGates(gates, (gate) => ({
    codigo: gate.nombre === 'B' ? 1 : 0,
  }));
  assert.equal(resultado.ok, false);
  assert.deepEqual(
    resultado.ejecutados.map((e) => e.nombre),
    ['A', 'B'],
  );
});

test('informa el código del gate que falló', () => {
  const resultado = ejecutarGates([{ nombre: 'A' }], () => ({ codigo: 7 }));
  assert.equal(resultado.ejecutados[0].codigo, 7);
});
