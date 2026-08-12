import { test } from 'node:test';
import assert from 'node:assert/strict';
import { titulo, exito, fallo, aviso } from '../lib/salida.mjs';

test('titulo incluye el texto', () => {
  assert.match(titulo('Gate de enlaces'), /Gate de enlaces/);
});

test('exito incluye el texto y una marca visible', () => {
  const linea = exito('Adaptadores al día');
  assert.match(linea, /Adaptadores al día/);
  assert.match(linea, /OK/);
});

test('fallo incluye el detalle cuando se proporciona', () => {
  const linea = fallo('Enlace roto', 'docs/ai/README.md:12 -> docs/ai/FALTA.md');
  assert.match(linea, /Enlace roto/);
  assert.match(linea, /FALTA\.md/);
});

test('fallo sin detalle no agrega una segunda línea', () => {
  assert.equal(fallo('Sin detalle').split('\n').length, 1);
});

test('aviso se distingue de un fallo', () => {
  assert.notEqual(aviso('Gate omitido'), fallo('Gate omitido'));
});
