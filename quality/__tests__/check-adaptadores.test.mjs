import { test } from 'node:test';
import assert from 'node:assert/strict';
import { verificarAdaptadores } from '../check-adaptadores.mjs';

const MANIFIESTO = [
  { harness: 'Claude Code', ruta: 'CLAUDE.md', contenido: 'esperado\n' },
  { harness: 'Cursor', ruta: '.cursorignore', contenido: 'bin/\n' },
];

test('sin desvíos cuando todo coincide', () => {
  const leer = (ruta) => MANIFIESTO.find((a) => a.ruta === ruta).contenido;
  assert.deepEqual(verificarAdaptadores(MANIFIESTO, leer), []);
});

test('detecta un adaptador que falta', () => {
  const leer = (ruta) => (ruta === 'CLAUDE.md' ? null : 'bin/\n');
  assert.deepEqual(verificarAdaptadores(MANIFIESTO, leer), [
    { ruta: 'CLAUDE.md', motivo: 'falta' },
  ]);
});

test('detecta un adaptador editado a mano', () => {
  const leer = (ruta) => (ruta === 'CLAUDE.md' ? 'editado a mano\n' : 'bin/\n');
  assert.deepEqual(verificarAdaptadores(MANIFIESTO, leer), [
    { ruta: 'CLAUDE.md', motivo: 'difiere' },
  ]);
});

test('detecta una diferencia de un solo carácter', () => {
  const leer = (ruta) => (ruta === 'CLAUDE.md' ? 'esperado' : 'bin/\n');
  assert.equal(verificarAdaptadores(MANIFIESTO, leer)[0].motivo, 'difiere');
});

test('acumula todos los desvíos, no solo el primero', () => {
  assert.equal(verificarAdaptadores(MANIFIESTO, () => null).length, 2);
});
