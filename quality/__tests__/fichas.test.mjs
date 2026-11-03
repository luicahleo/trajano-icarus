import { test } from 'node:test';
import assert from 'node:assert/strict';
import { generarIndice, leerFicha, lineaIndice, validarFicha, TOPES } from '../lib/fichas.mjs';
import { fichaValida } from './ficha-valida.mjs';

const opciones = (o = {}) => ({ id: '2026-11-01-ejemplo', aprobado: 'abc1234', existe: () => true, ...o });
const validar = (texto, o) => validarFicha(texto, opciones(o)).join('\n');

test('una ficha válida no tiene problemas', () => {
  assert.deepEqual(validarFicha(fichaValida(), opciones()), []);
});

test('acepta finales de línea CRLF', () => {
  assert.deepEqual(validarFicha(fichaValida().replace(/\n/g, '\r\n'), opciones()), []);
});

test('sin frontmatter es un problema', () => {
  assert.match(validar('# Ficha\n'), /frontmatter/);
});

test('detecta marcadores sin completar', () => {
  assert.match(validar(fichaValida().replace('Porque hacía falta.', 'COMPLETAR')), /COMPLETAR/);
});

test('el id debe coincidir con la carpeta', () => {
  assert.match(validar(fichaValida(), { id: '2026-11-01-otra' }), /no coincide/);
});

test('tipo y módulos pertenecen a listas cerradas', () => {
  assert.match(validar(fichaValida().replace('tipo: calidad', 'tipo: mejora')), /Tipo desconocido/);
  assert.match(validar(fichaValida().replace('modulos: [quality]', 'modulos: [Pagos]')), /Módulo desconocido: Pagos/);
  assert.match(validar(fichaValida().replace('modulos: [quality]', 'modulos: []')), /módulo/);
});

test('los temas son kebab-case sin acentos', () => {
  assert.match(validar(fichaValida().replace('temas: [fichas, indice]', 'temas: [vacunación]')), /Tema inválido/);
  assert.match(validar(fichaValida().replace('temas: [fichas, indice]', 'temas: []')), /temas/);
});

test('el resumen tiene un largo acotado', () => {
  const largo = 'x'.repeat(TOPES.resumenMax + 1);
  assert.match(validar(fichaValida().replace(/resumen: .*/, `resumen: ${largo}`)), /resumen/);
  assert.match(validar(fichaValida().replace(/resumen: .*/, 'resumen: corto')), /resumen/);
});

test('aprobado coincide con la bitácora y commits lista hashes', () => {
  assert.match(validar(fichaValida(), { aprobado: 'def5678' }), /def5678/);
  assert.match(validar(fichaValida().replace(/commits: .*/, 'commits: [no-es-hash]')), /commits/);
});

test('las secciones van completas y en orden', () => {
  const desordenada = fichaValida().replace('## Por qué', '## Temporal').replace('## Decisiones', '## Por qué').replace('## Temporal', '## Decisiones');
  assert.match(validar(desordenada), /Secciones esperadas/);
  assert.match(validar(fichaValida().replace('## Pendientes\n\n- Ninguno.\n', '## Pendientes\n\n')), /Pendientes está vacía/);
});

test('los archivos clave existen y van entre comillas invertidas', () => {
  assert.match(validar(fichaValida(), { existe: () => false }), /No existe el archivo clave quality\/lib\/fichas\.mjs/);
  assert.match(validar(fichaValida().replace('- `quality/lib/fichas.mjs`', '- quality/lib/fichas.mjs')), /sin ruta/);
});

test('los archivos clave son rutas relativas dentro del repositorio y fuera de .git', () => {
  const conRuta = (ruta) => validar(fichaValida({ ruta }));
  assert.match(conRuta('../../../../../Windows/win.ini'), /Ruta no admitida/);
  assert.match(conRuta('../Trajano-Icarus/AGENTS.md'), /Ruta no admitida/);
  assert.match(conRuta('docs/../../AGENTS.md'), /Ruta no admitida/);
  assert.match(conRuta('/etc/passwd'), /Ruta no admitida/);
  assert.match(conRuta('C:/Windows/win.ini'), /Ruta no admitida/);
  assert.match(conRuta('\\\\servidor\\x.txt'), /Ruta no admitida/);
  assert.match(conRuta('.git/config'), /Ruta no admitida/);
  assert.match(conRuta('sub/.git/config'), /Ruta no admitida/);
  assert.equal(conRuta('quality/lib/fichas.mjs'), '');
});

test('en Archivos clave toda línea no vacía es una viñeta con guion', () => {
  const ficha = fichaValida();
  assert.match(validar(ficha.replace('- `quality/lib/fichas.mjs`', '* `quality/lib/fichas.mjs`')), /Línea no admitida en Archivos clave/);
  assert.match(validar(ficha.replace('- `quality/lib/fichas.mjs`', '  - `quality/lib/fichas.mjs`')), /Línea no admitida en Archivos clave/);
  assert.match(validar(ficha.replace('- `quality/lib/fichas.mjs` — formato y validación.', '- `quality/lib/fichas.mjs`\n  continuación')), /Línea no admitida en Archivos clave/);
  const trece = Array.from({ length: TOPES.archivos }, (_, i) => `* \`a${i}.txt\``).join('\n');
  assert.match(validar(ficha.replace('- `quality/lib/fichas.mjs` — formato y validación.', `- \`a.txt\`\n${trece}`)), /Línea no admitida/);
});

test('respeta los topes de tamaño', () => {
  assert.match(validar(fichaValida().replace('Porque hacía falta.', 'x'.repeat(TOPES.bytes))), /bytes/);
  const muchas = Array.from({ length: TOPES.lineas }, () => '- x').join('\n');
  assert.match(validar(fichaValida().replace('- Algo cambió.', muchas)), /líneas/);
});

test('no admite credenciales', () => {
  assert.match(validar(fichaValida().replace('Porque hacía falta.', 'password=SuperSecreta1')), /credencial/);
});

test('la línea del índice une los campos con barras', () => {
  const { meta } = leerFicha(fichaValida());
  assert.equal(
    lineaIndice(meta),
    '- 2026-11-01-ejemplo | calidad | quality | fichas, indice | Ejemplo de ficha válida para las pruebas del formato.',
  );
});

test('el índice ordena de la más reciente a la más antigua y termina en un solo salto', () => {
  const meta = (id) => ({ ...leerFicha(fichaValida()).meta, id });
  const texto = generarIndice([meta('2026-10-30-a'), meta('2026-11-01-b')]);
  assert.ok(texto.indexOf('- 2026-11-01-b') < texto.indexOf('- 2026-10-30-a'));
  assert.match(texto, /No editar a mano/);
  assert.ok(texto.endsWith('\n') && !texto.endsWith('\n\n'));
  assert.equal(generarIndice([meta('2026-11-01-b'), meta('2026-10-30-a')]), texto);
});

test('un índice sin fichas no deja una línea en blanco al final', () => {
  const texto = generarIndice([]);
  assert.ok(texto.endsWith('\n') && !texto.endsWith('\n\n'));
});
