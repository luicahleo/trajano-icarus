// Pruebas del script de extracción de source maps privados.
// Ejecutar desde web/: node --test scripts/extraer-sourcemaps.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, existsSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

function prepararDist() {
  const base = mkdtempSync(join(tmpdir(), 'sourcmaps-'));
  const dist = join(base, 'dist', 'assets');
  mkdirSync(dist, { recursive: true });
  return { base, dist };
}

function ejecutar(env) {
  return spawnSync(process.execPath, ['scripts/extraer-sourcemaps.mjs'], {
    cwd: process.cwd(),
    env: { ...process.env, ...env },
    encoding: 'utf8',
  });
}

test('extrae los source maps a un artefacto privado por release y deja dist limpio', () => {
  const { base, dist } = prepararDist();
  const salida = join(base, 'sourcemaps', 'v1.2.3');
  writeFileSync(join(dist, 'index.js'), 'console.log(1)');
  writeFileSync(join(dist, 'index.js.map'), '{}');
  writeFileSync(join(dist, 'login.js.map'), '{}');

  const resultado = ejecutar({
    ICARUS_DIST: dist,
    ICARUS_RELEASE: 'v1.2.3',
    ICARUS_SOURCEMAPS_DIR: salida,
  });

  assert.equal(resultado.status, 0, resultado.stderr);
  assert.equal(existsSync(join(salida, 'index.js.map')), true);
  assert.equal(existsSync(join(salida, 'login.js.map')), true);
  assert.equal(existsSync(join(dist, 'index.js.map')), false);
  assert.equal(existsSync(join(dist, 'login.js.map')), false);
  assert.equal(existsSync(join(dist, 'index.js')), true);
  rmSync(base, { recursive: true, force: true });
});

test('usa desarrollo como release por defecto', () => {
  const { base, dist } = prepararDist();
  const salida = join(base, 'sourcemaps', 'development');
  writeFileSync(join(dist, 'index.js.map'), '{}');

  const resultado = ejecutar({ ICARUS_DIST: dist, ICARUS_SOURCEMAPS_DIR: salida });

  assert.equal(resultado.status, 0, resultado.stderr);
  assert.equal(existsSync(join(salida, 'index.js.map')), true);
  rmSync(base, { recursive: true, force: true });
});

test('no falla ni escribe cuando no hay source maps', () => {
  const { base, dist } = prepararDist();
  writeFileSync(join(dist, 'index.js'), 'console.log(1)');
  const salida = join(base, 'sourcemaps', 'v1');

  const resultado = ejecutar({ ICARUS_DIST: dist, ICARUS_SOURCEMAPS_DIR: salida });

  assert.equal(resultado.status, 0, resultado.stderr);
  assert.equal(existsSync(join(salida, 'index.js.map')), false);
  assert.equal(existsSync(join(dist, 'index.js')), true);
  rmSync(base, { recursive: true, force: true });
});

test('sanea el nombre del release para impedir rutas ajenas', () => {
  const { base, dist } = prepararDist();
  const salida = join(base, 'sourcemaps');
  writeFileSync(join(dist, 'index.js.map'), '{}');

  const resultado = ejecutar({
    ICARUS_DIST: dist,
    ICARUS_RELEASE: '../secreto',
    ICARUS_SOURCEMAPS_DIR: salida,
  });

  assert.equal(resultado.status, 0, resultado.stderr);
  assert.equal(existsSync(join(salida, 'secreto', 'index.js.map')), false);
  rmSync(base, { recursive: true, force: true });
});
