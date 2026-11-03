# Agente documentador — Plan de implementación

> **Para agentes:** ejecutar tarea por tarea con TDD (rojo, verde, limpieza). Los
> pasos usan casillas (`- [ ]`). Spec:
> [`2026-11-01-agente-documentador-design.md`](../specs/2026-11-01-agente-documentador-design.md).
> Tarea del sistema de agentes: `2026-11-01-agente-documentador`.
>
> **Antes de empezar:** las decisiones numeradas de `tarea.md` deben estar
> aprobadas por el usuario. Si alguna cambia, el planificador ajusta este plan.

**Objetivo:** que cada tarea cerrada deje una ficha corta de formato fijo y una
línea en un índice generado, que el gate `Tareas` y la transición a `CERRADA` lo
exijan, y que las sesiones nuevas consulten el índice antes de explorar.

**Arquitectura:** solo `quality/` y documentación de proceso. Lógica pura del
formato en `quality/lib/fichas.mjs`; lectura de disco en `quality/check-tareas.mjs`,
que también usa la CLI; estado nuevo `DOCUMENTACION` en `quality/lib/tareas.mjs`;
comandos `ficha` e `indice` en `quality/tarea.mjs`; rol fuente en
`docs/ai/agentes/roles/documentador.md` con adaptadores generados.

**Stack:** Node 22, `node:test`, git. Sin dependencias nuevas.

## Restricciones globales

- Español neutro, sin voseo, con acentos, en UTF-8 sin BOM.
- No hacer commit ni push: el desarrollador deja los cambios sin commitear y el
  orquestador commitea (`docs/ai/agentes/PROTOCOLO.md`).
- No relajar baselines, umbrales ni exclusiones. No tocar `quality/verify.mjs`.
- **Revisor obligatorio:** el cambio toca `quality/` y los roles. Tras la
  verificación, la tarea pasa por `REVISION` antes de `ENTORNO`.
- **Nivel de modelo:** alto. `ECONOMIA_TOKENS.md` no permite bajarlo en cambios
  de la puerta de calidad, aunque el código esté completo aquí.
- Registrar mientras se trabaja:
  `node quality/tarea.mjs registrar 2026-11-01-agente-documentador comando --texto "<comando y resultado>" --rol desarrollador --proveedor <p>`.
- Comando de prueba dirigida: `node --test quality/__tests__/<archivo>.test.mjs`.

## Mapa de archivos

| Archivo | Cambio |
|---|---|
| `quality/lib/tareas.mjs` | Rol `documentador`, estado `DOCUMENTACION`, guarda de `CERRADA` |
| `quality/lib/fichas.mjs` | Nuevo: formato, validación e índice |
| `quality/check-tareas.mjs` | Fichas e índice en el gate; funciones de disco compartidas |
| `quality/tarea.mjs` | Comandos `ficha` e `indice`; `CERRADA` comprueba la ficha |
| `quality/__tests__/ficha-valida.mjs` | Nuevo: ficha de ejemplo compartida por las pruebas |
| `quality/__tests__/fichas.test.mjs` | Nuevo |
| `quality/__tests__/tareas.test.mjs` | Pruebas del estado nuevo; el cierre pasa por `DOCUMENTACION` |
| `quality/__tests__/check-tareas.test.mjs` | Pruebas del gate con fichas |
| `quality/__tests__/tarea-cli.test.mjs` | Pruebas de `ficha`, `indice` y cierre |
| `quality/__tests__/manifiesto.test.mjs` | Lista de roles con `documentador` |
| `docs/ai/agentes/roles/documentador.md` | Nuevo rol fuente |
| `docs/ai/agentes/roles/{orquestador,planificador,desarrollador}.md` | Delegación, cierre y documentación de producto |
| `.claude/agents/`, `.kimi-code/agents/` | Generados |
| `docs/ai/agentes/PROTOCOLO.md`, `docs/ai/agentes/README.md` | Estado, comandos, commit final |
| `docs/ai/tareas/README.md` | Formato de la ficha y del índice |
| `docs/ai/PUERTA_CALIDAD.md` | Fila del gate `Tareas` |
| `AGENTS.md`, `docs/ai/README.md` | Regla de consulta del índice |
| `docs/ai/tareas/INDICE.md` | Generado |
| `docs/ai/tareas/2026-10-30-*/ficha.md` | Piloto retroactivo |

---

## Tarea 1. Estado `DOCUMENTACION` y rol `documentador` en la máquina de estados

**Archivos:** `quality/__tests__/tareas.test.mjs`, `quality/lib/tareas.mjs`.

- [ ] **1.1 Prueba roja.** En `quality/__tests__/tareas.test.mjs`:

  Añadir `ROLES` a la importación:

  ```js
  import {
    ESTADOS,
    DUENOS,
    ROLES,
    reconstruir,
    validarTransicion,
    destinoDeRechazo,
    validarBitacora,
    detectarSecretos,
  } from '../lib/tareas.mjs';
  ```

  Cambiar el contexto por defecto para que declare la ficha comprobada:

  ```js
  const contexto = (c = {}) => ({ arbol: 'a1', limpio: true, sinPush: 0, problemasFicha: [], ...c });
  ```

  Sustituir la prueba `CERRADA exige el árbol aprobado y ningún commit sin push`
  (el cierre ya no sale de `APROBADA`) por este bloque, situado justo después de
  `la aprobación deja la tarea en APROBADA`:

  ```js
  const documentando = [...aprobada, paso('APROBADA', 'DOCUMENTACION')];

  test('DOCUMENTACION es del documentador', () => {
    assert.ok(ROLES.includes('documentador'));
    assert.equal(DUENOS.DOCUMENTACION, 'documentador');
    assert.equal(reconstruir(documentando).responsable, 'documentador');
  });

  test('tras la aprobación se documenta antes de cerrar', () => {
    const t = reconstruir(aprobada);
    assert.equal(validarTransicion(t, 'DOCUMENTACION', { rol: 'orquestador', ...contexto() }), null);
    assert.match(validarTransicion(t, 'CERRADA', { rol: 'orquestador', ...contexto() }), /DOCUMENTACION/);
  });

  test('DOCUMENTACION solo sale hacia CERRADA', () => {
    const t = reconstruir(documentando);
    assert.match(validarTransicion(t, 'DESARROLLO', { rol: 'orquestador', ...contexto() }), /no permitida/);
  });

  test('CERRADA exige el árbol aprobado, la ficha comprobada y ningún commit sin push', () => {
    const t = reconstruir(documentando);
    const cerrar = (c) => validarTransicion(t, 'CERRADA', { rol: 'documentador', ...contexto(c) });
    assert.match(cerrar({ arbol: 'otro' }), /aprobado/);
    assert.match(cerrar({ problemasFicha: undefined }), /ficha/);
    assert.match(cerrar({ problemasFicha: ['Falta ficha.md.'] }), /ficha.*Falta ficha\.md/);
    assert.match(cerrar({ sinPush: 2 }), /push/);
    assert.equal(cerrar(), null);
  });

  test('validarBitacora acepta el cierre directo de las tareas anteriores al documentador', () => {
    assert.deepEqual(validarBitacora([...aprobada, paso('APROBADA', 'CERRADA')]), []);
    assert.deepEqual(validarBitacora([...documentando, paso('DOCUMENTACION', 'CERRADA')]), []);
  });
  ```

- [ ] **1.2 Ver rojo.** `node --test quality/__tests__/tareas.test.mjs`. Fallan
  las cinco pruebas nuevas: `ROLES` sin `documentador`, `DOCUMENTACION`
  desconocido y cierre directo permitido.

- [ ] **1.3 Implementación.** En `quality/lib/tareas.mjs`:

  En `DUENOS`, tras `APROBADA: 'orquestador',`:

  ```js
    DOCUMENTACION: 'documentador',
  ```

  En `ROLES`, tras `'operaciones',`:

  ```js
    'documentador',
  ```

  Sustituir el comentario y las entradas finales de `SIGUIENTES`:

  ```js
  // Sin PRUEBA_MANUAL: de ahí solo se sale con los eventos de aprobación y
  // rechazo. Sin BLOQUEADA: vuelve únicamente a su estado previo. APROBADA a
  // CERRADA solo sobrevive para validar bitácoras anteriores al documentador:
  // validarTransicion la prohíbe.
  ```

  ```js
    APROBADA: ['DOCUMENTACION', 'CERRADA'],
    DOCUMENTACION: ['CERRADA'],
    BLOQUEADA: [],
  ```

  En `validarTransicion`, cambiar la firma y añadir la guarda tras la de
  transición inexistente:

  ```js
  export function validarTransicion(tarea, destino, { rol, arbol, limpio, sinPush, problemasFicha }) {
    if (TERMINALES.includes(tarea.estado)) return `La tarea está en un estado terminal: ${tarea.estado}.`;
    if (!transicionExiste(tarea, destino)) return `Transición no permitida: ${tarea.estado} a ${destino}.`;
    if (tarea.estado === 'APROBADA' && destino === 'CERRADA') {
      return 'Tras la aprobación, la tarea pasa por DOCUMENTACION antes de cerrarse.';
    }
  ```

  Y sustituir el bloque de `CERRADA`:

  ```js
    if (destino === 'CERRADA') {
      if (tarea.aprobacion?.arbol !== arbol) return 'El contenido no es el aprobado por el humano.';
      if (!Array.isArray(problemasFicha)) return 'No se comprobó la ficha de la tarea.';
      if (problemasFicha.length > 0) return `La ficha no está lista: ${problemasFicha.join(' ')}`;
      if (sinPush !== 0) return `Hay ${sinPush} commit(s) sin push.`;
    }
  ```

- [ ] **1.4 Ver verde.** `node --test quality/__tests__/tareas.test.mjs`.

---

## Tarea 2. Formato de la ficha e índice

**Archivos:** `quality/__tests__/ficha-valida.mjs`, `quality/__tests__/fichas.test.mjs`,
`quality/lib/fichas.mjs`.

- [ ] **2.1 Ficha de ejemplo compartida.** Crear `quality/__tests__/ficha-valida.mjs`
  (no termina en `.test.mjs`, así que la puerta no lo ejecuta como prueba):

  ```js
  // Ficha mínima válida que comparten las pruebas. No es un test: la puerta solo
  // ejecuta *.test.mjs.

  export const fichaValida = ({
    id = '2026-11-01-ejemplo',
    aprobado = 'abc1234',
    ruta = 'quality/lib/fichas.mjs',
  } = {}) => `---
  id: ${id}
  tipo: calidad
  modulos: [quality]
  temas: [fichas, indice]
  resumen: Ejemplo de ficha válida para las pruebas del formato.
  aprobado: ${aprobado}
  commits: [${aprobado}]
  ---

  # Ejemplo

  ## Qué cambió

  - Algo cambió.

  ## Por qué

  Porque hacía falta.

  ## Decisiones

  - Una decisión, con su motivo.

  ## Archivos clave

  - \`${ruta}\` — formato y validación.

  ## Cómo probarlo

  - \`node --test quality/__tests__/fichas.test.mjs\`

  ## Pendientes

  - Ninguno.

  ## Enlaces

  - [Tarea](tarea.md)
  `;
  ```

  Importante: el contenido de la plantilla literal empieza en columna 0 en el
  archivo real (la sangría de este bloque es solo de la lista del plan).

- [ ] **2.2 Prueba roja.** Crear `quality/__tests__/fichas.test.mjs`:

  ```js
  import { test } from 'node:test';
  import assert from 'node:assert/strict';
  import { generarIndice, leerFicha, lineaIndice, validarFicha, TOPES } from '../lib/fichas.mjs';
  import { fichaValida } from './ficha-valida.mjs';

  const opciones = (o = {}) => ({ id: '2026-11-01-ejemplo', aprobado: 'abc1234', existe: () => true, ...o });
  const validar = (texto, o) => validarFicha(texto, opciones(o)).join('\n');

  test('una ficha válida no tiene problemas', () => {
    assert.deepEqual(validarFicha(fichaValida(), opciones()), []);
  });

  test('acepta finales de línea CRLF', () => {
    assert.deepEqual(validarFicha(fichaValida().replace(/\n/g, '\r\n'), opciones()), []);
  });

  test('sin frontmatter es un problema', () => {
    assert.match(validar('# Ficha\n'), /frontmatter/);
  });

  test('detecta marcadores sin completar', () => {
    assert.match(validar(fichaValida().replace('Porque hacía falta.', 'COMPLETAR')), /COMPLETAR/);
  });

  test('el id debe coincidir con la carpeta', () => {
    assert.match(validar(fichaValida(), { id: '2026-11-01-otra' }), /no coincide/);
  });

  test('tipo y módulos pertenecen a listas cerradas', () => {
    assert.match(validar(fichaValida().replace('tipo: calidad', 'tipo: mejora')), /Tipo desconocido/);
    assert.match(validar(fichaValida().replace('modulos: [quality]', 'modulos: [Pagos]')), /Módulo desconocido: Pagos/);
    assert.match(validar(fichaValida().replace('modulos: [quality]', 'modulos: []')), /módulo/);
  });

  test('los temas son kebab-case sin acentos', () => {
    assert.match(validar(fichaValida().replace('temas: [fichas, indice]', 'temas: [vacunación]')), /Tema inválido/);
    assert.match(validar(fichaValida().replace('temas: [fichas, indice]', 'temas: []')), /temas/);
  });

  test('el resumen tiene un largo acotado', () => {
    const largo = 'x'.repeat(TOPES.resumenMax + 1);
    assert.match(validar(fichaValida().replace(/resumen: .*/, `resumen: ${largo}`)), /resumen/);
    assert.match(validar(fichaValida().replace(/resumen: .*/, 'resumen: corto')), /resumen/);
  });

  test('aprobado coincide con la bitácora y commits lista hashes', () => {
    assert.match(validar(fichaValida(), { aprobado: 'def5678' }), /def5678/);
    assert.match(validar(fichaValida().replace(/commits: .*/, 'commits: [no-es-hash]')), /commits/);
  });

  test('las secciones van completas y en orden', () => {
    const desordenada = fichaValida().replace('## Por qué', '## Temporal').replace('## Decisiones', '## Por qué').replace('## Temporal', '## Decisiones');
    assert.match(validar(desordenada), /Secciones esperadas/);
    assert.match(validar(fichaValida().replace('## Pendientes\n\n- Ninguno.\n', '## Pendientes\n\n')), /Pendientes está vacía/);
  });

  test('los archivos clave existen y van entre comillas invertidas', () => {
    assert.match(validar(fichaValida(), { existe: () => false }), /No existe el archivo clave quality\/lib\/fichas\.mjs/);
    assert.match(validar(fichaValida().replace('- `quality/lib/fichas.mjs`', '- quality/lib/fichas.mjs')), /sin ruta/);
  });

  test('respeta los topes de tamaño', () => {
    assert.match(validar(fichaValida().replace('Porque hacía falta.', 'x'.repeat(TOPES.bytes))), /bytes/);
    const muchas = Array.from({ length: TOPES.lineas }, () => '- x').join('\n');
    assert.match(validar(fichaValida().replace('- Algo cambió.', muchas)), /líneas/);
  });

  test('no admite credenciales', () => {
    assert.match(validar(fichaValida().replace('Porque hacía falta.', 'password=SuperSecreta1')), /credencial/);
  });

  test('la línea del índice une los campos con barras', () => {
    const { meta } = leerFicha(fichaValida());
    assert.equal(
      lineaIndice(meta),
      '- 2026-11-01-ejemplo | calidad | quality | fichas, indice | Ejemplo de ficha válida para las pruebas del formato.',
    );
  });

  test('el índice ordena de la más reciente a la más antigua y termina en un solo salto', () => {
    const meta = (id) => ({ ...leerFicha(fichaValida()).meta, id });
    const texto = generarIndice([meta('2026-10-30-a'), meta('2026-11-01-b')]);
    assert.ok(texto.indexOf('- 2026-11-01-b') < texto.indexOf('- 2026-10-30-a'));
    assert.match(texto, /No editar a mano/);
    assert.ok(texto.endsWith('\n') && !texto.endsWith('\n\n'));
    assert.equal(generarIndice([meta('2026-11-01-b'), meta('2026-10-30-a')]), texto);
  });

  test('un índice sin fichas no deja una línea en blanco al final', () => {
    const texto = generarIndice([]);
    assert.ok(texto.endsWith('\n') && !texto.endsWith('\n\n'));
  });
  ```

- [ ] **2.3 Ver rojo.** `node --test quality/__tests__/fichas.test.mjs`: falla al
  importar `../lib/fichas.mjs`, que no existe.

- [ ] **2.4 Implementación.** Crear `quality/lib/fichas.mjs`:

  ```js
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

    const archivos = (lista.find((s) => s.titulo === 'Archivos clave')?.lineas ?? []).filter((l) => l.startsWith('- '));
    if (archivos.length > TOPES.archivos) p.push(`Máximo ${TOPES.archivos} archivos clave.`);
    for (const linea of archivos) {
      const ruta = linea.match(RUTA_CLAVE)?.[1];
      if (!ruta) p.push(`Archivo clave sin ruta entre comillas invertidas: ${linea}`);
      else if (!existe(ruta)) p.push(`No existe el archivo clave ${ruta}.`);
    }
    return p;
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
  ```

- [ ] **2.5 Ver verde.** `node --test quality/__tests__/fichas.test.mjs` y
  `node --test quality/__tests__/adaptadores-roles.test.mjs` (comparten
  `leerFrontmatter`).

---

## Tarea 3. El gate `Tareas` exige fichas e índice

**Archivos:** `quality/__tests__/check-tareas.test.mjs`, `quality/check-tareas.mjs`.

- [ ] **3.1 Prueba roja.** Añadir al final de `quality/__tests__/check-tareas.test.mjs`
  y ampliar sus importaciones:

  ```js
  import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
  import { tmpdir } from 'node:os';
  import { dirname, join } from 'node:path';
  import { revisarFicha, revisarRepositorio, revisarTarea } from '../check-tareas.mjs';
  import { generarIndice, leerFicha } from '../lib/fichas.mjs';
  import { fichaValida } from './ficha-valida.mjs';
  ```

  (sustituye la importación anterior de `revisarTarea`).

  ```js
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
  ```

- [ ] **3.2 Ver rojo.** `node --test quality/__tests__/check-tareas.test.mjs`:
  `revisarFicha` y `revisarRepositorio` no existen.

- [ ] **3.3 Implementación.** Sustituir `quality/check-tareas.mjs` completo:

  ```js
  #!/usr/bin/env node
  // Gate de tareas: rehace cada bitácora de docs/ai/tareas/, valida las fichas de
  // cierre y comprueba que INDICE.md coincida con ellas. Falla si una historia no
  // es legal, si una aprobación no es humana, si aparece una credencial, si una
  // tarea cerrada no tiene ficha válida o si el índice no está al día. La CLI usa
  // las mismas funciones para negar el cierre.

  import { existsSync, readdirSync, readFileSync } from 'node:fs';
  import { join } from 'node:path';
  import { fileURLToPath } from 'node:url';
  import { detectarSecretos, reconstruir, validarBitacora } from './lib/tareas.mjs';
  import { generarIndice, leerFicha, validarFicha } from './lib/fichas.mjs';
  import { exito, fallo } from './lib/salida.mjs';

  const DIR_TAREAS = 'docs/ai/tareas';
  const ARCHIVO_INDICE = 'INDICE.md';

  const leer = (ruta) => (existsSync(ruta) ? readFileSync(ruta, 'utf8') : null);

  export function revisarTarea(bitacora, tareaMd) {
    const problemas = [];
    const eventos = [];

    bitacora.split('\n').filter(Boolean).forEach((linea, i) => {
      try {
        eventos.push(JSON.parse(linea));
      } catch {
        problemas.push(`Línea ${i + 1}: no es JSON válido.`);
      }
    });

    if (problemas.length === 0) problemas.push(...validarBitacora(eventos));
    if (detectarSecretos(bitacora) || detectarSecretos(tareaMd)) {
      problemas.push('Contiene una posible credencial.');
    }
    return problemas;
  }

  function datosDeBitacora(bitacora) {
    try {
      const tarea = reconstruir(bitacora.split('\n').filter(Boolean).map((l) => JSON.parse(l)));
      return { estado: tarea.estado, aprobado: tarea.aprobacion?.commit };
    } catch {
      return { estado: null, aprobado: undefined };
    }
  }

  export function revisarFicha({ id, estado, aprobado, ficha, existe }) {
    if (ficha === null) {
      return estado === 'CERRADA' ? ['Falta ficha.md: toda tarea cerrada necesita su ficha.'] : [];
    }
    return validarFicha(ficha, { id, aprobado, existe }).map((p) => `ficha.md: ${p}`);
  }

  const idsDeTareas = (raiz) =>
    readdirSync(join(raiz, DIR_TAREAS), { withFileTypes: true })
      .filter((e) => e.isDirectory())
      .map((e) => e.name)
      .sort();

  export function revisarDocumentacion(raiz, id, { estado, aprobado }) {
    return revisarFicha({
      id,
      estado,
      aprobado,
      ficha: leer(join(raiz, DIR_TAREAS, id, 'ficha.md')),
      existe: (ruta) => existsSync(join(raiz, ruta)),
    });
  }

  export function indiceEsperado(raiz) {
    const metas = idsDeTareas(raiz).flatMap((id) => {
      const texto = leer(join(raiz, DIR_TAREAS, id, 'ficha.md'));
      const ficha = texto === null ? null : leerFicha(texto);
      return ficha ? [ficha.meta] : [];
    });
    return generarIndice(metas);
  }

  export function revisarIndice(raiz) {
    const actual = leer(join(raiz, DIR_TAREAS, ARCHIVO_INDICE))?.replace(/\r\n/g, '\n');
    return actual === indiceEsperado(raiz)
      ? []
      : [`${ARCHIVO_INDICE} no está al día: ejecuta node quality/tarea.mjs indice.`];
  }

  export function revisarRepositorio(raiz) {
    const ids = idsDeTareas(raiz);
    const fallidas = ids.flatMap((id) => {
      const dir = join(raiz, DIR_TAREAS, id);
      const bitacora = leer(join(dir, 'bitacora.jsonl')) ?? '';
      const problemas = [
        ...revisarTarea(bitacora, leer(join(dir, 'tarea.md')) ?? ''),
        ...revisarDocumentacion(raiz, id, datosDeBitacora(bitacora)),
      ];
      return problemas.length > 0 ? [{ name: id, problemas }] : [];
    });

    const indice = revisarIndice(raiz);
    if (indice.length > 0) fallidas.push({ name: ARCHIVO_INDICE, problemas: indice });
    return { total: ids.length, fallidas };
  }

  if (process.argv[1] && process.argv[1].endsWith('check-tareas.mjs')) {
    const { total, fallidas } = revisarRepositorio(fileURLToPath(new URL('..', import.meta.url)));

    if (fallidas.length > 0) {
      console.log(fallo(`Gate de tareas: ${fallidas.length} elemento(s) con problemas`));
      for (const { name, problemas } of fallidas) {
        for (const p of problemas) console.log(`       ${name}: ${p}`);
      }
      process.exit(1);
    }

    console.log(exito(`Gate de tareas: ${total} tarea(s) legales, con fichas e índice al día.`));
  }
  ```

- [ ] **3.4 Ver verde.** `node --test quality/__tests__/check-tareas.test.mjs`.
  `node quality/check-tareas.mjs` sobre el repositorio real falla hasta la tarea
  7 (las tres tareas cerradas no tienen ficha y no hay índice): es lo esperado.

---

## Tarea 4. CLI: `ficha`, `indice` y cierre con ficha

**Archivos:** `quality/__tests__/tarea-cli.test.mjs`, `quality/tarea.mjs`.

- [ ] **4.1 Prueba roja.** En `quality/__tests__/tarea-cli.test.mjs`, añadir la
  importación `import { fichaValida } from './ficha-valida.mjs';` y, al final:

  ```js
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
  ```

- [ ] **4.2 Ver rojo.** `node --test quality/__tests__/tarea-cli.test.mjs`: los
  comandos `ficha` e `indice` son desconocidos y el cierre no comprueba la ficha.

- [ ] **4.3 Implementación.** En `quality/tarea.mjs`:

  Importaciones, tras la de `./lib/tareas.mjs`:

  ```js
  import { indiceEsperado, revisarDocumentacion, revisarIndice } from './check-tareas.mjs';
  import { MARCADOR, SECCIONES_FICHA } from './lib/fichas.mjs';
  ```

  En `avanzar`, sustituir las dos líneas que calculan `tarea` y `motivo`:

  ```js
    const tarea = reconstruir(leerEventos(raiz, id));
    const ctx = contexto(raiz, actor.rol);
    if (destino === 'CERRADA') {
      ctx.problemasFicha = [
        ...revisarDocumentacion(raiz, id, { estado: 'CERRADA', aprobado: tarea.aprobacion?.commit }),
        ...revisarIndice(raiz),
      ];
    }
    const motivo = validarTransicion(tarea, destino, ctx);
    if (motivo) fallar(motivo);
  ```

  Antes de `const COMANDOS`, añadir:

  ```js
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
  ```

  Y ampliar la tabla de comandos:

  ```js
  const COMANDOS = { crear, avanzar, registrar, verificar, rechazar, aprobar, relevo, resumen, ficha, indice };
  ```

- [ ] **4.4 Ver verde.** `node --test quality/__tests__/tarea-cli.test.mjs` y
  `node --test quality/__tests__/tareas.test.mjs`.

---

## Tarea 5. Rol `documentador` y adaptadores

**Archivos:** `quality/__tests__/manifiesto.test.mjs`, `docs/ai/agentes/roles/*.md`,
adaptadores generados.

- [ ] **5.1 Prueba roja.** En `quality/__tests__/manifiesto.test.mjs`, línea 7:

  ```js
  const ROLES = ['desarrollador', 'documentador', 'operaciones', 'orquestador', 'planificador', 'revisor', 'verificador'];
  ```

- [ ] **5.2 Ver rojo.** `node --test quality/__tests__/manifiesto.test.mjs`:
  `falta Claude: documentador`.

- [ ] **5.3 Rol fuente.** Crear `docs/ai/agentes/roles/documentador.md` (sin
  enlaces relativos: lo exige `adaptadores-roles.test.mjs`):

  ```markdown
  ---
  nombre: documentador
  descripcion: Escribe la ficha de cierre de cada tarea de Trajano-Icarus y mantiene su índice, para que las sesiones futuras no relean specs, planes ni código
  cuando_usar: En el estado DOCUMENTACION, tras la aprobación humana, o para documentar una tarea ya cerrada
  nivel: intermedio
  herramientas: [leer, buscar, shell, editar]
  delega_en: []
  ---

  # Documentador

  Conviertes una tarea aprobada en memoria barata: una ficha corta de formato fijo
  y su línea en el índice. Solo escribes en `docs/ai/tareas/`.

  Antes de actuar, lee `docs/ai/agentes/PROTOCOLO.md` y el formato de
  `docs/ai/tareas/README.md`, y ejecuta `node quality/tarea.mjs resumen <id>`.

  ## Entradas

  - `docs/ai/tareas/<id>/tarea.md` y los eventos `decision` y `rechazo` de su
    bitácora.
  - `git show --stat` de los commits que trae la plantilla.
  - Solo los fragmentos de spec, plan o código necesarios para nombrar bien una
    decisión o un archivo. No leas specs ni planes completos: la ficha existe para
    que nadie tenga que hacerlo.

  ## Qué haces

  1. Ejecuta `node quality/tarea.mjs ficha <id> --rol documentador --proveedor <p>`.
  2. Sustituye cada `COMPLETAR`. Escribe lo que una sesión futura necesita para
     orientarse en un minuto. Enlaza, no copies: las reglas están en el glosario y
     en `docs/proyecto/`, los criterios en `tarea.md` y el detalle en la spec y el
     plan.
  3. Deja en `commits` solo los commits clave y en «Archivos clave» los puntos de
     entrada al código, no todos los archivos tocados.
  4. Comprueba si el cambio alteró el dominio o el comportamiento. Si
     `docs/proyecto/` o el glosario no lo reflejan, no los edites: anótalo en
     «Pendientes» y regístralo con `registrar <id> nota`.
  5. Ejecuta `node quality/tarea.mjs indice` y `node quality/check-tareas.mjs`, y
     corrige la ficha hasta que el gate no informe problemas.
  6. Devuelve la tarea al orquestador para que la cierre.

  ## Límites

  - No editas código, configuración, pruebas, `docs/proyecto/` ni el glosario:
    cambiarían el contenido aprobado.
  - No haces commit ni push, no avanzas a `CERRADA` y no apruebas nada.
  - No editas `INDICE.md` a mano: lo genera la CLI.
  - La ficha no supera 60 líneas ni 4000 bytes.
  - No registras datos personales ni credenciales. Describe los casos por rol.

  ## Termina cuando

  `node quality/check-tareas.mjs` no informa problemas de la ficha ni del índice.

  ## Informe final

  Tu último mensaje debe ser completo y autosuficiente: ruta de la ficha, tamaño
  en líneas y bytes, pendientes anotados y resultado del gate.
  ```

- [ ] **5.4 Orquestador.** En `docs/ai/agentes/roles/orquestador.md`:

  ```text
  delega_en: [planificador, desarrollador, verificador, revisor, operaciones, documentador]
  ```

  y sustituir el paso 7:

  ```markdown
  7. Tras la aprobación humana, haz push a `develop`, avanza a `DOCUMENTACION` e
     invoca al documentador. Cuando `node quality/check-tareas.mjs` no informe
     problemas, avanza a `CERRADA` y haz el commit final de la bitácora, la ficha y
     el índice.
  ```

- [ ] **5.5 Planificador.** En `docs/ai/agentes/roles/planificador.md`, tras el
  paso 3 de «Qué haces»:

  ```markdown
  4. En el alcance, indica qué documentos de `docs/proyecto/` y del glosario debe
     actualizar el desarrollador, o «ninguno». Tras la aprobación ya no se editan.
  ```

  y renumerar el paso siguiente como 5.

- [ ] **5.6 Desarrollador.** En `docs/ai/agentes/roles/desarrollador.md`, tras el
  paso 2 de «Qué haces»:

  ```markdown
  3. Si el cambio altera el dominio o el comportamiento, actualiza los documentos
     de `docs/proyecto/` y del glosario que fija el alcance. Enlaza en lugar de
     copiar.
  ```

  y renumerar los pasos siguientes (4 a 6).

- [ ] **5.7 Generar y ver verde.**

  ```powershell
  node quality/generar-adaptadores.mjs
  node --test quality/__tests__/manifiesto.test.mjs quality/__tests__/adaptadores-roles.test.mjs
  node quality/check-adaptadores.mjs
  ```

  Deben escribirse `.claude/agents/documentador.md`,
  `.kimi-code/agents/documentador.md` y los adaptadores de los tres roles editados.

---

## Tarea 6. Documentación de proceso y regla de consulta

**Archivos:** `docs/ai/agentes/PROTOCOLO.md`, `docs/ai/agentes/README.md`,
`docs/ai/tareas/README.md`, `docs/ai/PUERTA_CALIDAD.md`, `AGENTS.md`,
`docs/ai/README.md`.

- [ ] **6.1 PROTOCOLO.md.**

  En la tabla de estados, sustituir la fila de `APROBADA` por:

  ```markdown
  | `APROBADA` | orquestador | `DOCUMENTACION` |
  | `DOCUMENTACION` | documentador | `CERRADA`, con ficha válida, índice al día y sin commits pendientes de push |
  ```

  Tras el párrafo «Todo estado no terminal puede pasar…», añadir:

  ```markdown
  `APROBADA` a `CERRADA` solo aparece en bitácoras anteriores al 2026-11-01. La CLI
  ya no la permite.
  ```

  En el bloque de comandos, tras la línea de `relevo`:

  ```text
  node quality/tarea.mjs ficha <id> --rol documentador --proveedor <p>
  node quality/tarea.mjs indice
  ```

  En «Commit y push», sustituir los pasos 5 y 6 por:

  ```markdown
  5. Tras la aprobación, el orquestador hace push a `develop` y pasa a
     `DOCUMENTACION`.
  6. El documentador escribe la ficha y regenera el índice, sin commit.
  7. El orquestador avanza a `CERRADA` y commitea la bitácora, la ficha y el índice
     como cambio documental, tras los gates de mojibake, enlaces y tareas y
     `git diff --check`.
  ```

  Añadir antes de «Relevo entre proveedores»:

  ```markdown
  ## Ficha de cierre

  Toda tarea cerrada tiene una ficha en `docs/ai/tareas/<id>/ficha.md`, con el
  formato de [tareas/README.md](../tareas/README.md). La ficha enlaza, no copia.
  `docs/proyecto/` y el glosario se actualizan en `DESARROLLO`, junto al código:
  tras la aprobación solo se anota lo que falte.
  ```

- [ ] **6.2 agentes/README.md.** Cambiar «Seis roles especializados» por «Siete
  roles especializados» y añadir a la tabla «Dónde está cada cosa»:

  ```markdown
  | `docs/ai/tareas/INDICE.md` | Índice de fichas de cierre. Generado: no se edita |
  ```

- [ ] **6.3 tareas/README.md.** Sustituir el archivo completo:

  ````markdown
  # Tareas de agentes

  Cada carpeta es una tarea creada con `quality/tarea.mjs`. Contiene `tarea.md`,
  `bitacora.jsonl`, que solo admite eventos nuevos, y desde el cierre `ficha.md`.
  La bitácora no se edita a mano: el gate `Tareas` rehace la historia y falla si
  deja de ser legal.

  Las reglas están en [el protocolo de agentes](../agentes/PROTOCOLO.md).

  ## Consultar

  [INDICE.md](INDICE.md) tiene una línea por ficha: id, tipo, módulos, temas y
  resumen. Busca un término en él en lugar de leerlo entero, abre solo la ficha que
  coincida y parte de sus «Archivos clave». Abre la spec o el plan solo si la ficha
  no basta.

  ## Ficha de cierre

  La escribe el documentador en `DOCUMENTACION` a partir de la plantilla de
  `node quality/tarea.mjs ficha <id>`. Toda tarea cerrada tiene una.

  ```markdown
  ---
  id: <id de la tarea, igual a su carpeta>
  tipo: funcionalidad | correccion | calidad | configuracion | documentacion
  modulos: [GestionAvicola, web]
  temas: [vacunacion, campana]
  resumen: Una línea de 20 a 160 caracteres.
  aprobado: <commit de la aprobación en la bitácora>
  commits: [<commits clave>]
  ---

  # <título de la tarea>

  ## Qué cambió
  ## Por qué
  ## Decisiones
  ## Archivos clave
  ## Cómo probarlo
  ## Pendientes
  ## Enlaces
  ```

  - Las siete secciones van en ese orden y ninguna queda vacía.
  - Máximo 60 líneas y 4000 bytes.
  - `modulos` sale de una lista cerrada: `Identity`, `Clientes`, `GestionAvicola`,
    `ControlAcceso`, `BuildingBlocks`, `Host`, `GestorCaisy`, `web`, `infra`,
    `deploy`, `quality`, `docs`.
  - `temas`: de 1 a 8, en kebab-case y sin acentos, para que la búsqueda no
    dependa de escribir bien el acento.
  - «Archivos clave»: hasta 12 viñetas con la forma ``- `ruta` — papel``. La ruta
    debe existir; si renombras o borras un archivo citado, corrige la ficha
    antigua.
  - «Cómo probarlo»: las pruebas dirigidas y una remisión a los criterios de
    `tarea.md`, sin copiarlos.
  - «Pendientes»: lo que quedó fuera, incluida la documentación de producto que
    falte. «Ninguno.» si no hay.
  - Enlaza, no copies: reglas en el glosario y en `docs/proyecto/`, detalle en la
    spec y el plan.
  - Sin datos personales ni credenciales.

  ## Índice

  `INDICE.md` se genera con `node quality/tarea.mjs indice`. No se edita a mano;
  ante un conflicto, se regenera. El gate `Tareas` falla si no coincide con las
  fichas.
  ````

- [ ] **6.4 PUERTA_CALIDAD.md.** Sustituir la fila `Tareas`:

  ```markdown
  | Tareas | Que cada bitácora de `docs/ai/tareas/` sea una historia legal, con aprobaciones humanas y sin credenciales reconocibles; que toda tarea cerrada tenga una `ficha.md` válida y que `INDICE.md` coincida con las fichas | Registrar eventos con `quality/tarea.mjs`; completar la ficha y ejecutar `node quality/tarea.mjs indice` |
  ```

- [ ] **6.5 AGENTS.md.** En «Agentes por rol», sustituir el primer punto:

  ```markdown
  - Siete roles portables entre proveedores: orquestador, planificador,
    desarrollador, verificador, revisor, operaciones y documentador. La fuente
    está en `docs/ai/agentes/roles/`; `.claude/agents/` y `.kimi-code/agents/`
    son adaptadores generados.
  ```

  En «Descubrimiento eficiente», insertar como paso 3 y renumerar los siguientes
  (4 a 7):

  ```markdown
  3. Antes de explorar un área, buscar su término en `docs/ai/tareas/INDICE.md`
     (búsqueda, no lectura completa), abrir solo las fichas que coincidan y partir
     de sus «Archivos clave». Specs y planes, solo si la ficha no basta.
  ```

- [ ] **6.6 docs/ai/README.md.** Añadir a la tabla «Documentos»:

  ```markdown
  | [tareas/INDICE.md](tareas/INDICE.md) | Antes de explorar un área: buscar el término y abrir solo la ficha que coincida |
  | [tareas/README.md](tareas/README.md) | Al escribir o revisar una ficha de cierre |
  ```

  En «Qué leer al iniciar», sustituir las filas de pregunta y de feature:

  ```markdown
  | Pregunta o diagnóstico | `AGENTS.md`, las fichas del área según `docs/ai/tareas/INDICE.md` y los archivos directamente relevantes |
  | Feature o bloque | Lo anterior más `docs/ai/WORKFLOW.md` y el spec vigente |
  ```

  Y al final del párrafo de memoria consultable, añadir: «Antes de abrirlos,
  buscar en el índice de fichas: la ficha resume la tarea y enlaza su spec y su
  plan.»

- [ ] **6.7 Índice vacío y gates documentales.**

  ```powershell
  node quality/tarea.mjs indice
  node quality/generar-adaptadores.mjs
  node quality/check-adaptadores.mjs
  node quality/check-mojibake.mjs
  node quality/check-enlaces.mjs
  ```

  `indice` crea `INDICE.md` solo con la cabecera, para que los enlaces nuevos
  resuelvan. `generar-adaptadores` no debe escribir nada: `CLAUDE.md` y `GEMINI.md`
  importan `AGENTS.md`.

---

## Tarea 7. Piloto retroactivo

Se actúa como documentador (`docs/ai/agentes/roles/documentador.md`) sobre las tres
tareas cerradas. `ficha` no exige cerrojo sobre una tarea cerrada ni escribe en su
bitácora.

- [ ] **7.1 Gestor veterinario** (grande, con ampliación tras la prueba manual):

  ```powershell
  node quality/tarea.mjs ficha 2026-10-30-gestor-veterinario-vacunacion --rol documentador --proveedor <p>
  ```

  Orientación, no texto obligatorio: `tipo: funcionalidad`; módulos probables
  `GestionAvicola`, `GestorCaisy`, `Identity`, `web`, `docs`; temas como
  `vacunacion`, `gestor-veterinario`, `programas-vacunacion`, `campana`,
  `notificaciones`. `aprobado` debe ser `1573cd5`. Los commits clave son
  `3e4fffb` y `494df97`; comprobarlo con `git show --stat`. «Pendientes» debe
  recoger las decisiones que la ampliación dejó para confirmar, si siguen
  abiertas. Fuente principal: `tarea.md` y los eventos `decision`, no el plan de
  195 KB.

- [ ] **7.2 Gate de enlaces sin versionar** (pequeña, `tarea.md` vacío):
  `ficha 2026-10-30-enlaces-sin-versionar`. Fuente: su bitácora y
  `git show --stat 7e2d657`. Orientación: `tipo: calidad`, `modulos: [quality]`.

- [ ] **7.3 Gate de mojibake sin versionar** (pequeña): igual con
  `2026-10-30-mojibake-sin-versionar` y `git show --stat 4bf8645`.

- [ ] **7.4 Índice y gate.**

  ```powershell
  node quality/tarea.mjs indice
  node quality/check-tareas.mjs
  ```

  Verde con «4 tarea(s) legales, con fichas e índice al día».

- [ ] **7.5 Medición.** Para cada ficha, registrar su tamaño frente a lo que
  sustituye:

  ```powershell
  node quality/tarea.mjs registrar 2026-11-01-agente-documentador nota --texto "Piloto <id>: ficha de <n> bytes y <l> líneas frente a <m> bytes de spec, plan y tarea.md" --rol desarrollador --proveedor <p>
  ```

  Si una ficha grande no cabe en el tope, no se sube el tope: se recorta y se
  registra la dificultad como nota para que el usuario decida.

---

## Tarea 8. Puerta completa y paso a verificación

- [ ] **8.1** `./verify.ps1` completo (Docker en marcha). Todos los gates en verde.
- [ ] **8.2** `git diff --check` y `git status --short`: los archivos nuevos
  deben aparecer, incluidas las tres fichas, `INDICE.md` y los dos adaptadores.
- [ ] **8.3** Avanzar a `VERIFICACION` con una nota que liste los archivos. El
  orquestador debe encaminar después a `REVISION`: el cambio toca `quality/` y
  los roles. Al commitear, incluir explícitamente `docs/ai/tareas/INDICE.md` y
  `docs/ai/tareas/2026-10-30-*/ficha.md`, que están fuera del árbol verificado.

Tras la aprobación, esta tarea es la primera que se cierra por el flujo nuevo:
`DOCUMENTACION`, su propia ficha y `CERRADA`.
