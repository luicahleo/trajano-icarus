import { test } from 'node:test';
import assert from 'node:assert/strict';
import { comprobarEvidencias, leerCriterios, rojosRegistrados, validarCriterios } from '../lib/criterios.mjs';

// Las listas numeradas de otras secciones no son criterios: la tarea de ejemplo
// tiene una en «Pedido» y otra en «Plan de pruebas».
const tarea = (cuerpo) =>
  `# T\n\n## Pedido\n\n1. No es un criterio.\n\n## Criterios de aceptación\n\n${cuerpo}\n## Plan de pruebas\n\n1. Tampoco es un criterio.\n`;

test('lee los criterios numerados y sus evidencias de prueba y manuales', () => {
  const criterios = leerCriterios(
    tarea(
      [
        'Se comprueban en una terminal.',
        '',
        '1. El gate falla.',
        '   Evidencia: test `quality/__tests__/x.test.mjs` «falla el gate»',
        '2. Se ve en la pantalla',
        '   con una segunda línea.',
        '   Evidencia: manual — solo se aprecia con el entorno levantado.',
        '   Evidencia: test `web/src/a.test.tsx` «muestra el aviso»',
        '',
      ].join('\n'),
    ),
  );
  assert.deepEqual(criterios, [
    { numero: 1, evidencias: [{ tipo: 'test', ruta: 'quality/__tests__/x.test.mjs', nombre: 'falla el gate' }] },
    {
      numero: 2,
      evidencias: [
        { tipo: 'manual', justificacion: 'solo se aprecia con el entorno levantado.' },
        { tipo: 'test', ruta: 'web/src/a.test.tsx', nombre: 'muestra el aviso' },
      ],
    },
  ]);
});

test('un encabezado cierra el criterio anterior', () => {
  const criterios = leerCriterios(
    tarea('1. Uno.\n\n### Notas\n\nEvidencia: manual — esta línea no es de ningún criterio.\n'),
  );
  assert.deepEqual(criterios, [{ numero: 1, evidencias: [] }]);
});

test('un criterio sin línea de evidencia es un problema', () => {
  const md = tarea('1. Algo.\n2. Otra cosa.\n   Evidencia: manual — se comprueba leyendo el rol.\n');
  assert.deepEqual(validarCriterios(leerCriterios(md)), ['Criterio 1: sin línea «Evidencia:».']);
});

test('una evidencia con formato desconocido es un problema', () => {
  for (const texto of ['dotnet test pasa', 'test sin-comillas «nombre»', 'test `ruta.test.mjs` sin nombre', 'manual']) {
    const problemas = validarCriterios(leerCriterios(tarea(`1. Algo.\n   Evidencia: ${texto}\n`)));
    assert.deepEqual(problemas, [`Criterio 1: evidencia con formato desconocido: «${texto}».`], texto);
  }
});

test('una evidencia manual sin justificación suficiente es un problema', () => {
  const problemas = validarCriterios(leerCriterios(tarea('1. Algo.\n   Evidencia: manual — se ve\n')));
  assert.deepEqual(problemas, ['Criterio 1: la evidencia manual necesita una justificación de al menos 10 caracteres.']);
});

test('un número de criterio repetido es un problema', () => {
  const md = tarea(
    '1. Uno.\n   Evidencia: manual — se comprueba en la pantalla.\n\n### Ampliación\n\n1. Otro uno.\n   Evidencia: manual — se comprueba en la pantalla.\n',
  );
  assert.deepEqual(validarCriterios(leerCriterios(md)), ['Criterio 1: número repetido.']);
});

test('sin criterios solo es un problema si se exige al menos uno', () => {
  assert.deepEqual(validarCriterios([]), []);
  assert.deepEqual(validarCriterios([], { exigirAlMenosUno: true }), ['No hay criterios de aceptación numerados.']);
});

test('una prueba citada debe existir y contener el nombre', () => {
  const criterios = leerCriterios(
    tarea(
      '1. Uno.\n   Evidencia: test `a.test.mjs` «hace algo»\n2. Dos.\n   Evidencia: test `b.test.mjs` «hace otra cosa»\n',
    ),
  );
  const archivos = { 'a.test.mjs': "test('hace algo', () => {});\n" };
  const faltante = comprobarEvidencias(criterios, { leerPrueba: (r) => archivos[r] ?? null, rojos: new Set([1, 2]) });
  assert.deepEqual(faltante, ['Criterio 2: no existe la prueba b.test.mjs.']);

  const renombrada = comprobarEvidencias(criterios.slice(0, 1), {
    leerPrueba: () => "test('otra cosa', () => {});\n",
    rojos: new Set([1]),
  });
  assert.deepEqual(renombrada, ['Criterio 1: a.test.mjs no contiene la prueba «hace algo».']);
});

test('un criterio con prueba necesita su rojo registrado', () => {
  const criterios = leerCriterios(
    tarea('1. Uno.\n   Evidencia: test `a.test.mjs` «hace algo»\n2. Dos.\n   Evidencia: manual — se comprueba en la pantalla.\n'),
  );
  const problemas = comprobarEvidencias(criterios, { leerPrueba: () => "test('hace algo')", rojos: new Set() });
  assert.deepEqual(problemas, ['Criterio 1: falta registrar la prueba vista en rojo con «Rojo criterio 1: ...».']);
});

test('solo cuentan como rojo los eventos comando con el prefijo exacto', () => {
  const rojos = rojosRegistrados([
    { tipo: 'comando', texto: 'Rojo criterio 3: node --test a.test.mjs — falla porque falta la regla' },
    { tipo: 'nota', texto: 'Rojo criterio 4: no cuenta, es una nota' },
    { tipo: 'comando', texto: 'Visto en rojo el criterio 5' },
    { tipo: 'comando', texto: 'Rojo criterio 12: dotnet test --filter X — falla por la regla' },
    { tipo: 'comando' },
  ]);
  assert.deepEqual([...rojos].sort((a, b) => a - b), [3, 12]);
});
