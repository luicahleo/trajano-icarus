# Andamiaje de instrucciones para agentes — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Levantar en Trajano-Icarus la capa de instrucciones para agentes —`AGENTS.md` como única fuente, con adaptadores generados— y una puerta de calidad mínima de cuatro gates que se valida a sí misma, sin escribir una línea de código de aplicación.

**Architecture:** Todo el andamiaje vive en la raíz del repositorio. `quality/` contiene scripts Node ESM sin dependencias externas: una librería común (`lib/`), un gate por archivo, un manifiesto de adaptadores y un orquestador que corta al primer fallo. Cada gate expone funciones puras testeables y un bloque CLI que solo se activa cuando el archivo se invoca directamente, de modo que los tests importan la lógica sin disparar efectos. `verify.ps1` y `verify.sh` son envoltorios de una línea sobre `node quality/verify.mjs`.

**Tech Stack:** Node.js 22+ (ESM, `node:test`, `node:assert/strict`), Git, GitHub Actions. Sin npm, sin `package.json`, sin dependencias de terceros.

## Global Constraints

- **Node.js 22 o superior**. Los scripts usan solo módulos `node:` integrados. No se crea `package.json` en la raíz: los `.mjs` ya son ESM por extensión, y la raíz debe quedar libre para el `web/package.json` del subproyecto 3.
- **Sin dependencias externas** en `quality/`. Ningún `npm install`.
- **Idioma**: todo documento, comentario, mensaje de salida e identificador de dominio en español correcto con acentos. UTF-8 **sin BOM**.
- **Entorno de desarrollo**: PowerShell 5.1. No existen `&&` ni `||`; encadenar con `;` o con comandos separados. Para mensajes de commit multilínea, usar here-strings `@'...'@` con el cierre `'@` en la columna 0.
- **Ramas**: se trabaja directamente en `develop`. Nunca hacer merge ni push a `master` sin pedido explícito. No hay pull requests.
- **Un mojibake literal nunca se escribe en código fuente**: los patrones que el gate detecta se escriben siempre como escapes `\uXXXX` en `.mjs` y como spans de código entre acentos graves en `.md`. Un archivo que contenga el carácter literal fuera de un span se marca a sí mismo.
- **Prohibido `--no-verify`** en commit o push. Prohibido relajar un gate para que pase.
- **Rutas**: todas relativas a la raíz del repositorio (`C:\Users\lrcahuana\source\repos\Trajano-Icarus`). Los comandos `node quality/...` se ejecutan siempre desde la raíz.
- **Fuera de alcance** (subproyectos posteriores): solución .NET, `Icarus/`, frontend `web/`, contenedorización, gates de cobertura/mutación/complejidad/contrato.

**Orden de dependencias.** Las tareas están ordenadas para que ninguna instrucción mienta ni ningún puntero quede roto: la puerta existe antes de que `AGENTS.md` la prometa, y `docs/ai/` existe antes de que `AGENTS.md` lo referencie. La única excepción declarada está en la Tarea 8, señalada allí.

---

### Task 1: Cimientos del repositorio

**Files:**
- Create: `.gitignore`
- Create: `.gitattributes`

**Interfaces:**
- Produces: clasificación texto/binario que el gate de mojibake usa a través de `git grep -I` (Tarea 3), y exclusión del ruido que el gate de enlaces recorrería (Tarea 4).

- [x] **Step 1: Crear `.gitignore`**

`.gitignore`:
```gitignore
# Node
node_modules/

# .NET (subproyecto 2)
bin/
obj/
artifacts/
*.user

# Herramientas de agentes
.superpowers/
graphify-out/
.kombai/

# Handoff efímero: es estado de sesión, no memoria del proyecto.
# La plantilla versionada es docs/ai/HANDOFF.template.md.
docs/ai/HANDOFF.md

# Secretos y entorno
.env
.env.*
*.pfx
*.p12
*.key

# Ajustes locales de Claude Code (no compartibles)
.claude/settings.local.json

# Editores y sistema operativo
.vs/
.idea/
.DS_Store
Thumbs.db
```

- [x] **Step 2: Crear `.gitattributes`**

`.gitattributes`:
```gitattributes
# Normalización de fin de línea. `text=auto` deja que git clasifique cada
# archivo como texto o binario; el gate de mojibake se apoya en esa
# clasificación a través de `git grep -I`.
* text=auto eol=lf

# Scripts que Windows debe recibir con CRLF
*.ps1 text eol=crlf
*.cmd text eol=crlf
*.bat text eol=crlf

# Scripts POSIX siempre con LF
*.sh text eol=lf

# Binarios explícitos
*.png binary
*.jpg binary
*.jpeg binary
*.gif binary
*.ico binary
*.pdf binary
*.zip binary
```

- [x] **Step 3: Verificar la clasificación y las exclusiones**

Run:
```powershell
git check-attr text -- docs/superpowers/specs/2026-08-12-andamiaje-instrucciones-agentes-design.md
git check-ignore -v node_modules/paquete/index.js
git check-ignore -v .claude/settings.local.json
```
Expected: la primera línea informa `text: auto`; las dos siguientes informan la línea de `.gitignore` que produce la exclusión. Si `git check-ignore` no imprime nada, la regla no está tomando efecto y hay que revisar el patrón.

- [x] **Step 4: Commit**

```powershell
git add .gitignore .gitattributes
git commit -m "chore: cimientos del repositorio (gitignore y gitattributes)"
```

---

### Task 2: Librería común de la puerta de calidad

**Files:**
- Create: `quality/lib/salida.mjs`
- Create: `quality/lib/ejecutar.mjs`
- Test: `quality/__tests__/salida.test.mjs`
- Test: `quality/__tests__/ejecutar.test.mjs`

**Interfaces:**
- Produces:
  - `titulo(texto: string): string`, `exito(texto: string): string`, `fallo(texto: string, detalle?: string): string`, `aviso(texto: string): string` en `quality/lib/salida.mjs`. Devuelven cadenas; no imprimen, para que los tests puedan verificarlas.
  - `ejecutar(comando: string, args: string[], opciones?: { cwd?: string, silencioso?: boolean, sinShell?: boolean }): { codigo: number, salida: string, duracionMs: number }` en `quality/lib/ejecutar.mjs`. Nunca lanza: el llamador decide qué hacer con un código distinto de cero.
- Las Tareas 3, 4, 6, 7 y 9 consumen ambos módulos con esas firmas exactas.

- [x] **Step 1: Escribir los tests que fallan**

`quality/__tests__/salida.test.mjs`:
```javascript
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { titulo, exito, fallo, aviso } from '../lib/salida.mjs';

test('titulo incluye el texto', () => {
  assert.match(titulo('Gate de enlaces'), /Gate de enlaces/);
});

test('exito incluye el texto y una marca visible', () => {
  const linea = exito('Adaptadores al día');
  assert.match(linea, /Adaptadores al día/);
  assert.match(linea, /OK/);
});

test('fallo incluye el detalle cuando se proporciona', () => {
  const linea = fallo('Enlace roto', 'docs/ai/README.md:12 -> docs/ai/FALTA.md');
  assert.match(linea, /Enlace roto/);
  assert.match(linea, /FALTA\.md/);
});

test('fallo sin detalle no agrega una segunda línea', () => {
  assert.equal(fallo('Sin detalle').split('\n').length, 1);
});

test('aviso se distingue de un fallo', () => {
  assert.notEqual(aviso('Gate omitido'), fallo('Gate omitido'));
});
```

`quality/__tests__/ejecutar.test.mjs`:
```javascript
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ejecutar } from '../lib/ejecutar.mjs';

test('devuelve código 0 y la salida de un comando exitoso', () => {
  const r = ejecutar(process.execPath, ['-e', 'console.log("hola")'], {
    silencioso: true,
    sinShell: true,
  });
  assert.equal(r.codigo, 0);
  assert.match(r.salida, /hola/);
  assert.ok(r.duracionMs >= 0);
});

test('devuelve el código de salida de un comando fallido sin lanzar', () => {
  const r = ejecutar(process.execPath, ['-e', 'process.exit(3)'], {
    silencioso: true,
    sinShell: true,
  });
  assert.equal(r.codigo, 3);
});

test('un comando inexistente devuelve código distinto de cero sin lanzar', () => {
  const r = ejecutar('comando-que-no-existe-jamas', [], {
    silencioso: true,
    sinShell: true,
  });
  assert.notEqual(r.codigo, 0);
});

test('combina stdout y stderr en una sola cadena', () => {
  const r = ejecutar(
    process.execPath,
    ['-e', 'console.log("uno"); console.error("dos")'],
    { silencioso: true, sinShell: true },
  );
  assert.match(r.salida, /uno/);
  assert.match(r.salida, /dos/);
});
```

- [x] **Step 2: Ejecutar los tests para ver que fallan**

Run:
```powershell
node --test quality/__tests__
```
Expected: FAIL. Los dos archivos de test abortan porque no se pueden resolver los módulos `../lib/salida.mjs` y `../lib/ejecutar.mjs` (`ERR_MODULE_NOT_FOUND`).

- [x] **Step 3: Implementar `quality/lib/salida.mjs`**

`quality/lib/salida.mjs`:
```javascript
// Formato uniforme para la salida de los gates de calidad.
// No imprime: devuelve cadenas, para que los tests puedan verificarlas.

const VERDE = '\x1b[32m';
const ROJO = '\x1b[31m';
const AMARILLO = '\x1b[33m';
const NEGRITA = '\x1b[1m';
const RESET = '\x1b[0m';

export function titulo(texto) {
  return `${NEGRITA}== ${texto} ==${RESET}`;
}

export function exito(texto) {
  return `${VERDE}[OK]${RESET} ${texto}`;
}

export function fallo(texto, detalle) {
  const cabecera = `${ROJO}[FALLO]${RESET} ${texto}`;
  return detalle ? `${cabecera}\n       ${detalle}` : cabecera;
}

export function aviso(texto) {
  return `${AMARILLO}[AVISO]${RESET} ${texto}`;
}
```

- [x] **Step 4: Implementar `quality/lib/ejecutar.mjs`**

`quality/lib/ejecutar.mjs`:
```javascript
// Ejecuta un comando externo y devuelve su código, salida combinada y duración.
// Nunca lanza: el llamador decide qué hacer con un código distinto de cero.

import { spawnSync } from 'node:child_process';

export function ejecutar(comando, args, opciones = {}) {
  // En Windows, los lanzadores .cmd (npm, dotnet) necesitan shell. Un .exe
  // como git no, y evitar el shell mantiene los argumentos intactos: es lo
  // que pide `sinShell`.
  const usarShell = opciones.sinShell ? false : process.platform === 'win32';

  const inicio = process.hrtime.bigint();
  const resultado = spawnSync(comando, args, {
    cwd: opciones.cwd ?? process.cwd(),
    encoding: 'utf8',
    shell: usarShell,
    maxBuffer: 32 * 1024 * 1024,
  });
  const duracionMs = Number((process.hrtime.bigint() - inicio) / 1_000_000n);
  const salida = `${resultado.stdout ?? ''}${resultado.stderr ?? ''}`;

  if (!opciones.silencioso) {
    process.stdout.write(salida);
  }

  // status es null cuando el proceso no llegó a arrancar o murió por señal.
  return { codigo: resultado.status ?? 1, salida, duracionMs };
}
```

- [x] **Step 5: Ejecutar los tests para ver que pasan**

Run:
```powershell
node --test quality/__tests__
```
Expected: PASS. La línea de resumen informa `fail 0` y nueve tests superados.

- [x] **Step 6: Commit**

```powershell
git add quality/lib quality/__tests__
git commit -m "feat: librería común de salida y ejecución para la puerta de calidad"
```

---

### Task 3: Gate de mojibake

**Files:**
- Create: `quality/check-mojibake.mjs`
- Test: `quality/__tests__/check-mojibake.test.mjs`

**Interfaces:**
- Consumes: `ejecutar` y `exito`/`fallo` (Tarea 2).
- Produces:
  - `PATRONES: string[]` — los tres patrones detectados, escritos como escapes `\uXXXX`.
  - `sinSpansDeCodigo(linea: string): string`
  - `lineaTieneMojibake(ruta: string, linea: string): boolean`
  - `analizar(salidaGitGrep: string): Array<{ ruta: string, numero: number, linea: string }>`
- Ejecutado como CLI: sale con 0 si no hay hallazgos, 1 si los hay o si `git grep` falla.

**Notas de diseño (leer antes de implementar):**

1. El gate busca tres secuencias: el carácter de reemplazo `U+FFFD`, y los caracteres `U+00C3` y `U+00C2`, que son el primer carácter de casi todo mojibake producido al leer UTF-8 como Latin-1.
2. **La búsqueda se hace por secuencia de bytes UTF-8, no por byte suelto.** El byte `0xC3` inicia también las vocales acentuadas y la eñe correctas del español; buscarlo aislado marcaría todo el repositorio. `U+00C3` en UTF-8 es `0xC3 0x83` y `U+00C2` es `0xC3 0x82`: esas parejas sí son inequívocas.
3. Por eso el patrón se pasa a `git grep -P` con escapes de byte ASCII (`\xC3\x83`), y no como caracteres literales en `argv`: mantiene la línea de comandos en ASCII puro y evita problemas de codificación al lanzar procesos en Windows.
4. `git grep -I` salta los archivos que git clasifica como binarios y solo recorre archivos versionados, que es exactamente el alcance que pide el diseño.
5. La autoexcepción: en archivos `.md`, se descarta lo que esté entre acentos graves antes de decidir. Un mojibake accidental nunca está entre acentos graves; una cita deliberada del patrón sí. En archivos que no son `.md` no hay excepción alguna.

- [x] **Step 1: Escribir el test que falla**

`quality/__tests__/check-mojibake.test.mjs`:
```javascript
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  PATRONES,
  sinSpansDeCodigo,
  lineaTieneMojibake,
  analizar,
} from '../check-mojibake.mjs';

// Los literales se escriben con escapes a propósito: si estuvieran como
// caracteres, este archivo de test se marcaría a sí mismo.
const ENIE_ROTA = '\u00C3\u00B1';        // eñe leída como Latin-1
const REEMPLAZO = '\uFFFD';              // U+FFFD
const NBSP_ROTO = '\u00C2\u00A0';        // espacio duro leído como Latin-1

test('los tres patrones vigilados están declarados', () => {
  assert.deepEqual(PATRONES, ['\uFFFD', '\u00C3', '\u00C2']);
});

test('sinSpansDeCodigo quita el contenido entre acentos graves', () => {
  assert.equal(sinSpansDeCodigo('cita `roto` fin'), 'cita  fin');
});

test('sinSpansDeCodigo conserva el texto fuera de los acentos graves', () => {
  assert.equal(sinSpansDeCodigo('sin spans aquí'), 'sin spans aquí');
});

test('detecta mojibake suelto en un Markdown', () => {
  assert.equal(lineaTieneMojibake('docs/ai/README.md', `A${ENIE_ROTA}o`), true);
});

test('no detecta mojibake citado dentro de un span de código en Markdown', () => {
  assert.equal(
    lineaTieneMojibake('docs/ai/PUERTA_CALIDAD.md', `El gate busca \`${ENIE_ROTA}\`.`),
    false,
  );
});

test('en archivos que no son Markdown no hay excepción por acentos graves', () => {
  assert.equal(lineaTieneMojibake('quality/lib/salida.mjs', `\`${ENIE_ROTA}\``), true);
});

test('detecta el carácter de reemplazo', () => {
  assert.equal(lineaTieneMojibake('AGENTS.md', `texto ${REEMPLAZO} texto`), true);
});

test('detecta el espacio duro mal decodificado', () => {
  assert.equal(lineaTieneMojibake('AGENTS.md', `uno${NBSP_ROTO}dos`), true);
});

test('el texto correcto en español no produce hallazgos', () => {
  assert.equal(lineaTieneMojibake('AGENTS.md', 'Año, sesión, güero, ¿qué tal?'), false);
});

test('analizar parsea ruta, número de línea y contenido de git grep', () => {
  const salida = `AGENTS.md:12:Un a${ENIE_ROTA}o\n`;
  assert.deepEqual(analizar(salida), [
    { ruta: 'AGENTS.md', numero: 12, linea: `Un a${ENIE_ROTA}o` },
  ]);
});

test('analizar conserva los dos puntos que vengan dentro del contenido', () => {
  const salida = `docs/ai/README.md:3:nota: a${ENIE_ROTA}o\n`;
  assert.equal(analizar(salida)[0].linea, `nota: a${ENIE_ROTA}o`);
});

test('analizar descarta las líneas exentas por span de código', () => {
  const salida = `docs/ai/PUERTA_CALIDAD.md:7:cita \`${ENIE_ROTA}\`\n`;
  assert.deepEqual(analizar(salida), []);
});

test('analizar ignora una salida vacía', () => {
  assert.deepEqual(analizar(''), []);
});
```

- [x] **Step 2: Ejecutar el test para ver que falla**

Run:
```powershell
node --test quality/__tests__/check-mojibake.test.mjs
```
Expected: FAIL con `ERR_MODULE_NOT_FOUND`: no se puede resolver `../check-mojibake.mjs`.

- [x] **Step 3: Implementar el gate**

`quality/check-mojibake.mjs`:
```javascript
#!/usr/bin/env node
// Gate de mojibake: ningún archivo de texto versionado puede contener el
// carácter de reemplazo ni las secuencias que delatan UTF-8 leído como Latin-1.
//
// Los patrones se escriben con escapes \uXXXX y \xNN a propósito: escritos como
// caracteres literales, este archivo se marcaría a sí mismo.

import { ejecutar } from './lib/ejecutar.mjs';
import { exito, fallo } from './lib/salida.mjs';

// U+FFFD (reemplazo), U+00C3 y U+00C2 (primer carácter de casi todo mojibake).
export const PATRONES = ['\uFFFD', '\u00C3', '\u00C2'];

// Los mismos patrones como secuencias de bytes UTF-8, para git grep -P.
// Se busca la pareja completa, no el byte 0xC3 suelto: ese byte inicia también
// las vocales acentuadas y la eñe correctas, y marcaría todo el repositorio.
const PATRON_BYTES = '\\xEF\\xBF\\xBD|\\xC3\\x83|\\xC3\\x82';

// Un mojibake accidental nunca está entre acentos graves; una cita deliberada
// del patrón sí. Solo aplica a Markdown, donde el span de código existe.
export function sinSpansDeCodigo(linea) {
  return linea.replace(/`[^`]*`/g, '');
}

export function lineaTieneMojibake(ruta, linea) {
  const texto = ruta.endsWith('.md') ? sinSpansDeCodigo(linea) : linea;
  return PATRONES.some((patron) => texto.includes(patron));
}

export function analizar(salidaGitGrep) {
  const hallazgos = [];
  for (const fila of salidaGitGrep.split('\n')) {
    if (fila.trim() === '') continue;
    const primero = fila.indexOf(':');
    if (primero === -1) continue;
    const segundo = fila.indexOf(':', primero + 1);
    if (segundo === -1) continue;

    const ruta = fila.slice(0, primero);
    const numero = Number(fila.slice(primero + 1, segundo));
    const linea = fila.slice(segundo + 1);
    if (!Number.isInteger(numero)) continue;
    if (lineaTieneMojibake(ruta, linea)) hallazgos.push({ ruta, numero, linea });
  }
  return hallazgos;
}

// Punto de entrada CLI. Al importarse como módulo (tests) no se ejecuta.
if (process.argv[1] && process.argv[1].endsWith('check-mojibake.mjs')) {
  const { codigo, salida } = ejecutar(
    'git',
    ['grep', '-I', '-n', '-P', PATRON_BYTES],
    { silencioso: true, sinShell: true },
  );

  // git grep: 0 = hubo coincidencias, 1 = ninguna, >1 = error real.
  if (codigo > 1) {
    console.log(
      fallo(
        'Gate de mojibake',
        `git grep terminó con código ${codigo}. Si el mensaje menciona PCRE, ` +
          'este git no soporta -P y hay que instalar uno que sí.',
      ),
    );
    console.log(salida);
    process.exit(1);
  }

  const hallazgos = codigo === 0 ? analizar(salida) : [];

  if (hallazgos.length > 0) {
    console.log(fallo(`Gate de mojibake: ${hallazgos.length} hallazgo(s)`));
    for (const h of hallazgos) {
      console.log(`       ${h.ruta}:${h.numero}: ${h.linea.trim()}`);
    }
    console.log(
      '       Escribí el carácter correcto en UTF-8. Si necesitás citar el ' +
        'patrón en un .md, ponelo entre acentos graves.',
    );
    process.exit(1);
  }

  console.log(exito('Gate de mojibake: sin hallazgos.'));
}
```

- [x] **Step 4: Ejecutar el test para ver que pasa**

Run:
```powershell
node --test quality/__tests__/check-mojibake.test.mjs
```
Expected: PASS, `fail 0`.

- [x] **Step 5: Ejecutar el gate contra el repositorio real**

Run:
```powershell
node quality/check-mojibake.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[OK] Gate de mojibake: sin hallazgos.` y `codigo=0`. El spec de diseño cita los patrones entre acentos graves, así que queda exento.

- [x] **Step 6: Comprobar que el gate detecta un mojibake real**

Run:
```powershell
"Un a" + [char]0x00C3 + [char]0x00B1 + "o" | Out-File -FilePath prueba-mojibake.md -Encoding utf8
git add prueba-mojibake.md
node quality/check-mojibake.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[FALLO] Gate de mojibake: 1 hallazgo(s)` señalando `prueba-mojibake.md:1` y `codigo=1`.

Limpieza obligatoria antes de seguir:
```powershell
git rm --force --quiet prueba-mojibake.md
node quality/check-mojibake.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: vuelve a `[OK]` y `codigo=0`.

Nota: `Out-File -Encoding utf8` en PowerShell 5.1 escribe BOM. El BOM no afecta a este gate, y el archivo se borra en el mismo paso; para archivos que se conservan, usar el editor o `[System.IO.File]::WriteAllText` con `UTF8Encoding($false)`.

- [x] **Step 7: Commit**

```powershell
git add quality/check-mojibake.mjs quality/__tests__/check-mojibake.test.mjs
git commit -m "feat: gate de mojibake con autoexcepción por spans de código"
```

---

### Task 4: Gate de enlaces

**Files:**
- Create: `quality/check-enlaces.mjs`
- Test: `quality/__tests__/check-enlaces.test.mjs`

**Interfaces:**
- Consumes: `ejecutar` y `exito`/`fallo` (Tarea 2).
- Produces:
  - `extraerEnlaces(contenido: string): Array<{ numero: number, destino: string }>` — enlaces Markdown en línea, saltando bloques cercados y spans de código.
  - `esRelativo(destino: string): boolean`
  - `rutaDelDestino(destino: string): string` — quita ancla y query, decodifica `%20`.
  - `archivosMarkdown(): { ok: boolean, archivos?: string[], motivo?: string }`
- Ejecutado como CLI: sale con 0 si todo enlace relativo apunta a algo existente, 1 si no.

- [x] **Step 1: Escribir el test que falla**

`quality/__tests__/check-enlaces.test.mjs`:
```javascript
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { extraerEnlaces, esRelativo, rutaDelDestino } from '../check-enlaces.mjs';

test('extrae un enlace en línea con su número de línea', () => {
  const md = 'Intro\n\nVer el [mapa](docs/ai/README.md) del proceso.\n';
  assert.deepEqual(extraerEnlaces(md), [{ numero: 3, destino: 'docs/ai/README.md' }]);
});

test('extrae varios enlaces de la misma línea', () => {
  const md = '[uno](a.md) y [dos](b.md)\n';
  assert.deepEqual(extraerEnlaces(md), [
    { numero: 1, destino: 'a.md' },
    { numero: 1, destino: 'b.md' },
  ]);
});

test('descarta el título opcional del enlace', () => {
  const md = '[x](docs/ai/WORKFLOW.md "El flujo")\n';
  assert.deepEqual(extraerEnlaces(md), [{ numero: 1, destino: 'docs/ai/WORKFLOW.md' }]);
});

test('ignora los enlaces dentro de un bloque cercado', () => {
  const md = 'Texto\n```markdown\n[ejemplo](no-existe.md)\n```\nFin\n';
  assert.deepEqual(extraerEnlaces(md), []);
});

test('ignora los enlaces dentro de un span de código', () => {
  const md = 'Se escribe `[texto](destino.md)` así.\n';
  assert.deepEqual(extraerEnlaces(md), []);
});

test('reanuda la extracción después de cerrar el bloque cercado', () => {
  const md = '```\n[dentro](x.md)\n```\n[fuera](y.md)\n';
  assert.deepEqual(extraerEnlaces(md), [{ numero: 4, destino: 'y.md' }]);
});

test('esRelativo distingue los absolutos que no se comprueban', () => {
  assert.equal(esRelativo('https://github.com/luicahleo/trajano-icarus'), false);
  assert.equal(esRelativo('http://ejemplo.test'), false);
  assert.equal(esRelativo('mailto:alguien@ejemplo.test'), false);
  assert.equal(esRelativo('#seccion-interna'), false);
  assert.equal(esRelativo('docs/ai/README.md'), true);
  assert.equal(esRelativo('../AGENTS.md'), true);
});

test('rutaDelDestino recorta el ancla', () => {
  assert.equal(rutaDelDestino('docs/ai/WORKFLOW.md#brainstorming'), 'docs/ai/WORKFLOW.md');
});

test('rutaDelDestino decodifica los espacios escapados', () => {
  assert.equal(rutaDelDestino('docs/un%20archivo.md'), 'docs/un archivo.md');
});
```

- [x] **Step 2: Ejecutar el test para ver que falla**

Run:
```powershell
node --test quality/__tests__/check-enlaces.test.mjs
```
Expected: FAIL con `ERR_MODULE_NOT_FOUND`: no se puede resolver `../check-enlaces.mjs`.

- [x] **Step 3: Implementar el gate**

`quality/check-enlaces.mjs`:
```javascript
#!/usr/bin/env node
// Gate de enlaces: en los .md versionados, todo enlace relativo debe apuntar a
// un archivo existente. Los enlaces http(s) no se comprueban: verificar la red
// haría el gate lento y no determinista.

import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { ejecutar } from './lib/ejecutar.mjs';
import { exito, fallo } from './lib/salida.mjs';

const ENLACE = /\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g;

export function extraerEnlaces(contenido) {
  const enlaces = [];
  let dentroDeBloque = false;

  const lineas = contenido.split('\n');
  for (let i = 0; i < lineas.length; i += 1) {
    const linea = lineas[i];

    if (/^\s*(```|~~~)/.test(linea)) {
      dentroDeBloque = !dentroDeBloque;
      continue;
    }
    if (dentroDeBloque) continue;

    // Un enlace citado entre acentos graves es documentación del formato,
    // no una referencia a comprobar.
    const util = linea.replace(/`[^`]*`/g, '');
    for (const coincidencia of util.matchAll(ENLACE)) {
      enlaces.push({ numero: i + 1, destino: coincidencia[1] });
    }
  }
  return enlaces;
}

export function esRelativo(destino) {
  return !/^([a-z][a-z0-9+.-]*:|#|\/\/)/i.test(destino);
}

export function rutaDelDestino(destino) {
  const sinAncla = destino.split('#')[0].split('?')[0];
  try {
    return decodeURIComponent(sinAncla);
  } catch {
    return sinAncla;
  }
}

export function archivosMarkdown() {
  const { codigo, salida } = ejecutar('git', ['ls-files', '-z', '--', '*.md'], {
    silencioso: true,
    sinShell: true,
  });
  if (codigo !== 0) {
    return { ok: false, motivo: `git ls-files terminó con código ${codigo}` };
  }
  return { ok: true, archivos: salida.split('\0').filter((r) => r !== '') };
}

if (process.argv[1] && process.argv[1].endsWith('check-enlaces.mjs')) {
  const listado = archivosMarkdown();
  if (!listado.ok) {
    console.log(fallo('Gate de enlaces', listado.motivo));
    process.exit(1);
  }

  const rotos = [];
  for (const archivo of listado.archivos) {
    const contenido = await readFile(archivo, 'utf8');
    for (const { numero, destino } of extraerEnlaces(contenido)) {
      if (!esRelativo(destino)) continue;
      const ruta = rutaDelDestino(destino);
      if (ruta === '') continue; // ancla pura dentro del mismo documento
      if (!existsSync(resolve(dirname(archivo), ruta))) {
        rotos.push({ archivo, numero, destino });
      }
    }
  }

  if (rotos.length > 0) {
    console.log(fallo(`Gate de enlaces: ${rotos.length} enlace(s) roto(s)`));
    for (const r of rotos) {
      console.log(`       ${r.archivo}:${r.numero} -> ${r.destino}`);
    }
    console.log('       Corregí el enlace o creá el destino.');
    process.exit(1);
  }

  console.log(
    exito(`Gate de enlaces: ${listado.archivos.length} archivo(s) .md sin enlaces rotos.`),
  );
}
```

- [x] **Step 4: Ejecutar el test para ver que pasa**

Run:
```powershell
node --test quality/__tests__/check-enlaces.test.mjs
```
Expected: PASS, `fail 0`.

- [x] **Step 5: Ejecutar el gate contra el repositorio real**

Run:
```powershell
node quality/check-enlaces.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[OK] Gate de enlaces: ... sin enlaces rotos.` y `codigo=0`.

- [x] **Step 6: Comprobar que el gate detecta un enlace roto**

Run:
```powershell
Set-Content -Path prueba-enlace.md -Value 'Ver el [mapa](docs/ai/NO-EXISTE.md).' -Encoding utf8
git add prueba-enlace.md
node quality/check-enlaces.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[FALLO] Gate de enlaces: 1 enlace(s) roto(s)` con la línea `prueba-enlace.md:1 -> docs/ai/NO-EXISTE.md` y `codigo=1`.

Limpieza obligatoria:
```powershell
git rm --force --quiet prueba-enlace.md
node quality/check-enlaces.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: vuelve a `[OK]` y `codigo=0`.

- [x] **Step 7: Commit**

```powershell
git add quality/check-enlaces.mjs quality/__tests__/check-enlaces.test.mjs
git commit -m "feat: gate de enlaces relativos en Markdown versionado"
```

---

### Task 5: Orquestador y puntos de entrada

**Files:**
- Create: `quality/verify.mjs`
- Create: `verify.ps1`
- Create: `verify.sh`
- Test: `quality/__tests__/verify.test.mjs`

**Interfaces:**
- Consumes: `ejecutar`, `titulo`, `exito`, `fallo` (Tarea 2); los gates de las Tareas 3 y 4.
- Produces:
  - `GATES: Array<{ nombre: string, comando: string, args: string[] }>` — la lista ordenada. La Tarea 9 le agrega una entrada.
  - `ejecutarGates(gates, ejecutor): { ok: boolean, ejecutados: Array<{ nombre: string, codigo: number }> }` — `ejecutor` es una función `(gate) => { codigo: number }`, inyectable para poder testear el corte al primer fallo sin lanzar procesos.
- `verify.ps1` y `verify.sh` propagan el código de salida de `node quality/verify.mjs`.

- [x] **Step 1: Escribir el test que falla**

`quality/__tests__/verify.test.mjs`:
```javascript
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { GATES, ejecutarGates } from '../verify.mjs';

test('el primer gate es el de los tests de la propia puerta', () => {
  assert.equal(GATES[0].nombre, 'Tests de la puerta');
});

test('están declarados los gates de mojibake y de enlaces', () => {
  const nombres = GATES.map((g) => g.nombre);
  assert.ok(nombres.includes('Mojibake'));
  assert.ok(nombres.includes('Enlaces'));
});

test('todos los gates se invocan con node', () => {
  for (const gate of GATES) {
    assert.equal(gate.comando, 'node');
    assert.ok(Array.isArray(gate.args) && gate.args.length > 0);
  }
});

test('con todos los gates en verde devuelve ok y los ejecuta todos', () => {
  const gates = [{ nombre: 'A' }, { nombre: 'B' }, { nombre: 'C' }];
  const resultado = ejecutarGates(gates, () => ({ codigo: 0 }));
  assert.equal(resultado.ok, true);
  assert.equal(resultado.ejecutados.length, 3);
});

test('corta en el primer fallo y no ejecuta los gates siguientes', () => {
  const gates = [{ nombre: 'A' }, { nombre: 'B' }, { nombre: 'C' }];
  const resultado = ejecutarGates(gates, (gate) => ({
    codigo: gate.nombre === 'B' ? 1 : 0,
  }));
  assert.equal(resultado.ok, false);
  assert.deepEqual(
    resultado.ejecutados.map((e) => e.nombre),
    ['A', 'B'],
  );
});

test('informa el código del gate que falló', () => {
  const resultado = ejecutarGates([{ nombre: 'A' }], () => ({ codigo: 7 }));
  assert.equal(resultado.ejecutados[0].codigo, 7);
});
```

- [x] **Step 2: Ejecutar el test para ver que falla**

Run:
```powershell
node --test quality/__tests__/verify.test.mjs
```
Expected: FAIL con `ERR_MODULE_NOT_FOUND`: no se puede resolver `../verify.mjs`.

- [x] **Step 3: Implementar el orquestador**

`quality/verify.mjs`:
```javascript
#!/usr/bin/env node
// Puerta de calidad de Trajano-Icarus.
//   node quality/verify.mjs
// Ejecuta los gates en orden y se detiene en el primero que falla, para dar
// retroalimentación rápida. Ningún gate necesita Docker ni el SDK de .NET.

import { fileURLToPath } from 'node:url';
import { ejecutar } from './lib/ejecutar.mjs';
import { titulo, exito, fallo } from './lib/salida.mjs';

// fileURLToPath, no .pathname: en Windows .pathname produce "/C:/..." y rompe spawn.
const raiz = fileURLToPath(new URL('..', import.meta.url));

export const GATES = [
  // Va primero: si la propia puerta está rota, el resto de los veredictos no
  // vale nada.
  { nombre: 'Tests de la puerta', comando: 'node', args: ['--test', 'quality/__tests__'] },
  { nombre: 'Mojibake', comando: 'node', args: ['quality/check-mojibake.mjs'] },
  { nombre: 'Enlaces', comando: 'node', args: ['quality/check-enlaces.mjs'] },
];

export function ejecutarGates(gates, ejecutor) {
  const ejecutados = [];
  for (const gate of gates) {
    const { codigo } = ejecutor(gate);
    ejecutados.push({ nombre: gate.nombre, codigo });
    if (codigo !== 0) return { ok: false, ejecutados };
  }
  return { ok: true, ejecutados };
}

if (process.argv[1] && process.argv[1].endsWith('verify.mjs')) {
  const resultado = ejecutarGates(GATES, (gate) => {
    console.log(titulo(gate.nombre));
    const { codigo, duracionMs } = ejecutar(gate.comando, gate.args, { cwd: raiz });
    if (codigo === 0) console.log(exito(`${gate.nombre} (${duracionMs} ms)`));
    return { codigo };
  });

  if (!resultado.ok) {
    const ultimo = resultado.ejecutados.at(-1);
    console.log(fallo(`${ultimo.nombre} falló`, `código ${ultimo.codigo}`));
    console.log(fallo('La puerta de calidad no pasó. Arreglá el contenido, no el gate.'));
    process.exit(1);
  }

  console.log(exito('Puerta de calidad: verde.'));
}
```

- [x] **Step 4: Crear los puntos de entrada**

`verify.ps1`:
```powershell
# Puerta de calidad de Trajano-Icarus.
# Uso: ./verify.ps1
node quality/verify.mjs
exit $LASTEXITCODE
```

`verify.sh`:
```sh
#!/bin/sh
# Puerta de calidad de Trajano-Icarus.
# Uso: ./verify.sh
node quality/verify.mjs "$@"
```

- [x] **Step 5: Ejecutar el test para ver que pasa**

Run:
```powershell
node --test quality/__tests__/verify.test.mjs
```
Expected: PASS, `fail 0`.

- [x] **Step 6: Ejecutar la puerta completa por sus dos puntos de entrada**

Run:
```powershell
./verify.ps1
Write-Output "codigo=$LASTEXITCODE"
```
Expected: los tres títulos de gate, tres líneas `[OK]`, la línea final `[OK] Puerta de calidad: verde.` y `codigo=0`.

Si hay `bash` disponible (Git para Windows lo instala), comprobar también el punto de entrada POSIX:
```powershell
bash ./verify.sh
Write-Output "codigo=$LASTEXITCODE"
```
Expected: la misma salida y `codigo=0`. Si `bash` no está disponible, anotarlo: el CI de la Tarea 12 no usa `verify.sh`, así que no bloquea.

- [x] **Step 7: Commit**

```powershell
git add quality/verify.mjs quality/__tests__/verify.test.mjs verify.ps1 verify.sh
git commit -m "feat: orquestador de la puerta de calidad y puntos de entrada"
```

---

### Task 6: Documentos de proceso en `docs/ai/`

**Files:**
- Create: `docs/ai/README.md`
- Create: `docs/ai/WORKFLOW.md`
- Create: `docs/ai/PUERTA_CALIDAD.md`
- Create: `docs/ai/ECONOMIA_TOKENS.md`
- Create: `docs/ai/CONTEXT-EFFICIENCY.md`
- Create: `docs/ai/FLUJO_GIT.md`
- Create: `docs/ai/HANDOFF.template.md`

**Interfaces:**
- Consumes: la puerta de calidad de la Tarea 5, que `PUERTA_CALIDAD.md` describe.
- Produces: el mapa de lectura que `AGENTS.md` referenciará en la Tarea 7. Los siete archivos van en el mismo commit porque `README.md` enlaza a los otros seis y el gate de enlaces exige que el mapa esté completo.

**Nota:** `PUERTA_CALIDAD.md` describe los tres gates vigentes hoy. La Tarea 9 le agrega el de adaptadores cuando ese gate exista. Ningún documento nombra cantidades de tests ni de proyectos: esas cifras caducan en cada commit.

- [x] **Step 1: Crear `docs/ai/README.md`**

`docs/ai/README.md`:
```markdown
# Trabajo con agentes de IA

Este directorio define un proceso neutral para Codex, Kimi CLI, Claude, Gemini y
cualquier agente futuro. La información se divide por frecuencia de uso para no
pagar contexto inútil.

## Capas

1. `AGENTS.md` en la raíz: reglas esenciales, cargadas siempre.
2. `AGENTS.md` por árbol: reglas locales, solo al trabajar en ese árbol. Todavía
   no existe ninguno; llegan con el backend y el frontend.
3. Este directorio: proceso completo, bajo demanda.

## Documentos

| Documento | Cuándo leerlo |
|---|---|
| [WORKFLOW.md](WORKFLOW.md) | Antes de una feature, un bloque de fase o un cambio arquitectónico |
| [PUERTA_CALIDAD.md](PUERTA_CALIDAD.md) | Cuando un gate falla o hay que agregar uno |
| [FLUJO_GIT.md](FLUJO_GIT.md) | Antes de integrar, promover o tocar ramas |
| [ECONOMIA_TOKENS.md](ECONOMIA_TOKENS.md) | Al planificar la sesión: qué modelo y qué tamaño |
| [CONTEXT-EFFICIENCY.md](CONTEXT-EFFICIENCY.md) | Al explorar un repositorio que no conocés |
| [HANDOFF.template.md](HANDOFF.template.md) | Al cerrar una sesión con trabajo a medias |

## Qué leer al iniciar

| Tipo de tarea | Contexto inicial |
|---|---|
| Pregunta o diagnóstico | `AGENTS.md` y los archivos directamente relevantes |
| Cambio pequeño | Lo anterior más los tests vecinos |
| Feature o bloque | Lo anterior más `docs/ai/WORKFLOW.md` y el spec vigente |
| Continuación de otra sesión | Lo anterior más `docs/ai/HANDOFF.md`, validado contra git |

Los specs y planes de `docs/superpowers/` son **memoria consultable, no contexto
obligatorio**. Nunca cargarlos en bloque: buscar por nombre, símbolo o
dependencia y abrir solo los resultados relevantes.

## Compatibilidad entre agentes

El núcleo es siempre el mismo archivo. Cambia solo cómo llega a cada herramienta:

- Codex y Kimi CLI descubren `AGENTS.md` jerárquicamente, sin configuración.
- Claude Code lee `CLAUDE.md`, que importa el núcleo.
- Gemini CLI lee `GEMINI.md`, que importa el núcleo.
- Copilot lee `.github/copilot-instructions.md`, que apunta al núcleo en texto
  porque no soporta imports.
- DeepSeek es un modelo, no un harness: hereda el archivo del harness que lo
  hospeda.

Esos adaptadores están **generados**. No se editan a mano: se edita `AGENTS.md`
y se corre `node quality/generar-adaptadores.mjs`.

Las capacidades particulares de cada proveedor —memoria, hooks, subagentes,
comandos— son optimizaciones opcionales. Nunca deben ser necesarias para
entender ni ejecutar el proceso del proyecto.
```

- [x] **Step 2: Crear `docs/ai/WORKFLOW.md`**

`docs/ai/WORKFLOW.md`:
```markdown
# Flujo de trabajo

El ciclo completo, para features, bloques de fase y cambios arquitectónicos. La
ceremonia se escala al riesgo: una corrección de una línea no necesita spec, y un
bounded context nuevo no se improvisa.

## Escalado según riesgo

| Riesgo | Ceremonia |
|---|---|
| Pregunta, explicación, diagnóstico | Investigar y responder. No modificar nada. |
| Cambio pequeño y claro | Implementar, probar en proporción, resumir. |
| Feature o bloque | El ciclo completo de abajo. |
| Cambio arquitectónico o irreversible | El ciclo completo, y confirmación explícita antes de ejecutar. |

## El ciclo

1. **Preparación.** `git status --short --branch` y `git log -5 --oneline`. Leer
   el spec o el plan vigente si existe. No listar el repositorio entero.
2. **Brainstorming.** Explorar intención, alternativas y restricciones antes de
   diseñar. Termina cuando las decisiones están tomadas, no cuando se acaban las
   ideas.
3. **Spec.** Se guarda en `docs/superpowers/specs/AAAA-MM-DD-<tema>-design.md`.
   Registra decisiones y su porqué, y declara lo que queda fuera de alcance.
4. **Plan.** Se guarda en `docs/superpowers/plans/AAAA-MM-DD-<tema>.md`. Tareas
   pequeñas ordenadas por dependencias, con rutas exactas, prueba roja esperada,
   comando de verificación y commit previsto.
5. **TDD.** Escribir el test, verlo fallar por el motivo correcto, escribir la
   implementación mínima, verlo pasar. Un test que nunca se vio en rojo no
   prueba nada.
6. **Integración.** Puerta de calidad completa en verde, y commit.
7. **Revisión.** No hay revisor humano: la revisión es la puerta más una lectura
   del diff propio antes de hacer push.
8. **Cierre.** Actualizar el plan con lo hecho. Si el trabajo queda a medias,
   escribir `docs/ai/HANDOFF.md` desde la plantilla; si cierra, borrarlo.

## Reglas del ciclo

- Un spec o un plan que ya no describe la realidad es peor que ninguno.
- Los pasos se saltan hacia arriba, nunca hacia abajo: se puede decidir que algo
  no necesita spec; no se puede implementar sin haber visto el test en rojo.
- El alcance no se amplía sin autorización. Si aparece algo fuera de alcance, se
  anota y se sigue.
```

- [x] **Step 3: Crear `docs/ai/PUERTA_CALIDAD.md`**

`docs/ai/PUERTA_CALIDAD.md`:
```markdown
# Puerta de calidad

No hay revisión humana de código: hay un solo desarrollador. La puerta sustituye
a esa revisión, así que su autoridad no es negociable.

## Cómo se ejecuta

```powershell
./verify.ps1
```

En POSIX, `./verify.sh`. Ambos son envoltorios de `node quality/verify.mjs`, que
ejecuta los gates en orden y **se detiene en el primero que falla** para dar
retroalimentación rápida.

Ningún gate necesita Docker ni el SDK de .NET: la puerta corre en segundos.

## Gates vigentes

| Gate | Qué comprueba | Cómo se arregla un fallo |
|---|---|---|
| Tests de la puerta | Que los propios scripts de `quality/` estén verdes | Según el mensaje del test |
| Mojibake | Ausencia del carácter de reemplazo y de las secuencias que delatan UTF-8 leído como Latin-1, en todo archivo versionado que git clasifique como texto | Escribir el carácter correcto en UTF-8 |
| Enlaces | Que todo enlace relativo de los `.md` versionados apunte a un archivo existente | Corregir el enlace o crear el destino |

Los enlaces absolutos `http` y `https` no se comprueban: verificar la red haría
el gate lento y no determinista.

### Autoexcepción del gate de mojibake

Este documento necesita poder nombrar las secuencias que el gate detecta, así que
se marcaría a sí mismo. La solución no es una lista de archivos exentos —que se
convierte en un agujero permanente— sino una regla:

> En archivos `.md`, el gate ignora lo que esté **entre acentos graves**.

Un mojibake accidental nunca está entre acentos graves; una cita deliberada del
patrón sí. En archivos que no son `.md` no hay excepción: en `.mjs` los patrones
se escriben como escapes `\uXXXX`.

La regla vale para los spans en línea, no para los bloques cercados. Dentro de un
bloque de código en un `.md`, citar el patrón con su escape.

## Reglas innegociables

1. Nunca `--no-verify`, ni en commit ni en push.
2. Nunca relajar una baseline, un umbral o una exclusión para que pase el gate.
   Si el gate falla, el problema está en el contenido.
3. Las baselines solo se mueven hacia mejor, en commit propio que explique la
   mejora.
4. Nunca afirmar verde sin haber ejecutado el comando y visto la salida.

## Agregar un gate

Un gate nuevo es un archivo `quality/check-<algo>.mjs` con funciones puras
exportadas y un bloque CLI, su archivo de tests en `quality/__tests__/`, y una
entrada en la lista `GATES` de `quality/verify.mjs`. Si además corre en CI,
`.github/workflows/ci.yml` ya lo cubre: ese workflow invoca la puerta entera.

Este documento describe los gates, no cuenta tests ni proyectos: esas cifras
caducan en cada commit.
```

- [x] **Step 4: Crear `docs/ai/FLUJO_GIT.md`**

`docs/ai/FLUJO_GIT.md`:
```markdown
# Flujo git

Un solo desarrollador. **No hay pull requests**, y este documento es la única
descripción del flujo: si algo lo contradice, este documento manda.

## Ramas

| Rama | Papel |
|---|---|
| `develop` | Rama por defecto y de trabajo. Commit y push directos. |
| `master` | Producción. Solo recibe `develop`, y solo a pedido explícito. |

No se crean ramas de trabajo salvo pedido explícito del usuario.

## Ciclo normal

1. Trabajar en `develop`.
2. Ejecutar `./verify.ps1` y ver la salida en verde.
3. `git add` de las rutas concretas del cambio. Nunca `git add -A` a ciegas.
4. `git commit` con mensaje en español y en modo convencional
   (`feat:`, `fix:`, `chore:`, `docs:`, `test:`, `ci:`).
5. `git push`.

## Promoción a producción

Solo a pedido explícito del usuario:

1. `./verify.ps1` completo en verde sobre `develop`.
2. Merge fast-forward de `develop` a `master`.
3. Push de `master`, que dispara su propio run de CI.

La compuerta de despliegue del subproyecto 4 consultará
`ci.yml/runs?head_sha=<sha>&branch=master&event=push` y exigirá
`conclusion == success`. El filtro `event=push` implica que el commit tiene que
estar en `master` con su propio run en verde: por eso el mecanismo funciona sin
pull requests.

## Prohibiciones

- Nunca `--no-verify`.
- Nunca `push --force` sobre `develop` ni `master`.
- Nunca merge ni push a `master` sin pedido explícito.
- Nunca reescribir historia ya publicada.
```

- [x] **Step 5: Crear `docs/ai/ECONOMIA_TOKENS.md`**

`docs/ai/ECONOMIA_TOKENS.md`:
```markdown
# Economía de tokens

El desarrollo alterna entre proveedores. Lo que se elige no es un modelo
concreto, que caduca, sino un **nivel de capacidad**.

## Niveles

| Nivel | Cuándo usarlo |
|---|---|
| Alto | Diseño, arquitectura, decisiones irreversibles, depuración difícil, cambios en la puerta de calidad o en el build |
| Intermedio | Implementación guiada por un plan, refactors acotados, tests |
| Económico | Renombrados, formato, ediciones mecánicas de una sola pasada |

Regla: usar el nivel más económico que mantenga la calidad, y **elevarlo ante
decisiones de criterio**. No bajarlo en cambios de configuración de build o de
verificación, aunque parezcan mecánicos: un error ahí es caro y silencioso.

## Equivalencias por proveedor

| Nivel | Anthropic | OpenAI / Codex | Google | Moonshot (Kimi) | DeepSeek |
|---|---|---|---|---|---|
| Alto | Opus | el modelo de razonamiento más capaz disponible | Gemini Pro | Kimi con razonamiento extendido | DeepSeek en modo razonador |
| Intermedio | Sonnet | el modelo general de la generación vigente | Gemini Flash | Kimi estándar | DeepSeek en modo conversacional |
| Económico | Haiku | el modelo compacto de la generación vigente | Gemini Flash Lite | Kimi estándar | DeepSeek en modo conversacional |

La tabla nombra familias, no versiones. Al cambiar de generación se actualiza
sola: solo hay que preguntarse qué modelo ocupa hoy cada casilla.

## Tamaño de sesión

- Una feature o un bloque por sesión.
- Preferir sesiones nuevas con un handoff breve antes que historiales largos: un
  contexto largo cuesta en cada turno, un handoff cuesta una vez.
- No usar subagentes salvo trabajo verdaderamente independiente que compense su
  coste.
- Guardar las decisiones duraderas en specs. El chat no es documentación.

Ver también [CONTEXT-EFFICIENCY.md](CONTEXT-EFFICIENCY.md).
```

- [x] **Step 6: Crear `docs/ai/CONTEXT-EFFICIENCY.md`**

`docs/ai/CONTEXT-EFFICIENCY.md`:
```markdown
# Exploración eficiente

Leer de más cuesta en cada turno posterior, no solo en el turno que lee. Estas
reglas son las mismas que resume `AGENTS.md`; acá está el detalle.

## Orden de exploración

1. Empezar solo con `git status --short --branch` y `git log -5 --oneline`.
2. No ejecutar listados recursivos ni volcados de todos los archivos al iniciar.
3. Buscar por término, símbolo o ruta probable. Limitar primero el resultado y
   refinar la búsqueda antes de ampliarla.
4. Leer fragmentos antes que archivos completos. Abrir solo el spec, el plan, el
   código y los tests vinculados con la tarea actual.
5. Revisar primero `git diff --stat` y después solo los diffs relevantes.
6. Resumir logs y errores por causa, archivo y línea. No pegar salidas extensas.

## Qué no hacer

- No releer todos los specs históricos. `docs/superpowers/specs/` y
  `docs/superpowers/plans/` son memoria consultable, no contexto obligatorio.
- No abrir un archivo entero para confirmar un detalle que una búsqueda responde.
- No repetir una exploración cuyo resultado ya está en la conversación.
- No pegar el contenido de un archivo que ya se leyó para "tenerlo a mano".

## Señales de que se está explorando mal

- Se leyeron más de tres archivos sin haber formulado todavía la hipótesis.
- Se listó un directorio completo para encontrar un solo archivo.
- Se cargó un documento de proceso para una tarea trivial.
```

- [x] **Step 7: Crear `docs/ai/HANDOFF.template.md`**

`docs/ai/HANDOFF.template.md`:
```markdown
# Handoff

> Copiar este archivo a `docs/ai/HANDOFF.md` al cerrar una sesión con trabajo a
> medias. `HANDOFF.md` está en `.gitignore`: es estado efímero, no memoria del
> proyecto. **Borrarlo en cuanto el trabajo cierre**, para que no se convierta en
> documentación obsoleta que el próximo agente crea vigente.

- **Fecha**: AAAA-MM-DD
- **Rama**: develop
- **Último commit**: `<sha corto>` — `<asunto>`

## Objetivo de la sesión

Una o dos frases. Qué se estaba intentando lograr.

## Estado

- Hecho: …
- A medias: … (con la ruta exacta del archivo y qué falta)
- Sin empezar: …

## Verificación

- Último comando ejecutado: `./verify.ps1`
- Resultado observado: … (verde, o el gate que falló y su mensaje)

## Decisiones tomadas en la sesión

Las que no están en ningún spec todavía. Si alguna es duradera, moverla al spec
en vez de dejarla acá.

## Siguiente paso concreto

Una sola acción, con la ruta del archivo por donde retomar.

## Advertencias

Trampas encontradas, cosas que parecían ciertas y no lo eran, comandos que no
funcionan en este entorno.

> Verificar cada afirmación importante de este documento contra git y contra los
> archivos actuales antes de confiar en ella.
```

- [x] **Step 8: Ejecutar la puerta para validar los documentos recién creados**

Run:
```powershell
git add docs/ai
./verify.ps1
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[OK] Puerta de calidad: verde.` y `codigo=0`. El gate de enlaces ahora recorre `docs/ai/README.md` y confirma que sus seis enlaces relativos existen; el de mojibake confirma que `PUERTA_CALIDAD.md` no cita ningún patrón fuera de un span de código.

El `git add` va antes de `verify` a propósito: ambos gates solo ven archivos versionados.

- [x] **Step 9: Comprobar que el gate de enlaces protege el mapa**

Run:
```powershell
Add-Content -Path docs/ai/README.md -Value 'Enlace de prueba: [roto](NO-EXISTE.md)'
node quality/check-enlaces.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[FALLO] Gate de enlaces: 1 enlace(s) roto(s)` señalando `docs/ai/README.md` y `codigo=1`.

**No hacer `git add` de la versión rota.** El archivo ya está en el índice desde el Step 8, así que `git ls-files` lo lista y el gate lee su contenido del árbol de trabajo: la modificación se detecta igual, y el índice conserva la versión buena para poder revertir.

Revertir:
```powershell
git checkout -- docs/ai/README.md
node quality/check-enlaces.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: vuelve a `[OK]` y `codigo=0`. `git checkout --` restaura desde el índice, que tiene el contenido agregado en el Step 8.

- [x] **Step 10: Commit**

```powershell
git add docs/ai
git commit -m "docs: documentos de proceso para agentes en docs/ai"
```

---

### Task 7: `AGENTS.md` raíz

**Files:**
- Create: `AGENTS.md`

**Interfaces:**
- Consumes: la puerta de la Tarea 5 (que ya existe, así que la instrucción de ejecutarla es cierta) y los documentos de la Tarea 6 (que ya existen, así que las referencias apuntan a algo).
- Produces: la única fuente de instrucciones. La Tarea 8 genera adaptadores que apuntan a este archivo.

**Nota de forma:** las referencias a documentos van entre acentos graves, no como enlaces Markdown. Son rutas que el agente debe abrir con su propia herramienta, no navegación entre documentos, y así el gate de enlaces no las trata como enlaces.

- [x] **Step 1: Crear `AGENTS.md`**

`AGENTS.md`:
```markdown
# Trajano-Icarus — instrucciones para agentes

Fuente única para Codex, Kimi CLI, Claude, Gemini, Copilot y cualquier agente
futuro. Mantener este archivo **corto**: solo reglas que aplican a casi cualquier
tarea. Lo específico vive en `docs/ai/`.

Los archivos `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md` y los
`.*ignore` por proveedor están **generados** desde este archivo. No editarlos a
mano: editar este, y correr `node quality/generar-adaptadores.mjs`.

## Prioridades

- Seguir el pedido explícito del usuario y no ampliar el alcance sin
  autorización.
- Documentos, textos e identificadores de dominio en español correcto, con
  acentos y en UTF-8 sin BOM. Nunca mojibake.
- Anti-PII no negociable: nunca registrar datos biométricos, documentos de
  identidad, credenciales, tokens ni registros nominales de acceso de
  trabajadores. Usar mensajes de error genéricos.
- Preservar los cambios ajenos y evitar operaciones destructivas.
- Nunca afirmar que algo está verde sin haber ejecutado el comando y visto la
  salida.

## Ramas

- `develop` es la rama por defecto y de trabajo: commit y push directos tras
  verificar. No hay pull requests.
- `master` es producción: recibe `develop` por merge fast-forward, **solo a
  pedido explícito del usuario**.
- No crear ramas de trabajo salvo pedido explícito.
- Detalle en `docs/ai/FLUJO_GIT.md`.

## Descubrimiento eficiente

1. Empezar solo con `git status --short --branch` y `git log -5 --oneline`.
2. No ejecutar listados recursivos ni volcados de todos los archivos al iniciar.
3. Buscar por término, símbolo o ruta probable; limitar el resultado primero y
   refinar antes de ampliar.
4. Leer fragmentos antes que archivos completos. Abrir solo el spec, el plan, el
   código y los tests vinculados con la tarea actual.
5. Revisar primero `git diff --stat` y después solo los diffs relevantes.
6. Resumir logs y errores por causa, archivo y línea; no pegar salidas extensas.

`docs/superpowers/specs/` y `docs/superpowers/plans/` son memoria consultable,
no contexto obligatorio. No cargarlos en bloque.

## Selección de proceso

- Pregunta, explicación o diagnóstico: investigar y responder; no modificar.
- Cambio pequeño y claro: implementar, probar en proporción y resumir.
- Feature, bloque o cambio arquitectónico: seguir `docs/ai/WORKFLOW.md` desde
  brainstorming hasta cierre.
- Continuación de otra sesión: leer primero `docs/ai/HANDOFF.md` si existe, y
  verificar cada afirmación importante contra git y los archivos actuales.

## Proyecto

- Trajano-Icarus es la refactorización de ICARUS: control de acceso de
  trabajadores (zonas, biométricos) y gestión avícola (granjas, galpones,
  producción de huevos, mortalidad, vacunación, alimentación, despachos,
  precios).
- El vocabulario y las reglas del negocio están en
  `docs/dominio/glosario-avicola.md`. Consultarlo antes de nombrar una entidad o
  inventar una regla.
- Backend .NET bajo `Icarus/` y frontend React bajo `web/`: **todavía no
  existen**. Llegan en los subproyectos 2 y 3. No crearlos por iniciativa propia.
- Cuando existan, sus `AGENTS.md` locales complementarán a este archivo al
  trabajar en esos árboles.

## Verificación

- Durante TDD, ejecutar el test dirigido; la suite completa al integrar o cerrar.
- Un test que nunca se vio en rojo no prueba nada.
- Informar las pruebas no ejecutadas y el motivo.

## Puerta de calidad

- Ejecutar `./verify.ps1` (o `./verify.sh`) antes de cada commit y push. Es
  obligatorio y sustituye a la revisión humana del código.
- Prohibido `--no-verify` en commit o push.
- Prohibido relajar una baseline, un umbral o una exclusión para que pase el
  gate. Si el gate falla, se arregla el contenido, no el gate.
- Las baselines de `quality/` solo se actualizan hacia mejor, en commit propio
  que explique la mejora.
- Detalle de cada gate: `docs/ai/PUERTA_CALIDAD.md`.

## Economía de contexto

- Una feature o bloque por sesión.
- Preferir sesiones nuevas con handoff breve frente a historiales largos.
- Usar el nivel de modelo más económico que mantenga la calidad; elevarlo ante
  decisiones de criterio. No bajarlo en cambios de configuración de build o de
  verificación, aunque parezcan mecánicos.
- No usar subagentes salvo trabajo verdaderamente independiente que compense su
  coste.
- Guardar las decisiones duraderas en specs; el chat no es documentación.
- Detalle en `docs/ai/ECONOMIA_TOKENS.md` y `docs/ai/CONTEXT-EFFICIENCY.md`.

Mapa completo de documentación: `docs/ai/README.md`.
```

- [x] **Step 2: Verificar con la puerta**

Run:
```powershell
git add AGENTS.md
./verify.ps1
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[OK] Puerta de calidad: verde.` y `codigo=0`.

- [x] **Step 3: Verificar que no hay BOM**

Run:
```powershell
$bytes = [System.IO.File]::ReadAllBytes('AGENTS.md')
Write-Output ("primeros bytes: {0:X2} {1:X2} {2:X2}" -f $bytes[0], $bytes[1], $bytes[2])
```
Expected: los tres primeros bytes **no** son `EF BB BF`. Si lo son, reescribir el archivo sin BOM:
```powershell
$texto = [System.IO.File]::ReadAllText('AGENTS.md')
[System.IO.File]::WriteAllText((Resolve-Path 'AGENTS.md'), $texto, (New-Object System.Text.UTF8Encoding($false)))
```

- [x] **Step 4: Commit**

```powershell
git add AGENTS.md
git commit -m "docs: AGENTS.md como única fuente de instrucciones para agentes"
```

---

### Task 8: Manifiesto y generador de adaptadores

**Files:**
- Create: `quality/adaptadores/manifiesto.mjs`
- Create: `quality/generar-adaptadores.mjs`
- Test: `quality/__tests__/manifiesto.test.mjs`
- Generated: `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, `.clineignore`, `.cursorignore`, `.geminiignore`

**Interfaces:**
- Consumes: `AGENTS.md` (Tarea 7), `exito`/`fallo` (Tarea 2).
- Produces:
  - `AVISO: string` — la línea que marca todo archivo generado.
  - `ADAPTADORES: Array<{ harness: string, ruta: string, contenido: string }>` en `quality/adaptadores/manifiesto.mjs`.
  - `generar(adaptadores, raiz): Array<{ ruta: string, accion: 'escrito' | 'sin-cambios' }>` en `quality/generar-adaptadores.mjs`.
  - La Tarea 9 consume `ADAPTADORES` con esa forma exacta.

**Convenciones verificadas de cada herramienta** (no se adivinan; al agregar un harness nuevo se comprueba contra su documentación):

| Harness | Archivo | Mecanismo |
|---|---|---|
| Codex | — | `AGENTS.md` nativo, jerárquico |
| Kimi CLI | — | `AGENTS.md` nativo, jerárquico; el del proyecto sobrescribe al global |
| Claude Code | `CLAUDE.md` | import con arroba: `@AGENTS.md` |
| Gemini CLI | `GEMINI.md` | import con arroba y ruta explícita: `@./AGENTS.md` |
| Copilot | `.github/copilot-instructions.md` | puntero textual; no soporta imports |
| DeepSeek | — | es un modelo, no un harness |

- [x] **Step 1: Escribir el test que falla**

`quality/__tests__/manifiesto.test.mjs`:
```javascript
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { AVISO, ADAPTADORES } from '../adaptadores/manifiesto.mjs';

test('cada adaptador declara harness, ruta y contenido', () => {
  for (const a of ADAPTADORES) {
    assert.ok(a.harness, 'falta harness');
    assert.ok(a.ruta, `falta ruta en ${a.harness}`);
    assert.ok(a.contenido.length > 0, `contenido vacío en ${a.ruta}`);
  }
});

test('no hay dos adaptadores para la misma ruta', () => {
  const rutas = ADAPTADORES.map((a) => a.ruta);
  assert.equal(new Set(rutas).size, rutas.length);
});

test('están los seis archivos que el diseño manda generar', () => {
  const rutas = ADAPTADORES.map((a) => a.ruta).sort();
  assert.deepEqual(rutas, [
    '.clineignore',
    '.cursorignore',
    '.geminiignore',
    '.github/copilot-instructions.md',
    'CLAUDE.md',
    'GEMINI.md',
  ]);
});

test('todo contenido lleva el aviso de archivo generado', () => {
  for (const a of ADAPTADORES) {
    assert.ok(a.contenido.includes(AVISO), `sin aviso en ${a.ruta}`);
  }
});

test('todo contenido termina en un único salto de línea', () => {
  for (const a of ADAPTADORES) {
    assert.ok(a.contenido.endsWith('\n'), `sin salto final en ${a.ruta}`);
    assert.ok(!a.contenido.endsWith('\n\n'), `salto final duplicado en ${a.ruta}`);
  }
});

test('los adaptadores de instrucciones apuntan al núcleo', () => {
  const instrucciones = ADAPTADORES.filter((a) => a.ruta.endsWith('.md'));
  assert.equal(instrucciones.length, 3);
  for (const a of instrucciones) {
    assert.match(a.contenido, /AGENTS\.md/);
  }
});

test('los adaptadores de instrucciones no acumulan reglas propias', () => {
  for (const a of ADAPTADORES.filter((x) => x.ruta.endsWith('.md'))) {
    const lineas = a.contenido.trimEnd().split('\n').filter((l) => l.trim() !== '');
    assert.ok(lineas.length <= 5, `${a.ruta} tiene ${lineas.length} líneas útiles, máximo 5`);
  }
});

test('los tres archivos de ignorados tienen contenido idéntico', () => {
  const ignores = ADAPTADORES.filter((a) => a.ruta.endsWith('ignore'));
  assert.equal(ignores.length, 3);
  const primero = ignores[0].contenido;
  for (const a of ignores) assert.equal(a.contenido, primero);
});

test('los ignorados cubren los secretos y el ruido de build', () => {
  const ignore = ADAPTADORES.find((a) => a.ruta === '.clineignore').contenido;
  for (const patron of ['node_modules/', 'bin/', 'obj/', '.env', '.git/']) {
    assert.ok(ignore.includes(patron), `falta ${patron} en los ignorados`);
  }
});
```

- [x] **Step 2: Ejecutar el test para ver que falla**

Run:
```powershell
node --test quality/__tests__/manifiesto.test.mjs
```
Expected: FAIL con `ERR_MODULE_NOT_FOUND`: no se puede resolver `../adaptadores/manifiesto.mjs`.

- [x] **Step 3: Implementar el manifiesto**

`quality/adaptadores/manifiesto.mjs`:
```javascript
// Tabla harness -> archivo -> contenido. Es la única fuente de los adaptadores.
// Agregar un harness es agregar una entrada acá y correr el generador. La
// convención de archivo de cada herramienta se verifica contra su documentación
// en el momento de agregarla; no se adivina.
//
// Codex y Kimi CLI no aparecen: descubren AGENTS.md de forma nativa y
// jerárquica, así que no necesitan adaptador. DeepSeek tampoco: es un modelo,
// no un harness, y hereda el archivo del harness que lo hospeda.

export const AVISO =
  'Archivo generado por quality/generar-adaptadores.mjs. No editar a mano: editar AGENTS.md.';

const IGNORADOS = `# ${AVISO}
# Rutas que ningún agente necesita leer: ruido de build, dependencias y
# secretos. Mantener el contenido idéntico en los tres archivos es justamente
# lo que este generador garantiza.

.git/
node_modules/
bin/
obj/
artifacts/
dist/
coverage/

.env
.env.*
*.pfx
*.p12
*.key

.vs/
.idea/
graphify-out/
.superpowers/
`;

export const ADAPTADORES = [
  {
    harness: 'Claude Code',
    ruta: 'CLAUDE.md',
    contenido: `<!-- ${AVISO} -->

@AGENTS.md
`,
  },
  {
    harness: 'Gemini CLI',
    ruta: 'GEMINI.md',
    contenido: `<!-- ${AVISO} -->

@./AGENTS.md
`,
  },
  {
    harness: 'Copilot',
    ruta: '.github/copilot-instructions.md',
    contenido: `<!-- ${AVISO} -->

Las instrucciones de este proyecto viven en \`AGENTS.md\`, en la raíz del
repositorio. Leelo completo antes de proponer cambios.

Copilot no soporta imports: este archivo es solo un puntero y no debe acumular
reglas propias, que divergirían del núcleo.
`,
  },
  { harness: 'Cline', ruta: '.clineignore', contenido: IGNORADOS },
  { harness: 'Cursor', ruta: '.cursorignore', contenido: IGNORADOS },
  { harness: 'Gemini CLI', ruta: '.geminiignore', contenido: IGNORADOS },
];
```

- [x] **Step 4: Implementar el generador**

`quality/generar-adaptadores.mjs`:
```javascript
#!/usr/bin/env node
// Escribe los adaptadores declarados en el manifiesto. Idempotente: si el
// archivo ya coincide, no lo toca, así que una segunda corrida deja el árbol
// limpio.

import { mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ADAPTADORES } from './adaptadores/manifiesto.mjs';
import { exito } from './lib/salida.mjs';

export function generar(adaptadores, raiz) {
  const resultados = [];
  for (const { ruta, contenido } of adaptadores) {
    const destino = join(raiz, ruta);
    const actual = existsSync(destino) ? readFileSync(destino, 'utf8') : null;

    if (actual === contenido) {
      resultados.push({ ruta, accion: 'sin-cambios' });
      continue;
    }
    mkdirSync(dirname(destino), { recursive: true });
    // writeFileSync con utf8 no escribe BOM: es justo lo que hace falta.
    writeFileSync(destino, contenido, 'utf8');
    resultados.push({ ruta, accion: 'escrito' });
  }
  return resultados;
}

if (process.argv[1] && process.argv[1].endsWith('generar-adaptadores.mjs')) {
  const raiz = fileURLToPath(new URL('..', import.meta.url));
  const resultados = generar(ADAPTADORES, raiz);
  const escritos = resultados.filter((r) => r.accion === 'escrito');

  for (const r of escritos) console.log(`       ${r.ruta}`);
  console.log(
    exito(
      `Adaptadores generados: ${escritos.length} escrito(s), ` +
        `${resultados.length - escritos.length} sin cambios.`,
    ),
  );
}
```

- [x] **Step 5: Ejecutar el test para ver que pasa**

Run:
```powershell
node --test quality/__tests__/manifiesto.test.mjs
```
Expected: PASS, `fail 0`.

- [x] **Step 6: Generar los adaptadores y comprobar la idempotencia**

Run:
```powershell
node quality/generar-adaptadores.mjs
```
Expected: seis rutas listadas y `[OK] Adaptadores generados: 6 escrito(s), 0 sin cambios.`

Segunda corrida:
```powershell
node quality/generar-adaptadores.mjs
git status --short
```
Expected: `[OK] Adaptadores generados: 0 escrito(s), 6 sin cambios.` y `git status --short` muestra los seis archivos como no versionados (`??`), sin ninguna modificación adicional: el generador es idempotente.

- [x] **Step 7: Verificar con la puerta**

Run:
```powershell
git add CLAUDE.md GEMINI.md .github/copilot-instructions.md .clineignore .cursorignore .geminiignore
./verify.ps1
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[OK] Puerta de calidad: verde.` y `codigo=0`.

- [x] **Step 8: Commit**

```powershell
git add quality/adaptadores quality/generar-adaptadores.mjs quality/__tests__/manifiesto.test.mjs CLAUDE.md GEMINI.md .github/copilot-instructions.md .clineignore .cursorignore .geminiignore
git commit -m "feat: manifiesto y generador de adaptadores por harness"
```

---

### Task 9: Gate de adaptadores

**Files:**
- Create: `quality/check-adaptadores.mjs`
- Test: `quality/__tests__/check-adaptadores.test.mjs`
- Modify: `quality/verify.mjs` (agregar el gate a la lista `GATES`)
- Modify: `quality/__tests__/verify.test.mjs` (cubrir la entrada nueva)
- Modify: `docs/ai/PUERTA_CALIDAD.md` (agregar la fila del gate)

**Interfaces:**
- Consumes: `ADAPTADORES` (Tarea 8), `exito`/`fallo` (Tarea 2).
- Produces: `verificarAdaptadores(adaptadores, leer): Array<{ ruta: string, motivo: 'falta' | 'difiere' }>`, donde `leer` es `(ruta: string) => string | null` y devuelve `null` si el archivo no existe.

- [x] **Step 1: Escribir el test que falla**

`quality/__tests__/check-adaptadores.test.mjs`:
```javascript
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { verificarAdaptadores } from '../check-adaptadores.mjs';

const MANIFIESTO = [
  { harness: 'Claude Code', ruta: 'CLAUDE.md', contenido: 'esperado\n' },
  { harness: 'Cursor', ruta: '.cursorignore', contenido: 'bin/\n' },
];

test('sin desvíos cuando todo coincide', () => {
  const leer = (ruta) => MANIFIESTO.find((a) => a.ruta === ruta).contenido;
  assert.deepEqual(verificarAdaptadores(MANIFIESTO, leer), []);
});

test('detecta un adaptador que falta', () => {
  const leer = (ruta) => (ruta === 'CLAUDE.md' ? null : 'bin/\n');
  assert.deepEqual(verificarAdaptadores(MANIFIESTO, leer), [
    { ruta: 'CLAUDE.md', motivo: 'falta' },
  ]);
});

test('detecta un adaptador editado a mano', () => {
  const leer = (ruta) => (ruta === 'CLAUDE.md' ? 'editado a mano\n' : 'bin/\n');
  assert.deepEqual(verificarAdaptadores(MANIFIESTO, leer), [
    { ruta: 'CLAUDE.md', motivo: 'difiere' },
  ]);
});

test('detecta una diferencia de un solo carácter', () => {
  const leer = (ruta) => (ruta === 'CLAUDE.md' ? 'esperado' : 'bin/\n');
  assert.equal(verificarAdaptadores(MANIFIESTO, leer)[0].motivo, 'difiere');
});

test('acumula todos los desvíos, no solo el primero', () => {
  assert.equal(verificarAdaptadores(MANIFIESTO, () => null).length, 2);
});
```

Agregar al final de `quality/__tests__/verify.test.mjs`:
```javascript
test('el gate de adaptadores está en la lista y corre antes que mojibake', () => {
  const nombres = GATES.map((g) => g.nombre);
  assert.ok(nombres.includes('Adaptadores'));
  assert.ok(nombres.indexOf('Adaptadores') < nombres.indexOf('Mojibake'));
});
```

- [x] **Step 2: Ejecutar los tests para verlos fallar**

Run:
```powershell
node --test quality/__tests__/check-adaptadores.test.mjs quality/__tests__/verify.test.mjs
```
Expected: FAIL doble. El primer archivo aborta con `ERR_MODULE_NOT_FOUND` por `../check-adaptadores.mjs`; el segundo falla en el test nuevo porque `GATES` todavía no incluye `'Adaptadores'`.

- [x] **Step 3: Implementar el gate**

`quality/check-adaptadores.mjs`:
```javascript
#!/usr/bin/env node
// Gate de adaptadores: cada archivo generado debe coincidir exactamente con lo
// que el manifiesto declara. Es lo que impide que un adaptador editado a mano
// se convierta en una segunda fuente de instrucciones.

import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { ADAPTADORES } from './adaptadores/manifiesto.mjs';
import { exito, fallo } from './lib/salida.mjs';

export function verificarAdaptadores(adaptadores, leer) {
  const desvios = [];
  for (const { ruta, contenido } of adaptadores) {
    const actual = leer(ruta);
    if (actual === null) desvios.push({ ruta, motivo: 'falta' });
    else if (actual !== contenido) desvios.push({ ruta, motivo: 'difiere' });
  }
  return desvios;
}

if (process.argv[1] && process.argv[1].endsWith('check-adaptadores.mjs')) {
  const raiz = fileURLToPath(new URL('..', import.meta.url));
  const leer = (ruta) => {
    const destino = join(raiz, ruta);
    return existsSync(destino) ? readFileSync(destino, 'utf8') : null;
  };

  const desvios = verificarAdaptadores(ADAPTADORES, leer);

  if (desvios.length > 0) {
    console.log(fallo(`Gate de adaptadores: ${desvios.length} desvío(s)`));
    for (const d of desvios) {
      console.log(`       ${d.ruta}: ${d.motivo === 'falta' ? 'no existe' : 'no coincide con el manifiesto'}`);
    }
    console.log('       Corré: node quality/generar-adaptadores.mjs');
    console.log('       Si el cambio era deliberado, va en el manifiesto, no en el archivo generado.');
    process.exit(1);
  }

  console.log(exito(`Gate de adaptadores: ${ADAPTADORES.length} archivo(s) al día.`));
}
```

- [x] **Step 4: Registrar el gate en el orquestador**

En `quality/verify.mjs`, dentro de `GATES`, insertar la entrada nueva **entre** `'Tests de la puerta'` y `'Mojibake'`, de modo que la lista quede así:
```javascript
export const GATES = [
  // Va primero: si la propia puerta está rota, el resto de los veredictos no
  // vale nada.
  { nombre: 'Tests de la puerta', comando: 'node', args: ['--test', 'quality/__tests__'] },
  { nombre: 'Adaptadores', comando: 'node', args: ['quality/check-adaptadores.mjs'] },
  { nombre: 'Mojibake', comando: 'node', args: ['quality/check-mojibake.mjs'] },
  { nombre: 'Enlaces', comando: 'node', args: ['quality/check-enlaces.mjs'] },
];
```

- [x] **Step 5: Ejecutar los tests para verlos pasar**

Run:
```powershell
node --test quality/__tests__/check-adaptadores.test.mjs quality/__tests__/verify.test.mjs
```
Expected: PASS, `fail 0`.

- [x] **Step 6: Comprobar que el gate detecta una edición manual**

Run:
```powershell
Add-Content -Path CLAUDE.md -Value 'Regla agregada a mano que divergiría del núcleo.'
node quality/check-adaptadores.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[FALLO] Gate de adaptadores: 1 desvío(s)` con `CLAUDE.md: no coincide con el manifiesto` y `codigo=1`.

Restaurar con el generador, que es justamente el remedio que el gate indica:
```powershell
node quality/generar-adaptadores.mjs
node quality/check-adaptadores.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: el generador informa `1 escrito(s)`, el gate vuelve a `[OK]` y `codigo=0`.

- [x] **Step 7: Documentar el gate**

En `docs/ai/PUERTA_CALIDAD.md`, en la tabla «Gates vigentes», insertar esta fila justo después de la de «Tests de la puerta»:

```markdown
| Adaptadores | Que cada archivo generado (`CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, los `.*ignore`) coincida con el manifiesto | `node quality/generar-adaptadores.mjs`; si el cambio era deliberado, va en `quality/adaptadores/manifiesto.mjs` |
```

- [x] **Step 8: Ejecutar la puerta completa**

Run:
```powershell
git add quality docs/ai/PUERTA_CALIDAD.md
./verify.ps1
Write-Output "codigo=$LASTEXITCODE"
```
Expected: los **cuatro** títulos de gate, cuatro líneas `[OK]`, `[OK] Puerta de calidad: verde.` y `codigo=0`.

- [x] **Step 9: Commit**

```powershell
git add quality/check-adaptadores.mjs quality/__tests__/check-adaptadores.test.mjs quality/verify.mjs quality/__tests__/verify.test.mjs docs/ai/PUERTA_CALIDAD.md
git commit -m "feat: gate de adaptadores contra el manifiesto"
```

---

### Task 10: Glosario de dominio

**Files:**
- Create: `docs/dominio/glosario-avicola.md`

**Interfaces:**
- Consumes: nada. `AGENTS.md` ya lo referencia (Tarea 7), así que esta tarea cierra ese puntero.
- Produces: el vocabulario y las reglas de negocio que los subproyectos 2 y 5+ usan para nombrar entidades.

**Origen:** son las reglas rescatadas de `.github/copilot-instructions.md` de ICARUS antes de descartarlo. Se rescata el conocimiento del negocio; las convenciones de estilo de aquel archivo se descartan porque son incompatibles con esta puerta —notablemente la prohibición de compilar y el logging obligatorio de valores de variables, que choca con el anti-PII en un dominio con datos biométricos.

- [x] **Step 1: Crear el glosario**

`docs/dominio/glosario-avicola.md`:
```markdown
# Glosario del dominio avícola

Vocabulario y reglas de negocio de Trajano-Icarus. Es conocimiento del negocio,
no convención de estilo: las convenciones viven en `AGENTS.md` y en la puerta de
calidad.

Consultar este documento **antes de nombrar una entidad o inventar una regla**.
Los identificadores de dominio van en español, igual que el resto del proyecto.

## Módulos

| Módulo | Alcance |
|---|---|
| Control de acceso | Trabajadores, zonas, registros biométricos, entradas y salidas |
| Gestión avícola | Granjas, galpones, producción de huevos, mortalidad, vacunación, alimentación, despachos, precios |

## Unidades

| Término | Definición |
|---|---|
| Maple | Unidad estándar de empaque de huevos. **Un maple son 30 huevos.** |
| Unidades incompletas | Huevos sueltos que no completan un maple. Siempre menos de 30. |

Cálculo del total, sin excepciones:

```
Total Huevos = (CantidadMaples * 30) + UnidadesIncompletas
```

La constante 30 pertenece al dominio y se declara una sola vez. Nunca repetirla
como número suelto en el código.

## Reglas transversales

1. **Soft delete en todas las entidades.** Nunca se hace un borrado físico: se
   marca `EstaActivo = false`. Las consultas normales filtran por `EstaActivo`.
   El motivo es trazabilidad: registros de acceso y de producción no se borran.
2. **Ninguna fecha del dominio admite futuro.** Una producción, una mortalidad,
   una vacunación o un registro de acceso ocurren en el pasado o en el presente.
   La validación es de dominio, no de interfaz.
3. **Los datos biométricos y los registros nominales de acceso son sensibles.**
   Nunca aparecen en logs, mensajes de error ni trazas. Ver la regla anti-PII en
   `AGENTS.md`.

## Pendiente

Las entidades, sus atributos y las relaciones entre módulos se definen al migrar
cada bounded context, en los subproyectos 5 y siguientes. Este documento se
amplía ahí; no se anticipa acá.
```

- [x] **Step 2: Verificar con la puerta**

Run:
```powershell
git add docs/dominio
./verify.ps1
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[OK] Puerta de calidad: verde.` y `codigo=0`.

- [x] **Step 3: Commit**

```powershell
git add docs/dominio
git commit -m "docs: glosario del dominio avícola rescatado de ICARUS"
```

---

### Task 11: Hook de Claude Code para regenerar adaptadores

**Files:**
- Create: `.claude/hooks/regenerar-adaptadores.mjs`
- Create: `.claude/settings.json`

**Interfaces:**
- Consumes: el evento `PostToolUse` que Claude Code entrega por stdin como JSON, con la ruta editada en `tool_input.file_path`; y `quality/generar-adaptadores.mjs` (Tarea 8).
- Produces: regeneración automática de los adaptadores cada vez que se edita `AGENTS.md`, de modo que el gate de la Tarea 9 no falle por olvido.

**Nota:** el comando usa `$CLAUDE_PROJECT_DIR`, nunca una ruta absoluta de usuario. Ese es uno de los defectos de Caserito que este diseño corrige.

- [x] **Step 1: Crear el script del hook**

`.claude/hooks/regenerar-adaptadores.mjs`:
```javascript
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
  evento = JSON.parse(crudo);
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
```

- [x] **Step 2: Crear `.claude/settings.json`**

Si el archivo ya existe, fusionar la clave `hooks` sin borrar el resto.

`.claude/settings.json`:
```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "node \"$CLAUDE_PROJECT_DIR/.claude/hooks/regenerar-adaptadores.mjs\""
          }
        ]
      }
    ]
  }
}
```

- [x] **Step 3: Verificar que el JSON es válido**

Run:
```powershell
node -e "JSON.parse(require('fs').readFileSync('.claude/settings.json','utf8')); console.log('json-ok')"
```
Expected: `json-ok`.

- [x] **Step 4: Verificar el hook alimentándolo con un evento simulado**

Primero, un evento que **no** debe disparar nada:
```powershell
'{"tool_input":{"file_path":"docs/ai/README.md"}}' | node .claude/hooks/regenerar-adaptadores.mjs
Write-Output "codigo=$LASTEXITCODE"
git status --short
```
Expected: `codigo=0` y ningún cambio nuevo en `git status --short`.

Ahora un evento que sí debe disparar la regeneración, con un adaptador roto a propósito:
```powershell
Add-Content -Path GEMINI.md -Value 'divergencia introducida a mano'
'{"tool_input":{"file_path":"AGENTS.md"}}' | node .claude/hooks/regenerar-adaptadores.mjs
node quality/check-adaptadores.mjs
Write-Output "codigo=$LASTEXITCODE"
```
Expected: el hook regenera `GEMINI.md`, el gate informa `[OK] Gate de adaptadores: 6 archivo(s) al día.` y `codigo=0`.

Si el hook no restauró el archivo, comprobar que `CLAUDE_PROJECT_DIR` está definido; fuera de una sesión de Claude Code el script cae en `process.cwd()`, así que el comando debe ejecutarse desde la raíz del repositorio.

- [x] **Step 5: Ejecutar la puerta completa**

Run:
```powershell
git add .claude
./verify.ps1
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[OK] Puerta de calidad: verde.` y `codigo=0`. Nótese que `.claude/settings.local.json` está en `.gitignore`: solo se versiona `settings.json`.

- [x] **Step 6: Commit**

```powershell
git add .claude/settings.json .claude/hooks/regenerar-adaptadores.mjs
git commit -m "chore: hook de Claude Code que regenera los adaptadores al editar AGENTS.md"
```

---

### Task 12: Integración continua

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `quality/verify.mjs` (Tareas 5 y 9).
- Produces: el único job `calidad`. Los subproyectos 2, 3 y 4 le agregan jobs de backend, frontend, contrato y despliegue.

**Decisiones que el workflow refleja:**
- Solo trigger `push` sobre `develop` y `master`. Sin `pull_request`: no hay flujo de PR, y agregarlo después es trivial.
- `fetch-depth: 0`: los gates futuros necesitan historial para calcular diffs.
- Las actions se pinnean **por SHA, no por tag**: un tag es mutable, un SHA no.

- [x] **Step 1: Crear el workflow**

`.github/workflows/ci.yml`:
```yaml
name: CI

on:
  push:
    branches: [ develop, master ]

jobs:
  calidad:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
        with:
          fetch-depth: 0   # los gates futuros necesitan historial para el diff

      - name: Setup Node
        uses: actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020 # v4
        with:
          node-version: 22

      - name: Puerta de calidad
        run: node quality/verify.mjs
```

- [x] **Step 2: Verificar que el YAML es válido y que los mismos comandos pasan en local**

Run:
```powershell
git add .github/workflows/ci.yml
./verify.ps1
Write-Output "codigo=$LASTEXITCODE"
```
Expected: `[OK] Puerta de calidad: verde.` y `codigo=0`. Es exactamente el comando que ejecuta el job.

Comprobación de sintaxis del YAML (opcional pero barata, no requiere dependencias):
```powershell
node -e "const t=require('fs').readFileSync('.github/workflows/ci.yml','utf8'); if(!/^name: CI/m.test(t)||!/jobs:/m.test(t)||t.includes('\t')){throw new Error('YAML sospechoso: revisar name, jobs y tabulaciones')}; console.log('yaml-ok')"
```
Expected: `yaml-ok`. Un tabulador en YAML es un error de sintaxis; esta comprobación lo descarta.

- [x] **Step 3: Commit**

```powershell
git add .github/workflows/ci.yml
git commit -m "ci: workflow de GitHub Actions con el job de calidad"
```

- [x] **Step 4: Push y confirmación del CI en verde**

Run:
```powershell
git push origin develop
```

Luego, comprobar la conclusión del run. Con `gh` disponible:
```powershell
gh run list --branch develop --limit 1
```
Expected: el run más reciente del workflow `CI` figura con `completed` y `success`.

Sin `gh`, abrir la pestaña Actions de `github.com/luicahleo/trajano-icarus` y confirmar que el job `calidad` terminó en verde.

**Si el run falla**, no relajar el gate: leer el log del paso «Puerta de calidad», reproducir el fallo en local con `./verify.ps1`, corregir el contenido y volver a hacer push. La causa más probable de una divergencia local/CI es un archivo con final de línea inconsistente que `.gitattributes` normaliza al hacer checkout en Linux.

---

## Verificación end-to-end (al terminar todas las tareas)

Estos siete puntos son la condición de aceptación que fija el spec de diseño.
Ejecutarlos **desde un clone limpio**, para que ningún archivo no versionado
enmascare un fallo:

```powershell
git clone https://github.com/luicahleo/trajano-icarus.git C:\Users\LRCAHU~1\AppData\Local\Temp\trajano-verificacion
cd C:\Users\LRCAHU~1\AppData\Local\Temp\trajano-verificacion
```

1. **La puerta pasa por sus dos puntos de entrada.**
   ```powershell
   ./verify.ps1
   Write-Output "codigo=$LASTEXITCODE"
   bash ./verify.sh
   Write-Output "codigo=$LASTEXITCODE"
   ```
   Expected: cuatro gates en `[OK]`, `[OK] Puerta de calidad: verde.` y `codigo=0` en ambos.

2. **Un adaptador editado a mano rompe la puerta.**
   ```powershell
   Add-Content -Path CLAUDE.md -Value 'edicion manual'
   ./verify.ps1
   Write-Output "codigo=$LASTEXITCODE"
   node quality/generar-adaptadores.mjs
   ```
   Expected: falla en el gate `Adaptadores` señalando `CLAUDE.md`, `codigo=1`, y el generador lo restaura.

3. **Un mojibake en un `.md` rompe la puerta.**
   ```powershell
   Add-Content -Path AGENTS.md -Value ("a" + [char]0x00C3 + [char]0x00B1 + "o")
   ./verify.ps1
   Write-Output "codigo=$LASTEXITCODE"
   git checkout -- AGENTS.md
   ```
   Expected: falla en el gate `Mojibake` con archivo y número de línea, `codigo=1`.

4. **Un enlace relativo roto rompe la puerta.**
   ```powershell
   Add-Content -Path docs/ai/README.md -Value 'Prueba: [roto](NO-EXISTE.md)'
   ./verify.ps1
   Write-Output "codigo=$LASTEXITCODE"
   git checkout -- docs/ai/README.md
   ```
   Expected: falla en el gate `Enlaces` señalando `docs/ai/README.md`, `codigo=1`.

5. **El generador es idempotente.**
   ```powershell
   node quality/generar-adaptadores.mjs
   git status --porcelain
   ```
   Expected: `0 escrito(s), 6 sin cambios.` y `git status --porcelain` sin ninguna línea.

6. **El CI está en verde sobre `develop`.** Confirmado en la Tarea 12, Step 4.

7. **Los cinco agentes reciben el mismo núcleo.** Comprobación documental, archivo por archivo:
   ```powershell
   Get-Content CLAUDE.md
   Get-Content GEMINI.md
   Get-Content .github/copilot-instructions.md
   ```
   Expected: `CLAUDE.md` contiene `@AGENTS.md`; `GEMINI.md` contiene `@./AGENTS.md`; el archivo de Copilot apunta a `AGENTS.md` en texto. Codex y Kimi CLI no necesitan archivo: descubren `AGENTS.md` de forma nativa y jerárquica, lo que se comprueba abriendo el repositorio con cada uno y pidiéndole que resuma sus instrucciones de proyecto.

Limpieza:
```powershell
cd C:\Users\lrcahuana\source\repos\Trajano-Icarus
Remove-Item -Recurse -Force C:\Users\LRCAHU~1\AppData\Local\Temp\trajano-verificacion
```

## Desviaciones detectadas al ejecutar (2026-08-12)

### 1. Patrón PCRE del gate de mojibake (Tarea 3, Step 3)

- **Qué decía el plan:** pasar el patrón `\xC3\x83` a `git grep -P`, tratándolo como una secuencia de bytes.
- **Por qué falló:** el PCRE2 de este Git trabaja en modo UTF-8. Por eso, `\xC3` denota U+00C3 (bytes `c3 83`) y `\x83` denota U+0083 (bytes `c2 83`); el patrón resultante buscaba `c3 83 c2 83` y no coincidía con el mojibake real. El gate podía dar verde aunque el índice contuviera texto corrupto.
- **Qué se implementó:** la constante `PATRON_PCRE` de `quality/check-mojibake.mjs` usa `\x{FFFD}|\x{00C3}|\x{00C2}`. En modo UTF identifica los caracteres inequívocos que delatan mojibake sin marcar las vocales acentuadas correctas del español.

### 2. Descubrimiento de tests en Node 24 (Tareas 2 y 5)

- **Qué decía el plan:** ejecutar `node --test quality/__tests__`.
- **Por qué falló:** en Node 24 los argumentos posicionales de `--test` son patrones glob; el directorio se intentaba cargar como módulo y el proceso abortaba con `MODULE_NOT_FOUND`.
- **Qué se implementó:** la constante `PATRON_TESTS` de `quality/verify.mjs` contiene el glob `quality/__tests__/*.test.mjs`, verificado en Windows con shell y en el CI Linux sin shell.

### 3. Lectura del stdin en el hook de Claude (Tarea 11, Step 1)

- **Qué decía el plan:** aplicar `JSON.parse` directamente al texto recibido por stdin.
- **Por qué falló:** PowerShell 5.1 antepone un BOM al stdin de una tubería; `JSON.parse` lanzaba una excepción y el `catch` terminaba silenciosamente con código 0, sin regenerar nada.
- **Qué se implementó:** `.claude/hooks/regenerar-adaptadores.mjs` aplica `.trim()` antes de `JSON.parse`, eliminando el BOM y el espacio periférico antes de interpretar el JSON.

### 4. Inyección de mojibake en la verificación end-to-end (punto 3)

- **Qué decía el plan:** usar `Add-Content` con caracteres no ASCII para agregar el caso corrupto a `AGENTS.md`.
- **Por qué falló:** en PowerShell 5.1, `Add-Content` escribe en ANSI y depositaría un byte `0xC3` aislado, no la secuencia UTF-8 de un mojibake real; por tanto, la prueba no reproducía el defecto que debía detectar.
- **Qué se implementó:** la prueba debe agregar el texto con `[System.IO.File]::AppendAllText`, pasando `(New-Object System.Text.UTF8Encoding($false))`, para escribir UTF-8 sin BOM y reproducir la secuencia real.

## Lo que este plan deja fuera a propósito

Solución .NET y bounded contexts; `Icarus/AGENTS.md`; `.editorconfig` de C#,
`Directory.Build.props`, `Directory.Packages.props`, `global.json`; tests de
arquitectura; frontend React y `web/AGENTS.md`; contenedorización; `deploy.yml` y
el environment de producción; gates de cobertura, mutación, complejidad y
contrato; hook de formato de C#; y skills de scaffolding.

Cada uno pertenece a un subproyecto posterior, y ninguno puede existir antes de
que haya código que medir.
