import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, dirname, join } from 'node:path';
import { existeArchivoEnRepositorio, revisarFicha, revisarRepositorio, revisarTarea } from '../check-tareas.mjs';
import { generarIndice, leerFicha } from '../lib/fichas.mjs';
import { fichaValida } from './ficha-valida.mjs';

const creada = JSON.stringify({ ts: 't', tipo: 'creada', rol: 'orquestador', proveedor: 'anthropic', titulo: 'x' });
const paso = JSON.stringify({ ts: 't', tipo: 'estado', rol: 'orquestador', proveedor: 'anthropic', de: 'RECIBIDA', a: 'DESARROLLO' });

test('una tarea correcta no tiene problemas', () => {
  assert.deepEqual(revisarTarea(`${creada}\n${paso}\n`, '# Tarea\n'), []);
});

test('detecta una línea que no es JSON', () => {
  assert.match(revisarTarea(`${creada}\nesto no\n`, '').join(), /JSON/);
});

test('detecta una historia ilegal', () => {
  const salto = JSON.stringify({ ts: 't', tipo: 'estado', rol: 'orquestador', proveedor: 'a', de: 'RECIBIDA', a: 'CERRADA' });
  assert.match(revisarTarea(`${creada}\n${salto}\n`, '').join(), /no permitida/);
});

test('detecta un secreto en la bitácora y en tarea.md', () => {
  assert.match(revisarTarea(`${creada}\n`, 'password=SuperSecreta1').join(), /credencial/);
  const nota = JSON.stringify({ ts: 't', tipo: 'nota', rol: 'x', proveedor: 'a', texto: 'password=SuperSecreta1' });
  assert.match(revisarTarea(`${creada}\n${nota}\n`, '').join(), /credencial/);
});

test('una bitácora vacía es un problema', () => {
  assert.equal(revisarTarea('', '').length, 1);
});

const ID = '2026-11-01-ejemplo';
const T = `docs/ai/tareas/${ID}`;
const ev = (o) => JSON.stringify({ ts: 't', rol: 'orquestador', proveedor: 'a', ...o });
const transiciones = [
  ['RECIBIDA', 'DESARROLLO'],
  ['DESARROLLO', 'VERIFICACION'],
  ['VERIFICACION', 'ENTORNO'],
  ['ENTORNO', 'PRUEBA_MANUAL'],
];
const abierta = `${[ev({ tipo: 'creada', titulo: 'x' }), ev({ tipo: 'estado', de: 'RECIBIDA', a: 'DESARROLLO' })].join('\n')}\n`;
const cerrada = `${[
  ev({ tipo: 'creada', titulo: 'x' }),
  ...transiciones.map(([de, a]) => ev({ tipo: 'estado', de, a })),
  ev({ tipo: 'aprobacion', rol: 'humano', proveedor: 'humano', arbol: 'x', commit: 'abc1234' }),
  ev({ tipo: 'estado', de: 'APROBADA', a: 'DOCUMENTACION' }),
  ev({ tipo: 'estado', de: 'DOCUMENTACION', a: 'CERRADA' }),
].join('\n')}\n`;
const ficha = fichaValida({ id: ID, ruta: 'a.txt' });

function conArbol(archivos, cuerpo) {
  return () => {
    const raiz = mkdtempSync(join(tmpdir(), 'tareas-'));
    try {
      for (const [ruta, contenido] of Object.entries(archivos)) {
        mkdirSync(dirname(join(raiz, ruta)), { recursive: true });
        writeFileSync(join(raiz, ruta), contenido);
      }
      cuerpo(raiz);
    } finally {
      rmSync(raiz, { recursive: true, force: true });
    }
  };
}

const problemas = (raiz) => revisarRepositorio(raiz).fallidas.flatMap((f) => f.problemas.map((p) => `${f.name}: ${p}`)).join('\n');

test('existeArchivoEnRepositorio solo acepta archivos dentro de la raíz y fuera de .git', conArbol(
  { 'a.txt': 'uno\n', 'docs/b.md': 'dos\n', '.git/config': '[core]\n' },
  (raiz) => {
    const existe = existeArchivoEnRepositorio(raiz);
    assert.equal(existe('a.txt'), true);
    assert.equal(existe('docs/b.md'), true);
    assert.equal(existe('docs'), false, 'un directorio no es un archivo');
    assert.equal(existe('.git/config'), false, 'los internos de git no cuentan');
    assert.equal(existe('no-existe.txt'), false);
    assert.equal(existe(`../${basename(raiz)}/a.txt`), false, 'no sale y vuelve a entrar');
    assert.equal(existe('docs/../a.txt'), false, 'no admite ..');
    assert.equal(existe(join(raiz, 'a.txt')), false, 'no admite rutas absolutas');
    assert.equal(existe('../../../../../Windows/win.ini'), false);
  },
));

test('una tarea abierta sin ficha no es un problema', () => {
  assert.deepEqual(revisarFicha({ id: ID, estado: 'DESARROLLO', ficha: null, existe: () => true }), []);
});

test('una tarea cerrada sin ficha falla', conArbol(
  { [`${T}/bitacora.jsonl`]: cerrada, [`${T}/tarea.md`]: '# x\n', 'docs/ai/tareas/INDICE.md': generarIndice([]) },
  (raiz) => assert.match(problemas(raiz), /Falta ficha\.md/),
));

test('una tarea cerrada con ficha válida e índice al día pasa', conArbol(
  {
    'a.txt': 'uno\n',
    [`${T}/bitacora.jsonl`]: cerrada,
    [`${T}/tarea.md`]: '# x\n',
    [`${T}/ficha.md`]: ficha,
    'docs/ai/tareas/INDICE.md': generarIndice([leerFicha(ficha).meta]),
  },
  (raiz) => assert.equal(problemas(raiz), ''),
));

test('la ficha debe citar el commit aprobado en la bitácora', conArbol(
  {
    'a.txt': 'uno\n',
    [`${T}/bitacora.jsonl`]: cerrada.replace('abc1234', 'def5678'),
    [`${T}/ficha.md`]: ficha,
    'docs/ai/tareas/INDICE.md': generarIndice([leerFicha(ficha).meta]),
  },
  (raiz) => assert.match(problemas(raiz), /def5678/),
));

test('un índice desactualizado falla', conArbol(
  { 'a.txt': 'uno\n', [`${T}/bitacora.jsonl`]: cerrada, [`${T}/ficha.md`]: ficha, 'docs/ai/tareas/INDICE.md': '# viejo\n' },
  (raiz) => assert.match(problemas(raiz), /INDICE\.md: .*no está al día/),
));

test('una ficha inválida falla aunque la tarea siga abierta', conArbol(
  {
    [`${T}/bitacora.jsonl`]: abierta,
    [`${T}/ficha.md`]: ficha.replace('Porque hacía falta.', 'COMPLETAR'),
    'docs/ai/tareas/INDICE.md': generarIndice([leerFicha(ficha).meta]),
  },
  (raiz) => assert.match(problemas(raiz), /ficha\.md: Quedan marcadores/),
));

const indiceVacio = { 'docs/ai/tareas/INDICE.md': generarIndice([]) };
const conCriterio = (evidencia) =>
  `# x\n\n## Criterios de aceptación\n\n1. Algo comprobable.\n${evidencia ? `   ${evidencia}\n` : ''}\n## Plan de pruebas\n`;
const EVIDENCIA_PRUEBA = 'Evidencia: test `p.test.mjs` «hace algo»';
const PRUEBA = "test('hace algo', () => {});\n";
const rojo1 = ev({ tipo: 'comando', rol: 'desarrollador', texto: 'Rojo criterio 1: node --test p.test.mjs — falla porque falta la regla' });
const enVerificacion = (...extra) =>
  `${[
    ev({ tipo: 'creada', titulo: 'x' }),
    ev({ tipo: 'estado', de: 'RECIBIDA', a: 'DESARROLLO' }),
    ...extra,
    ev({ tipo: 'estado', de: 'DESARROLLO', a: 'VERIFICACION' }),
  ].join('\n')}\n`;

test('en DESARROLLO un criterio sin evidencia falla el gate', conArbol(
  { [`${T}/bitacora.jsonl`]: abierta, [`${T}/tarea.md`]: conCriterio(null), ...indiceVacio },
  (raiz) => assert.match(problemas(raiz), /2026-11-01-ejemplo: tarea\.md: Criterio 1: sin línea «Evidencia:»/),
));

test('en VERIFICACION una evidencia que apunta a una prueba inexistente falla el gate', conArbol(
  { [`${T}/bitacora.jsonl`]: enVerificacion(rojo1), [`${T}/tarea.md`]: conCriterio(EVIDENCIA_PRUEBA), ...indiceVacio },
  (raiz) => assert.match(problemas(raiz), /tarea\.md: Criterio 1: no existe la prueba p\.test\.mjs/),
));

test('en VERIFICACION un criterio con prueba sin rojo registrado falla el gate', conArbol(
  { 'p.test.mjs': PRUEBA, [`${T}/bitacora.jsonl`]: enVerificacion(), [`${T}/tarea.md`]: conCriterio(EVIDENCIA_PRUEBA), ...indiceVacio },
  (raiz) => assert.match(problemas(raiz), /tarea\.md: Criterio 1: falta registrar la prueba vista en rojo/),
));

test('en VERIFICACION un criterio con prueba existente y rojo registrado pasa', conArbol(
  { 'p.test.mjs': PRUEBA, [`${T}/bitacora.jsonl`]: enVerificacion(rojo1), [`${T}/tarea.md`]: conCriterio(EVIDENCIA_PRUEBA), ...indiceVacio },
  (raiz) => assert.equal(problemas(raiz), ''),
));

test('las tareas cerradas no se revisan por criterios', conArbol(
  {
    'a.txt': 'uno\n',
    [`${T}/bitacora.jsonl`]: cerrada,
    [`${T}/tarea.md`]: conCriterio(null),
    [`${T}/ficha.md`]: ficha,
    'docs/ai/tareas/INDICE.md': generarIndice([leerFicha(ficha).meta]),
  },
  (raiz) => assert.equal(problemas(raiz), ''),
));

test('una tarea en planificación con criterios incompletos no falla el gate', conArbol(
  {
    [`${T}/bitacora.jsonl`]: `${[ev({ tipo: 'creada', titulo: 'x' }), ev({ tipo: 'estado', de: 'RECIBIDA', a: 'PLANIFICACION' })].join('\n')}\n`,
    [`${T}/tarea.md`]: conCriterio(null),
    ...indiceVacio,
  },
  (raiz) => assert.equal(problemas(raiz), ''),
));
