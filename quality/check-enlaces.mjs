#!/usr/bin/env node
// Gate de enlaces: en los .md versionados, todo enlace relativo debe apuntar a
// un archivo existente. Los enlaces http(s) no se comprueban: verificar la red
// haría el gate lento y no determinista.

import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { ejecutar } from './lib/ejecutar.mjs';
import { exito, fallo } from './lib/salida.mjs';

const ENLACE = /\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g;

export function extraerEnlaces(contenido) {
  const enlaces = [];
  let dentroDeBloque = false;

  const lineas = contenido.split('\n');
  for (let i = 0; i < lineas.length; i += 1) {
    const linea = lineas[i];

    if (/^\s*(```|~~~)/.test(linea)) {
      dentroDeBloque = !dentroDeBloque;
      continue;
    }
    if (dentroDeBloque) continue;

    // Un enlace citado entre acentos graves es documentación del formato,
    // no una referencia a comprobar.
    const util = linea.replace(/`[^`]*`/g, '');
    for (const coincidencia of util.matchAll(ENLACE)) {
      enlaces.push({ numero: i + 1, destino: coincidencia[1] });
    }
  }
  return enlaces;
}

export function esRelativo(destino) {
  return !/^([a-z][a-z0-9+.-]*:|#|\/\/)/i.test(destino);
}

export function rutaDelDestino(destino) {
  const sinAncla = destino.split('#')[0].split('?')[0];
  try {
    return decodeURIComponent(sinAncla);
  } catch {
    return sinAncla;
  }
}

export function archivosMarkdown() {
  const { codigo, salida } = ejecutar('git', ['ls-files', '-z', '--', '*.md'], {
    silencioso: true,
    sinShell: true,
  });
  if (codigo !== 0) {
    return { ok: false, motivo: `git ls-files terminó con código ${codigo}` };
  }
  return { ok: true, archivos: salida.split('\0').filter((r) => r !== '') };
}

if (process.argv[1] && process.argv[1].endsWith('check-enlaces.mjs')) {
  const listado = archivosMarkdown();
  if (!listado.ok) {
    console.log(fallo('Gate de enlaces', listado.motivo));
    process.exit(1);
  }

  const rotos = [];
  for (const archivo of listado.archivos) {
    const contenido = await readFile(archivo, 'utf8');
    for (const { numero, destino } of extraerEnlaces(contenido)) {
      if (!esRelativo(destino)) continue;
      const ruta = rutaDelDestino(destino);
      if (ruta === '') continue; // ancla pura dentro del mismo documento
      if (!existsSync(resolve(dirname(archivo), ruta))) {
        rotos.push({ archivo, numero, destino });
      }
    }
  }

  if (rotos.length > 0) {
    console.log(fallo(`Gate de enlaces: ${rotos.length} enlace(s) roto(s)`));
    for (const r of rotos) {
      console.log(`       ${r.archivo}:${r.numero} -> ${r.destino}`);
    }
    console.log('       Corregí el enlace o creá el destino.');
    process.exit(1);
  }

  console.log(
    exito(`Gate de enlaces: ${listado.archivos.length} archivo(s) .md sin enlaces rotos.`),
  );
}
