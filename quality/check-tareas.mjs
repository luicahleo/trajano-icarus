#!/usr/bin/env node
// Gate de tareas: rehace cada bitácora de docs/ai/tareas/, valida las fichas de
// cierre y comprueba que INDICE.md coincida con ellas. Falla si una historia no
// es legal, si una aprobación no es humana, si aparece una credencial, si una
// tarea cerrada no tiene ficha válida o si el índice no está al día. La CLI usa
// las mismas funciones para negar el cierre.

import { existsSync, readdirSync, readFileSync, realpathSync, statSync } from 'node:fs';
import { isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { detectarSecretos, reconstruir, validarBitacora } from './lib/tareas.mjs';
import { generarIndice, leerFicha, rutaRelativaAdmitida, validarFicha } from './lib/fichas.mjs';
import { exito, fallo } from './lib/salida.mjs';

const DIR_TAREAS = 'docs/ai/tareas';
const ARCHIVO_INDICE = 'INDICE.md';

const leer = (ruta) => (existsSync(ruta) ? readFileSync(ruta, 'utf8') : null);

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

function datosDeBitacora(bitacora) {
  try {
    const tarea = reconstruir(bitacora.split('\n').filter(Boolean).map((l) => JSON.parse(l)));
    return { estado: tarea.estado, aprobado: tarea.aprobacion?.commit };
  } catch {
    return { estado: null, aprobado: undefined };
  }
}

export function revisarFicha({ id, estado, aprobado, ficha, existe }) {
  if (ficha === null) {
    return estado === 'CERRADA' ? ['Falta ficha.md: toda tarea cerrada necesita su ficha.'] : [];
  }
  return validarFicha(ficha, { id, aprobado, existe }).map((p) => `ficha.md: ${p}`);
}

const idsDeTareas = (raiz) =>
  readdirSync(join(raiz, DIR_TAREAS), { withFileTypes: true })
    .filter((e) => e.isDirectory())
    .map((e) => e.name)
    .sort();

// Comprobación de los archivos clave: archivo regular dentro de la raíz, fuera de
// .git, sin rutas absolutas ni «..», y sin escapar por enlaces simbólicos. No exige
// que el archivo esté versionado.
export function existeArchivoEnRepositorio(raiz) {
  return (ruta) => {
    if (!rutaRelativaAdmitida(ruta)) return false;
    try {
      const base = realpathSync(raiz);
      const real = realpathSync(resolve(base, ruta));
      const rel = relative(base, real);
      if (!rel || isAbsolute(rel) || rel.split(sep).some((s) => s === '..' || s.toLowerCase() === '.git')) return false;
      return statSync(real).isFile();
    } catch {
      return false;
    }
  };
}

export function revisarDocumentacion(raiz, id, { estado, aprobado }) {
  return revisarFicha({
    id,
    estado,
    aprobado,
    ficha: leer(join(raiz, DIR_TAREAS, id, 'ficha.md')),
    existe: existeArchivoEnRepositorio(raiz),
  });
}

export function indiceEsperado(raiz) {
  const metas = idsDeTareas(raiz).flatMap((id) => {
    const texto = leer(join(raiz, DIR_TAREAS, id, 'ficha.md'));
    const ficha = texto === null ? null : leerFicha(texto);
    return ficha ? [ficha.meta] : [];
  });
  return generarIndice(metas);
}

export function revisarIndice(raiz) {
  const actual = leer(join(raiz, DIR_TAREAS, ARCHIVO_INDICE))?.replace(/\r\n/g, '\n');
  return actual === indiceEsperado(raiz)
    ? []
    : [`${ARCHIVO_INDICE} no está al día: ejecuta node quality/tarea.mjs indice.`];
}

export function revisarRepositorio(raiz) {
  const ids = idsDeTareas(raiz);
  const fallidas = ids.flatMap((id) => {
    const dir = join(raiz, DIR_TAREAS, id);
    const bitacora = leer(join(dir, 'bitacora.jsonl')) ?? '';
    const problemas = [
      ...revisarTarea(bitacora, leer(join(dir, 'tarea.md')) ?? ''),
      ...revisarDocumentacion(raiz, id, datosDeBitacora(bitacora)),
    ];
    return problemas.length > 0 ? [{ name: id, problemas }] : [];
  });

  const indice = revisarIndice(raiz);
  if (indice.length > 0) fallidas.push({ name: ARCHIVO_INDICE, problemas: indice });
  return { total: ids.length, fallidas };
}

if (process.argv[1] && process.argv[1].endsWith('check-tareas.mjs')) {
  const { total, fallidas } = revisarRepositorio(fileURLToPath(new URL('..', import.meta.url)));

  if (fallidas.length > 0) {
    console.log(fallo(`Gate de tareas: ${fallidas.length} elemento(s) con problemas`));
    for (const { name, problemas } of fallidas) {
      for (const p of problemas) console.log(`       ${name}: ${p}`);
    }
    process.exit(1);
  }

  console.log(exito(`Gate de tareas: ${total} tarea(s) legales, con fichas e índice al día.`));
}
