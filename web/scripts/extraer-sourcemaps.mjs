#!/usr/bin/env node
// Extrae los source maps generados por Vite a un artefacto privado por release,
// para que el directorio público nunca los sirva (spec: source maps privados).
// Ejecutar desde web/ tras un build: node scripts/extraer-sourcemaps.mjs
// Variables: ICARUS_DIST (dist por defecto), ICARUS_RELEASE (VITE_RELEASE o
// development), ICARUS_SOURCEMAPS_DIR (sourcemaps/<release> fuera de dist).
import { readdirSync, cpSync, rmSync, mkdirSync, statSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const raizWeb = join(dirname(fileURLToPath(import.meta.url)), '..');
const dist = process.env.ICARUS_DIST ?? join(raizWeb, 'dist');
const release = sanitizarRelease(
  process.env.ICARUS_RELEASE ?? process.env.VITE_RELEASE ?? 'development',
);
const salida = process.env.ICARUS_SOURCEMAPS_DIR ?? join(raizWeb, '..', 'sourcemaps', release);

function sanitizarRelease(valor) {
  const limpio = (valor ?? '').replace(/[^A-Za-z0-9._-]/g, '').slice(0, 40);
  return limpio === '' ? 'development' : limpio;
}

function buscarMaps(directorio) {
  const encontrados = [];
  for (const entrada of readdirSync(directorio, { withFileTypes: true })) {
    const ruta = join(directorio, entrada.name);
    if (entrada.isDirectory()) {
      encontrados.push(...buscarMaps(ruta));
    } else if (entrada.name.endsWith('.map')) {
      encontrados.push(ruta);
    }
  }
  return encontrados;
}

function ejecutar() {
  if (!statSync(dist, { throwIfNoEntry: false })?.isDirectory()) {
    console.log(`No existe ${dist}; no hay source maps que extraer.`);
    return 0;
  }

  const maps = buscarMaps(dist);
  if (maps.length === 0) {
    console.log(`Sin source maps en ${dist}; nada que extraer.`);
    return 0;
  }

  mkdirSync(salida, { recursive: true });
  for (const mapa of maps) {
    const relativa = mapa.slice(dist.length + 1);
    const destino = join(salida, relativa);
    mkdirSync(dirname(destino), { recursive: true });
    cpSync(mapa, destino);
    rmSync(mapa);
  }
  console.log(`Extraídos ${maps.length} source maps a ${salida}`);
  return 0;
}

process.exitCode = ejecutar();
