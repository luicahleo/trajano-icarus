// Fichas de cierre de tarea: formato fijo y corto para que una sesión futura
// entienda una tarea sin releer su spec ni su plan. Lógica pura: quien llama
// inyecta la comprobación de rutas. Formato en docs/ai/tareas/README.md.

import { leerFrontmatter } from '../adaptadores/roles.mjs';
import { detectarSecretos } from './tareas.mjs';

export const TIPOS_FICHA = ['funcionalidad', 'correccion', 'calidad', 'configuracion', 'documentacion'];

export const MODULOS = [
  'Identity',
  'Clientes',
  'GestionAvicola',
  'ControlAcceso',
  'BuildingBlocks',
  'Host',
  'GestorCaisy',
  'web',
  'infra',
  'deploy',
  'quality',
  'docs',
];

export const SECCIONES_FICHA = [
  'Qué cambió',
  'Por qué',
  'Decisiones',
  'Archivos clave',
  'Cómo probarlo',
  'Pendientes',
  'Enlaces',
];

export const MARCADOR = 'COMPLETAR';

export const TOPES = { bytes: 4000, lineas: 60, resumenMin: 20, resumenMax: 160, temas: 8, archivos: 12 };

const TEMA = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const HASH = /^[0-9a-f]{7,40}$/;
const RUTA_CLAVE = /^- `([^`]+)`/;

const normalizar = (texto) => texto.replace(/\r\n/g, '\n');
const comoLista = (valor) => (Array.isArray(valor) ? valor : []);

function secciones(cuerpo) {
  const resultado = [];
  for (const linea of cuerpo.split('\n')) {
    const titulo = linea.match(/^## (.+)$/);
    if (titulo) resultado.push({ titulo: titulo[1].trim(), lineas: [] });
    else resultado.at(-1)?.lineas.push(linea);
  }
  return resultado;
}

// Devuelve null si el texto no tiene frontmatter.
export function leerFicha(texto) {
  try {
    const { meta, cuerpo } = leerFrontmatter(normalizar(texto));
    return { meta, secciones: secciones(cuerpo) };
  } catch {
    return null;
  }
}

function validarMeta(meta, { id, aprobado }) {
  const p = [];
  if (meta.id !== id) p.push(`El id ${meta.id} no coincide con la carpeta ${id}.`);
  if (!TIPOS_FICHA.includes(meta.tipo)) p.push(`Tipo desconocido: ${meta.tipo}. Usa ${TIPOS_FICHA.join(', ')}.`);

  const modulos = comoLista(meta.modulos);
  if (modulos.length === 0) p.push('Falta al menos un módulo.');
  for (const m of modulos.filter((x) => !MODULOS.includes(x))) p.push(`Módulo desconocido: ${m}.`);

  const temas = comoLista(meta.temas);
  if (temas.length === 0 || temas.length > TOPES.temas) p.push(`Hacen falta entre 1 y ${TOPES.temas} temas.`);
  for (const t of temas.filter((x) => !TEMA.test(x))) p.push(`Tema inválido: ${t}. Usa kebab-case sin acentos.`);

  const resumen = typeof meta.resumen === 'string' ? meta.resumen : '';
  if (resumen.length < TOPES.resumenMin || resumen.length > TOPES.resumenMax) {
    p.push(`El resumen debe tener entre ${TOPES.resumenMin} y ${TOPES.resumenMax} caracteres.`);
  }

  if (!HASH.test(meta.aprobado ?? '')) p.push('aprobado debe ser un hash de commit.');
  else if (aprobado && meta.aprobado !== aprobado) {
    p.push(`aprobado debe ser ${aprobado}, el commit aprobado en la bitácora.`);
  }

  const commits = comoLista(meta.commits);
  if (commits.length === 0 || commits.some((c) => !HASH.test(c))) p.push('commits debe listar hashes de commit.');
  return p;
}

function validarCuerpo(lista, existe) {
  const p = [];
  if (lista.map((s) => s.titulo).join('|') !== SECCIONES_FICHA.join('|')) {
    p.push(`Secciones esperadas, en este orden: ${SECCIONES_FICHA.join(', ')}.`);
  }
  for (const s of lista.filter((x) => !x.lineas.some((l) => l.trim()))) {
    p.push(`La sección ${s.titulo} está vacía.`);
  }

  const lineas = (lista.find((s) => s.titulo === 'Archivos clave')?.lineas ?? []).filter((l) => l.trim());
  for (const linea of lineas.filter((l) => !l.startsWith('- '))) {
    p.push(`Línea no admitida en Archivos clave: ${linea.trim()}. Usa una viñeta «- \`ruta\`» por archivo, sin sangría.`);
  }
  const archivos = lineas.filter((l) => l.startsWith('- '));
  if (archivos.length > TOPES.archivos) p.push(`Máximo ${TOPES.archivos} archivos clave.`);
  for (const linea of archivos) {
    const ruta = linea.match(RUTA_CLAVE)?.[1];
    if (!ruta) p.push(`Archivo clave sin ruta entre comillas invertidas: ${linea}`);
    else if (!rutaRelativaAdmitida(ruta)) {
      p.push(`Ruta no admitida en Archivos clave: ${ruta}. Usa una ruta relativa a la raíz, sin «..» y fuera de .git.`);
    } else if (!existe(ruta)) p.push(`No existe el archivo clave ${ruta}.`);
  }
  return p;
}

// Ruta relativa a la raíz del repositorio, sin unidad, sin «..» y fuera de .git.
// La existencia la comprueba quien llama.
export function rutaRelativaAdmitida(ruta) {
  if (typeof ruta !== 'string' || ruta.length === 0) return false;
  if (/^[\\/]/.test(ruta) || /^[A-Za-z]:/.test(ruta)) return false;
  const segmentos = ruta.split(/[\\/]/);
  return !segmentos.some((s) => s === '..' || s.toLowerCase() === '.git');
}

// Devuelve la lista de problemas; vacía si la ficha es válida.
export function validarFicha(texto, { id, aprobado, existe }) {
  const normal = normalizar(texto);
  const ficha = leerFicha(normal);
  if (!ficha) return ['La ficha no tiene frontmatter.'];

  const p = [];
  if (Buffer.byteLength(normal, 'utf8') > TOPES.bytes) p.push(`Supera ${TOPES.bytes} bytes.`);
  if (normal.trimEnd().split('\n').length > TOPES.lineas) p.push(`Supera ${TOPES.lineas} líneas.`);
  if (normal.includes(MARCADOR)) p.push(`Quedan marcadores ${MARCADOR}.`);
  if (detectarSecretos(normal)) p.push('Contiene una posible credencial.');
  return [...p, ...validarMeta(ficha.meta, { id, aprobado }), ...validarCuerpo(ficha.secciones, existe)];
}

const CABECERA = [
  '# Índice de fichas de tarea',
  '',
  '<!-- Generado por node quality/tarea.mjs indice. No editar a mano. -->',
  '',
  'Una línea por ficha, de la más reciente a la más antigua: id | tipo | módulos |',
  'temas | resumen. Busca un término en lugar de leer el archivo entero y abre solo',
  '`docs/ai/tareas/<id>/ficha.md`. Formato en [README.md](README.md).',
];

export const lineaIndice = (meta) =>
  `- ${meta.id} | ${meta.tipo} | ${comoLista(meta.modulos).join(', ')} | ${comoLista(meta.temas).join(', ')} | ${meta.resumen}`;

export function generarIndice(metas) {
  const lineas = [...metas].sort((a, b) => String(b.id).localeCompare(String(a.id))).map(lineaIndice);
  const todo = lineas.length > 0 ? [...CABECERA, '', ...lineas] : CABECERA;
  return `${todo.join('\n')}\n`;
}
