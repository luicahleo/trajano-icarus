#!/usr/bin/env node
// Gate de tareas: rehace cada bitácora de docs/ai/tareas/. Falla si la historia
// no es legal, si una aprobación no es humana o si aparece una credencial.

import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { detectarSecretos, validarBitacora } from './lib/tareas.mjs';
import { exito, fallo } from './lib/salida.mjs';

export function revisarTarea(bitacora, tareaMd) {
  const problemas = [];
  const eventos = [];

  bitacora.split('\n').filter(Boolean).forEach((linea, i) => {
    try {
      eventos.push(JSON.parse(linea));
    } catch {
      problemas.push(`Línea ${i + 1}: no es JSON válido.`);
    }
  });

  if (problemas.length === 0) problemas.push(...validarBitacora(eventos));
  if (detectarSecretos(bitacora) || detectarSecretos(tareaMd)) {
    problemas.push('Contiene una posible credencial.');
  }
  return problemas;
}

if (process.argv[1] && process.argv[1].endsWith('check-tareas.mjs')) {
  const dir = fileURLToPath(new URL('../docs/ai/tareas/', import.meta.url));
  const tareas = readdirSync(dir, { withFileTypes: true }).filter((e) => e.isDirectory());
  const leer = (ruta) => (existsSync(ruta) ? readFileSync(ruta, 'utf8') : '');

  const fallidas = tareas.flatMap(({ name }) => {
    const problemas = revisarTarea(leer(join(dir, name, 'bitacora.jsonl')), leer(join(dir, name, 'tarea.md')));
    return problemas.length > 0 ? [{ name, problemas }] : [];
  });

  if (fallidas.length > 0) {
    console.log(fallo(`Gate de tareas: ${fallidas.length} tarea(s) con problemas`));
    for (const { name, problemas } of fallidas) {
      for (const p of problemas) console.log(`       ${name}: ${p}`);
    }
    process.exit(1);
  }

  console.log(exito(`Gate de tareas: ${tareas.length} tarea(s) legales.`));
}
