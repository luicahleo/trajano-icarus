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

test('cada gate se invoca con el comando que le corresponde', () => {
  for (const gate of GATES) {
    if (gate.nombre.startsWith('Backend')) {
      assert.equal(gate.comando, 'dotnet');
    } else if (gate.nombre.startsWith('Frontend')) {
      // En Windows se invoca el CLI de npm con node directamente (sin shell,
      // para no disparar el aviso DEP0190); en POSIX, el binario npm.
      if (process.platform === 'win32' && gate.comando === process.execPath) {
        assert.match(gate.args[0], /npm-cli\.js$/);
      } else {
        assert.equal(gate.comando, 'npm');
      }
      assert.ok(gate.args.includes('run'));
    } else {
      assert.equal(gate.comando, 'node');
    }
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

test('el gate de adaptadores está en la lista y corre antes que mojibake', () => {
  const nombres = GATES.map((g) => g.nombre);
  assert.ok(nombres.includes('Adaptadores'));
  assert.ok(nombres.indexOf('Adaptadores') < nombres.indexOf('Mojibake'));
});
