#!/usr/bin/env node
// Puerta de calidad de Trajano-Icarus.
//   node quality/verify.mjs
// Ejecuta los gates en orden y se detiene en el primero que falla, para dar
// retroalimentación rápida. Los gates de backend necesitan el SDK de .NET 10;
// los tests de integración con Testcontainers (planes 2-3) necesitan Docker.

import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';
import { existsSync } from 'node:fs';
import { ejecutar } from './lib/ejecutar.mjs';
import { titulo, exito, fallo } from './lib/salida.mjs';

// fileURLToPath, no .pathname: en Windows .pathname produce "/C:/..." y rompe spawn.
const raiz = fileURLToPath(new URL('..', import.meta.url));
const web = resolve(raiz, 'web');

// npm en Windows es npm.cmd: lanzarlo con shell dispara el aviso DEP0190 de
// Node (args concatenados sin escapar). Se invoca el CLI de npm con el propio
// node, sin shell. En POSIX npm es un binario directo.
const npmCli = join(dirname(process.execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js');
const gateNpm = (script) =>
  process.platform === 'win32' && existsSync(npmCli)
    ? { comando: process.execPath, args: [npmCli, 'run', script], cwd: web }
    : { comando: 'npm', args: ['run', script], cwd: web, shell: true };

// Los argumentos posicionales de `node --test` son patrones glob, no rutas de
// directorio: un directorio suelto se intentaría cargar como módulo y fallaría.
const PATRON_TESTS = 'quality/__tests__/*.test.mjs';

export const GATES = [
  // Va primero: si la propia puerta está rota, el resto de los veredictos no
  // vale nada.
  { nombre: 'Tests de la puerta', comando: 'node', args: ['--test', PATRON_TESTS] },
  { nombre: 'Adaptadores', comando: 'node', args: ['quality/check-adaptadores.mjs'] },
  { nombre: 'Mojibake', comando: 'node', args: ['quality/check-mojibake.mjs'] },
  { nombre: 'Enlaces', comando: 'node', args: ['quality/check-enlaces.mjs'] },
  { nombre: 'Frontend lint', ...gateNpm('lint') },
  { nombre: 'Frontend build', ...gateNpm('build') },
  { nombre: 'Frontend tests', ...gateNpm('test') },
  { nombre: 'Backend build', comando: 'dotnet', args: ['build', 'Icarus/Icarus.sln', '--nologo'] },
  { nombre: 'Backend tests', comando: 'dotnet', args: ['test', 'Icarus/Icarus.sln', '--nologo', '--no-build'] },
];

export function ejecutarGates(gates, ejecutor) {
  const ejecutados = [];
  for (const gate of gates) {
    const { codigo } = ejecutor(gate);
    ejecutados.push({ nombre: gate.nombre, codigo });
    if (codigo !== 0) return { ok: false, ejecutados };
  }
  return { ok: true, ejecutados };
}

if (process.argv[1] && process.argv[1].endsWith('verify.mjs')) {
  const resultado = ejecutarGates(GATES, (gate) => {
    console.log(titulo(gate.nombre));
    const { cwd = raiz, shell = false } = gate;
    const { codigo, duracionMs } = ejecutar(gate.comando, gate.args, { cwd, sinShell: shell ? false : true });
    if (codigo === 0) console.log(exito(`${gate.nombre} (${duracionMs} ms)`));
    return { codigo };
  });

  if (!resultado.ok) {
    const ultimo = resultado.ejecutados.at(-1);
    console.log(fallo(`${ultimo.nombre} falló`, `código ${ultimo.codigo}`));
    console.log(fallo('La puerta de calidad no pasó. Arreglá el contenido, no el gate.'));
    process.exit(1);
  }

  console.log(exito('Puerta de calidad: verde.'));
}
