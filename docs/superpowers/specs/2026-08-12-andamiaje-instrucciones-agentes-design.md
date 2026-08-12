# Diseño: andamiaje de instrucciones para agentes — Trajano-Icarus

- **Fecha**: 2026-08-12
- **Estado**: Aprobado (brainstorming)
- **Alcance**: la capa de instrucciones para agentes y una puerta de calidad mínima que
  se valida a sí misma. NO decide la solución .NET, los bounded contexts, el frontend,
  la contenedorización ni la migración de la lógica de ICARUS.

## Contexto

Trajano-Icarus (`github.com/luicahleo/trajano-icarus`) es la refactorización de ICARUS
(`repos/dev/ICARUS`) tomando como modelo la estructura de Caserito
(`repos/dev_Caserito`): backend .NET por bounded contexts, frontend React y la misma
gobernanza de agentes.

El repositorio es greenfield: dos commits (`facd27c`), rama `develop` como default y
`master` reservada a producción. No hay código, ni solución, ni instrucciones.

El objetivo declarado es que **las instrucciones sean homogéneas** entre Codex, Kimi,
DeepSeek y Claude, porque el desarrollo va a alternar entre ellos. Hay un solo
desarrollador, así que no hay revisión humana de código: la homogeneidad y la calidad
las tiene que sostener un mecanismo verificable, no la disciplina de cada sesión.

### Precedente y por qué no se copia tal cual

Caserito llegó a su estructura actual en unos cinco specs a lo largo de un mes: el
andamiaje (`2026-07-13-andamiaje-homogeneidad`) no incluía ni `docs/ai/` ni la puerta de
calidad, que aparecieron 25 días después (`2026-08-07-puerta-calidad`).

Trajano-Icarus tiene la ventaja de conocer el destino, así que la capa de instrucciones
nace ya en su forma madura. Pero el orden de dependencias sigue siendo real: los gates de
cobertura y mutación no pueden existir antes de que haya código que medir, y el gate de
contrato no puede existir antes de que haya OpenAPI y cliente generado.

### Descomposición acordada

| # | Subproyecto | Depende de |
|---|---|---|
| **1** | **Instrucciones para agentes + puerta mínima** ← este spec | — |
| 2 | Fundaciones .NET + gates de backend | 1 |
| 3 | Frontend React + gates web + gate de contrato | 2 |
| 4 | Contenedorización y despliegue | 3 |
| 5+ | Migración de la lógica de ICARUS, un spec por bounded context | 4 |

Este subproyecto es el único que no necesita una línea de código.

### Por qué la puerta mínima va acá y no en el subproyecto 2

Un `AGENTS.md` que ordena «ejecutá `./verify.ps1` antes de cada commit» junto a un
`verify.ps1` inexistente es una instrucción que miente durante todo el intervalo entre
ambos specs, y una instrucción que miente entrena al agente a ignorar las demás. La
puerta nace con tres gates que validan lo único que existe en esta etapa —los propios
documentos— y crece en los subproyectos siguientes.

## Decisiones tomadas

| Decisión | Valor |
|---|---|
| Fuente de instrucciones | `AGENTS.md` es la única fuente; todo lo demás son adaptadores generados |
| Ramas | `develop` default y de trabajo, `master` producción, sin pull requests |
| Puerta inicial | tres gates: adaptadores, mojibake, enlaces; más los tests de la propia puerta |
| Nombre del backend | directorio `Icarus/`, proyectos `Icarus.*` — decisión anticipada que este spec registra pero no implementa; se aplica en el subproyecto 2 |
| `copilot-instructions.md` de ICARUS | se abandona; sus reglas de negocio se rescatan al glosario de dominio |
| Idioma | documentos, textos e identificadores de dominio en español correcto, UTF-8 sin mojibake |

## Estructura de directorios

```
trajano-icarus/                        ← raíz git
├─ AGENTS.md                           ← única fuente de instrucciones
├─ CLAUDE.md                           ← adaptador generado
├─ GEMINI.md                           ← adaptador generado
├─ .github/
│  ├─ copilot-instructions.md          ← adaptador generado
│  └─ workflows/ci.yml                 ← solo el job `calidad`
├─ .claude/settings.json               ← hooks, rutas relativas
├─ .clineignore · .cursorignore · .geminiignore   ← generados
├─ docs/
│  ├─ ai/
│  │  ├─ README.md
│  │  ├─ WORKFLOW.md
│  │  ├─ PUERTA_CALIDAD.md
│  │  ├─ ECONOMIA_TOKENS.md
│  │  ├─ CONTEXT-EFFICIENCY.md
│  │  ├─ FLUJO_GIT.md
│  │  └─ HANDOFF.template.md
│  ├─ dominio/glosario-avicola.md
│  └─ superpowers/{specs,plans}/
├─ quality/
│  ├─ verify.mjs                       ← orquestador; corta al primer fallo
│  ├─ adaptadores/manifiesto.mjs       ← tabla harness → archivo → contenido
│  ├─ generar-adaptadores.mjs
│  ├─ check-adaptadores.mjs
│  ├─ check-mojibake.mjs
│  ├─ check-enlaces.mjs
│  ├─ lib/{ejecutar,salida}.mjs
│  └─ __tests__/*.test.mjs
├─ verify.ps1 · verify.sh
├─ .gitignore · .gitattributes
```

No aparecen `Icarus/` ni `web/`: son los subproyectos 2 y 3.

## Capa de instrucciones

### Las tres capas

1. **`AGENTS.md` raíz** — reglas esenciales, cargadas siempre. Corto: solo lo que aplica
   a casi cualquier tarea. Contiene prioridades, anti-PII, ramas, descubrimiento
   eficiente, selección de proceso, proyecto, verificación y puerta de calidad.
2. **`AGENTS.md` por árbol** — reglas locales, solo al trabajar en ese árbol. Se crean en
   los subproyectos 2 (`Icarus/AGENTS.md`) y 3 (`web/AGENTS.md`).
3. **`docs/ai/`** — proceso completo, bajo demanda. No se carga para preguntas ni cambios
   triviales.

Codex y Kimi CLI descubren `AGENTS.md` jerárquicamente, así que las capas 1 y 2 les
funcionan sin adaptador ni configuración.

### Manifiesto de adaptadores

`quality/adaptadores/manifiesto.mjs` exporta la tabla que define, para cada harness, qué
archivo se genera y con qué contenido:

| Harness | Archivo | Mecanismo |
|---|---|---|
| Codex | — | `AGENTS.md` nativo, jerárquico |
| Kimi CLI | — | `AGENTS.md` nativo, jerárquico (`~/.kimi/AGENTS.md` global, el del proyecto sobrescribe) |
| Claude Code | `CLAUDE.md` | `@AGENTS.md` |
| Gemini CLI | `GEMINI.md` | `@./AGENTS.md` |
| Copilot | `.github/copilot-instructions.md` | puntero textual; no soporta import |
| DeepSeek | — | es un modelo, no un harness: hereda el archivo del harness que lo hospeda |

Cada adaptador tiene como máximo cinco líneas y nadie los edita a mano:
`node quality/generar-adaptadores.mjs` los escribe.

Kimi CLI **no** lee `CLAUDE.md` (hay un issue abierto pidiéndolo), lo que confirma que el
núcleo tiene que llamarse `AGENTS.md` y no lo contrario.

Agregar un harness nuevo es una entrada en el manifiesto más una corrida del generador.
La convención de archivo de cada herramienta se verifica contra su documentación en el
momento de agregarla; no se adivina.

Los archivos `.*ignore` por proveedor (`.clineignore`, `.cursorignore`, `.geminiignore`)
salen del mismo manifiesto y del mismo generador, por el mismo motivo: en Caserito son
tres archivos de contenido casi idéntico mantenidos a mano y ya divergiendo.

## Puerta de calidad mínima

`quality/verify.mjs` orquesta los gates en orden y **corta en el primer fallo**, para dar
retroalimentación rápida. `verify.ps1` y `verify.sh` son los puntos de entrada.

| Gate | Qué comprueba | Cómo se arregla un fallo |
|---|---|---|
| Tests de la puerta | `node --test quality/**/*.test.mjs` | según el mensaje |
| Adaptadores | regenera cada adaptador desde el manifiesto y compara | `node quality/generar-adaptadores.mjs` |
| Mojibake | ausencia de `U+FFFD` y de secuencias delatoras (`Ã±`, `Ã©`, `Â`) en todo archivo versionado que git clasifique como texto (`git grep` respeta esa clasificación) | escribir el carácter correcto en UTF-8 |
| Enlaces | en los `.md` versionados, todo enlace relativo apunta a un archivo existente; los enlaces absolutos `http(s)` no se comprueban | corregir el enlace o crear el destino |

Racional de cada gate: los tres defienden una regla que `AGENTS.md` afirma y que hoy nada
verifica. El de mojibake está porque Caserito **tiene** mojibake versionado
(`.claude/settings.local.json`) pese a prohibirlo en su `AGENTS.md`: es la clase de regla
que se degrada sin gate. El de enlaces está porque `docs/ai/README.md` es un mapa de
lectura, y un mapa con destinos inexistentes hace que el agente lea de más o de menos.

Los gates se ejecutan sin Docker y sin SDK de .NET, así que la puerta corre en segundos.

### Autoexcepción del gate de mojibake

Los documentos que **describen** el gate necesitan citar las secuencias que detecta, así
que se marcarían a sí mismos. Este spec y `docs/ai/PUERTA_CALIDAD.md` son los dos casos.

La solución no es una lista de archivos exentos, que se convierte en un agujero permanente:
el gate ignora las secuencias que aparezcan **dentro de un span de código** en Markdown
(entre acentos graves). Un mojibake accidental nunca está entre acentos graves; una cita
deliberada del patrón sí. La regla es verificable y no depende de mantener exclusiones.

### Reglas innegociables

Se heredan de Caserito porque son la razón por la que su puerta funciona:

1. Nunca `--no-verify`, ni en commit ni en push.
2. Nunca relajar una baseline, un umbral o una exclusión para que pase el gate. Si el gate
   falla, el problema está en el contenido.
3. Las baselines solo se mueven hacia mejor, en commit propio que explique la mejora.
4. Nunca afirmar verde sin haber ejecutado el comando y visto la salida.

`docs/ai/PUERTA_CALIDAD.md` describe los gates y el procedimiento de baselines **sin
cantidades concretas**: en Caserito ese documento afirma «379 tests», «224 tests» y «23
proyectos», cifras que caducan en cada commit.

## CI

`.github/workflows/ci.yml` con un único job `calidad`, disparado por `push` sobre
`develop` y `master`. Sin trigger `pull_request`: no hay flujo de PR, y es trivial
agregarlo si algún día lo hay.

Pasos: `actions/checkout` con `fetch-depth: 0` (los gates futuros necesitan historial para
el diff), `actions/setup-node`, y `node quality/verify.mjs`. Las actions se pinnean por
SHA, no por tag, como hace Caserito.

El workflow crece con cada subproyecto: jobs de backend en el 2, de frontend y contrato en
el 3, y `deploy.yml` en el 4.

## Flujo git

Un solo desarrollador, sin pull requests.

- `develop` es la rama por defecto y de trabajo: commit y push directo tras `verify`.
- `master` es producción: recibe `develop` por merge fast-forward, solo a pedido explícito.
- La promoción exige `verify` completo en verde y, en el subproyecto 4, el despliegue queda
  detrás de un `workflow_dispatch` con confirmación.

La compuerta de producción del subproyecto 4 **no será un PR**: replicará el mecanismo de
Caserito, que consulta
`ci.yml/runs?head_sha=<sha>&branch=master&event=push` y exige `conclusion == success`. El
filtro `event=push` implica que un CI verde de pull request no sirve; el commit tiene que
estar en `master` con su propio run de push en verde. Por eso el mecanismo funciona sin
PR y se porta sin cambios.

`docs/ai/FLUJO_GIT.md` describe este único flujo. En Caserito ese documento contradice a su
propio `AGENTS.md` —uno manda trabajar directo en `develop`, el otro manda abrir un PR— y
un agente nuevo elige al azar entre los dos.

## Documentos de proceso

`docs/ai/` replica la estructura por capas de Caserito, con estas diferencias:

- **`README.md`** — mapa de las capas y tabla «qué leer según tipo de tarea». Es la pieza
  que más ahorra contexto: dice explícitamente que specs y planes históricos son memoria
  consultable, no contexto obligatorio.
- **`WORKFLOW.md`** — el ciclo preparación → brainstorming → spec → plan → TDD →
  integración → revisión → cierre, con la ceremonia escalada al riesgo.
- **`PUERTA_CALIDAD.md`** — los gates vigentes y las cuatro reglas innegociables, sin
  cifras que caduquen.
- **`ECONOMIA_TOKENS.md`** — niveles de capacidad (alto / intermedio / económico) con la
  tabla de equivalencias por proveedor, sumando DeepSeek, que en Caserito falta.
- **`CONTEXT-EFFICIENCY.md`** — exploración mínima: buscar por término antes de listar,
  leer fragmentos antes que archivos completos.
- **`FLUJO_GIT.md`** — el flujo sin PR descrito arriba.
- **`HANDOFF.template.md`** — plantilla de traspaso entre sesiones, que se borra cuando el
  trabajo cierra para que no se vuelva memoria obsoleta.

## Glosario de dominio

`docs/dominio/glosario-avicola.md` recibe las reglas de negocio rescatadas de
`.github/copilot-instructions.md` de ICARUS antes de descartarlo:

- `Maple` = 30 huevos, unidad estándar.
- `Total Huevos = (CantidadMaples × 30) + UnidadesIncompletas`.
- Soft delete en todas las entidades vía `EstaActivo = false`; nunca hard delete.
- Fechas validadas para no admitir futuro.
- Módulos del dominio: control de acceso (trabajadores, zonas, biométricos) y gestión
  avícola (granjas, galpones, producción de huevos, mortalidad, vacunación, alimentación,
  despachos, precios).

Es conocimiento del negocio, no convención de estilo, y hoy no está escrito en ningún otro
lado. El resto de `copilot-instructions.md` se descarta.

### Por qué se descarta el resto

Las convenciones de ICARUS son incompatibles con esta puerta de calidad. Dos choques son
irreconciliables: «**nunca compiles**, la compilación se hará desde Visual Studio 2022»
impide ejecutar cualquier gate; y su logging obligatorio de valores de variables choca con
el anti-PII, en un dominio que maneja datos biométricos y registros de acceso de
trabajadores. Las demás —`this.` obligatorio, `#region` obligatorio, AutoMapper
obligatorio, prohibición de `var` en tipos built-in, identificadores en inglés— se
reemplazan por el enforcement mecánico del subproyecto 2.

## Correcciones a los defectos observados en Caserito

| Defecto | Corrección en este diseño |
|---|---|
| `AGENTS.md` y `FLUJO_GIT.md` se contradicen sobre ramas | un solo flujo, sin PR, escrito una vez |
| rutas absolutas de usuario en hooks (`C:/Users/lrcahuana/...`) | `$CLAUDE_PROJECT_DIR` y rutas relativas |
| mojibake en archivos versionados | gate `check-mojibake` |
| `.superpowers/sdd/` con diffs de cientos de KB versionados | `.gitignore` real desde el inicio |
| cifras de tests hardcodeadas en `PUERTA_CALIDAD.md` | el documento describe gates, no cantidades |
| `.*ignore` por proveedor divergiendo a mano | generados desde el manifiesto |
| `.agents/` vacío y abandonado | no se crea |

## Fuera de alcance (explícito)

Solución .NET y bounded contexts; `.editorconfig` de C#, `Directory.Build.props`,
`Directory.Packages.props`, `global.json`; tests de arquitectura; frontend React;
contenedorización; `deploy.yml` y el environment `production`; gates de cobertura,
mutación y complejidad; hook de formato de C#; skills de scaffolding; y la migración de la
lógica de ICARUS.

Cada uno pertenece a un subproyecto posterior.

## Verificación

El andamiaje se considera correcto cuando, desde un clone limpio:

1. `./verify.ps1` y `./verify.sh` pasan en verde.
2. Editar un adaptador a mano y correr `verify` falla mostrando el diff.
3. Introducir `Ã±` en cualquier `.md` y correr `verify` falla señalando archivo y línea.
4. Romper un enlace relativo en `docs/ai/README.md` y correr `verify` falla.
5. `node quality/generar-adaptadores.mjs` deja el árbol limpio si ya estaba correcto
   (idempotente).
6. El push a `develop` dispara `ci.yml` y completa en verde.
7. Los cuatro agentes reciben el mismo núcleo: Codex y Kimi por descubrimiento jerárquico,
   Claude y Gemini por import, Copilot por puntero.
