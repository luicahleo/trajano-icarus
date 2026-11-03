import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, existsSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { arbolDeTrabajo, ejecutarCli } from '../tarea.mjs';
import { fichaValida } from './ficha-valida.mjs';

const CLI = join(dirname(fileURLToPath(import.meta.url)), '..', 'tarea.mjs');

function repoTemporal() {
  const raiz = mkdtempSync(join(tmpdir(), 'tarea-'));
  const git = (...a) => execFileSync('git', a, { cwd: raiz, encoding: 'utf8' });
  git('init', '-q');
  git('config', 'user.email', 'prueba@ejemplo.local');
  git('config', 'user.name', 'Prueba');
  writeFileSync(join(raiz, 'a.txt'), 'uno\n');
  writeFileSync(join(raiz, '.gitignore'), '.local/\nlogs/\n');
  git('add', '.');
  git('commit', '-qm', 'inicial');
  return { raiz, git };
}

function cli(raiz, args, extra = {}) {
  const salida = [];
  const codigo = ejecutarCli(args, {
    raiz,
    esTTY: false,
    leerLinea: () => '',
    escribir: (t) => salida.push(t),
    verificarPuerta: () => ({ codigo: 0 }),
    ...extra,
  });
  return { codigo, salida: salida.join('\n') };
}

const COMUN = ['--rol', 'orquestador', '--proveedor', 'anthropic'];
const crear = (raiz) => cli(raiz, ['crear', 'prueba', '--titulo', 'Tarea de prueba', ...COMUN]);
const hoy = new Date().toISOString().slice(0, 10);
const ID = `${hoy}-prueba`;
const bitacora = (raiz) => readFileSync(join(raiz, 'docs/ai/tareas', ID, 'bitacora.jsonl'), 'utf8');

function conRepo(cuerpo) {
  return () => {
    const { raiz, git } = repoTemporal();
    try {
      cuerpo(raiz, git);
    } finally {
      rmSync(raiz, { recursive: true, force: true });
    }
  };
}

test('arbolDeTrabajo cambia con el contenido e ignora las tareas', conRepo((raiz) => {
  const a = arbolDeTrabajo(raiz);
  mkdirSync(join(raiz, 'docs/ai/tareas/x'), { recursive: true });
  writeFileSync(join(raiz, 'docs/ai/tareas/x/bitacora.jsonl'), '{}\n');
  assert.equal(arbolDeTrabajo(raiz), a);
  writeFileSync(join(raiz, 'a.txt'), 'dos\n');
  assert.notEqual(arbolDeTrabajo(raiz), a);
}));

test('arbolDeTrabajo no altera el índice real', conRepo((raiz, git) => {
  writeFileSync(join(raiz, 'b.txt'), 'nuevo\n');
  arbolDeTrabajo(raiz);
  assert.equal(git('diff', '--cached', '--name-only').trim(), '');
}));

test('arbolDeTrabajo coincide antes y después del commit', conRepo((raiz, git) => {
  writeFileSync(join(raiz, 'a.txt'), 'dos\n');
  const antes = arbolDeTrabajo(raiz);
  git('commit', '-qam', 'cambio');
  assert.equal(arbolDeTrabajo(raiz), antes);
}));

test('arbolDeTrabajo funciona en un worktree de git', conRepo((raiz, git) => {
  const otro = `${raiz}-wt`;
  git('worktree', 'add', '-q', otro);
  try {
    assert.equal(arbolDeTrabajo(otro), arbolDeTrabajo(raiz));
  } finally {
    git('worktree', 'remove', '--force', otro);
  }
}));

test('crear genera la tarea, la bitácora y toma el cerrojo', conRepo((raiz) => {
  assert.equal(crear(raiz).codigo, 0);
  assert.ok(existsSync(join(raiz, 'docs/ai/tareas', ID, 'tarea.md')));
  assert.ok(existsSync(join(raiz, 'docs/ai/tareas', ID, 'bitacora.jsonl')));
  assert.ok(existsSync(join(raiz, '.local/agentes/tarea-activa.json')));
}));

test('el cerrojo impide una segunda tarea activa', conRepo((raiz) => {
  crear(raiz);
  const r = cli(raiz, ['crear', 'otra', '--titulo', 'Otra', ...COMUN]);
  assert.equal(r.codigo, 1);
  assert.match(r.salida, /activa/i);
  assert.ok(!existsSync(join(raiz, 'docs/ai/tareas', `${hoy}-otra`)));
}));

test('otro proveedor no puede actuar sin relevo', conRepo((raiz) => {
  crear(raiz);
  const r = cli(raiz, ['avanzar', ID, 'DESARROLLO', '--rol', 'orquestador', '--proveedor', 'moonshot']);
  assert.equal(r.codigo, 1);
  assert.match(r.salida, /relevo/);
  assert.equal(cli(raiz, ['relevo', ID, '--proveedor', 'moonshot', '--motivo', 'sin tokens']).codigo, 0);
  assert.equal(cli(raiz, ['avanzar', ID, 'DESARROLLO', '--rol', 'orquestador', '--proveedor', 'moonshot']).codigo, 0);
}));

test('el relevo exige motivo', conRepo((raiz) => {
  crear(raiz);
  assert.equal(cli(raiz, ['relevo', ID, '--proveedor', 'moonshot']).codigo, 1);
}));

test('registrar anota eventos y rechaza secretos sin escribirlos', conRepo((raiz) => {
  crear(raiz);
  assert.equal(cli(raiz, ['registrar', ID, 'decision', '--texto', 'Se mantiene el umbral', ...COMUN]).codigo, 0);
  assert.match(bitacora(raiz), /"tipo":"decision"/);
  const r = cli(raiz, ['registrar', ID, 'nota', '--texto', 'password=SuperSecreta1', ...COMUN]);
  assert.equal(r.codigo, 1);
  assert.ok(!bitacora(raiz).includes('SuperSecreta1'));
}));

test('registrar no admite tipos de evento reservados', conRepo((raiz) => {
  crear(raiz);
  assert.equal(cli(raiz, ['registrar', ID, 'aprobacion', '--texto', 'hecho', ...COMUN]).codigo, 1);
  assert.doesNotMatch(bitacora(raiz), /"tipo":"aprobacion"/);
}));

function hastaPruebaManual(raiz, git) {
  crear(raiz);
  cli(raiz, ['avanzar', ID, 'DESARROLLO', ...COMUN]);
  writeFileSync(join(raiz, 'a.txt'), 'cambio\n');
  cli(raiz, ['avanzar', ID, 'VERIFICACION', '--rol', 'desarrollador', '--proveedor', 'anthropic']);
  assert.equal(cli(raiz, ['verificar', ID, '--rol', 'verificador', '--proveedor', 'anthropic']).codigo, 0);
  cli(raiz, ['avanzar', ID, 'ENTORNO', '--rol', 'verificador', '--proveedor', 'anthropic']);
  git('add', 'a.txt');
  git('commit', '-qm', 'cambio');
  const r = cli(raiz, ['avanzar', ID, 'PRUEBA_MANUAL', '--rol', 'operaciones', '--proveedor', 'anthropic']);
  assert.equal(r.codigo, 0, r.salida);
}

test('verificar registra rojo y devuelve fallo sin ocultarlo', conRepo((raiz) => {
  crear(raiz);
  cli(raiz, ['avanzar', ID, 'DESARROLLO', ...COMUN]);
  cli(raiz, ['avanzar', ID, 'VERIFICACION', '--rol', 'desarrollador', '--proveedor', 'anthropic']);
  const r = cli(raiz, ['verificar', ID, '--rol', 'verificador', '--proveedor', 'anthropic'], {
    verificarPuerta: () => ({ codigo: 3 }),
  });
  assert.equal(r.codigo, 1);
  assert.match(bitacora(raiz), /"resultado":"rojo"/);
}));

test('verificar solo corre en VERIFICACION', conRepo((raiz) => {
  crear(raiz);
  assert.equal(cli(raiz, ['verificar', ID, '--rol', 'verificador', '--proveedor', 'anthropic']).codigo, 1);
}));

test('aprobar exige terminal interactiva', conRepo((raiz, git) => {
  hastaPruebaManual(raiz, git);
  const r = cli(raiz, ['aprobar', ID]);
  assert.equal(r.codigo, 1);
  assert.match(r.salida, /terminal/);
}));

test('aprobar exige escribir el commit a mano', conRepo((raiz, git) => {
  hastaPruebaManual(raiz, git);
  const r = cli(raiz, ['aprobar', ID], { esTTY: true, leerLinea: () => 'cualquiera' });
  assert.equal(r.codigo, 1);
  assert.doesNotMatch(bitacora(raiz), /"tipo":"aprobacion"/);
}));

test('aprobar en terminal con el commit correcto deja APROBADA', conRepo((raiz, git) => {
  hastaPruebaManual(raiz, git);
  const commit = git('rev-parse', '--short', 'HEAD').trim();
  const r = cli(raiz, ['aprobar', ID], { esTTY: true, leerLinea: () => commit });
  assert.equal(r.codigo, 0, r.salida);
  assert.match(cli(raiz, ['resumen', ID]).salida, /APROBADA/);
}));

test('un cambio tras la verificación invalida la aprobación', conRepo((raiz, git) => {
  hastaPruebaManual(raiz, git);
  writeFileSync(join(raiz, 'a.txt'), 'otro cambio\n');
  git('commit', '-qam', 'colado');
  const commit = git('rev-parse', '--short', 'HEAD').trim();
  const r = cli(raiz, ['aprobar', ID], { esTTY: true, leerLinea: () => commit });
  assert.equal(r.codigo, 1);
  assert.match(r.salida, /cambió/);
}));

test('rechazar exige pasos, actual, esperado y clasificación, y encamina', conRepo((raiz, git) => {
  hastaPruebaManual(raiz, git);
  assert.equal(cli(raiz, ['rechazar', ID, '--clasificacion', 'funcional', ...COMUN]).codigo, 1);
  const r = cli(raiz, [
    'rechazar', ID, '--clasificacion', 'funcional',
    '--pasos', 'Abrir la pantalla', '--actual', 'Falla', '--esperado', 'Funciona', ...COMUN,
  ]);
  assert.equal(r.codigo, 0, r.salida);
  assert.match(cli(raiz, ['resumen', ID]).salida, /Estado: DESARROLLO/);
}));

test('cerrar o cancelar libera el cerrojo', conRepo((raiz) => {
  crear(raiz);
  assert.equal(cli(raiz, ['avanzar', ID, 'CANCELADA', ...COMUN]).codigo, 0);
  assert.ok(!existsSync(join(raiz, '.local/agentes/tarea-activa.json')));
  assert.equal(cli(raiz, ['crear', 'otra', '--titulo', 'Otra', ...COMUN]).codigo, 0);
}));

test('resumen muestra si la evidencia sigue vigente', conRepo((raiz) => {
  crear(raiz);
  cli(raiz, ['avanzar', ID, 'DESARROLLO', ...COMUN]);
  cli(raiz, ['avanzar', ID, 'VERIFICACION', '--rol', 'desarrollador', '--proveedor', 'anthropic']);
  cli(raiz, ['verificar', ID, '--rol', 'verificador', '--proveedor', 'anthropic']);
  assert.match(cli(raiz, ['resumen', ID]).salida, /vigente/);
  writeFileSync(join(raiz, 'a.txt'), 'tres\n');
  assert.match(cli(raiz, ['resumen', ID]).salida, /caducada/);
}));

test('el proceso real responde con código 1 ante un comando desconocido', () => {
  const r = spawnSync(process.execPath, [CLI, 'inexistente'], { encoding: 'utf8' });
  assert.equal(r.status, 1);
});

const DOC = ['--rol', 'documentador', '--proveedor', 'anthropic'];
const rutaFicha = (raiz) => join(raiz, 'docs/ai/tareas', ID, 'ficha.md');

function hastaAprobada(raiz, git) {
  hastaPruebaManual(raiz, git);
  const commit = git('rev-parse', '--short', 'HEAD').trim();
  assert.equal(cli(raiz, ['aprobar', ID], { esTTY: true, leerLinea: () => commit }).codigo, 0);
  return commit;
}

function conRemoto(git) {
  const remoto = mkdtempSync(join(tmpdir(), 'remoto-'));
  execFileSync('git', ['init', '--bare', '-q', remoto]);
  git('remote', 'add', 'origin', remoto);
  git('push', '-q', '-u', 'origin', 'HEAD');
  return remoto;
}

test('tras aprobar no se cierra sin pasar por DOCUMENTACION', conRepo((raiz, git) => {
  hastaAprobada(raiz, git);
  const r = cli(raiz, ['avanzar', ID, 'CERRADA', ...COMUN]);
  assert.equal(r.codigo, 1);
  assert.match(r.salida, /DOCUMENTACION/);
}));

test('ficha solo la escribe el documentador en DOCUMENTACION o en una tarea cerrada', conRepo((raiz) => {
  crear(raiz);
  const r = cli(raiz, ['ficha', ID, ...DOC]);
  assert.equal(r.codigo, 1);
  assert.match(r.salida, /DOCUMENTACION/);
  assert.equal(cli(raiz, ['ficha', ID, '--rol', 'desarrollador', '--proveedor', 'anthropic']).codigo, 1);
  assert.ok(!existsSync(rutaFicha(raiz)));
}));

test('ficha crea la plantilla con el commit aprobado y no la sobrescribe', conRepo((raiz, git) => {
  const commit = hastaAprobada(raiz, git);
  cli(raiz, ['avanzar', ID, 'DOCUMENTACION', ...COMUN]);
  assert.match(cli(raiz, ['resumen', ID]).salida, /Responsable: documentador/);
  assert.equal(cli(raiz, ['ficha', ID, ...DOC]).codigo, 0);
  const texto = readFileSync(rutaFicha(raiz), 'utf8');
  assert.match(texto, new RegExp(`aprobado: ${commit}`));
  assert.match(texto, new RegExp(`commits: \\[${commit}\\]`));
  assert.match(texto, /COMPLETAR/);
  assert.match(texto, /\[Tarea\]\(tarea\.md\)/);
  assert.equal(cli(raiz, ['ficha', ID, ...DOC]).codigo, 1);
}));

test('CERRADA exige ficha válida e índice al día', conRepo((raiz, git) => {
  const commit = hastaAprobada(raiz, git);
  const remoto = conRemoto(git);
  try {
    cli(raiz, ['avanzar', ID, 'DOCUMENTACION', ...COMUN]);
    let r = cli(raiz, ['avanzar', ID, 'CERRADA', ...COMUN]);
    assert.equal(r.codigo, 1);
    assert.match(r.salida, /Falta ficha\.md/);

    writeFileSync(rutaFicha(raiz), fichaValida({ id: ID, aprobado: commit, ruta: 'a.txt' }));
    r = cli(raiz, ['avanzar', ID, 'CERRADA', ...COMUN]);
    assert.equal(r.codigo, 1);
    assert.match(r.salida, /INDICE\.md no está al día/);

    assert.equal(cli(raiz, ['indice']).codigo, 0);
    r = cli(raiz, ['avanzar', ID, 'CERRADA', ...COMUN]);
    assert.equal(r.codigo, 0, r.salida);
    assert.match(cli(raiz, ['resumen', ID]).salida, /Estado: CERRADA/);
  } finally {
    rmSync(remoto, { recursive: true, force: true });
  }
}));

test('indice es idempotente', conRepo((raiz) => {
  crear(raiz);
  assert.equal(cli(raiz, ['indice']).codigo, 0);
  const r = cli(raiz, ['indice']);
  assert.equal(r.codigo, 0);
  assert.match(r.salida, /sin cambios/);
}));

const PLANIFICADOR = ['--rol', 'planificador', '--proveedor', 'anthropic'];
const rutaTareaMd = (raiz) => join(raiz, 'docs/ai/tareas', ID, 'tarea.md');
const conCriterios = (lineas) => `# T\n\n## Criterios de aceptación\n\n${lineas}\n\n## Plan de pruebas\n`;

test('avanzar de PLANIFICACION a DESARROLLO exige criterios con evidencia', conRepo((raiz) => {
  crear(raiz);
  cli(raiz, ['avanzar', ID, 'PLANIFICACION', ...COMUN]);

  const sinCriterios = cli(raiz, ['avanzar', ID, 'DESARROLLO', ...PLANIFICADOR]);
  assert.equal(sinCriterios.codigo, 1);
  assert.match(sinCriterios.salida, /No hay criterios de aceptación/);

  writeFileSync(rutaTareaMd(raiz), conCriterios('1. Algo visible.'));
  const sinEvidencia = cli(raiz, ['avanzar', ID, 'DESARROLLO', ...PLANIFICADOR]);
  assert.equal(sinEvidencia.codigo, 1);
  assert.match(sinEvidencia.salida, /Criterio 1: sin línea/);

  writeFileSync(rutaTareaMd(raiz), conCriterios('1. Algo visible.\n   Evidencia: manual — solo se aprecia en la pantalla real.'));
  const listo = cli(raiz, ['avanzar', ID, 'DESARROLLO', ...PLANIFICADOR]);
  assert.equal(listo.codigo, 0, listo.salida);
  assert.match(bitacora(raiz), /"a":"DESARROLLO"/);
}));

test('la plantilla de crear indica el formato de evidencia de los criterios', conRepo((raiz) => {
  crear(raiz);
  const md = readFileSync(rutaTareaMd(raiz), 'utf8');
  assert.match(md, /## Criterios de aceptación\n\nCada criterio numerado lleva al menos una línea «Evidencia:»/);
  assert.match(md, /docs\/ai\/tareas\/README\.md/);
}));

test('avanzar de ESPERA_DECISION a DESARROLLO exige criterios con evidencia', conRepo((raiz) => {
  crear(raiz);
  cli(raiz, ['avanzar', ID, 'PLANIFICACION', ...COMUN]);
  cli(raiz, ['avanzar', ID, 'ESPERA_DECISION', ...PLANIFICADOR]);

  const sinCriterios = cli(raiz, ['avanzar', ID, 'DESARROLLO', ...COMUN]);
  assert.equal(sinCriterios.codigo, 1);
  assert.match(sinCriterios.salida, /No hay criterios de aceptación/);

  writeFileSync(rutaTareaMd(raiz), conCriterios('1. Algo visible.'));
  const sinEvidencia = cli(raiz, ['avanzar', ID, 'DESARROLLO', ...COMUN]);
  assert.equal(sinEvidencia.codigo, 1);
  assert.match(sinEvidencia.salida, /Criterio 1: sin línea/);

  writeFileSync(rutaTareaMd(raiz), conCriterios('1. Algo visible.\n   Evidencia: manual — solo se aprecia en la pantalla real.'));
  const listo = cli(raiz, ['avanzar', ID, 'DESARROLLO', ...COMUN]);
  assert.equal(listo.codigo, 0, listo.salida);
  assert.match(bitacora(raiz), /"a":"DESARROLLO"/);
}));
