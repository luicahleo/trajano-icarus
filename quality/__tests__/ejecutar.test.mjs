import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ejecutar } from '../lib/ejecutar.mjs';

test('devuelve código 0 y la salida de un comando exitoso', () => {
  const r = ejecutar(process.execPath, ['-e', 'console.log("hola")'], {
    silencioso: true,
    sinShell: true,
  });
  assert.equal(r.codigo, 0);
  assert.match(r.salida, /hola/);
  assert.ok(r.duracionMs >= 0);
});

test('devuelve el código de salida de un comando fallido sin lanzar', () => {
  const r = ejecutar(process.execPath, ['-e', 'process.exit(3)'], {
    silencioso: true,
    sinShell: true,
  });
  assert.equal(r.codigo, 3);
});

test('un comando inexistente devuelve código distinto de cero sin lanzar', () => {
  const r = ejecutar('comando-que-no-existe-jamas', [], {
    silencioso: true,
    sinShell: true,
  });
  assert.notEqual(r.codigo, 0);
});

test('combina stdout y stderr en una sola cadena', () => {
  const r = ejecutar(
    process.execPath,
    ['-e', 'console.log("uno"); console.error("dos")'],
    { silencioso: true, sinShell: true },
  );
  assert.match(r.salida, /uno/);
  assert.match(r.salida, /dos/);
});
