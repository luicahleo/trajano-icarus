import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { cargarGates, resolverGate, validarConfiguracion } from '../lib/gates.mjs';
import { ejecutarGates } from '../verify.mjs';

const QUALITY = join(dirname(fileURLToPath(import.meta.url)), '..');

test('una configuración válida no tiene problemas', () => {
  const datos = {
    gates: [
      { nombre: 'A', tipo: 'node', args: ['a.mjs'], motivo: 'Va primero.' },
      { nombre: 'B', tipo: 'npm', script: 'lint', cwd: 'web' },
      { nombre: 'C', tipo: 'dotnet', args: ['build', 'x.sln'], cwd: 'sub/dir' },
    ],
  };
  assert.deepEqual(validarConfiguracion(datos), []);
});

test('una configuración inválida se rechaza con un motivo por cada problema', () => {
  const problemas = validarConfiguracion({
    gates: [
      { nombre: 'A', tipo: 'bash', args: ['x'] },
      { nombre: 'B', tipo: 'node', args: [] },
      { nombre: 'B', tipo: 'node', args: ['x'] },
      { nombre: 'C', tipo: 'npm' },
      { nombre: 'D', tipo: 'node', args: ['x'], omitir: true },
    ],
    extra: 1,
  });
  assert.deepEqual(problemas, [
    'Clave no admitida: extra.',
    'Gate 1 (A): tipo desconocido: bash. Usa node, npm, dotnet.',
    'Gate 2 (B): args debe ser una lista no vacía de textos.',
    'Gate 3: nombre repetido: B.',
    'Gate 4 (C): falta el script de npm.',
    'Gate 5 (D): clave no admitida para node: omitir.',
  ]);
  assert.match(validarConfiguracion({ gates: [] }).join(), /vacía/);
  assert.match(validarConfiguracion([]).join(), /objeto/);
  assert.match(validarConfiguracion({ gates: [null] }).join(), /Gate 1: no es un objeto/);
});

test('un cwd absoluto o con .. se rechaza', () => {
  for (const cwd of ['/tmp', 'C:\\web', '../otro', 'web/../..', '']) {
    const problemas = validarConfiguracion({ gates: [{ nombre: 'A', tipo: 'node', args: ['x'], cwd }] });
    assert.deepEqual(problemas, ['Gate 1 (A): cwd debe ser una ruta relativa dentro del repositorio.'], cwd);
  }
});

test('cargarGates falla cerrado si el archivo falta o no es JSON', () => {
  const raiz = mkdtempSync(join(tmpdir(), 'gates-'));
  try {
    const faltante = cargarGates(raiz);
    assert.deepEqual(faltante, { gates: [], problemas: ['No existe quality/gates.json.'] });

    mkdirSync(join(raiz, 'quality'));
    writeFileSync(join(raiz, 'quality/gates.json'), '{ "gates": [ ');
    const roto = cargarGates(raiz);
    assert.deepEqual(roto.gates, []);
    assert.match(roto.problemas.join(), /no es JSON válido/);

    writeFileSync(join(raiz, 'quality/gates.json'), JSON.stringify({ gates: [{ nombre: 'A', tipo: 'bash', args: ['x'] }] }));
    assert.deepEqual(cargarGates(raiz).gates, [], 'con un gate inválido no devuelve ninguno');

    const valido = { nombre: 'A', tipo: 'node', args: ['a.mjs'] };
    writeFileSync(join(raiz, 'quality/gates.json'), JSON.stringify({ gates: [valido] }));
    assert.deepEqual(cargarGates(raiz), { gates: [valido], problemas: [] });
  } finally {
    rmSync(raiz, { recursive: true, force: true });
  }
});

test('un gate node o dotnet se resuelve con su comando y sus argumentos', () => {
  const contexto = { raiz: '/repo', plataforma: 'linux', execPath: '/opt/node/bin/node', existe: () => false };
  assert.deepEqual(resolverGate({ nombre: 'A', tipo: 'node', args: ['a.mjs'] }, contexto), {
    nombre: 'A',
    comando: 'node',
    args: ['a.mjs'],
  });
  assert.deepEqual(resolverGate({ nombre: 'B', tipo: 'dotnet', args: ['build', 'x.sln'], cwd: 'sub' }, contexto), {
    nombre: 'B',
    comando: 'dotnet',
    args: ['build', 'x.sln'],
    cwd: resolve('/repo', 'sub'),
  });
});

test('un gate npm usa el CLI de npm con node en Windows y npm en POSIX (el ejecutor solo aplica shell en win32)', () => {
  const gate = { nombre: 'Lint', tipo: 'npm', script: 'lint', cwd: 'web' };
  const base = { raiz: '/repo', execPath: '/opt/node/node.exe' };

  const windows = resolverGate(gate, { ...base, plataforma: 'win32', existe: () => true });
  assert.equal(windows.comando, '/opt/node/node.exe');
  assert.match(windows.args[0], /npm-cli\.js$/);
  assert.deepEqual(windows.args.slice(1), ['run', 'lint']);
  assert.equal(windows.cwd, resolve('/repo', 'web'));
  assert.equal(windows.shell, undefined);

  const posix = resolverGate(gate, { ...base, plataforma: 'linux', existe: () => true });
  assert.deepEqual(posix, { nombre: 'Lint', comando: 'npm', args: ['run', 'lint'], cwd: resolve('/repo', 'web'), shell: true });

  const windowsSinCli = resolverGate(gate, { ...base, plataforma: 'win32', existe: () => false });
  assert.equal(windowsSinCli.comando, 'npm');
  assert.equal(windowsSinCli.shell, true);
});

test('sin gates la puerta no da verde', () => {
  let llamadas = 0;
  const resultado = ejecutarGates([], () => {
    llamadas += 1;
    return { codigo: 0 };
  });
  assert.deepEqual(resultado, { ok: false, ejecutados: [] });
  assert.equal(llamadas, 0);
});

test('el ejecutor no nombra rutas de este proyecto', () => {
  const PROPIOS = [/Icarus/, /Trajano/, /\bweb\b/, /\.sln\b/];
  for (const archivo of ['verify.mjs', 'lib/gates.mjs']) {
    const fuente = readFileSync(join(QUALITY, archivo), 'utf8');
    for (const propio of PROPIOS) assert.doesNotMatch(fuente, propio, `${archivo} nombra ${propio}`);
  }
});

test('el script de un gate npm solo admite letras, números, guion, guion bajo y dos puntos', () => {
  const con = (script) => validarConfiguracion({ gates: [{ nombre: 'L', tipo: 'npm', script }] });
  for (const valido of ['lint', 'test:unit', 'build-web', 'a_b']) assert.deepEqual(con(valido), [], valido);
  for (const invalido of ['lint & dir', 'lint; x', 'a b', '$(x)', 'a|b', '../x', '', 7]) {
    const problemas = con(invalido);
    assert.equal(problemas.length, 1, String(invalido));
    assert.match(problemas[0], /Gate 1 \(L\): .*script/, String(invalido));
  }
});
