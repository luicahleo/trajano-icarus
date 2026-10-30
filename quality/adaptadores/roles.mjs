// Traduce los roles portables de docs/ai/agentes/roles/ al formato de agente de
// cada proveedor. Agregar un proveedor es agregar su función de traducción y
// sus entradas en adaptadoresDeRoles(). Los formatos están comprobados contra
// la documentación de Claude Code y de Kimi Code del 2026-10-30.

import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';

export const AVISO_ROL =
  'Archivo generado por quality/generar-adaptadores.mjs. No editar a mano: editar docs/ai/agentes/roles/.';

const DIR_ROLES = fileURLToPath(new URL('../../docs/ai/agentes/roles/', import.meta.url));

const HERRAMIENTAS_CLAUDE = {
  leer: ['Read'],
  buscar: ['Grep', 'Glob'],
  shell: ['Bash', 'PowerShell'],
  editar: ['Edit', 'Write'],
};

const HERRAMIENTAS_KIMI = {
  leer: ['Read'],
  buscar: ['Grep', 'Glob'],
  shell: ['Bash'],
  editar: ['Edit', 'Write'],
};

// Kimi no tiene campo de modelo por agente: el nivel se elige con kimi -m.
const MODELOS_CLAUDE = { alto: 'opus', intermedio: 'sonnet' };

function leerValor(crudo) {
  const lista = crudo.match(/^\[(.*)\]$/);
  if (!lista) return crudo;
  return lista[1].split(',').map((x) => x.trim()).filter(Boolean);
}

export function leerFrontmatter(texto) {
  const partes = texto.match(/^---\n([\s\S]*?)\n---\n+([\s\S]*)$/);
  if (!partes) throw new Error('El rol no tiene frontmatter.');

  const meta = {};
  for (const linea of partes[1].split('\n')) {
    const separador = linea.indexOf(':');
    meta[linea.slice(0, separador).trim()] = leerValor(linea.slice(separador + 1).trim());
  }
  return { meta, cuerpo: partes[2] };
}

function traducir(herramientas, tabla) {
  return herramientas.flatMap((h) => {
    if (!tabla[h]) throw new Error(`Herramienta desconocida: ${h}.`);
    return tabla[h];
  });
}

const componer = (cabecera, aviso, cuerpo) =>
  `---\n${cabecera.join('\n')}\n---\n\n<!-- ${aviso} -->\n\n${cuerpo.trimEnd()}\n`;

export function adaptadorClaude(fuente) {
  const { meta, cuerpo } = leerFrontmatter(fuente);
  const modelo = MODELOS_CLAUDE[meta.nivel];
  if (!modelo) throw new Error(`Nivel desconocido: ${meta.nivel}.`);

  const herramientas = traducir(meta.herramientas, HERRAMIENTAS_CLAUDE);
  if (meta.delega_en.length > 0) herramientas.push(`Agent(${meta.delega_en.join(', ')})`);

  return componer(
    [
      `name: ${meta.nombre}`,
      `description: ${meta.descripcion}. ${meta.cuando_usar}`,
      `tools: ${herramientas.join(', ')}`,
      `model: ${modelo}`,
    ],
    AVISO_ROL,
    cuerpo,
  );
}

const listaYaml = (clave, valores) =>
  valores.length === 0 ? [`${clave}: []`] : [`${clave}:`, ...valores.map((v) => `  - ${v}`)];

export function adaptadorKimi(fuente) {
  const { meta, cuerpo } = leerFrontmatter(fuente);
  const herramientas = traducir(meta.herramientas, HERRAMIENTAS_KIMI);
  if (meta.delega_en.length > 0) herramientas.push('Agent');

  return componer(
    [
      `name: ${meta.nombre}`,
      `description: ${meta.descripcion}`,
      `whenToUse: ${meta.cuando_usar}`,
      ...listaYaml('tools', herramientas),
      ...listaYaml('subagents', meta.delega_en),
    ],
    AVISO_ROL,
    cuerpo,
  );
}

export function adaptadoresDeRoles() {
  return readdirSync(DIR_ROLES)
    .filter((archivo) => archivo.endsWith('.md'))
    .sort()
    .flatMap((archivo) => {
      const fuente = readFileSync(join(DIR_ROLES, archivo), 'utf8');
      return [
        { harness: 'Claude Code', ruta: `.claude/agents/${archivo}`, contenido: adaptadorClaude(fuente) },
        { harness: 'Kimi Code', ruta: `.kimi-code/agents/${archivo}`, contenido: adaptadorKimi(fuente) },
      ];
    });
}
