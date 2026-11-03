# Proceso portable: evidencia, decisiones y revisor común — Plan de implementación

> **Para agentes:** ejecutar tarea por tarea con TDD (rojo, verde, limpieza). Los
> pasos usan casillas (`- [ ]`). Spec:
> [`2026-11-03-proceso-portable-evidencia-decisiones-design.md`](../specs/2026-11-03-proceso-portable-evidencia-decisiones-design.md).
> Tarea del sistema de agentes: `2026-11-03-proceso-portable-evidencia-decisiones`.
>
> **Antes de empezar:** las decisiones 1 a 3 de `tarea.md` deben estar respondidas
> por el usuario. El plan está escrito con las recomendaciones (revisión ligera
> siempre, `docs/decisiones/`, voseo restante corregido). Cada tarea afectada
> indica qué cambia si la respuesta fue otra.

**Objetivo:** que cada criterio de aceptación declare su evidencia y la CLI y el
gate `Tareas` lo exijan; que las decisiones transversales vivan en
`docs/decisiones/` con motivo humano; que el revisor aplique un checklist común y
uno de dominio; y que la puerta lea sus gates de `quality/gates.json`.

**Arquitectura:** lógica pura nueva en `quality/lib/criterios.mjs` y
`quality/lib/gates.mjs` (portables); `quality/check-tareas.mjs` suma la revisión de
criterios por estado; `quality/lib/tareas.mjs` y `quality/tarea.mjs` comprueban
los criterios al salir de `PLANIFICACION`; `quality/verify.mjs` queda como ejecutor
genérico. El resto es documentación de proceso y de producto.

**Stack:** Node 22, `node:test`, git. Sin dependencias nuevas.

## Restricciones globales

- Español neutro, sin voseo, con acentos, en UTF-8 sin BOM. No usar
  `Get-Content`/`Set-Content` de PowerShell 5.1 para editar archivos con acentos.
- No hacer commit ni push: el desarrollador deja los cambios sin commitear y el
  orquestador commitea (`docs/ai/agentes/PROTOCOLO.md`).
- No relajar baselines, umbrales ni exclusiones. La lista de gates de
  `quality/gates.json` debe ser **idéntica** a la actual de `verify.mjs`.
- **No modificar pruebas existentes.** `verify.test.mjs` no se toca. En
  `check-tareas.test.mjs`, `tareas.test.mjs` y `tarea-cli.test.mjs` solo se
  **añaden** pruebas al final del archivo.
- **Rojo por criterio.** Este plan aplica ya el formato nuevo: cada criterio de
  `tarea.md` con evidencia `test` necesita su rojo registrado antes de
  `VERIFICACION`, porque el gate `Tareas` lo comprobará sobre esta misma tarea:

  ```powershell
  node quality/tarea.mjs registrar 2026-11-03-proceso-portable-evidencia-decisiones comando --texto "Rojo criterio <N>: <comando> — <motivo del fallo>" --rol desarrollador --proveedor <p>
  ```

  Los nombres de las pruebas de este plan coinciden carácter a carácter con los
  de `tarea.md`. Si cambias uno, cambia los dos.
- **Revisor obligatorio, revisión profunda:** el cambio toca `quality/` y los roles.
- **Nivel de modelo:** alto. `ECONOMIA_TOKENS.md` no permite bajarlo en cambios
  de la puerta de calidad, aunque el código esté completo aquí.
- Comando de prueba dirigida: `node --test quality/__tests__/<archivo>.test.mjs`.

## Mapa de archivos

| Archivo | Cambio |
|---|---|
| `quality/lib/criterios.mjs` | Nuevo, portable: leer y validar criterios con evidencia |
| `quality/__tests__/criterios.test.mjs` | Nuevo |
| `quality/check-tareas.mjs` | `revisarCriterios` por estado y lector de pruebas |
| `quality/__tests__/check-tareas.test.mjs` | Pruebas nuevas al final |
| `quality/lib/tareas.mjs` | `validarTransicion` con `problemasCriterios` |
| `quality/__tests__/tareas.test.mjs` | Prueba nueva al final |
| `quality/tarea.mjs` | Plantilla de `crear`; `avanzar` comprueba criterios |
| `quality/__tests__/tarea-cli.test.mjs` | Pruebas nuevas al final |
| `quality/lib/gates.mjs` | Nuevo, portable: validar, cargar y resolver gates |
| `quality/gates.json` | Nuevo, propio: los diez gates vigentes |
| `quality/verify.mjs` | Ejecutor genérico; voseo corregido |
| `quality/__tests__/gates.test.mjs` | Nuevo, portable |
| `quality/__tests__/gates-proyecto.test.mjs` | Nuevo, propio: congela la lista |
| `quality/check-adaptadores.mjs`, `quality/check-enlaces.mjs`, `quality/check-mojibake.mjs` | Voseo (decisión 3) |
| `docs/decisiones/README.md`, `docs/decisiones/plantilla.md` | Nuevos, portables |
| `docs/decisiones/001-*.md` … `008-*.md` | Nuevos, propios |
| `docs/ai/revision/comun.md`, `docs/ai/revision/dominio.md` | Nuevos |
| `docs/ai/KIT_PROCESO.md` | Nuevo: inventario |
| `docs/ai/agentes/roles/{planificador,desarrollador,verificador,revisor,orquestador,operaciones}.md` | Evidencia, decisiones y revisión |
| `.claude/agents/`, `.kimi-code/agents/`, adaptadores de `AGENTS.md` | Generados |
| `docs/ai/tareas/README.md` | Formato de criterios con evidencia |
| `docs/ai/agentes/PROTOCOLO.md`, `docs/ai/agentes/README.md` | Principios, revisión, comandos, mapa |
| `AGENTS.md`, `docs/ai/README.md`, `docs/ai/WORKFLOW.md`, `docs/ai/PUERTA_CALIDAD.md` | Proceso |
| `docs/proyecto/arquitectura.md`, `docs/proyecto/operacion-calidad.md`, `docs/proyecto/README.md` | Producto |

---

## Tarea 1. Lectura y validación de criterios con evidencia

Criterios de `tarea.md`: 2, 3 y parte del 5.

**Archivos:** `quality/__tests__/criterios.test.mjs`, `quality/lib/criterios.mjs`.

- [ ] **1.1 Prueba roja.** Crear `quality/__tests__/criterios.test.mjs`:

  ```js
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
  ```

- [ ] **1.2 Ver rojo.** `node --test quality/__tests__/criterios.test.mjs`. Falla al
  importar: `quality/lib/criterios.mjs` no existe. Registrar los rojos de los
  criterios 2, 3 y 5 de `tarea.md` con ese comando y ese motivo.

- [ ] **1.3 Implementación.** Crear `quality/lib/criterios.mjs`:

  ```js
  // Criterios de aceptación con evidencia. Portable: lógica pura que no conoce
  // rutas ni estados de ningún proyecto; quien llama inyecta la lectura de las
  // pruebas. Formato en docs/ai/tareas/README.md.

  export const MINIMO_JUSTIFICACION = 10;

  const SECCION = /^## Criterios de aceptación\s*$/;
  const OTRA_SECCION = /^## /;
  const ENCABEZADO = /^#{1,6} /;
  const ITEM = /^(\d+)\.\s/;
  const EVIDENCIA = /^Evidencia:\s*(.*)$/;
  const TEST = /^test\s+`([^`]+)`\s+«([^»]+)»$/;
  const MANUAL = /^manual\s+[—-]\s+(.+)$/;
  const ROJO = /^Rojo criterio (\d+):/;

  function lineasDeLaSeccion(tareaMd) {
    const lineas = tareaMd.replace(/\r\n/g, '\n').split('\n');
    const inicio = lineas.findIndex((linea) => SECCION.test(linea));
    if (inicio === -1) return [];
    const resto = lineas.slice(inicio + 1);
    const fin = resto.findIndex((linea) => OTRA_SECCION.test(linea));
    return fin === -1 ? resto : resto.slice(0, fin);
  }

  export function leerEvidencia(texto) {
    const test = texto.match(TEST);
    if (test) return { tipo: 'test', ruta: test[1], nombre: test[2] };
    const manual = texto.match(MANUAL);
    if (manual) return { tipo: 'manual', justificacion: manual[1].trim() };
    return { tipo: 'desconocida', texto };
  }

  // Un criterio empieza en una línea «N. » sin sangría y dura hasta el siguiente
  // criterio o encabezado. Sus evidencias son las líneas «Evidencia:» de ese tramo.
  export function leerCriterios(tareaMd) {
    const criterios = [];
    let actual = null;
    for (const linea of lineasDeLaSeccion(tareaMd)) {
      const item = linea.match(ITEM);
      if (item) {
        actual = { numero: Number(item[1]), evidencias: [] };
        criterios.push(actual);
      } else if (ENCABEZADO.test(linea)) {
        actual = null;
      } else {
        const evidencia = linea.trim().match(EVIDENCIA);
        if (evidencia && actual) actual.evidencias.push(leerEvidencia(evidencia[1].trim()));
      }
    }
    return criterios;
  }

  export function validarCriterios(criterios, { exigirAlMenosUno = false } = {}) {
    const problemas = [];
    if (exigirAlMenosUno && criterios.length === 0) problemas.push('No hay criterios de aceptación numerados.');

    const vistos = new Set();
    for (const { numero, evidencias } of criterios) {
      if (vistos.has(numero)) problemas.push(`Criterio ${numero}: número repetido.`);
      vistos.add(numero);
      if (evidencias.length === 0) problemas.push(`Criterio ${numero}: sin línea «Evidencia:».`);
      for (const evidencia of evidencias) {
        if (evidencia.tipo === 'desconocida') {
          problemas.push(`Criterio ${numero}: evidencia con formato desconocido: «${evidencia.texto}».`);
        } else if (evidencia.tipo === 'manual' && evidencia.justificacion.length < MINIMO_JUSTIFICACION) {
          problemas.push(
            `Criterio ${numero}: la evidencia manual necesita una justificación de al menos ${MINIMO_JUSTIFICACION} caracteres.`,
          );
        }
      }
    }
    return problemas;
  }

  // El rojo lo registra el desarrollador con un evento comando cuyo texto empieza
  // por «Rojo criterio N:». Basta uno por criterio.
  export const rojosRegistrados = (eventos) =>
    new Set(
      eventos
        .filter((evento) => evento.tipo === 'comando' && typeof evento.texto === 'string')
        .map((evento) => evento.texto.match(ROJO))
        .filter(Boolean)
        .map((coincidencia) => Number(coincidencia[1])),
    );

  // leerPrueba(ruta) devuelve el contenido del archivo, o null si no existe o la
  // ruta no es admisible.
  export function comprobarEvidencias(criterios, { leerPrueba, rojos }) {
    const problemas = [];
    for (const { numero, evidencias } of criterios) {
      const pruebas = evidencias.filter((evidencia) => evidencia.tipo === 'test');
      for (const { ruta, nombre } of pruebas) {
        const contenido = leerPrueba(ruta);
        if (contenido === null) problemas.push(`Criterio ${numero}: no existe la prueba ${ruta}.`);
        else if (!contenido.includes(nombre)) problemas.push(`Criterio ${numero}: ${ruta} no contiene la prueba «${nombre}».`);
      }
      if (pruebas.length > 0 && !rojos.has(numero)) {
        problemas.push(`Criterio ${numero}: falta registrar la prueba vista en rojo con «Rojo criterio ${numero}: ...».`);
      }
    }
    return problemas;
  }
  ```

- [ ] **1.4 Ver verde.** `node --test quality/__tests__/criterios.test.mjs`.

---

## Tarea 2. El gate `Tareas` revisa los criterios según el estado

Criterios de `tarea.md`: 4, el resto del 5 y 6. El orden importa: la prueba de
estados excluidos (2.5) solo puede verse en rojo si antes existe una revisión sin
filtro de estado.

**Archivos:** `quality/__tests__/check-tareas.test.mjs`, `quality/check-tareas.mjs`.

- [ ] **2.1 Prueba roja del formato y de la existencia.** Añadir **al final** de
  `quality/__tests__/check-tareas.test.mjs` (usa `conArbol`, `ev`, `T`, `abierta`,
  `problemas` y `generarIndice`, que ya existen en el archivo):

  ```js
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
  ```

- [ ] **2.2 Ver rojo.** `node --test quality/__tests__/check-tareas.test.mjs`.
  Fallan las tres primeras pruebas nuevas: el gate no mira los criterios. Registrar
  el rojo del criterio 4. (La cuarta pasa ya: no aporta rojo y no hace falta.)

- [ ] **2.3 Implementación sin filtro de estado.** En `quality/check-tareas.mjs`:

  Sustituir el comentario inicial:

  ```js
  // Gate de tareas: rehace cada bitácora de docs/ai/tareas/, valida las fichas de
  // cierre, comprueba que INDICE.md coincida con ellas y revisa la evidencia de
  // los criterios de aceptación de las tareas activas. Falla si una historia no es
  // legal, si una aprobación no es humana, si aparece una credencial, si una tarea
  // cerrada no tiene ficha válida, si el índice no está al día o si un criterio no
  // declara o no respalda su evidencia. La CLI usa las mismas funciones para negar
  // el cierre.
  ```

  Añadir la importación tras la de `./lib/fichas.mjs`:

  ```js
  import { comprobarEvidencias, leerCriterios, rojosRegistrados, validarCriterios } from './lib/criterios.mjs';
  ```

  Sustituir `datosDeBitacora` para que devuelva también los eventos:

  ```js
  function datosDeBitacora(bitacora) {
    try {
      const eventos = bitacora.split('\n').filter(Boolean).map((l) => JSON.parse(l));
      const tarea = reconstruir(eventos);
      return { estado: tarea.estado, aprobado: tarea.aprobacion?.commit, eventos };
    } catch {
      return { estado: null, aprobado: undefined, eventos: [] };
    }
  }
  ```

  Añadir, justo antes de `export function indiceEsperado(raiz)`:

  ```js
  export function lectorDePruebas(raiz) {
    const existe = existeArchivoEnRepositorio(raiz);
    return (ruta) => (existe(ruta) ? readFileSync(resolve(raiz, ruta), 'utf8') : null);
  }

  export function revisarCriterios({ tareaMd, eventos, leerPrueba }) {
    const criterios = leerCriterios(tareaMd);
    const problemas = [
      ...validarCriterios(criterios),
      ...comprobarEvidencias(criterios, { leerPrueba, rojos: rojosRegistrados(eventos) }),
    ];
    return problemas.map((p) => `tarea.md: ${p}`);
  }
  ```

  Sustituir el cuerpo de `revisarRepositorio` hasta el `return` de `fallidas`:

  ```js
  export function revisarRepositorio(raiz) {
    const ids = idsDeTareas(raiz);
    const leerPrueba = lectorDePruebas(raiz);
    const fallidas = ids.flatMap((id) => {
      const dir = join(raiz, DIR_TAREAS, id);
      const bitacora = leer(join(dir, 'bitacora.jsonl')) ?? '';
      const tareaMd = leer(join(dir, 'tarea.md')) ?? '';
      const datos = datosDeBitacora(bitacora);
      const problemas = [
        ...revisarTarea(bitacora, tareaMd),
        ...revisarDocumentacion(raiz, id, datos),
        ...revisarCriterios({ ...datos, tareaMd, leerPrueba }),
      ];
      return problemas.length > 0 ? [{ name: id, problemas }] : [];
    });
  ```

  (El resto de la función, con el índice y el `return`, no cambia.)

- [ ] **2.4 Ver verde.** `node --test quality/__tests__/check-tareas.test.mjs`.
  Las cuatro pruebas nuevas pasan y las existentes también: sus `tarea.md` no
  tienen criterios.

- [ ] **2.5 Prueba roja de los estados excluidos.** Añadir **al final** del mismo
  archivo (usa `cerrada`, `ficha`, `leerFicha` y `ev`, que ya existen):

  ```js
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
  ```

- [ ] **2.6 Ver rojo.** `node --test quality/__tests__/check-tareas.test.mjs`.
  Fallan las dos: el gate revisa criterios en cualquier estado. Registrar el rojo
  del criterio 6.

- [ ] **2.7 Filtro de estado.** En `quality/check-tareas.mjs`, justo antes de
  `export function lectorDePruebas`:

  ```js
  // Estados en los que tarea.md ya debe declarar la evidencia de cada criterio.
  // Antes, los criterios pueden estar a medio escribir; las tareas cerradas son
  // historia, y sus pruebas pueden haberse movido después.
  const ESTADOS_CON_CRITERIOS = ['DESARROLLO', 'VERIFICACION', 'REVISION', 'ENTORNO', 'PRUEBA_MANUAL', 'APROBADA', 'DOCUMENTACION'];
  // Desde VERIFICACION las pruebas citadas ya existen y se vieron en rojo.
  const ESTADOS_CON_PRUEBAS = ESTADOS_CON_CRITERIOS.filter((estado) => estado !== 'DESARROLLO');
  ```

  Y sustituir `revisarCriterios`:

  ```js
  export function revisarCriterios({ estado, tareaMd, eventos, leerPrueba }) {
    if (!ESTADOS_CON_CRITERIOS.includes(estado)) return [];
    const criterios = leerCriterios(tareaMd);
    const problemas = validarCriterios(criterios);
    if (ESTADOS_CON_PRUEBAS.includes(estado)) {
      problemas.push(...comprobarEvidencias(criterios, { leerPrueba, rojos: rojosRegistrados(eventos) }));
    }
    return problemas.map((p) => `tarea.md: ${p}`);
  }
  ```

- [ ] **2.8 Ver verde.** `node --test quality/__tests__/check-tareas.test.mjs` y
  `node quality/check-tareas.mjs` sobre el repositorio real: verde, con las cuatro
  tareas cerradas intactas. Esta tarea está en `DESARROLLO`, así que su `tarea.md`
  debe pasar ya la comprobación de formato.

---

## Tarea 3. La CLI exige criterios al salir de planificación y la plantilla los anuncia

Criterios de `tarea.md`: 1 y 7.

**Archivos:** `quality/__tests__/tareas.test.mjs`, `quality/lib/tareas.mjs`,
`quality/__tests__/tarea-cli.test.mjs`, `quality/tarea.mjs`.

- [ ] **3.1 Prueba roja de la transición.** Añadir **al final** de
  `quality/__tests__/tareas.test.mjs`:

  ```js
  test('PLANIFICACION a DESARROLLO exige criterios comprobados y sin problemas', () => {
    const t = reconstruir([...base, paso('RECIBIDA', 'PLANIFICACION')]);
    const desarrollar = (c) => validarTransicion(t, 'DESARROLLO', { rol: 'planificador', ...contexto(c) });
    assert.match(desarrollar(), /No se comprobaron los criterios/);
    assert.match(
      desarrollar({ problemasCriterios: ['Criterio 1: sin línea «Evidencia:».'] }),
      /criterios no están listos: Criterio 1: sin línea/,
    );
    assert.equal(desarrollar({ problemasCriterios: [] }), null);
  });
  ```

- [ ] **3.2 Pruebas rojas de la CLI.** Añadir **al final** de
  `quality/__tests__/tarea-cli.test.mjs`:

  ```js
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
  ```

- [ ] **3.3 Ver rojo.** `node --test quality/__tests__/tareas.test.mjs` y
  `node --test quality/__tests__/tarea-cli.test.mjs`. Fallan las tres pruebas
  nuevas: la transición no mira los criterios y la plantilla no los anuncia.
  Registrar los rojos de los criterios 7 y 1.

- [ ] **3.4 Implementación de la transición.** En `quality/lib/tareas.mjs`,
  cambiar la firma de `validarTransicion`:

  ```js
  export function validarTransicion(tarea, destino, { rol, arbol, limpio, sinPush, problemasFicha, problemasCriterios }) {
  ```

  Y añadir, justo después del bloque `if (haciaAdelante) { ... }`:

  ```js
    if (tarea.estado === 'PLANIFICACION' && destino === 'DESARROLLO') {
      if (!Array.isArray(problemasCriterios)) return 'No se comprobaron los criterios de aceptación.';
      if (problemasCriterios.length > 0) return `Los criterios no están listos: ${problemasCriterios.join(' ')}`;
    }
  ```

- [ ] **3.5 Implementación de la CLI.** En `quality/tarea.mjs`:

  Añadir la importación tras la de `./lib/fichas.mjs`:

  ```js
  import { leerCriterios, validarCriterios } from './lib/criterios.mjs';
  ```

  Sustituir la plantilla:

  ```js
  const PLANTILLA = (titulo) => `# ${titulo}

  ## Pedido

  ## Alcance

  ## Fuera de alcance

  ## Criterios de aceptación

  Cada criterio numerado lleva al menos una línea «Evidencia:» (formato en
  docs/ai/tareas/README.md).

  ## Plan de pruebas
  `;
  ```

  (Sin sangría: las líneas de la plantilla empiezan en la columna 0, como hoy.)

  En `avanzar`, justo después del bloque `if (destino === 'CERRADA') { ... }`:

  ```js
    if (tarea.estado === 'PLANIFICACION' && destino === 'DESARROLLO') {
      const ruta = join(dirTarea(raiz, id), 'tarea.md');
      const tareaMd = existsSync(ruta) ? readFileSync(ruta, 'utf8') : '';
      ctx.problemasCriterios = validarCriterios(leerCriterios(tareaMd), { exigirAlMenosUno: true });
    }
  ```

- [ ] **3.6 Ver verde.** `node --test quality/__tests__/tareas.test.mjs` y
  `node --test quality/__tests__/tarea-cli.test.mjs`.

---

## Tarea 4. Lista de gates configurable con ejecutor genérico

Criterios de `tarea.md`: 8, 9, 10 y la parte de `verify.mjs` del 11.

**Archivos:** `quality/__tests__/gates.test.mjs`,
`quality/__tests__/gates-proyecto.test.mjs`, `quality/lib/gates.mjs`,
`quality/gates.json`, `quality/verify.mjs`.

- [ ] **4.1 Prueba roja portable.** Crear `quality/__tests__/gates.test.mjs`:

  ```js
  import { test } from 'node:test';
  import assert from 'node:assert/strict';
  import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
  import { tmpdir } from 'node:os';
  import { dirname, join, resolve } from 'node:path';
  import { fileURLToPath } from 'node:url';
  import { cargarGates, resolverGate, validarConfiguracion } from '../lib/gates.mjs';
  import { ejecutarGates } from '../verify.mjs';

  const QUALITY = join(dirname(fileURLToPath(import.meta.url)), '..');

  test('una configuración válida no tiene problemas', () => {
    const datos = {
      gates: [
        { nombre: 'A', tipo: 'node', args: ['a.mjs'], motivo: 'Va primero.' },
        { nombre: 'B', tipo: 'npm', script: 'lint', cwd: 'web' },
        { nombre: 'C', tipo: 'dotnet', args: ['build', 'x.sln'], cwd: 'sub/dir' },
      ],
    };
    assert.deepEqual(validarConfiguracion(datos), []);
  });

  test('una configuración inválida se rechaza con un motivo por cada problema', () => {
    const problemas = validarConfiguracion({
      gates: [
        { nombre: 'A', tipo: 'bash', args: ['x'] },
        { nombre: 'B', tipo: 'node', args: [] },
        { nombre: 'B', tipo: 'node', args: ['x'] },
        { nombre: 'C', tipo: 'npm' },
        { nombre: 'D', tipo: 'node', args: ['x'], omitir: true },
      ],
      extra: 1,
    });
    assert.deepEqual(problemas, [
      'Clave no admitida: extra.',
      'Gate 1 (A): tipo desconocido: bash. Usa node, npm, dotnet.',
      'Gate 2 (B): args debe ser una lista no vacía de textos.',
      'Gate 3: nombre repetido: B.',
      'Gate 4 (C): falta el script de npm.',
      'Gate 5 (D): clave no admitida para node: omitir.',
    ]);
    assert.match(validarConfiguracion({ gates: [] }).join(), /vacía/);
    assert.match(validarConfiguracion([]).join(), /objeto/);
    assert.match(validarConfiguracion({ gates: [null] }).join(), /Gate 1: no es un objeto/);
  });

  test('un cwd absoluto o con .. se rechaza', () => {
    for (const cwd of ['/tmp', 'C:\\web', '../otro', 'web/../..', '']) {
      const problemas = validarConfiguracion({ gates: [{ nombre: 'A', tipo: 'node', args: ['x'], cwd }] });
      assert.deepEqual(problemas, ['Gate 1 (A): cwd debe ser una ruta relativa dentro del repositorio.'], cwd);
    }
  });

  test('cargarGates falla cerrado si el archivo falta o no es JSON', () => {
    const raiz = mkdtempSync(join(tmpdir(), 'gates-'));
    try {
      const faltante = cargarGates(raiz);
      assert.deepEqual(faltante, { gates: [], problemas: ['No existe quality/gates.json.'] });

      mkdirSync(join(raiz, 'quality'));
      writeFileSync(join(raiz, 'quality/gates.json'), '{ "gates": [ ');
      const roto = cargarGates(raiz);
      assert.deepEqual(roto.gates, []);
      assert.match(roto.problemas.join(), /no es JSON válido/);

      writeFileSync(join(raiz, 'quality/gates.json'), JSON.stringify({ gates: [{ nombre: 'A', tipo: 'bash', args: ['x'] }] }));
      assert.deepEqual(cargarGates(raiz).gates, [], 'con un gate inválido no devuelve ninguno');

      const valido = { nombre: 'A', tipo: 'node', args: ['a.mjs'] };
      writeFileSync(join(raiz, 'quality/gates.json'), JSON.stringify({ gates: [valido] }));
      assert.deepEqual(cargarGates(raiz), { gates: [valido], problemas: [] });
    } finally {
      rmSync(raiz, { recursive: true, force: true });
    }
  });

  test('un gate node o dotnet se resuelve con su comando y sus argumentos', () => {
    const contexto = { raiz: '/repo', plataforma: 'linux', execPath: '/opt/node/bin/node', existe: () => false };
    assert.deepEqual(resolverGate({ nombre: 'A', tipo: 'node', args: ['a.mjs'] }, contexto), {
      nombre: 'A',
      comando: 'node',
      args: ['a.mjs'],
    });
    assert.deepEqual(resolverGate({ nombre: 'B', tipo: 'dotnet', args: ['build', 'x.sln'], cwd: 'sub' }, contexto), {
      nombre: 'B',
      comando: 'dotnet',
      args: ['build', 'x.sln'],
      cwd: resolve('/repo', 'sub'),
    });
  });

  test('un gate npm usa el CLI de npm con node en Windows y npm con shell en POSIX', () => {
    const gate = { nombre: 'Lint', tipo: 'npm', script: 'lint', cwd: 'web' };
    const base = { raiz: '/repo', execPath: '/opt/node/node.exe' };

    const windows = resolverGate(gate, { ...base, plataforma: 'win32', existe: () => true });
    assert.equal(windows.comando, '/opt/node/node.exe');
    assert.match(windows.args[0], /npm-cli\.js$/);
    assert.deepEqual(windows.args.slice(1), ['run', 'lint']);
    assert.equal(windows.cwd, resolve('/repo', 'web'));
    assert.equal(windows.shell, undefined);

    const posix = resolverGate(gate, { ...base, plataforma: 'linux', existe: () => true });
    assert.deepEqual(posix, { nombre: 'Lint', comando: 'npm', args: ['run', 'lint'], cwd: resolve('/repo', 'web'), shell: true });

    const windowsSinCli = resolverGate(gate, { ...base, plataforma: 'win32', existe: () => false });
    assert.equal(windowsSinCli.comando, 'npm');
    assert.equal(windowsSinCli.shell, true);
  });

  test('sin gates la puerta no da verde', () => {
    let llamadas = 0;
    const resultado = ejecutarGates([], () => {
      llamadas += 1;
      return { codigo: 0 };
    });
    assert.deepEqual(resultado, { ok: false, ejecutados: [] });
    assert.equal(llamadas, 0);
  });

  test('el ejecutor no nombra rutas de este proyecto', () => {
    const PROPIOS = [/Icarus/, /Trajano/, /\bweb\b/, /\.sln\b/];
    for (const archivo of ['verify.mjs', 'lib/gates.mjs']) {
      const fuente = readFileSync(join(QUALITY, archivo), 'utf8');
      for (const propio of PROPIOS) assert.doesNotMatch(fuente, propio, `${archivo} nombra ${propio}`);
    }
  });
  ```

- [ ] **4.2 Prueba roja del proyecto.** Crear
  `quality/__tests__/gates-proyecto.test.mjs`:

  ```js
  // Propia de Trajano-Icarus. Congela la lista de gates: quitar, reordenar o
  // debilitar uno exige cambiar esta prueba en el mismo diff, y el revisor lo ve.
  // Añadir un gate también pasa por aquí.
  import { test } from 'node:test';
  import assert from 'node:assert/strict';
  import { fileURLToPath } from 'node:url';
  import { cargarGates } from '../lib/gates.mjs';

  const raiz = fileURLToPath(new URL('../..', import.meta.url));

  test('la configuración del proyecto declara los diez gates vigentes en orden y con sus argumentos', () => {
    const { gates, problemas } = cargarGates(raiz);
    assert.deepEqual(problemas, []);
    assert.deepEqual(
      gates.map(({ motivo, ...gate }) => gate),
      [
        { nombre: 'Tests de la puerta', tipo: 'node', args: ['--test', 'quality/__tests__/*.test.mjs'] },
        { nombre: 'Adaptadores', tipo: 'node', args: ['quality/check-adaptadores.mjs'] },
        { nombre: 'Mojibake', tipo: 'node', args: ['quality/check-mojibake.mjs'] },
        { nombre: 'Enlaces', tipo: 'node', args: ['quality/check-enlaces.mjs'] },
        { nombre: 'Tareas', tipo: 'node', args: ['quality/check-tareas.mjs'] },
        { nombre: 'Frontend lint', tipo: 'npm', script: 'lint', cwd: 'web' },
        { nombre: 'Frontend build', tipo: 'npm', script: 'build', cwd: 'web' },
        { nombre: 'Frontend tests', tipo: 'npm', script: 'test', cwd: 'web' },
        { nombre: 'Backend build', tipo: 'dotnet', args: ['build', 'Icarus/Icarus.sln', '--nologo'] },
        { nombre: 'Backend tests', tipo: 'dotnet', args: ['test', 'Icarus/Icarus.sln', '--nologo', '--no-build'] },
      ],
    );
  });
  ```

- [ ] **4.3 Ver rojo.** `node --test quality/__tests__/gates.test.mjs` y
  `node --test quality/__tests__/gates-proyecto.test.mjs`. Fallan al importar
  `quality/lib/gates.mjs`. Registrar los rojos de los criterios 8, 9 y 10.

- [ ] **4.4 Implementación del módulo portable.** Crear `quality/lib/gates.mjs`:

  ```js
  // Configuración de la puerta de calidad. Portable: valida, carga y resuelve los
  // gates que cada proyecto declara en quality/gates.json. Falla cerrado: ante
  // cualquier problema no devuelve ningún gate, y una puerta sin gates no da verde.

  import { existsSync, readFileSync } from 'node:fs';
  import { dirname, isAbsolute, join, resolve } from 'node:path';

  export const RUTA_CONFIGURACION = 'quality/gates.json';
  export const TIPOS_GATE = ['node', 'npm', 'dotnet'];

  const CLAVES_POR_TIPO = {
    node: ['nombre', 'tipo', 'args', 'cwd', 'motivo'],
    npm: ['nombre', 'tipo', 'script', 'cwd', 'motivo'],
    dotnet: ['nombre', 'tipo', 'args', 'cwd', 'motivo'],
  };

  const esTexto = (valor) => typeof valor === 'string' && valor.trim() !== '';
  const esObjeto = (valor) => valor !== null && typeof valor === 'object' && !Array.isArray(valor);

  // Relativa y dentro del repositorio. isAbsolute de POSIX no reconoce «C:\», así
  // que la unidad se comprueba aparte.
  const cwdAdmitido = (cwd) =>
    esTexto(cwd) && !isAbsolute(cwd) && !/^[a-zA-Z]:/.test(cwd) && !cwd.split(/[\\/]/).includes('..');

  function validarGate(gate, indice, nombres) {
    const donde = `Gate ${indice + 1}`;
    if (!esObjeto(gate)) return [`${donde}: no es un objeto.`];

    const problemas = [];
    let etiqueta = donde;
    if (!esTexto(gate.nombre)) {
      problemas.push(`${donde}: falta el nombre.`);
    } else if (nombres.has(gate.nombre)) {
      problemas.push(`${donde}: nombre repetido: ${gate.nombre}.`);
    } else {
      nombres.add(gate.nombre);
      etiqueta = `${donde} (${gate.nombre})`;
    }

    if (!TIPOS_GATE.includes(gate.tipo)) {
      problemas.push(`${etiqueta}: tipo desconocido: ${gate.tipo}. Usa ${TIPOS_GATE.join(', ')}.`);
      return problemas;
    }
    for (const clave of Object.keys(gate).filter((k) => !CLAVES_POR_TIPO[gate.tipo].includes(k))) {
      problemas.push(`${etiqueta}: clave no admitida para ${gate.tipo}: ${clave}.`);
    }
    if (gate.tipo === 'npm') {
      if (!esTexto(gate.script)) problemas.push(`${etiqueta}: falta el script de npm.`);
    } else if (!Array.isArray(gate.args) || gate.args.length === 0 || !gate.args.every(esTexto)) {
      problemas.push(`${etiqueta}: args debe ser una lista no vacía de textos.`);
    }
    if (gate.cwd !== undefined && !cwdAdmitido(gate.cwd)) {
      problemas.push(`${etiqueta}: cwd debe ser una ruta relativa dentro del repositorio.`);
    }
    if (gate.motivo !== undefined && !esTexto(gate.motivo)) problemas.push(`${etiqueta}: motivo debe ser un texto.`);
    return problemas;
  }

  export function validarConfiguracion(datos) {
    if (!esObjeto(datos)) return ['La configuración debe ser un objeto con la lista «gates».'];
    const problemas = Object.keys(datos)
      .filter((clave) => clave !== 'gates')
      .map((clave) => `Clave no admitida: ${clave}.`);
    if (!Array.isArray(datos.gates) || datos.gates.length === 0) {
      return [...problemas, 'La lista «gates» falta o está vacía: una puerta sin gates no comprueba nada.'];
    }
    const nombres = new Set();
    datos.gates.forEach((gate, indice) => problemas.push(...validarGate(gate, indice, nombres)));
    return problemas;
  }

  export function cargarGates(raiz, ruta = RUTA_CONFIGURACION) {
    const archivo = join(raiz, ruta);
    if (!existsSync(archivo)) return { gates: [], problemas: [`No existe ${ruta}.`] };
    let datos;
    try {
      datos = JSON.parse(readFileSync(archivo, 'utf8'));
    } catch (error) {
      return { gates: [], problemas: [`${ruta} no es JSON válido: ${error.message}`] };
    }
    const problemas = validarConfiguracion(datos);
    return problemas.length > 0 ? { gates: [], problemas } : { gates: datos.gates, problemas: [] };
  }

  // npm en Windows es npm.cmd: lanzarlo con shell dispara el aviso DEP0190 de Node
  // (argumentos concatenados sin escapar). Se invoca el CLI de npm con el propio
  // node, sin shell. En POSIX npm es un binario directo.
  export function resolverGate(gate, { raiz, plataforma, execPath, existe }) {
    const conCwd = gate.cwd === undefined ? {} : { cwd: resolve(raiz, gate.cwd) };
    if (gate.tipo !== 'npm') return { nombre: gate.nombre, comando: gate.tipo, args: [...gate.args], ...conCwd };

    const npmCli = join(dirname(execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js');
    return plataforma === 'win32' && existe(npmCli)
      ? { nombre: gate.nombre, comando: execPath, args: [npmCli, 'run', gate.script], ...conCwd }
      : { nombre: gate.nombre, comando: 'npm', args: ['run', gate.script], ...conCwd, shell: true };
  }
  ```

  Nota sobre la prueba de 4.1: con el nombre repetido, la etiqueta queda en
  `Gate 3` sin nombre, como espera la aserción.

- [ ] **4.5 Configuración del proyecto.** Crear `quality/gates.json` (UTF-8 sin
  BOM; `JSON.parse` falla con BOM, y la puerta se negaría a correr):

  ```json
  {
    "gates": [
      {
        "nombre": "Tests de la puerta",
        "tipo": "node",
        "args": ["--test", "quality/__tests__/*.test.mjs"],
        "motivo": "Va primero: si la propia puerta está rota, el resto de los veredictos no vale nada. Los argumentos de node --test son patrones glob, no directorios."
      },
      { "nombre": "Adaptadores", "tipo": "node", "args": ["quality/check-adaptadores.mjs"] },
      { "nombre": "Mojibake", "tipo": "node", "args": ["quality/check-mojibake.mjs"] },
      { "nombre": "Enlaces", "tipo": "node", "args": ["quality/check-enlaces.mjs"] },
      { "nombre": "Tareas", "tipo": "node", "args": ["quality/check-tareas.mjs"] },
      { "nombre": "Frontend lint", "tipo": "npm", "script": "lint", "cwd": "web" },
      { "nombre": "Frontend build", "tipo": "npm", "script": "build", "cwd": "web" },
      { "nombre": "Frontend tests", "tipo": "npm", "script": "test", "cwd": "web" },
      { "nombre": "Backend build", "tipo": "dotnet", "args": ["build", "Icarus/Icarus.sln", "--nologo"] },
      {
        "nombre": "Backend tests",
        "tipo": "dotnet",
        "args": ["test", "Icarus/Icarus.sln", "--nologo", "--no-build"],
        "motivo": "Incluye los tests de integración con Testcontainers.MsSql: necesita Docker."
      }
    ]
  }
  ```

- [ ] **4.6 Ejecutor genérico.** Sustituir `quality/verify.mjs` completo:

  ```js
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
  ```

- [ ] **4.7 Ver verde.** `node --test quality/__tests__/gates.test.mjs`,
  `node --test quality/__tests__/gates-proyecto.test.mjs` y, sin modificarlo,
  `node --test quality/__tests__/verify.test.mjs`. Comprobar
  `git diff --stat -- quality/__tests__/verify.test.mjs`: vacío.

- [ ] **4.8 Comprobación manual de los criterios 10 y 11.** Copiar
  `quality/gates.json` a un temporal, cambiar `"tipo": "node"` del gate
  `Adaptadores` por `"tipo": "bash"`, ejecutar `node quality/verify.mjs`: debe
  imprimir el motivo y terminar con código 1 sin ejecutar ningún gate. Restaurar.
  Después cambiar `quality/check-mojibake.mjs` por `quality/check-mojibak.mjs` en
  los argumentos, ejecutar de nuevo: falla «Tests de la puerta» (la prueba que
  congela la lista) y termina con «Arregla el contenido, no el gate.». Restaurar y
  comprobar con `git diff -- quality/gates.json` que no quedó nada. Registrar ambas
  comprobaciones con `registrar ... comando`.

---

## Tarea 5. Voseo restante (decisión 3)

Si el usuario respondió que no a la decisión 3, saltar esta tarea y limitar el
criterio 11 a `verify.mjs`.

- [ ] **5.1** En `quality/check-adaptadores.mjs`, sustituir
  `'       Corré: node quality/generar-adaptadores.mjs'` por
  `'       Ejecuta: node quality/generar-adaptadores.mjs'`.
- [ ] **5.2** En `quality/check-mojibake.mjs`, sustituir el mensaje por:

  ```js
      console.log(
        '       Escribe el carácter correcto en UTF-8. Si necesitas citar el ' +
          'patrón en un .md, ponlo entre acentos graves.',
      );
  ```

- [ ] **5.3** En `quality/check-enlaces.mjs`, sustituir
  `'       Corregí el enlace o creá el destino.'` por
  `'       Corrige el enlace o crea el destino.'`.
- [ ] **5.4** En `docs/ai/README.md`, sustituir «que no conocés» por «que no
  conoces».
- [ ] **5.5** Comprobar: `git grep -nE "Arreglá|Corré|Corregí|creá|Escribí|necesitás|ponelo|conocés" -- . ":(exclude)docs/superpowers" ":(exclude)docs/ai/tareas"`
  no devuelve nada, y `node --test "quality/__tests__/*.test.mjs"` sigue verde
  (ninguna prueba cita estos mensajes).

---

## Tarea 6. Registro de decisiones

Criterio de `tarea.md`: 12. Si el usuario eligió otra ruta en la decisión 2,
sustituir `docs/decisiones/` por esa ruta en esta tarea y en las siguientes, y
ajustar los enlaces relativos.

Regla para todos los registros: **no inventes motivos.** Solo 001 lleva motivo,
tomado de la nota de origen de la bitácora. Si el usuario dio motivos al responder
la decisión 4 de `tarea.md`, escríbelos tal cual en su registro.

- [ ] **6.1** Crear `docs/decisiones/README.md`:

  ```markdown
  # Registro de decisiones

  > Portable: este README y [plantilla.md](plantilla.md) forman parte del kit de
  > proceso. Los registros numerados son de cada proyecto.

  Un registro por decisión transversal y duradera: la que condiciona varias
  tareas o módulos y sería cara de revertir. Las decisiones locales de una tarea
  siguen en su spec.

  ## Formato

  - Archivo `NNN-slug.md`: número correlativo de tres cifras y slug en kebab-case
    sin acentos.
  - Los seis apartados de [plantilla.md](plantilla.md), en orden: estado y fecha,
    contexto, decisión, motivo, alternativas descartadas y reconsiderar cuándo.
  - `Alcance` dice si la decisión es del proyecto o del proceso.
  - Corto, unas 40 líneas. Enlaza la spec o el código; no los copies.

  ## Quién escribe qué

  - El agente redacta el contexto, la decisión, las alternativas y la condición
    de reconsideración.
  - **El motivo lo pone el humano.** Si el agente no lo conoce por una fuente
    citable (una respuesta del usuario registrada en una bitácora o un pasaje
    explícito de una spec, enlazado), escribe `PENDIENTE: lo completa el humano.`
    Un motivo inventado es peor que ninguno.
  - El motivo usa datos del sistema cuando existen: volúmenes, tiempos,
    incidentes o costes.

  ## Estados y cambios

  - `Aceptada`: vigente. Una decisión nueva entra con la tarea que la introduce, y
    el humano la acepta al aprobar esa tarea.
  - `Sustituida por NNN`: dejó de estar vigente; enlaza la que la sustituye.
  - Un registro aceptado no se edita. Solo admite dos cambios: sustituir
    `PENDIENTE` por el motivo que da el humano, y pasar el estado a
    `Sustituida por NNN` en el mismo cambio que crea el registro nuevo.
  - Para cambiar una decisión se crea un registro nuevo que explica por qué
    sustituye al anterior.

  ## Lectura obligatoria

  - Planificador, desarrollador y revisor listan esta carpeta y leen las
    decisiones aceptadas antes de diseñar, implementar o revisar. Son cortas a
    propósito.
  - Si la tarea contradice una decisión, o se cumple su condición de
    «Reconsiderar cuándo», el agente para: registra la pregunta con
    `node quality/tarea.mjs registrar <id> decision` y pasa a `ESPERA_DECISION`.
    No la reinterpreta ni la ignora.
  ```

- [ ] **6.2** Crear `docs/decisiones/plantilla.md`:

  ```markdown
  # NNN. Título en forma de decisión

  > Portable: plantilla del kit de proceso. Copiarla como `NNN-slug.md` y borrar
  > esta nota.

  - Estado: Aceptada
  - Fecha: AAAA-MM-DD
  - Alcance: <proyecto> | proceso

  ## Contexto

  Qué situación obliga a decidir. Enlaza la spec, la tarea o el código.

  ## Decisión

  Qué se decide, en presente y sin ambigüedad.

  ## Motivo

  PENDIENTE: lo completa el humano.

  ## Alternativas descartadas

  - Alternativa: por qué no.

  ## Reconsiderar cuándo

  - Condición observable que obligaría a revisar la decisión.
  ```

- [ ] **6.3** Crear `docs/decisiones/001-piloto-kit-proceso.md`:

  ```markdown
  # 001. Trajano-Icarus es el piloto del kit de proceso

  - Estado: Aceptada
  - Fecha: 2026-11-03
  - Alcance: proceso

  ## Contexto

  El sistema de agentes por rol, las tareas con bitácora, la puerta de calidad
  en Node y la documentación de `docs/ai/` nacieron en este repositorio. Otros
  proyectos comparten la base técnica: .NET 10 con building blocks, Serilog, Seq,
  Testcontainers, React y puerta en Node. dev_Caserito, por ejemplo, tiene gates
  deterministas que aquí no existen (TDD, formato, complejidad, cobertura y
  mutación) y no tiene el sistema de agentes. Origen:
  [tarea 2026-11-03-proceso-portable-evidencia-decisiones](../ai/tareas/2026-11-03-proceso-portable-evidencia-decisiones/tarea.md).

  ## Decisión

  Trajano-Icarus es el piloto de un kit de proceso reutilizable. Cada pieza de
  proceso separa visiblemente lo genérico de lo específico del proyecto, y el
  inventario vive en [KIT_PROCESO.md](../ai/KIT_PROCESO.md). El kit no se extrae a
  un repositorio propio hasta que un segundo proyecto lo use.

  ## Motivo

  Del usuario, en la nota de origen de la tarea (2026-11-03): el proceso se
  replicará en varios proyectos con la misma base, y no se extrae nada antes del
  segundo uso real.

  ## Alternativas descartadas

  - Extraer ya un repositorio-kit: con un solo uso, la frontera entre lo genérico
    y lo propio se adivinaría.
  - Copiar los archivos a cada proyecto sin separarlos: cada copia divergiría y no
    se sabría qué puede actualizarse desde el piloto.

  ## Reconsiderar cuándo

  - Cuando un segundo proyecto adopte el kit: toca extraerlo.
  - Cuando una pieza marcada como portable necesite cambios para encajar en otro
    proyecto: la frontera estaba mal trazada.
  ```

- [ ] **6.4** Crear `docs/decisiones/002-errores-por-excepciones-de-dominio.md`:

  ```markdown
  # 002. Errores por excepciones de dominio, no Result pattern

  - Estado: Aceptada
  - Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
  - Alcance: Trajano-Icarus

  ## Contexto

  El dominio y la aplicación señalan los errores esperados lanzando excepciones
  que derivan de `DomainException`: `NotFoundException`, `ConflictException` y
  `ForbiddenException` en
  `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs`, y
  `ReglaNegocioException` en `ReglaNegocioException.cs`, en la misma carpeta.
  `ExceptionHandlingMiddleware`, en
  `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/`, las traduce a
  respuestas HTTP.

  ## Decisión

  Los errores de regla de negocio, de existencia, de conflicto y de permiso se
  comunican con esas excepciones. Los handlers no devuelven `Result<T>` ni tipos
  equivalentes.

  ## Motivo

  PENDIENTE: lo completa el humano.

  ## Alternativas descartadas

  - Result pattern (`Result<T>` con errores tipados): hace explícito el error en
    la firma, a cambio de propagarlo a mano en cada capa. Por qué se descartó es
    parte del motivo pendiente.

  ## Reconsiderar cuándo

  - Si una operación necesita devolver varios errores de negocio a la vez.
  - Si los errores esperados se vuelven frecuentes y su coste aparece en la
    telemetría.
  ```

- [ ] **6.5** Crear `docs/decisiones/003-multi-tenant-por-filas-y-claims.md`:

  ```markdown
  # 003. Multi-tenant por filas y claims

  - Estado: Aceptada
  - Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
  - Alcance: Trajano-Icarus

  ## Contexto

  El Cliente es el tenant: sus datos operativos no se mezclan con los de otros
  clientes. Antes del registro estaba en `docs/proyecto/arquitectura.md` §9.
  Detalle en [identidad, clientes y permisos](../proyecto/identidad-clientes-permisos.md)
  y en el [modelo de datos](../proyecto/datos.md).

  ## Decisión

  Todos los clientes comparten una base de datos. Cada fila de datos de un
  cliente lo identifica, y los filtros de los contextos de EF Core toman el
  cliente activo de los claims del token. No hay una base ni un esquema por
  cliente.

  ## Motivo

  PENDIENTE: lo completa el humano.

  ## Alternativas descartadas

  - Una base de datos por cliente.
  - Un esquema por cliente en la misma base.

  ## Reconsiderar cuándo

  - Si un cliente exige aislamiento físico de sus datos por contrato o por norma.
  - Si el volumen de un cliente degrada el servicio de los demás.
  ```

- [ ] **6.6** Crear `docs/decisiones/004-soft-delete-en-agregados.md`:

  ```markdown
  # 004. Soft delete en los agregados de negocio

  - Estado: Aceptada
  - Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
  - Alcance: Trajano-Icarus

  ## Contexto

  La regla viene de ICARUS y se rescató al glosario al arrancar el proyecto
  ([spec del andamiaje](../superpowers/specs/2026-08-12-andamiaje-instrucciones-agentes-design.md)):
  las entidades se desactivan con `EstaActivo = false`. Antes del registro
  estaba en `docs/proyecto/arquitectura.md` §9.

  ## Decisión

  Los agregados de negocio se desactivan, no se borran. Los historiales no se
  borran físicamente.

  ## Motivo

  PENDIENTE: lo completa el humano.

  ## Alternativas descartadas

  - Borrado físico con tablas de auditoría aparte.

  ## Reconsiderar cuándo

  - Si una norma de protección de datos exige borrar de verdad datos personales.
  - Si el volumen de registros inactivos degrada las consultas.
  ```

- [ ] **6.7** Crear `docs/decisiones/005-fechas-de-negocio-bolivia-y-utc.md`:

  ```markdown
  # 005. Fechas de negocio de Bolivia e instantes técnicos en UTC

  - Estado: Aceptada
  - Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
  - Alcance: Trajano-Icarus

  ## Contexto

  Las operaciones comerciales y la asistencia se razonan por día de calendario
  local; los instantes técnicos necesitan una referencia sin zona. Antes del
  registro estaba en `docs/proyecto/arquitectura.md` §9.

  ## Decisión

  Las operaciones comerciales y la asistencia usan la fecha de negocio de
  Bolivia. Los instantes técnicos se persisten en UTC donde el modelo lo
  requiere.

  ## Motivo

  PENDIENTE: lo completa el humano.

  ## Alternativas descartadas

  - Todo en UTC, con la conversión solo en la interfaz.

  ## Reconsiderar cuándo

  - Si se incorpora un cliente que opera en otra zona horaria.
  ```

- [ ] **6.8** Crear `docs/decisiones/006-online-deliberado-pedidos-y-despachos.md`:

  ```markdown
  # 006. Online deliberado para pedidos y despachos

  - Estado: Aceptada
  - Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
  - Alcance: Trajano-Icarus

  ## Contexto

  La PWA trabaja sin conexión solo para la recogida y la mortalidad, con una cola
  en IndexedDB ([recogida y mortalidad](../proyecto/recogida-huevos-mortalidad.md)).
  La [spec de SP8](../superpowers/specs/2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md)
  afirma que el pedido móvil de ICARUS no era realmente offline y que SP8 es
  deliberadamente online; la
  [bandeja de despachos](../superpowers/specs/2026-09-11-sp9f-bandeja-novedades-despacho-huevo-pwa-design.md)
  sigue el mismo criterio. Antes del registro estaba en
  `docs/proyecto/arquitectura.md` §9.

  ## Decisión

  Los pedidos de alimento y los despachos de huevo solo funcionan con conexión:
  sin cola offline ni IndexedDB. El modo offline queda limitado a la recogida y la
  mortalidad.

  ## Motivo

  PENDIENTE: lo completa el humano.

  ## Alternativas descartadas

  - Cola offline también para pedidos y despachos.

  ## Reconsiderar cuándo

  - Si los usuarios de galpones sin cobertura necesitan registrar pedidos o
    despachos sin red.
  ```

- [ ] **6.9** Crear `docs/decisiones/007-sin-procesos-programados.md`:

  ```markdown
  # 007. Sin procesos programados para vigencias o crédito

  - Estado: Aceptada
  - Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
  - Alcance: Trajano-Icarus

  ## Contexto

  Las vigencias de precios y el crédito cambian con el tiempo. La
  [campana de tareas de vacunación](../superpowers/specs/2026-11-01-campana-tareas-vacunacion-trabajador-design.md)
  aplica el mismo criterio: la consulta es la fuente de verdad, sin jobs ni push.
  Antes del registro estaba en `docs/proyecto/arquitectura.md` §9.

  ## Decisión

  No hay procesos programados para vigencias ni crédito: se resuelven al
  consultar o al ejecutar el comando correspondiente.

  ## Motivo

  PENDIENTE: lo completa el humano.

  ## Alternativas descartadas

  - Un proceso en segundo plano que actualice vigencias y saldos a hora fija.

  ## Reconsiderar cuándo

  - Si una regla tiene que actuar sin que nadie consulte, por ejemplo un aviso de
    vencimiento a una hora fija.
  - Si calcular al consultar se vuelve lento.
  ```

- [ ] **6.10** Crear `docs/decisiones/008-notificaciones-internas-persistentes.md`:

  ```markdown
  # 008. Notificaciones internas persistentes

  - Estado: Aceptada
  - Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
  - Alcance: Trajano-Icarus

  ## Contexto

  La [spec de SP8](../superpowers/specs/2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md),
  en «Notificaciones internas», define las notificaciones como entidades
  persistentes que cada destinatario lista, cuenta y marca como leídas; el aviso
  en tiempo real es un añadido y el registro persistente es la fuente de verdad.
  Antes del registro estaba en `docs/proyecto/arquitectura.md` §9.

  ## Decisión

  Las notificaciones internas se persisten. Las notificaciones push o en tiempo
  real nunca son la fuente de verdad.

  ## Motivo

  PENDIENTE: lo completa el humano.

  ## Alternativas descartadas

  - Push como único canal, sin registro persistente.

  ## Reconsiderar cuándo

  - Si hace falta avisar con la aplicación cerrada.
  ```

- [ ] **6.11** En `docs/proyecto/arquitectura.md`, sustituir la sección 9 completa
  (desde `## 9. Decisiones transversales` hasta la línea anterior a
  `## 10. Fuentes principales`) por:

  ```markdown
  ## 9. Decisiones transversales

  Cada decisión tiene su registro, con contexto, motivo, alternativas y cuándo
  reconsiderarla, en el [registro de decisiones](../decisiones/README.md):

  - [002](../decisiones/002-errores-por-excepciones-de-dominio.md): errores por
    excepciones de dominio, no Result pattern.
  - [003](../decisiones/003-multi-tenant-por-filas-y-claims.md): multi-tenant por
    filas y claims, no una base por cliente.
  - [004](../decisiones/004-soft-delete-en-agregados.md): soft delete en los
    agregados de negocio; los historiales no se borran físicamente.
  - [005](../decisiones/005-fechas-de-negocio-bolivia-y-utc.md): fechas de negocio
    de Bolivia; instantes técnicos en UTC donde el modelo lo requiere.
  - [006](../decisiones/006-online-deliberado-pedidos-y-despachos.md): online
    deliberado para pedidos y despachos; offline solo para recogida y mortalidad.
  - [007](../decisiones/007-sin-procesos-programados.md): sin procesos programados
    para vigencias o crédito.
  - [008](../decisiones/008-notificaciones-internas-persistentes.md):
    notificaciones internas persistentes, no push como fuente de verdad.

  ```

- [ ] **6.12** En `docs/proyecto/README.md`, en «Fuentes y convenciones», añadir
  tras la viñeta del glosario:

  ```markdown
  - Decisiones transversales: [registro de decisiones](../decisiones/README.md).
  ```

- [ ] **6.13** `node quality/check-enlaces.mjs` y `node quality/check-mojibake.mjs`:
  verdes.

---

## Tarea 7. Checklists de revisión

Criterio de `tarea.md`: 14.

- [ ] **7.1** Crear `docs/ai/revision/comun.md`:

  ```markdown
  # Checklist de revisión común

  > Portable: vale para cualquier proyecto con la misma base (.NET con building
  > blocks, React y puerta en Node). Lo propio de cada proyecto va en
  > [dominio.md](dominio.md).

  Se aplica en toda revisión, ligera o profunda, sobre el diff de la tarea. Cada
  hallazgo indica archivo y línea, gravedad (grave, media o menor), escenario
  concreto de fallo y evidencia.

  ## 1. Criterios y evidencia

  - Cada prueba citada como evidencia comprueba de verdad lo que dice su
    criterio. Léela: no te fíes del nombre.
  - Un criterio con evidencia `manual` que podía automatizarse con sensatez es
    hallazgo medio.
  - El rojo registrado describe un fallo por el motivo correcto (falta la regla),
    no un error de compilación o de importación ajeno al criterio.

  ## 2. Pruebas vacías o engañosas

  Grave si la prueba es la única evidencia de un criterio; media en otro caso.

  - Aserción débil: solo comprueba que algo no es nulo, existe o es verdadero
    (`Assert.NotNull`, `Assert.True(true)`, `toBeDefined()`, `toBeTruthy()`,
    `assert.ok(x)` sobre un valor que siempre existe) sin mirar el valor que
    importa.
  - Sin aserción, o con la aserción en una rama que no se ejecuta.
  - Prueba del doble: verifica lo que se configuró en el mock o el stub, no el
    comportamiento del código.
  - Fórmula duplicada: calcula el esperado con la misma fórmula que el código. El
    esperado se escribe literal.
  - Un `try`/`catch` que traga el fallo, o un `catch` vacío.
  - Nombre engañoso: promete algo que el cuerpo no comprueba.
  - Prueba omitida o exclusiva: `Skip`, `.skip`, `.only`, `.todo`.

  ## 3. Pruebas existentes modificadas

  - Lista las pruebas existentes que toca el diff (`git diff --stat` sobre los
    archivos de prueba) y mira en cada una las líneas borradas.
  - Cada aserción cambiada, debilitada o borrada responde a un cambio de
    comportamiento que acuerda `tarea.md`. Si no, es grave: la prueba se adaptó al
    código.

  ## 4. .NET

  - Inyección de dependencias: cada registro nuevo o cambiado tiene el lifetime
    correcto. Un singleton que captura un servicio scoped (DbContext, usuario
    actual, unidad de trabajo) es grave. Un lifetime distinto del habitual del
    proyecto lleva un comentario con su motivo; si falta, hallazgo menor.
  - `CancellationToken`: se recibe y se propaga hasta EF Core, `HttpClient` y toda
    llamada asíncrona. `CancellationToken.None` o `default` donde había un token
    disponible es hallazgo medio.
  - Prohibido bloquear lo asíncrono: `.Result`, `.Wait()` y
    `.GetAwaiter().GetResult()`. `async void` solo en manejadores de eventos.
  - Prohibido el operador `!` para silenciar una advertencia de nulabilidad: se
    corrige el flujo o se valida. Lo mismo vale para `#pragma warning disable` y
    `SuppressMessage` sin motivo escrito.

  ## 5. TypeScript y React

  - Prohibido silenciar el compilador o el linter con `any`, `as unknown as`,
    `// @ts-ignore`, `// @ts-expect-error` o `eslint-disable` sin motivo escrito.

  ## 6. Seguridad básica

  - Cada endpoint nuevo declara su autorización; ninguno queda anónimo por
    omisión.
  - Ninguna credencial, token o secreto en el código, la configuración
    versionada, los logs ni los mensajes de error. Los errores al cliente son
    genéricos.
  - La entrada externa se valida antes de usarla.

  ## 7. Proceso y puerta

  - Un cambio en `quality/` o en la lista de gates no quita un gate, no debilita
    sus argumentos, no sube un umbral y no amplía una exclusión.
  - El cambio no contradice una decisión aceptada de `docs/decisiones/` sin un
    registro nuevo que la sustituya. Si lo hace, es grave.
  - No hay alcance que `tarea.md` no pida.
  ```

- [ ] **7.2** Crear `docs/ai/revision/dominio.md`:

  ```markdown
  # Checklist de revisión de dominio

  > Propio de Trajano-Icarus. Complementa al [checklist común](comun.md) en la
  > revisión profunda. Otro proyecto escribe el suyo.

  ## Cuándo la revisión es profunda

  Es obligatoria si el diff toca alguna de estas zonas, o si el orquestador la
  pide con una nota al avanzar a `REVISION`:

  - `quality/`, `verify.ps1`, `verify.sh` y `.github/workflows/`.
  - Migraciones: `Icarus/src/*/*.Infrastructure/Migrations/`.
  - Identidad y autorización: `Icarus/src/Identity/`, políticas y atributos de
    autorización, entitlement por módulo o funcionalidad, y la sesión de
    `Icarus/src/Apps/Trajano.GestorCaisy/`.
  - Aislamiento de tenant: filtros de consulta, `IgnoreQueryFilters`,
    `IClienteActivo` e `ICurrentUser`.
  - Control de acceso y biometría: `Icarus/src/ControlAcceso/` y la integración
    con ARGOS.
  - Máquinas de estados y reglas de negocio delicadas: pedidos, despachos,
    recepciones, crédito, precios congelados, cupo semanal y vacunación.

  ## Qué compruebas

  - **Tenant:** toda consulta de datos de un cliente pasa por el filtro del
    cliente activo; `IgnoreQueryFilters` solo en cuentas globales y con su motivo.
    El Cliente es el tenant ([decisión 003](../../decisiones/003-multi-tenant-por-filas-y-claims.md)).
  - **Autorización:** cada endpoint exige rol y, donde corresponde, módulo o
    funcionalidad. En GestorCaisy, rol `GestorCaisy` más la funcionalidad de la
    sección.
  - **Anti-PII:** ningún dato biométrico, documento de identidad, registro
    nominal de acceso de trabajadores, credencial ni token en logs, errores,
    bitácoras o fixtures.
  - **Migraciones:** reversibles, seguras con los datos existentes y con la
    concurrencia prevista.
  - **Reglas de negocio:** contra el
    [glosario avícola](../../dominio/glosario-avicola.md), sin suponer ninguna.
  - **Máquinas de estados:** cada transición contra
    [máquinas de estados](../../proyecto/maquinas-estados.md).
  - **Fechas:** fecha de negocio de Bolivia e instantes técnicos en UTC
    ([decisión 005](../../decisiones/005-fechas-de-negocio-bolivia-y-utc.md)).
  - **Offline:** solo recogida y mortalidad, con cola idempotente; pedidos y
    despachos online
    ([decisión 006](../../decisiones/006-online-deliberado-pedidos-y-despachos.md)).
  - **GestorCaisy:** solo HTTP contra la API, sin DbContext ni SQL; tokens en la
    cookie protegida HttpOnly, nunca en localStorage; sin service worker, caché
    offline ni IndexedDB.
  - **Errores:** excepciones de dominio
    ([decisión 002](../../decisiones/002-errores-por-excepciones-de-dominio.md)).
  ```

- [ ] **7.3** `node quality/check-enlaces.mjs`: verde.

---

## Tarea 8. Roles y adaptadores

Criterios de `tarea.md`: 13 y la parte del revisor del 14. Los roles no admiten
enlaces relativos (prueba `adaptadores-roles.test.mjs`): las rutas van entre
acentos graves.

- [ ] **8.1** Sustituir el cuerpo de `docs/ai/agentes/roles/planificador.md`
  (desde `## Entradas` hasta el final) por:

  ```markdown
  ## Entradas

  - `docs/ai/tareas/<id>/tarea.md`.
  - Las decisiones aceptadas de `docs/decisiones/`.
  - `docs/dominio/glosario-avicola.md` y `docs/proyecto/`, solo lo relacionado.
  - El código y las pruebas del área afectada.

  ## Salidas

  - `tarea.md` completo: pedido, alcance, fuera de alcance, criterios de aceptación
    con su evidencia y plan de pruebas.
  - Para features y bloques, spec y plan según `docs/ai/WORKFLOW.md`.

  ## Qué haces

  1. Lee las decisiones aceptadas. Si el pedido contradice una, o cumple su
     condición de «Reconsiderar cuándo», registra la pregunta con
     `registrar <id> decision` y avanza a `ESPERA_DECISION`.
  2. Busca qué existe antes de proponer nada. Sigue los patrones del área.
  3. Escribe cada criterio de aceptación como un resultado observable, en la
     pantalla, en una respuesta de la API o en una terminal. Esos criterios guían
     la prueba manual.
  4. Declara la evidencia de cada criterio con el formato de
     `docs/ai/tareas/README.md`: una prueba automática, con ruta y nombre
     propuesto, que se verá en rojo antes de implementar; o `manual`, con la
     justificación de por qué no se automatiza. «Compila y las pruebas pasan» no
     es evidencia.
  5. Indica qué otras pruebas automáticas harán falta y qué riesgos conoces.
  6. En el alcance, indica qué documentos de `docs/proyecto/` y del glosario debe
     actualizar el desarrollador, o «ninguno», y qué decisiones nuevas debe
     registrar en `docs/decisiones/`, o «ninguna». Tras la aprobación ya no se
     editan.
  7. Avanza a `DESARROLLO`: la CLI se niega si no hay criterios o si alguno no
     tiene evidencia válida. Si hay una regla de negocio ambigua, registra la
     pregunta con `registrar <id> decision` y avanza a `ESPERA_DECISION`.

  ## Límites

  - No inventas reglas: consultas el glosario y, si no alcanza, preguntas.
  - No inventas el motivo de una decisión: si no lo conoces, queda `PENDIENTE`
    para el humano.
  - No amplías el alcance pedido.
  - No registras datos personales ni credenciales.

  ## Termina cuando

  Cada criterio es comprobable y tiene evidencia, y no quedan decisiones abiertas,
  o la tarea está en `ESPERA_DECISION` con la pregunta registrada.

  ## Informe final

  Tu último mensaje debe ser completo y autosuficiente: alcance decidido, criterios
  con su evidencia, riesgos y siguiente estado.
  ```

  Y en el frontmatter, `descripcion`:

  ```yaml
  descripcion: Analiza requisitos de Trajano-Icarus y define alcance, criterios de aceptación con su evidencia, cambios y pruebas
  ```

- [ ] **8.2** En `docs/ai/agentes/roles/desarrollador.md`:

  En «Entradas», añadir al final:

  ```markdown
  - Las decisiones aceptadas de `docs/decisiones/` que toquen el área.
  ```

  Sustituir los pasos 1 a 3 de «Qué haces» por:

  ```markdown
  1. Por cada criterio con evidencia `test`, escribe la prueba con el nombre que
     figura en `tarea.md`, mírala fallar por el motivo correcto y registra el rojo
     en cuanto lo veas:
     `registrar <id> comando --texto "Rojo criterio <N>: <comando> — <motivo del fallo>"`.
     Después implementa lo mínimo y mírala pasar. Una prueba que nunca viste en
     rojo no prueba nada. Puedes ajustar ruta o nombre actualizando la línea
     `Evidencia:`; cambiar una evidencia `test` por `manual` exige volver a
     `PLANIFICACION`.
  2. En una corrección, reproduce primero el fallo con una prueba.
  3. Si el cambio altera el dominio o el comportamiento, actualiza los documentos
     de `docs/proyecto/` y del glosario que fija el alcance, y escribe en
     `docs/decisiones/` las decisiones nuevas que fija, con su plantilla. El motivo
     queda `PENDIENTE` salvo que el humano lo haya dado. Enlaza en lugar de
     copiar.
  ```

  En «Límites», tras «Si la regla de negocio no está clara, ve a `ESPERA_DECISION`.»:

  ```markdown
  - Si el cambio contradice una decisión aceptada, o cumple su condición de
    reconsideración, ve a `ESPERA_DECISION`.
  ```

  Sustituir «Termina cuando»:

  ```markdown
  ## Termina cuando

  Cada prueba citada como evidencia pasa después de haberla visto en rojo, con su
  rojo registrado, y la tarea está en `VERIFICACION`. Si no puedes llegar ahí,
  registra el bloqueo con evidencia.
  ```

- [ ] **8.3** En `docs/ai/agentes/roles/verificador.md`:

  En «Entradas», sustituir la viñeta de `tarea.md` por:

  ```markdown
  - `docs/ai/tareas/<id>/tarea.md`, para saber qué debería comprobarse y qué
    evidencia declara cada criterio.
  ```

  Sustituir el paso 2 y el 4 de «Qué haces»:

  ```markdown
  2. Ejecuta `node quality/tarea.mjs verificar <id> --rol verificador --proveedor <p>`.
     Esa orden corre la puerta completa y escribe la evidencia. Tarda varios
     minutos. El gate `Tareas` comprueba que cada prueba citada en `tarea.md`
     exista y que su rojo esté registrado.
  ```

  ```markdown
  4. Si sale verde, avanza a `REVISION`, salvo que el cambio sea exclusivamente
     documental: entonces avanza a `ENTORNO`.
  ```

  Sustituir «Informe final»:

  ```markdown
  ## Informe final

  Tu último mensaje debe ser completo y autosuficiente: resultado, árbol y commit
  verificados, ruta del registro, gates no ejecutados con su motivo, y cada
  criterio con su evidencia: las pruebas, cubiertas por la puerta, y los criterios
  manuales, que quedan para la prueba manual del usuario.
  ```

  **Si la decisión 1 fue no:** el paso 4 queda como estaba.

- [ ] **8.4** Sustituir `docs/ai/agentes/roles/revisor.md` completo por:

  ```markdown
  ---
  nombre: revisor
  descripcion: Revisa el diff de cada tarea de código de Trajano-Icarus con un checklist común y, en los cambios delicados, también con el de dominio
  cuando_usar: En el estado REVISION, en toda tarea que cambia código, pruebas o configuración; con revisión profunda en las zonas sensibles o a pedido del orquestador
  nivel: alto
  herramientas: [leer, buscar, shell]
  delega_en: []
  ---

  # Revisor

  Buscas defectos que las pruebas no detectan. No corriges nada.

  Antes de actuar, lee `docs/ai/agentes/PROTOCOLO.md` y ejecuta
  `node quality/tarea.mjs resumen <id>`.

  ## Entradas

  - El diff de la tarea: `git diff --stat` primero y luego lo relevante.
  - `tarea.md`, con sus criterios y su evidencia, y los rojos de la bitácora.
  - `docs/ai/revision/comun.md`, siempre.
  - En la revisión profunda, además `docs/ai/revision/dominio.md`, el glosario y el
    documento de `docs/proyecto/` del área.
  - Las decisiones aceptadas de `docs/decisiones/` que toquen el área.

  ## Profundidad

  - **Ligera:** el checklist común sobre el diff.
  - **Profunda:** el común y el de dominio. Es obligatoria si el diff toca una zona
    que lista `docs/ai/revision/dominio.md`, o si el orquestador la pidió en la nota
    del paso a `REVISION`. Decídelo leyendo las rutas del diff y dilo en tu
    informe.

  ## Qué compruebas

  Cada punto de los checklists que aplique al diff. Un cambio que contradice una
  decisión aceptada sin un registro nuevo que la sustituya es un hallazgo grave.

  ## Salida

  Cada hallazgo indica archivo y línea, gravedad, escenario concreto de fallo y
  evidencia. Regístralo con `registrar <id> nota`. Si no hay hallazgos, dilo.

  ## Qué haces después

  - Con hallazgos graves, devuelve la tarea a `DESARROLLO`.
  - Con una regla dudosa, o una decisión aceptada en conflicto, pasa a
    `ESPERA_DECISION`.
  - Sin hallazgos que bloqueen, avanza a `ENTORNO`.

  ## Límites

  - No editas nada.
  - No opinas sobre estilo si no afecta al funcionamiento.
  - No apruebas la funcionalidad: tu revisión no sustituye la prueba manual.

  ## Termina cuando

  Cada hallazgo es verificable o declaras que no hay hallazgos, y la tarea avanzó.

  ## Informe final

  Tu último mensaje debe ser completo y autosuficiente: profundidad aplicada y por
  qué, hallazgos por gravedad, qué revisaste y qué no.
  ```

  **Si la decisión 1 fue no:** `cuando_usar` queda «En el estado REVISION, cuando el
  orquestador lo pide por el riesgo del cambio», y «Profundidad» se sustituye por:
  «Aplica siempre los dos checklists: el común y el de dominio.»

- [ ] **8.5** En `docs/ai/agentes/roles/orquestador.md`, sustituir el paso 5 por:

  ```markdown
  5. Tras una verificación verde, toda tarea que cambia código, pruebas o
     configuración pasa por `REVISION`; solo una exclusivamente documental va
     directa a `ENTORNO`. El revisor decide la profundidad con
     `docs/ai/revision/dominio.md`. Si conoces un riesgo que las rutas del diff no
     muestran, pide la revisión profunda con
     `--nota "revisión profunda: <motivo>"` al avanzar.
  ```

  **Si la decisión 1 fue no:** el paso 5 queda como estaba.

- [ ] **8.6** En `docs/ai/agentes/roles/operaciones.md`, sustituir el paso 5 por:

  ```markdown
  5. Indica al usuario la URL y los criterios de aceptación de `tarea.md`,
     empezando por los de evidencia manual: son los que solo él comprueba.
  ```

- [ ] **8.7** `node quality/generar-adaptadores.mjs`, luego
  `node quality/check-adaptadores.mjs` y
  `node --test quality/__tests__/adaptadores-roles.test.mjs quality/__tests__/manifiesto.test.mjs`:
  verdes.

---

## Tarea 9. Documentación de proceso, producto e inventario

Criterios de `tarea.md`: 1 (README), 15 y 16.

- [ ] **9.1** En `docs/ai/tareas/README.md`, añadir esta sección justo antes de
  `## Consultar`:

  ````markdown
  ## Criterios con evidencia

  > Portable: el formato y su comprobación forman parte del kit de proceso.

  Cada criterio de aceptación de `tarea.md` declara cómo se demuestra. Un
  criterio sin evidencia es un deseo, y «compila y las pruebas pasan» no es
  evidencia: ya pasaban antes del cambio.

  ```markdown
  ## Criterios de aceptación

  1. Al guardar una recogida con más descarte que el total, la pantalla muestra
     el aviso y no guarda nada.
     Evidencia: test `web/src/gestion-avicola/Recogida.test.tsx` «rechaza más descarte que el total»
     Evidencia: manual — el aviso se valida en el dispositivo del galpón.
  ```

  - Criterios numerados al inicio de línea, dentro de `## Criterios de aceptación`.
    Un encabezado `###` agrupa y cierra el criterio anterior. Los números no se
    repiten.
  - Cada criterio lleva al menos una línea `Evidencia:`, de uno de dos tipos:
    - ``Evidencia: test `<ruta>` «<nombre>»``: el nombre aparece literal en el
      archivo de pruebas (el texto de `test(...)` o `it(...)`, o el nombre del
      método en .NET).
    - `Evidencia: manual — <justificación>`: solo lo que no se automatiza con
      sensatez. La justificación dice por qué, con 10 caracteres como mínimo.
  - El planificador propone ruta y nombre; el desarrollador puede ajustarlos al
    escribir la prueba. Cambiar `test` por `manual` exige volver a
    `PLANIFICACION`.
  - El desarrollador registra el rojo de cada criterio con prueba en cuanto lo ve:
    `node quality/tarea.mjs registrar <id> comando --texto "Rojo criterio <N>: <comando> — <motivo del fallo>" --rol desarrollador --proveedor <p>`.

  Lo comprueban la CLI y el gate `Tareas`:

  - `avanzar` de `PLANIFICACION` a `DESARROLLO` exige al menos un criterio y que
    todos tengan evidencia válida.
  - De `DESARROLLO` a `DOCUMENTACION`, el gate exige el formato.
  - Desde `VERIFICACION`, además, que cada prueba citada exista y contenga su
    nombre, y que cada criterio con prueba tenga su «Rojo criterio N:».
  - Las tareas cerradas no se revisan: son historia, y sus pruebas pueden haberse
    movido.

  ````

- [ ] **9.2** En `docs/ai/agentes/PROTOCOLO.md`:

  En «Principios», tras «La evidencia la escribe la CLI, no el agente.», añadir:

  ```markdown
  - Cada criterio de aceptación declara su evidencia: una prueba vista en rojo o
    una comprobación manual justificada. Formato en
    [tareas/README.md](../tareas/README.md).
  - Las decisiones aceptadas del [registro de decisiones](../../decisiones/README.md)
    mandan. Si una tarea contradice una, o cumple su condición de
    reconsideración, pasa a `ESPERA_DECISION`.
  ```

  Añadir esta sección justo antes de `## Rechazo tras la prueba manual`:

  ```markdown
  ## Revisión

  Toda tarea que cambia código, pruebas o configuración pasa por `REVISION` tras
  la verificación. La revisión ligera aplica el
  [checklist común](../revision/comun.md); la profunda añade el
  [de dominio](../revision/dominio.md) y es obligatoria en las zonas que este
  lista. Una tarea exclusivamente documental va de `VERIFICACION` a `ENTORNO`.

  ```

  **Si la decisión 1 fue no:** la sección dice en su lugar: «El orquestador decide
  si interviene el revisor; es obligatorio en las zonas que lista el
  `[checklist de dominio](../revision/dominio.md)`. El revisor aplica siempre el
  `[checklist común](../revision/comun.md)` y el de dominio.» (Escribe los
  enlaces sin los acentos graves, que aquí solo evitan que el gate Enlaces los
  resuelva desde este plan.)

  En el bloque de «Comandos», tras la línea de `registrar`, añadir:

  ```text
  node quality/tarea.mjs registrar <id> comando --texto "Rojo criterio <N>: <comando> — <motivo del fallo>" --rol desarrollador --proveedor <p>
  ```

- [ ] **9.3** En `docs/ai/agentes/README.md`, en la tabla «Dónde está cada cosa»,
  añadir tras la fila de `docs/ai/tareas/INDICE.md`:

  ```markdown
  | `docs/ai/revision/` | Checklists del revisor: común (portable) y de dominio |
  | `docs/decisiones/` | Registro de decisiones transversales |
  ```

- [ ] **9.4** En `AGENTS.md`, sustituir la viñeta de «Selección de proceso»:

  ```markdown
  - Feature, bloque o cambio arquitectónico: seguir `docs/ai/WORKFLOW.md` desde
    brainstorming hasta cierre, leyendo antes las decisiones aceptadas de
    `docs/decisiones/`.
  ```

  Después, `node quality/generar-adaptadores.mjs`.

- [ ] **9.5** En `docs/ai/README.md`, en la tabla «Documentos», añadir tras la
  fila de `tareas/README.md`:

  ```markdown
  | [../decisiones/README.md](../decisiones/README.md) | Antes de diseñar, implementar o revisar: decisiones aceptadas |
  | [revision/comun.md](revision/comun.md) | Al revisar un diff |
  | [revision/dominio.md](revision/dominio.md) | En la revisión profunda |
  | [KIT_PROCESO.md](KIT_PROCESO.md) | Al llevar el proceso a otro proyecto: qué es portable y qué es propio |
  ```

- [ ] **9.6** En `docs/ai/WORKFLOW.md`, en «El ciclo»:

  Sustituir el paso 2:

  ```markdown
  2. **Brainstorming.** Leer antes las decisiones aceptadas de `docs/decisiones/`;
     si la tarea choca con una, se pregunta. Explorar intención, alternativas y
     restricciones antes de diseñar. Termina cuando las decisiones están tomadas,
     no cuando se acaban las ideas.
  ```

  Añadir al final del paso 3: «Si fija una decisión transversal y duradera,
  además se registra en `docs/decisiones/` con su plantilla; el motivo lo pone el
  humano.»

  Añadir al final del paso 4: «Cada criterio de aceptación declara su evidencia,
  con el formato de `docs/ai/tareas/README.md`.»

  Sustituir el paso 7:

  ```markdown
  7. **Revisión.** No hay revisor humano de código: la revisión es la puerta, el
     agente revisor con los checklists de `docs/ai/revision/` y una lectura del
     diff propio antes de hacer push.
  ```

- [ ] **9.7** En `docs/ai/PUERTA_CALIDAD.md`:

  En «Cómo se ejecuta», sustituir la frase que empieza en «Ambos son envoltorios»
  por:

  ```markdown
  En POSIX, `./verify.sh`. Ambos son envoltorios de `node quality/verify.mjs`, que
  lee la lista de gates de `quality/gates.json`, los ejecuta en orden y **se
  detiene en el primero que falla** para dar retroalimentación rápida. Si la
  configuración falta o no es válida, la puerta falla sin ejecutar ningún gate.
  ```

  En la fila `Tareas` de la tabla, sustituir la segunda y la tercera columna por:

  ```markdown
  | Tareas | Que cada bitácora de `docs/ai/tareas/` sea una historia legal, con aprobaciones humanas y sin credenciales reconocibles; que toda tarea cerrada tenga una `ficha.md` válida y que `INDICE.md` coincida con las fichas; que las tareas activas declaren la evidencia de cada criterio y, desde `VERIFICACION`, que sus pruebas existan y su rojo esté registrado | Registrar eventos con `quality/tarea.mjs`; completar la ficha y ejecutar `node quality/tarea.mjs indice`; completar la evidencia según `docs/ai/tareas/README.md` |
  ```

  Sustituir la sección «Agregar un gate» completa por:

  ```markdown
  ## Agregar un gate

  Un gate nuevo es un archivo `quality/check-<algo>.mjs` con funciones puras
  exportadas y un bloque CLI, su archivo de tests en `quality/__tests__/`, y una
  entrada en `quality/gates.json`. Tipos admitidos: `node` y `dotnet`, con `args`,
  y `npm`, con `script`; `cwd` opcional y relativo; `motivo` opcional para explicar
  el orden o un requisito. Una clave desconocida es un error, para que nada
  desactive un gate en silencio.

  `quality/__tests__/gates-proyecto.test.mjs` congela la lista: añadir, quitar o
  cambiar un gate exige cambiar esa prueba en el mismo commit, y el revisor lo ve.
  Si además corre en CI, `.github/workflows/ci.yml` ya lo cubre: ese workflow
  invoca la puerta entera.

  ## Portable y propio

  `quality/verify.mjs` y `quality/lib/gates.mjs` son el ejecutor portable y no
  nombran nada de este proyecto. `quality/gates.json` y
  `quality/__tests__/gates-proyecto.test.mjs` son propios de Trajano-Icarus.
  Inventario completo en [KIT_PROCESO.md](KIT_PROCESO.md).
  ```

- [ ] **9.8** En `docs/proyecto/operacion-calidad.md` §6, sustituir desde «En
  POSIX: `./verify.sh`.» hasta el ítem 7 de la lista por:

  ```markdown
  En POSIX: `./verify.sh`. La lista de gates vive en `quality/gates.json`; la
  puerta se detiene en el primer fallo e incluye:

  1. tests de scripts `quality/`;
  2. adaptadores generados;
  3. mojibake;
  4. enlaces Markdown relativos;
  5. tareas de agentes: bitácoras, fichas, índice y evidencia de los criterios;
  6. lint, build y tests frontend;
  7. build backend con warnings como errores;
  8. tests unitarios, arquitectura e integración backend.
  ```

- [ ] **9.9** Crear `docs/ai/KIT_PROCESO.md`:

  ```markdown
  # Kit de proceso: qué es portable y qué es propio

  Trajano-Icarus es el piloto del kit
  ([decisión 001](../decisiones/001-piloto-kit-proceso.md)). Esta tabla dice, pieza
  por pieza, qué se llevaría tal cual a otro proyecto con la misma base y qué es
  propio de este. No se extrae nada hasta el segundo uso real.

  | Pieza | Portable | Propio de Trajano-Icarus |
  |---|---|---|
  | Puerta de calidad | `quality/verify.mjs`, `quality/lib/gates.mjs`, `quality/lib/ejecutar.mjs`, `quality/lib/salida.mjs`, `verify.ps1`, `verify.sh` | `quality/gates.json` |
  | Gates documentales | `quality/check-mojibake.mjs`, `quality/check-enlaces.mjs` | Ninguno |
  | Adaptadores | `quality/generar-adaptadores.mjs`, `quality/check-adaptadores.mjs`, `quality/adaptadores/roles.mjs` | `quality/adaptadores/manifiesto.mjs` |
  | Tareas de agentes | `quality/tarea.mjs`, `quality/lib/tareas.mjs`, `quality/check-tareas.mjs`, `quality/lib/criterios.mjs`, `quality/guardia-comandos.mjs` | Las carpetas de `docs/ai/tareas/` |
  | Fichas de cierre | `quality/lib/fichas.mjs`, salvo `MODULOS` | La lista `MODULOS` de `quality/lib/fichas.mjs`: pendiente de sacar a configuración en el bloque 3 |
  | Pruebas de la puerta | Las de los archivos portables de esta tabla | `gates-proyecto.test.mjs`, `compose-restart.test.mjs`, `pc-script.test.mjs` |
  | Roles | `docs/ai/agentes/roles/*.md` | Las menciones al proyecto en `descripcion`, al glosario avícola y, en operaciones, a los scripts de arranque |
  | Protocolo y flujo | `docs/ai/agentes/PROTOCOLO.md`, `docs/ai/WORKFLOW.md`, `docs/ai/tareas/README.md` | Ninguno |
  | Revisión | `docs/ai/revision/comun.md` | `docs/ai/revision/dominio.md` |
  | Decisiones | `docs/decisiones/README.md`, `docs/decisiones/plantilla.md` | Los registros numerados de `docs/decisiones/` |
  | Instrucciones raíz | La estructura de `AGENTS.md` | Su sección «Proyecto» |
  ```

- [ ] **9.10** `node quality/check-adaptadores.mjs`, `node quality/check-mojibake.mjs`,
  `node quality/check-enlaces.mjs` y `git diff --check`: verdes.

---

## Tarea 10. Cierre del desarrollo

- [ ] **10.1** `node --test "quality/__tests__/*.test.mjs"`: verde.
- [ ] **10.2** `node quality/check-tareas.mjs`: verde. Comprueba esta misma tarea
  en `DESARROLLO`.
- [ ] **10.3** Confirmar en la bitácora un «Rojo criterio N:» para los criterios
  1, 2, 3, 4, 5, 6, 7, 8, 9 y 10 de `tarea.md`:
  `node quality/tarea.mjs resumen 2026-11-03-proceso-portable-evidencia-decisiones`
  y, si hace falta, buscar `Rojo criterio` en `bitacora.jsonl`.
- [ ] **10.4** `git diff --stat -- quality/__tests__/verify.test.mjs` y
  `git diff --stat -- docs/ai/tareas/2026-10-30-* docs/ai/tareas/2026-11-01-*`:
  vacíos.
- [ ] **10.5** Registrar una nota con los archivos cambiados y avanzar a
  `VERIFICACION`:

  ```powershell
  node quality/tarea.mjs avanzar 2026-11-03-proceso-portable-evidencia-decisiones VERIFICACION --rol desarrollador --proveedor <p>
  ```

  En `VERIFICACION`, el gate `Tareas` exigirá que las pruebas citadas en
  `tarea.md` existan y que los rojos estén registrados. Tras el verde, la tarea
  pasa por `REVISION` profunda: toca `quality/`.
