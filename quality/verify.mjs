#!/usr/bin/env node
// Puerta de calidad de Trajano-Icarus.
//   node quality/verify.mjs
// Ejecuta los gates en orden y se detiene en el primero que falla, para dar
// retroalimentación rápida. Los gates de backend necesitan el SDK de .NET 10;
// los tests de integración con Testcontainers (planes 2-3) necesitan Docker.

import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { ejecutar } from './lib/ejecutar.mjs';
import { titulo, exito, fallo } from './lib/salida.mjs';

// fileURLToPath, no .pathname: en Windows .pathname produce "/C:/..." y rompe spawn.
const raiz = fileURLToPath(new URL('..', import.meta.url));
const web = resolve(raiz, 'web');

// npm en Windows es npm.cmd y necesita shell; dotnet/git no (se mantiene sinShell).
const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';

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
  { nombre: 'Frontend lint', comando: npm, args: ['run', 'lint'], cwd: web, shell: true },
  { nombre: 'Frontend build', comando: npm, args: ['run', 'build'], cwd: web, shell: true },
  { nombre: 'Frontend tests', comando: npm, args: ['run', 'test'], cwd: web, shell: true },
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
