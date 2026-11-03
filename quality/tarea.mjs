#!/usr/bin/env node
// CLI de tareas de agentes. Es igual para cualquier proveedor: solo necesita
// node y git. Uso y reglas en docs/ai/agentes/README.md.

import { execFileSync, spawnSync } from 'node:child_process';
import {
  appendFileSync,
  closeSync,
  copyFileSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  openSync,
  readFileSync,
  readSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { isAbsolute, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  ESTADOS,
  ROLES,
  TERMINALES,
  destinoDeRechazo,
  detectarSecretos,
  reconstruir,
  validarTransicion,
} from './lib/tareas.mjs';
import { indiceEsperado, revisarDocumentacion, revisarIndice } from './check-tareas.mjs';
import { MARCADOR, SECCIONES_FICHA } from './lib/fichas.mjs';
import { leerCriterios, validarCriterios } from './lib/criterios.mjs';

const DIR_TAREAS = 'docs/ai/tareas';
const RUTA_CERROJO = '.local/agentes/tarea-activa.json';
const TIPOS_REGISTRABLES = ['decision', 'bloqueo', 'comando', 'nota'];

class ErrorTarea extends Error {}

const fallar = (mensaje) => {
  throw new ErrorTarea(mensaje);
};

function git(raiz, args, env) {
  return execFileSync('git', args, {
    cwd: raiz,
    encoding: 'utf8',
    env: env ?? process.env,
    stdio: ['ignore', 'pipe', 'ignore'],
    maxBuffer: 64 * 1024 * 1024,
  }).trim();
}

// Hash git del árbol de trabajo sin las tareas. Usa un índice temporal para no
// tocar el real: es lo que permite comparar el contenido verificado con el
// actual, antes y después del commit.
export function arbolDeTrabajo(raiz) {
  const dir = mkdtempSync(join(tmpdir(), 'arbol-'));
  try {
    const indice = join(dir, 'index');
    copyFileSync(resolve(raiz, git(raiz, ['rev-parse', '--git-path', 'index'])), indice);
    const env = { ...process.env, GIT_INDEX_FILE: indice };
    git(raiz, ['add', '-A', '--', '.', `:(exclude)${DIR_TAREAS}`], env);
    git(raiz, ['rm', '-r', '--cached', '-q', '--ignore-unmatch', '--', DIR_TAREAS], env);
    return git(raiz, ['write-tree'], env);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

const commitActual = (raiz) => git(raiz, ['rev-parse', '--short', 'HEAD']);

const estaLimpio = (raiz) =>
  git(raiz, ['status', '--porcelain', '--', '.', `:(exclude)${DIR_TAREAS}`]) === '';

function commitsSinPush(raiz) {
  try {
    return Number(git(raiz, ['rev-list', '--count', '@{u}..HEAD']));
  } catch {
    // Sin rama remota no se puede afirmar que no falte el push.
    return 1;
  }
}

function contexto(raiz, rol) {
  return { rol, arbol: arbolDeTrabajo(raiz), limpio: estaLimpio(raiz), sinPush: commitsSinPush(raiz) };
}

const dirTarea = (raiz, id) => join(raiz, DIR_TAREAS, id);
const rutaBitacora = (raiz, id) => join(dirTarea(raiz, id), 'bitacora.jsonl');
const rutaCerrojo = (raiz) => join(raiz, RUTA_CERROJO);

function leerEventos(raiz, id) {
  const ruta = rutaBitacora(raiz, id);
  if (!existsSync(ruta)) fallar(`No existe la tarea ${id}.`);
  return readFileSync(ruta, 'utf8')
    .split('\n')
    .filter(Boolean)
    .map((linea) => JSON.parse(linea));
}

function agregarEvento(raiz, id, evento) {
  const linea = JSON.stringify({ ts: new Date().toISOString(), ...evento });
  if (detectarSecretos(linea)) fallar('El texto parece contener una credencial: no se registró.');
  appendFileSync(rutaBitacora(raiz, id), `${linea}\n`, 'utf8');
}

function leerCerrojo(raiz) {
  const ruta = rutaCerrojo(raiz);
  return existsSync(ruta) ? JSON.parse(readFileSync(ruta, 'utf8')) : null;
}

function escribirCerrojo(raiz, datos, { exclusivo }) {
  mkdirSync(join(raiz, '.local/agentes'), { recursive: true });
  try {
    writeFileSync(rutaCerrojo(raiz), JSON.stringify(datos), { flag: exclusivo ? 'wx' : 'w' });
  } catch (error) {
    if (error.code !== 'EEXIST') throw error;
    fallar(`Ya hay una tarea activa: ${leerCerrojo(raiz)?.tarea}.`);
  }
}

function exigirCerrojo(raiz, id, proveedor) {
  const cerrojo = leerCerrojo(raiz);
  if (cerrojo?.tarea !== id) fallar(`La tarea ${id} no tiene el cerrojo: usa relevo.`);
  if (cerrojo.proveedor !== proveedor) {
    fallar(`La tarea ${id} está a cargo de ${cerrojo.proveedor}: usa relevo.`);
  }
}

function leerOpciones(args) {
  const opciones = {};
  const posicionales = [];
  for (let i = 0; i < args.length; i += 1) {
    if (args[i].startsWith('--')) {
      opciones[args[i].slice(2)] = args[i + 1];
      i += 1;
    } else {
      posicionales.push(args[i]);
    }
  }
  return { opciones, posicionales };
}

function exigirOpciones(opciones, nombres) {
  for (const nombre of nombres) {
    if (!opciones[nombre]) fallar(`Falta la opción --${nombre}.`);
  }
}

function exigirActor(opciones) {
  exigirOpciones(opciones, ['rol', 'proveedor']);
  if (!ROLES.includes(opciones.rol)) fallar(`Rol desconocido: ${opciones.rol}.`);
  return { rol: opciones.rol, proveedor: opciones.proveedor };
}

const PLANTILLA = (titulo) => `# ${titulo}

## Pedido

## Alcance

## Fuera de alcance

## Criterios de aceptación

Cada criterio numerado lleva al menos una línea «Evidencia:» (formato en
docs/ai/tareas/README.md).

## Plan de pruebas
`;

function crear({ raiz, escribir }, [slug], opciones) {
  const actor = exigirActor(opciones);
  exigirOpciones(opciones, ['titulo']);
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(slug ?? '')) fallar('El identificador debe ser kebab-case.');

  const id = `${new Date().toISOString().slice(0, 10)}-${slug}`;
  if (existsSync(dirTarea(raiz, id))) fallar(`Ya existe la tarea ${id}.`);

  escribirCerrojo(raiz, { tarea: id, proveedor: actor.proveedor, desde: new Date().toISOString() }, { exclusivo: true });
  mkdirSync(dirTarea(raiz, id), { recursive: true });
  writeFileSync(join(dirTarea(raiz, id), 'tarea.md'), PLANTILLA(opciones.titulo), 'utf8');
  writeFileSync(rutaBitacora(raiz, id), '', 'utf8');
  agregarEvento(raiz, id, { tipo: 'creada', ...actor, titulo: opciones.titulo });
  escribir(`Tarea creada: ${id}`);
}

function avanzar({ raiz, escribir }, [id, destino], opciones) {
  const actor = exigirActor(opciones);
  if (!ESTADOS.includes(destino)) fallar(`Estado desconocido: ${destino}.`);
  exigirCerrojo(raiz, id, actor.proveedor);

  const tarea = reconstruir(leerEventos(raiz, id));
  const ctx = contexto(raiz, actor.rol);
  if (destino === 'CERRADA') {
    ctx.problemasFicha = [
      ...revisarDocumentacion(raiz, id, { estado: 'CERRADA', aprobado: tarea.aprobacion?.commit }),
      ...revisarIndice(raiz),
    ];
  }
  if (tarea.estado === 'PLANIFICACION' && destino === 'DESARROLLO') {
    const ruta = join(dirTarea(raiz, id), 'tarea.md');
    const tareaMd = existsSync(ruta) ? readFileSync(ruta, 'utf8') : '';
    ctx.problemasCriterios = validarCriterios(leerCriterios(tareaMd), { exigirAlMenosUno: true });
  }
  const motivo = validarTransicion(tarea, destino, ctx);
  if (motivo) fallar(motivo);

  agregarEvento(raiz, id, {
    tipo: 'estado',
    ...actor,
    de: tarea.estado,
    a: destino,
    commit: commitActual(raiz),
    ...(opciones.nota ? { nota: opciones.nota } : {}),
  });
  if (TERMINALES.includes(destino)) rmSync(rutaCerrojo(raiz), { force: true });
  escribir(`${id}: ${tarea.estado} a ${destino}`);
}

function registrar({ raiz, escribir }, [id, tipo], opciones) {
  const actor = exigirActor(opciones);
  exigirOpciones(opciones, ['texto']);
  if (!TIPOS_REGISTRABLES.includes(tipo)) fallar(`Tipo no registrable: ${tipo}.`);
  exigirCerrojo(raiz, id, actor.proveedor);
  agregarEvento(raiz, id, { tipo, ...actor, texto: opciones.texto });
  escribir(`${id}: ${tipo} registrado`);
}

export function ejecutarPuerta({ raiz, log }) {
  const salida = openSync(log, 'w');
  try {
    const r = spawnSync(process.execPath, ['quality/verify.mjs'], {
      cwd: raiz,
      stdio: ['ignore', salida, salida],
    });
    return { codigo: r.status ?? 1 };
  } finally {
    closeSync(salida);
  }
}

function marcaDeTiempo() {
  return new Date().toISOString().replace(/[-:]/g, '').slice(0, 15).replace('T', '-');
}

function verificar({ raiz, escribir, verificarPuerta }, [id], opciones) {
  const actor = exigirActor(opciones);
  if (!['verificador', 'orquestador'].includes(actor.rol)) fallar('Solo el verificador registra evidencia.');
  exigirCerrojo(raiz, id, actor.proveedor);
  const tarea = reconstruir(leerEventos(raiz, id));
  if (tarea.estado !== 'VERIFICACION') fallar(`La tarea está en ${tarea.estado}, no en VERIFICACION.`);

  mkdirSync(join(raiz, 'logs'), { recursive: true });
  const log = `logs/puerta-${id}-${marcaDeTiempo()}.log`;
  const arbol = arbolDeTrabajo(raiz);
  const inicio = Date.now();
  const { codigo } = verificarPuerta({ raiz, log: join(raiz, log) });
  const intacto = arbolDeTrabajo(raiz) === arbol;
  const resultado = codigo === 0 && intacto ? 'verde' : 'rojo';

  agregarEvento(raiz, id, {
    tipo: 'verificacion',
    ...actor,
    resultado,
    codigo,
    arbol,
    commit: commitActual(raiz),
    limpio: estaLimpio(raiz),
    log,
    duracionMs: Date.now() - inicio,
    ...(intacto ? {} : { nota: 'El contenido cambió durante la verificación.' }),
  });
  escribir(`${id}: verificación ${resultado} (código ${codigo}). Registro: ${log}`);
  if (resultado !== 'verde') return 1;
  return 0;
}

function rechazar({ raiz, escribir }, [id], opciones) {
  const actor = exigirActor(opciones);
  exigirOpciones(opciones, ['clasificacion', 'pasos', 'actual', 'esperado']);
  const destino = destinoDeRechazo(opciones.clasificacion);
  if (!destino) fallar(`Clasificación desconocida: ${opciones.clasificacion}.`);
  exigirCerrojo(raiz, id, actor.proveedor);
  const tarea = reconstruir(leerEventos(raiz, id));
  if (tarea.estado !== 'PRUEBA_MANUAL') fallar(`La tarea está en ${tarea.estado}, no en PRUEBA_MANUAL.`);

  agregarEvento(raiz, id, {
    tipo: 'rechazo',
    ...actor,
    clasificacion: opciones.clasificacion,
    pasos: opciones.pasos,
    actual: opciones.actual,
    esperado: opciones.esperado,
    a: destino,
  });
  escribir(`${id}: rechazo ${opciones.clasificacion}, pasa a ${destino}`);
}

function aprobar({ raiz, escribir, esTTY, leerLinea }, [id]) {
  if (!esTTY) fallar('aprobar solo funciona en tu terminal interactiva: ningún agente puede aprobar.');
  const tarea = reconstruir(leerEventos(raiz, id));
  if (tarea.estado !== 'PRUEBA_MANUAL') fallar(`La tarea está en ${tarea.estado}, no en PRUEBA_MANUAL.`);

  const { arbol, limpio } = contexto(raiz);
  if (tarea.ultimaVerificacion?.arbol !== arbol) {
    fallar('El contenido cambió desde la última verificación: no se puede aprobar.');
  }
  if (!limpio) fallar('Hay cambios sin commitear: no se puede aprobar.');

  const commit = commitActual(raiz);
  if (leerLinea(`Escribe ${commit} para aprobar la tarea ${id}: `).trim() !== commit) {
    fallar('Confirmación incorrecta: no se aprobó.');
  }
  agregarEvento(raiz, id, { tipo: 'aprobacion', rol: 'humano', proveedor: 'humano', arbol, commit });
  escribir(`${id}: aprobada por el humano en ${commit}`);
}

function relevo({ raiz, escribir }, [id], opciones) {
  exigirOpciones(opciones, ['proveedor', 'motivo']);
  const tarea = reconstruir(leerEventos(raiz, id));
  if (TERMINALES.includes(tarea.estado)) fallar(`La tarea está en un estado terminal: ${tarea.estado}.`);

  const cerrojo = leerCerrojo(raiz);
  if (cerrojo && cerrojo.tarea !== id) fallar(`Ya hay una tarea activa: ${cerrojo.tarea}.`);

  escribirCerrojo(raiz, { tarea: id, proveedor: opciones.proveedor, desde: new Date().toISOString() }, { exclusivo: false });
  agregarEvento(raiz, id, {
    tipo: 'relevo',
    rol: 'orquestador',
    proveedor: opciones.proveedor,
    anterior: cerrojo?.proveedor ?? null,
    motivo: opciones.motivo,
  });
  escribir(`${id}: relevo a ${opciones.proveedor}`);
}

function describirEvento(ev) {
  const detalle = ev.tipo === 'estado' ? `${ev.de} a ${ev.a}` : ev.texto ?? ev.resultado ?? ev.motivo ?? ev.clasificacion ?? ev.titulo ?? '';
  return `  ${ev.ts} ${ev.tipo} [${ev.rol}/${ev.proveedor}] ${detalle}`;
}

function resumen({ raiz, escribir }, [idPedido]) {
  const id = idPedido ?? leerCerrojo(raiz)?.tarea ?? fallar('No hay tarea activa: indica el identificador.');
  const eventos = leerEventos(raiz, id);
  const tarea = reconstruir(eventos);
  const cerrojo = leerCerrojo(raiz);
  const arbol = arbolDeTrabajo(raiz);
  const v = tarea.ultimaVerificacion;
  const evidencia = v ? `${v.resultado}, ${v.arbol === arbol ? 'vigente' : 'caducada'} (${v.log})` : 'sin evidencia';

  escribir(
    [
      `Tarea: ${id}`,
      `Título: ${tarea.titulo}`,
      `Estado: ${tarea.estado}`,
      `Responsable: ${tarea.responsable ?? 'ninguno'}`,
      `Cerrojo: ${cerrojo?.tarea === id ? cerrojo.proveedor : 'libre'}`,
      `Rechazos: ${tarea.rechazos}`,
      `Verificación: ${evidencia}`,
      `Commit: ${commitActual(raiz)}`,
      'Últimos eventos:',
      ...eventos.slice(-10).map(describirEvento),
      'Cambios sin commitear:',
      git(raiz, ['status', '--short', '--', '.', `:(exclude)${DIR_TAREAS}`]) || '  ninguno',
      'Diff frente al último commit:',
      git(raiz, ['diff', '--stat', 'HEAD', '--', '.', `:(exclude)${DIR_TAREAS}`]) || '  ninguno',
    ].join('\n'),
  );
}

const ENLACE_SPEC_O_PLAN = /\]\(([^)\s]*superpowers\/(specs|plans)\/[^)\s]+\.md)\)/g;

function enlacesDeTarea(tareaMd) {
  const vistos = new Map();
  for (const [, ruta, carpeta] of tareaMd.matchAll(ENLACE_SPEC_O_PLAN)) {
    if (!vistos.has(ruta)) vistos.set(ruta, carpeta === 'specs' ? 'Spec' : 'Plan');
  }
  return [...vistos].map(([ruta, etiqueta]) => `- [${etiqueta}](${ruta})`);
}

// Commits que tocaron algo fuera de las tareas entre el inicio de la tarea y
// el commit aprobado. Es un punto de partida: el documentador deja los clave.
function commitsDeLaTarea(raiz, eventos, aprobado) {
  const inicio = eventos.find((ev) => ev.tipo === 'estado' && ev.commit)?.commit;
  if (!inicio) return [aprobado];
  try {
    const salida = git(raiz, ['log', '--reverse', '--format=%h', `${inicio}..${aprobado}`, '--', '.', `:(exclude)${DIR_TAREAS}`]);
    return salida ? salida.split('\n') : [aprobado];
  } catch {
    return [aprobado];
  }
}

const CONTENIDO_FICHA = {
  'Por qué': [MARCADOR],
  'Archivos clave': [`- \`${MARCADOR}\` — ${MARCADOR}`],
  Pendientes: ['- Ninguno.'],
};

function plantillaFicha({ id, titulo, aprobado, commits, enlaces }) {
  const cuerpo = SECCIONES_FICHA.flatMap((seccion) => {
    const contenido =
      seccion === 'Enlaces' ? ['- [Tarea](tarea.md)', ...enlaces] : CONTENIDO_FICHA[seccion] ?? [`- ${MARCADOR}`];
    return [`## ${seccion}`, '', ...contenido, ''];
  });
  return [
    '---',
    `id: ${id}`,
    `tipo: ${MARCADOR}`,
    `modulos: [${MARCADOR}]`,
    `temas: [${MARCADOR}]`,
    `resumen: ${MARCADOR}`,
    `aprobado: ${aprobado}`,
    `commits: [${commits.join(', ')}]`,
    '---',
    '',
    `# ${titulo}`,
    '',
    ...cuerpo,
  ].join('\n');
}

function ficha({ raiz, escribir }, [id], opciones) {
  const actor = exigirActor(opciones);
  if (!['documentador', 'orquestador'].includes(actor.rol)) fallar('Solo el documentador escribe la ficha.');
  const eventos = leerEventos(raiz, id);
  const tarea = reconstruir(eventos);
  if (!['DOCUMENTACION', 'CERRADA'].includes(tarea.estado)) {
    fallar(`La tarea está en ${tarea.estado}: la ficha se escribe en DOCUMENTACION o sobre una tarea cerrada.`);
  }
  const ruta = join(dirTarea(raiz, id), 'ficha.md');
  if (existsSync(ruta)) fallar('La ficha ya existe: edítala en lugar de regenerarla.');

  const aprobado = tarea.aprobacion?.commit ?? commitActual(raiz);
  const rutaTareaMd = join(dirTarea(raiz, id), 'tarea.md');
  const tareaMd = existsSync(rutaTareaMd) ? readFileSync(rutaTareaMd, 'utf8') : '';
  writeFileSync(
    ruta,
    plantillaFicha({
      id,
      titulo: tarea.titulo,
      aprobado,
      commits: commitsDeLaTarea(raiz, eventos, aprobado),
      enlaces: enlacesDeTarea(tareaMd),
    }),
    'utf8',
  );
  escribir(`Ficha creada: ${DIR_TAREAS}/${id}/ficha.md. Sustituye cada ${MARCADOR} y ejecuta indice.`);
}

function indice({ raiz, escribir }) {
  const ruta = join(raiz, DIR_TAREAS, 'INDICE.md');
  const contenido = indiceEsperado(raiz);
  if (existsSync(ruta) && readFileSync(ruta, 'utf8') === contenido) {
    escribir('Índice sin cambios.');
    return;
  }
  writeFileSync(ruta, contenido, 'utf8');
  escribir(`Índice actualizado: ${DIR_TAREAS}/INDICE.md`);
}

const COMANDOS = { crear, avanzar, registrar, verificar, rechazar, aprobar, relevo, resumen, ficha, indice };

export function ejecutarCli(args, deps) {
  const [comando, ...resto] = args;
  const { opciones, posicionales } = leerOpciones(resto);
  try {
    if (!COMANDOS[comando]) fallar(`Comando desconocido: ${comando}. Disponibles: ${Object.keys(COMANDOS).join(', ')}.`);
    return COMANDOS[comando](deps, posicionales, opciones) ?? 0;
  } catch (error) {
    if (!(error instanceof ErrorTarea)) throw error;
    deps.escribir(error.message);
    return 1;
  }
}

function leerLineaDeTerminal(mensaje) {
  process.stdout.write(mensaje);
  const byte = Buffer.alloc(1);
  const bytes = [];
  while (readSync(0, byte, 0, 1, null) === 1 && byte[0] !== 10) bytes.push(byte[0]);
  return Buffer.from(bytes).toString('utf8');
}

const ejecutadoDirecto =
  process.argv[1] && !relative(fileURLToPath(import.meta.url), process.argv[1]) && isAbsolute(process.argv[1]);

if (ejecutadoDirecto) {
  process.exitCode = ejecutarCli(process.argv.slice(2), {
    raiz: fileURLToPath(new URL('..', import.meta.url)),
    esTTY: Boolean(process.stdin.isTTY && process.stdout.isTTY),
    leerLinea: leerLineaDeTerminal,
    escribir: (texto) => console.log(texto),
    verificarPuerta: ejecutarPuerta,
  });
}
