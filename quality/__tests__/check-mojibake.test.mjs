import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  PATRONES,
  sinSpansDeCodigo,
  lineaTieneMojibake,
  analizar,
} from '../check-mojibake.mjs';

// Los literales se escriben con escapes a propósito: si estuvieran como
// caracteres, este archivo de test se marcaría a sí mismo.
const ENIE_ROTA = '\u00C3\u00B1';   // la letra ene leida como Latin-1
const REEMPLAZO = '\uFFFD';            // U+FFFD
const NBSP_ROTO = '\u00C2\u00A0';   // espacio duro leido como Latin-1

test('los tres patrones vigilados están declarados', () => {
  assert.deepEqual(PATRONES, ['\uFFFD', '\u00C3', '\u00C2']);
});

test('sinSpansDeCodigo quita el contenido entre acentos graves', () => {
  assert.equal(sinSpansDeCodigo('cita `roto` fin'), 'cita  fin');
});

test('sinSpansDeCodigo conserva el texto fuera de los acentos graves', () => {
  assert.equal(sinSpansDeCodigo('sin spans aquí'), 'sin spans aquí');
});

test('detecta mojibake suelto en un Markdown', () => {
  assert.equal(lineaTieneMojibake('docs/ai/README.md', `A${ENIE_ROTA}o`), true);
});

test('no detecta mojibake citado dentro de un span de código en Markdown', () => {
  assert.equal(
    lineaTieneMojibake('docs/ai/PUERTA_CALIDAD.md', `El gate busca \`${ENIE_ROTA}\`.`),
    false,
  );
});

test('en archivos que no son Markdown no hay excepción por acentos graves', () => {
  assert.equal(lineaTieneMojibake('quality/lib/salida.mjs', `\`${ENIE_ROTA}\``), true);
});

test('detecta el carácter de reemplazo', () => {
  assert.equal(lineaTieneMojibake('AGENTS.md', `texto ${REEMPLAZO} texto`), true);
});

test('detecta el espacio duro mal decodificado', () => {
  assert.equal(lineaTieneMojibake('AGENTS.md', `uno${NBSP_ROTO}dos`), true);
});

test('el texto correcto en español no produce hallazgos', () => {
  assert.equal(lineaTieneMojibake('AGENTS.md', 'Año, sesión, güero, ¿qué tal?'), false);
});

test('analizar parsea ruta, número de línea y contenido de git grep', () => {
  const salida = `AGENTS.md:12:Un a${ENIE_ROTA}o\n`;
  assert.deepEqual(analizar(salida), [
    { ruta: 'AGENTS.md', numero: 12, linea: `Un a${ENIE_ROTA}o` },
  ]);
});

test('analizar conserva los dos puntos que vengan dentro del contenido', () => {
  const salida = `docs/ai/README.md:3:nota: a${ENIE_ROTA}o\n`;
  assert.equal(analizar(salida)[0].linea, `nota: a${ENIE_ROTA}o`);
});

test('analizar descarta las líneas exentas por span de código', () => {
  const salida = `docs/ai/PUERTA_CALIDAD.md:7:cita \`${ENIE_ROTA}\`\n`;
  assert.deepEqual(analizar(salida), []);
});

test('analizar ignora una salida vacía', () => {
  assert.deepEqual(analizar(''), []);
});
