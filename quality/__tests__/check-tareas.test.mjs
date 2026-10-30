import { test } from 'node:test';
import assert from 'node:assert/strict';
import { revisarTarea } from '../check-tareas.mjs';

const creada = JSON.stringify({ ts: 't', tipo: 'creada', rol: 'orquestador', proveedor: 'anthropic', titulo: 'x' });
const paso = JSON.stringify({ ts: 't', tipo: 'estado', rol: 'orquestador', proveedor: 'anthropic', de: 'RECIBIDA', a: 'DESARROLLO' });

test('una tarea correcta no tiene problemas', () => {
  assert.deepEqual(revisarTarea(`${creada}\n${paso}\n`, '# Tarea\n'), []);
});

test('detecta una línea que no es JSON', () => {
  assert.match(revisarTarea(`${creada}\nesto no\n`, '').join(), /JSON/);
});

test('detecta una historia ilegal', () => {
  const salto = JSON.stringify({ ts: 't', tipo: 'estado', rol: 'orquestador', proveedor: 'a', de: 'RECIBIDA', a: 'CERRADA' });
  assert.match(revisarTarea(`${creada}\n${salto}\n`, '').join(), /no permitida/);
});

test('detecta un secreto en la bitácora y en tarea.md', () => {
  assert.match(revisarTarea(`${creada}\n`, 'password=SuperSecreta1').join(), /credencial/);
  const nota = JSON.stringify({ ts: 't', tipo: 'nota', rol: 'x', proveedor: 'a', texto: 'password=SuperSecreta1' });
  assert.match(revisarTarea(`${creada}\n${nota}\n`, '').join(), /credencial/);
});

test('una bitácora vacía es un problema', () => {
  assert.equal(revisarTarea('', '').length, 1);
});
