#!/usr/bin/env node
// Escribe los adaptadores declarados en el manifiesto. Idempotente: si el
// archivo ya coincide, no lo toca, así que una segunda corrida deja el árbol
// limpio.

import { mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ADAPTADORES } from './adaptadores/manifiesto.mjs';
import { exito } from './lib/salida.mjs';

export function generar(adaptadores, raiz) {
  const resultados = [];
  for (const { ruta, contenido } of adaptadores) {
    const destino = join(raiz, ruta);
    const actual = existsSync(destino) ? readFileSync(destino, 'utf8') : null;

    if (actual === contenido) {
      resultados.push({ ruta, accion: 'sin-cambios' });
      continue;
    }
    mkdirSync(dirname(destino), { recursive: true });
    // writeFileSync con utf8 no escribe BOM: es justo lo que hace falta.
    writeFileSync(destino, contenido, 'utf8');
    resultados.push({ ruta, accion: 'escrito' });
  }
  return resultados;
}

if (process.argv[1] && process.argv[1].endsWith('generar-adaptadores.mjs')) {
  const raiz = fileURLToPath(new URL('..', import.meta.url));
  const resultados = generar(ADAPTADORES, raiz);
  const escritos = resultados.filter((r) => r.accion === 'escrito');

  for (const r of escritos) console.log(`       ${r.ruta}`);
  console.log(
    exito(
      `Adaptadores generados: ${escritos.length} escrito(s), ` +
        `${resultados.length - escritos.length} sin cambios.`,
    ),
  );
}
