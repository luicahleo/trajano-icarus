#!/usr/bin/env node
// Hook PreToolUse común a Claude Code y Kimi Code. Es un cinturón contra errores
// y atajos, no una frontera de seguridad: un agente con shell podría sortearlo.
// Fuera de una tarea activa solo bloquea --no-verify, para no interferir con
// las sesiones normales, incluidas las que el usuario autoriza a desplegar.

import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const SEGMENTO = '[^\\n;|&]*';
const GIT_PUSH = `\\bgit(?:\\s+-\\S+(?:\\s+\\S+)?)*\\s+push\\b${SEGMENTO}`;

const SIEMPRE = [
  { patron: /(?:^|\s)--no-verify(?:\s|$)/, motivo: '--no-verify está prohibido.' },
];

const CON_TAREA = [
  { patron: new RegExp(`${GIT_PUSH}\\bmaster\\b`), motivo: 'Push a master: requiere pedido explícito fuera de una tarea.' },
  { patron: new RegExp(`${GIT_PUSH}(?:\\s--force\\b|\\s-f\\b)`), motivo: 'Push forzado prohibido.' },
  { patron: /deploy-produccion/, motivo: 'El despliegue no está habilitado para agentes.' },
  { patron: new RegExp(`\\bgh\\b${SEGMENTO}\\bworkflow\\b${SEGMENTO}\\brun\\b`), motivo: 'Lanzar workflows no está habilitado para agentes.' },
  { patron: new RegExp(`\\bgit\\b${SEGMENTO}\\breset\\b${SEGMENTO}--hard`), motivo: 'git reset --hard destruye cambios ajenos.' },
  { patron: new RegExp(`\\bgit\\s+clean\\b${SEGMENTO}\\s-[a-z]*f`), motivo: 'git clean destruye cambios ajenos.' },
  { patron: /-RecrearDatos|-ConfirmarBorradoDatos/, motivo: 'Borrar los datos locales requiere tu pedido explícito.' },
  { patron: new RegExp(`\\bdocker\\b${SEGMENTO}\\bdown\\b${SEGMENTO}(?:--volumes|\\s-v\\b)`), motivo: 'Borrar volúmenes requiere tu pedido explícito.' },
  { patron: /\bdocker\s+(?:volume\s+(?:rm|prune)|system\s+prune)\b/, motivo: 'Borrar volúmenes requiere tu pedido explícito.' },
  { patron: /tarea\.mjs\s+aprobar\b/, motivo: 'Solo el humano aprueba, desde su terminal.' },
];

const LECTURA_ENV = /(?:^|[\s/\\'"=])\.env(?=$|[\s'"])/;
const ES_COMMIT = /^\s*git\s+commit\b/;

const permitir = { permitido: true };
const denegar = (motivo) => ({ permitido: false, motivo });

export function evaluarComando(comando, { tareaActiva }) {
  const reglas = tareaActiva ? [...SIEMPRE, ...CON_TAREA] : SIEMPRE;
  const regla = reglas.find(({ patron }) => patron.test(comando));
  if (regla) return denegar(regla.motivo);
  // Un mensaje de commit puede nombrar .env sin leerlo.
  if (tareaActiva && LECTURA_ENV.test(comando) && !ES_COMMIT.test(comando)) {
    return denegar('.env contiene credenciales: ningún agente lo lee.');
  }
  return permitir;
}

export function evaluarEvento(evento, contexto) {
  const entrada = evento?.tool_input ?? {};
  if (evento?.tool_name === 'Bash' || evento?.tool_name === 'PowerShell') {
    return evaluarComando(entrada.command ?? '', contexto);
  }
  if (evento?.tool_name === 'Read' && contexto.tareaActiva) {
    const ruta = entrada.file_path ?? entrada.path ?? '';
    if (ruta.split(/[\\/]/).pop() === '.env') {
      return denegar('.env contiene credenciales: ningún agente lo lee.');
    }
  }
  return permitir;
}

const normalizar = (ruta) => ruta.replace(/\\/g, '/').replace(/\/$/, '').toLowerCase();

async function principal() {
  const raiz = fileURLToPath(new URL('..', import.meta.url));

  let crudo = '';
  process.stdin.setEncoding('utf8');
  for await (const trozo of process.stdin) crudo += trozo;

  let evento;
  try {
    // trim(): descarta el BOM y el salto final que agregan algunas tuberías.
    evento = JSON.parse(crudo.trim());
  } catch {
    return;
  }

  // El hook de Kimi es global: fuera de este repositorio no aplica la tarea.
  const enRepositorio = !evento.cwd || normalizar(evento.cwd).startsWith(normalizar(raiz));
  const tareaActiva = enRepositorio && existsSync(join(raiz, '.local/agentes/tarea-activa.json'));

  const { permitido, motivo } = evaluarEvento(evento, { tareaActiva });
  if (!permitido) {
    process.stderr.write(`Guardia de agentes: ${motivo}\n`);
    process.exitCode = 2;
  }
}

if (process.argv[1] && process.argv[1].endsWith('guardia-comandos.mjs')) {
  await principal();
}
