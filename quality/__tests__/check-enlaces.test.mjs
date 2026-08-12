import { test } from 'node:test';
import assert from 'node:assert/strict';
import { extraerEnlaces, esRelativo, rutaDelDestino } from '../check-enlaces.mjs';

test('extrae un enlace en línea con su número de línea', () => {
  const md = 'Intro\n\nVer el [mapa](docs/ai/README.md) del proceso.\n';
  assert.deepEqual(extraerEnlaces(md), [{ numero: 3, destino: 'docs/ai/README.md' }]);
});

test('extrae varios enlaces de la misma línea', () => {
  const md = '[uno](a.md) y [dos](b.md)\n';
  assert.deepEqual(extraerEnlaces(md), [
    { numero: 1, destino: 'a.md' },
    { numero: 1, destino: 'b.md' },
  ]);
});

test('descarta el título opcional del enlace', () => {
  const md = '[x](docs/ai/WORKFLOW.md "El flujo")\n';
  assert.deepEqual(extraerEnlaces(md), [{ numero: 1, destino: 'docs/ai/WORKFLOW.md' }]);
});

test('ignora los enlaces dentro de un bloque cercado', () => {
  const md = 'Texto\n```markdown\n[ejemplo](no-existe.md)\n```\nFin\n';
  assert.deepEqual(extraerEnlaces(md), []);
});

test('ignora los enlaces dentro de un span de código', () => {
  const md = 'Se escribe `[texto](destino.md)` así.\n';
  assert.deepEqual(extraerEnlaces(md), []);
});

test('reanuda la extracción después de cerrar el bloque cercado', () => {
  const md = '```\n[dentro](x.md)\n```\n[fuera](y.md)\n';
  assert.deepEqual(extraerEnlaces(md), [{ numero: 4, destino: 'y.md' }]);
});

test('esRelativo distingue los absolutos que no se comprueban', () => {
  assert.equal(esRelativo('https://github.com/luicahleo/trajano-icarus'), false);
  assert.equal(esRelativo('http://ejemplo.test'), false);
  assert.equal(esRelativo('mailto:alguien@ejemplo.test'), false);
  assert.equal(esRelativo('#seccion-interna'), false);
  assert.equal(esRelativo('docs/ai/README.md'), true);
  assert.equal(esRelativo('../AGENTS.md'), true);
});

test('rutaDelDestino recorta el ancla', () => {
  assert.equal(rutaDelDestino('docs/ai/WORKFLOW.md#brainstorming'), 'docs/ai/WORKFLOW.md');
});

test('rutaDelDestino decodifica los espacios escapados', () => {
  assert.equal(rutaDelDestino('docs/un%20archivo.md'), 'docs/un archivo.md');
});
