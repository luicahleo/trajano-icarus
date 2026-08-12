#!/usr/bin/env node
// Puerta de calidad de Trajano-Icarus.
//   node quality/verify.mjs
// Ejecuta los gates en orden y se detiene en el primero que falla, para dar
// retroalimentación rápida. Ningún gate necesita Docker ni el SDK de .NET.

import { fileURLToPath } from 'node:url';
import { ejecutar } from './lib/ejecutar.mjs';
import { titulo, exito, fallo } from './lib/salida.mjs';

// fileURLToPath, no .pathname: en Windows .pathname produce "/C:/..." y rompe spawn.
const raiz = fileURLToPath(new URL('..', import.meta.url));

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
    const { codigo, duracionMs } = ejecutar(gate.comando, gate.args, { cwd: raiz, sinShell: true });
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
