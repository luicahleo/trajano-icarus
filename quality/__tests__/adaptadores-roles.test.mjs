import { test } from 'node:test';
import assert from 'node:assert/strict';
import { leerFrontmatter, adaptadorClaude, adaptadorKimi, adaptadoresDeRoles, AVISO_ROL } from '../adaptadores/roles.mjs';
import { esRelativo, extraerEnlaces } from '../check-enlaces.mjs';

const fuente = `---
nombre: verificador
descripcion: Ejecuta pruebas y registra evidencia
cuando_usar: Tras cada cambio de código
nivel: intermedio
herramientas: [leer, buscar, shell]
delega_en: []
---

# Verificador

Cuerpo del rol.
`;

const orquestador = fuente
  .replace('nombre: verificador', 'nombre: orquestador')
  .replace('nivel: intermedio', 'nivel: alto')
  .replace('delega_en: []', 'delega_en: [planificador, desarrollador]');

test('lee el frontmatter neutral', () => {
  const { meta, cuerpo } = leerFrontmatter(fuente);
  assert.equal(meta.nombre, 'verificador');
  assert.equal(meta.descripcion, 'Ejecuta pruebas y registra evidencia');
  assert.deepEqual(meta.herramientas, ['leer', 'buscar', 'shell']);
  assert.deepEqual(meta.delega_en, []);
  assert.ok(cuerpo.startsWith('# Verificador'));
});

test('un frontmatter ausente es un error', () => {
  assert.throws(() => leerFrontmatter('# Sin frontmatter\n'), /frontmatter/);
});

test('una herramienta desconocida es un error', () => {
  assert.throws(() => adaptadorClaude(fuente.replace('shell', 'magia')), /magia/);
  assert.throws(() => adaptadorKimi(fuente.replace('shell', 'magia')), /magia/);
});

test('un nivel desconocido es un error', () => {
  assert.throws(() => adaptadorClaude(fuente.replace('nivel: intermedio', 'nivel: extremo')), /extremo/);
});

test('el adaptador de Claude traduce herramientas y modelo', () => {
  const a = adaptadorClaude(fuente);
  assert.match(a, /^---\nname: verificador\n/);
  assert.match(a, /\ntools: Read, Grep, Glob, Bash, PowerShell\n/);
  assert.match(a, /\nmodel: sonnet\n/);
  assert.match(a, /Tras cada cambio de código/);
  assert.ok(!/\nmodel: opus/.test(a));
});

test('el adaptador de Claude acota la delegación', () => {
  const a = adaptadorClaude(orquestador);
  assert.match(a, /\ntools: Read, Grep, Glob, Bash, PowerShell, Agent\(planificador, desarrollador\)\n/);
  assert.match(a, /\nmodel: opus\n/);
});

test('el adaptador de Kimi usa sus campos y no declara modelo', () => {
  const a = adaptadorKimi(fuente);
  assert.match(a, /^---\nname: verificador\n/);
  assert.match(a, /\nwhenToUse: Tras cada cambio de código\n/);
  assert.match(a, /\ntools:\n  - Read\n  - Grep\n  - Glob\n  - Bash\n/);
  assert.ok(!/\nmodel:/.test(a));
  assert.ok(!/PowerShell/.test(a));
  assert.match(a, /\nsubagents: \[\]\n/);
});

test('el adaptador de Kimi acota la delegación', () => {
  const a = adaptadorKimi(orquestador);
  assert.match(a, /  - Agent\n/);
  assert.match(a, /\nsubagents:\n  - planificador\n  - desarrollador\n/);
});

test('los adaptadores conservan el cuerpo y llevan el aviso tras el frontmatter', () => {
  for (const a of [adaptadorClaude(fuente), adaptadorKimi(fuente)]) {
    assert.ok(a.includes('Cuerpo del rol.'));
    assert.ok(a.includes(AVISO_ROL));
    assert.ok(a.indexOf(AVISO_ROL) > a.indexOf('\n---\n'));
    assert.ok(a.endsWith('\n') && !a.endsWith('\n\n'));
  }
});

test('los roles no usan enlaces relativos: se copian a otra carpeta y el agente trabaja desde la raíz', () => {
  for (const { ruta, contenido } of adaptadoresDeRoles()) {
    const relativos = extraerEnlaces(contenido).filter(({ destino }) => esRelativo(destino));
    assert.deepEqual(relativos, [], ruta);
  }
});
