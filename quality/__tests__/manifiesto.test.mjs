import { test } from 'node:test';
import assert from 'node:assert/strict';
import { AVISO, ADAPTADORES } from '../adaptadores/manifiesto.mjs';
import { AVISO_ROL } from '../adaptadores/roles.mjs';

const INSTRUCCIONES = ADAPTADORES.filter((a) => !a.ruta.includes('/agents/'));
const ROLES = ['desarrollador', 'documentador', 'operaciones', 'orquestador', 'planificador', 'revisor', 'verificador'];

test('cada adaptador declara harness, ruta y contenido', () => {
  for (const a of ADAPTADORES) {
    assert.ok(a.harness, 'falta harness');
    assert.ok(a.ruta, `falta ruta en ${a.harness}`);
    assert.ok(a.contenido.length > 0, `contenido vacío en ${a.ruta}`);
  }
});

test('no hay dos adaptadores para la misma ruta', () => {
  const rutas = ADAPTADORES.map((a) => a.ruta);
  assert.equal(new Set(rutas).size, rutas.length);
});

test('están los seis archivos que el diseño manda generar', () => {
  const rutas = INSTRUCCIONES.map((a) => a.ruta).sort();
  assert.deepEqual(rutas, [
    '.clineignore',
    '.cursorignore',
    '.geminiignore',
    '.github/copilot-instructions.md',
    'CLAUDE.md',
    'GEMINI.md',
  ]);
});

test('todo contenido lleva el aviso de archivo generado', () => {
  for (const a of ADAPTADORES) {
    const aviso = a.ruta.includes('/agents/') ? AVISO_ROL : AVISO;
    assert.ok(a.contenido.includes(aviso), `sin aviso en ${a.ruta}`);
  }
});

test('todo contenido termina en un único salto de línea', () => {
  for (const a of ADAPTADORES) {
    assert.ok(a.contenido.endsWith('\n'), `sin salto final en ${a.ruta}`);
    assert.ok(!a.contenido.endsWith('\n\n'), `salto final duplicado en ${a.ruta}`);
  }
});

test('los adaptadores de instrucciones apuntan al núcleo', () => {
  const instrucciones = INSTRUCCIONES.filter((a) => a.ruta.endsWith('.md'));
  assert.equal(instrucciones.length, 3);
  for (const a of instrucciones) {
    assert.match(a.contenido, /AGENTS\.md/);
  }
});

test('los adaptadores de instrucciones no acumulan reglas propias', () => {
  for (const a of INSTRUCCIONES.filter((x) => x.ruta.endsWith('.md'))) {
    const lineas = a.contenido.trimEnd().split('\n').filter((l) => l.trim() !== '');
    assert.ok(lineas.length <= 5, `${a.ruta} tiene ${lineas.length} líneas útiles, máximo 5`);
  }
});

test('cada rol tiene adaptador de Claude y de Kimi', () => {
  const rutas = ADAPTADORES.map((a) => a.ruta);
  for (const rol of ROLES) {
    assert.ok(rutas.includes(`.claude/agents/${rol}.md`), `falta Claude: ${rol}`);
    assert.ok(rutas.includes(`.kimi-code/agents/${rol}.md`), `falta Kimi: ${rol}`);
  }
  assert.equal(rutas.filter((r) => r.includes('/agents/')).length, ROLES.length * 2);
});

test('los tres archivos de ignorados tienen contenido idéntico', () => {
  const ignores = ADAPTADORES.filter((a) => a.ruta.endsWith('ignore'));
  assert.equal(ignores.length, 3);
  const primero = ignores[0].contenido;
  for (const a of ignores) assert.equal(a.contenido, primero);
});

test('los ignorados cubren los secretos y el ruido de build', () => {
  const ignore = ADAPTADORES.find((a) => a.ruta === '.clineignore').contenido;
  for (const patron of ['node_modules/', 'bin/', 'obj/', '.env', '.git/']) {
    assert.ok(ignore.includes(patron), `falta ${patron} en los ignorados`);
  }
});
