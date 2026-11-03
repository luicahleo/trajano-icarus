#!/usr/bin/env node
// Puerta de calidad: ejecutor genérico y portable.
//   node quality/verify.mjs
// Lee los gates de quality/gates.json, los ejecuta en orden y se detiene en el
// primero que falla, para dar retroalimentación rápida. Lo propio de cada
// proyecto vive en esa configuración, no aquí.

import { fileURLToPath } from 'node:url';
import { existsSync } from 'node:fs';
import { ejecutar } from './lib/ejecutar.mjs';
import { RUTA_CONFIGURACION, cargarGates, resolverGate } from './lib/gates.mjs';
import { titulo, exito, fallo } from './lib/salida.mjs';

// fileURLToPath, no .pathname: en Windows .pathname produce "/C:/..." y rompe spawn.
const raiz = fileURLToPath(new URL('..', import.meta.url));
const configuracion = cargarGates(raiz);

export const GATES = configuracion.gates.map((gate) =>
  resolverGate(gate, { raiz, plataforma: process.platform, execPath: process.execPath, existe: existsSync }),
);

export function ejecutarGates(gates, ejecutor) {
  // Una lista vacía no es una puerta verde: es una puerta que no comprobó nada.
  if (gates.length === 0) return { ok: false, ejecutados: [] };
  const ejecutados = [];
  for (const gate of gates) {
    const { codigo } = ejecutor(gate);
    ejecutados.push({ nombre: gate.nombre, codigo });
    if (codigo !== 0) return { ok: false, ejecutados };
  }
  return { ok: true, ejecutados };
}

if (process.argv[1] && process.argv[1].endsWith('verify.mjs')) {
  if (configuracion.problemas.length > 0) {
    console.log(fallo(`La configuración de la puerta no es válida: ${RUTA_CONFIGURACION}`));
    for (const problema of configuracion.problemas) console.log(`       ${problema}`);
    console.log(fallo('La puerta de calidad no pasó. Arregla la configuración; no quites gates.'));
    process.exit(1);
  }

  const resultado = ejecutarGates(GATES, (gate) => {
    console.log(titulo(gate.nombre));
    const { cwd = raiz, shell = false } = gate;
    const { codigo, duracionMs } = ejecutar(gate.comando, gate.args, { cwd, sinShell: shell ? false : true });
    if (codigo === 0) console.log(exito(`${gate.nombre} (${duracionMs} ms)`));
    return { codigo };
  });

  if (!resultado.ok) {
    const ultimo = resultado.ejecutados.at(-1);
    if (ultimo) console.log(fallo(`${ultimo.nombre} falló`, `código ${ultimo.codigo}`));
    console.log(fallo('La puerta de calidad no pasó. Arregla el contenido, no el gate.'));
    process.exit(1);
  }

  console.log(exito('Puerta de calidad: verde.'));
}
