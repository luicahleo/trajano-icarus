#!/usr/bin/env node
// Gate de mojibake: ningún archivo de texto versionado puede contener el
// carácter de reemplazo ni las secuencias que delatan UTF-8 leído como Latin-1.
//
// Los patrones se escriben con escapes unicode y de byte a propósito: escritos
// como caracteres literales, este archivo se marcaría a sí mismo.

import { ejecutar } from './lib/ejecutar.mjs';
import { exito, fallo } from './lib/salida.mjs';

// U+FFFD (reemplazo), U+00C3 y U+00C2 (primer carácter de casi todo mojibake).
export const PATRONES = ['\uFFFD', '\u00C3', '\u00C2'];

// Los mismos patrones para git grep -P, escritos como puntos de código con la
// forma \x{XXXX}. El PCRE2 de git trabaja en modo UTF-8: \x{00C3} denota el
// carácter U+00C3, cuya codificación es la pareja de bytes 0xC3 0x83, que es
// inequívoca. No se escribe el byte 0xC3 suelto, porque ese byte inicia también
// las vocales acentuadas y la letra ene correctas del español.
const PATRON_PCRE = '\\x{FFFD}|\\x{00C3}|\\x{00C2}';

// Un mojibake accidental nunca está entre acentos graves; una cita deliberada
// del patrón sí. Solo aplica a Markdown, donde el span de código existe.
export function sinSpansDeCodigo(linea) {
  return linea.replace(/`[^`]*`/g, '');
}

export function lineaTieneMojibake(ruta, linea) {
  const texto = ruta.endsWith('.md') ? sinSpansDeCodigo(linea) : linea;
  return PATRONES.some((patron) => texto.includes(patron));
}

export function analizar(salidaGitGrep) {
  const hallazgos = [];
  for (const fila of salidaGitGrep.split('\n')) {
    if (fila.trim() === '') continue;
    const primero = fila.indexOf(':');
    if (primero === -1) continue;
    const segundo = fila.indexOf(':', primero + 1);
    if (segundo === -1) continue;

    const ruta = fila.slice(0, primero);
    const numero = Number(fila.slice(primero + 1, segundo));
    const linea = fila.slice(segundo + 1);
    if (!Number.isInteger(numero)) continue;
    if (lineaTieneMojibake(ruta, linea)) hallazgos.push({ ruta, numero, linea });
  }
  return hallazgos;
}

// Punto de entrada CLI. Al importarse como módulo (tests) no se ejecuta.
if (process.argv[1] && process.argv[1].endsWith('check-mojibake.mjs')) {
  const { codigo, salida } = ejecutar(
    'git',
    ['grep', '-I', '-n', '-P', PATRON_PCRE],
    { silencioso: true, sinShell: true },
  );

  // git grep: 0 = hubo coincidencias, 1 = ninguna, >1 = error real.
  if (codigo > 1) {
    console.log(
      fallo(
        'Gate de mojibake',
        `git grep terminó con código ${codigo}. Si el mensaje menciona PCRE, ` +
          'este git no soporta -P y hay que instalar uno que sí.',
      ),
    );
    console.log(salida);
    process.exit(1);
  }

  const hallazgos = codigo === 0 ? analizar(salida) : [];

  if (hallazgos.length > 0) {
    console.log(fallo(`Gate de mojibake: ${hallazgos.length} hallazgo(s)`));
    for (const h of hallazgos) {
      console.log(`       ${h.ruta}:${h.numero}: ${h.linea.trim()}`);
    }
    console.log(
      '       Escribí el carácter correcto en UTF-8. Si necesitás citar el ' +
        'patrón en un .md, ponelo entre acentos graves.',
    );
    process.exit(1);
  }

  console.log(exito('Gate de mojibake: sin hallazgos.'));
}
