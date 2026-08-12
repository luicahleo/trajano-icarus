#!/usr/bin/env node
// Hook PostToolUse: cuando se edita AGENTS.md, regenera los adaptadores.
// Sin esto, el gate de adaptadores falla por olvido en vez de por un problema
// real. Nunca falla el hook: un error acá no debe bloquear la sesión.

import { spawnSync } from 'node:child_process';
import { join } from 'node:path';

const raiz = process.env.CLAUDE_PROJECT_DIR ?? process.cwd();

let crudo = '';
process.stdin.setEncoding('utf8');
for await (const trozo of process.stdin) crudo += trozo;

let evento;
try {
  // trim() antes de parsear: descarta el BOM y el salto final que agregan
  // algunas tuberías (PowerShell 5.1, notablemente). Sin esto, JSON.parse
  // lanzaría y el hook saldría en silencio sin haber hecho nada.
  evento = JSON.parse(crudo.trim());
} catch {
  process.exit(0);
}

const archivo = evento?.tool_input?.file_path;
if (typeof archivo !== 'string') process.exit(0);

const normalizado = archivo.replace(/\\/g, '/');
if (!normalizado.endsWith('/AGENTS.md') && normalizado !== 'AGENTS.md') process.exit(0);

spawnSync(process.execPath, [join(raiz, 'quality', 'generar-adaptadores.mjs')], {
  cwd: raiz,
  stdio: 'ignore',
});
process.exit(0);
