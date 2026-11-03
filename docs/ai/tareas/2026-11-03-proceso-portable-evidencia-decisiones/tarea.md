# Proceso portable: criterio con evidencia, registro de decisiones y revisor común

## Pedido

Llevar al proceso de agentes tres ideas del curso «Ingeniería de Software con
Agentes de IA»: cada criterio de aceptación declara la evidencia que lo demuestra,
las decisiones transversales se registran con su motivo y su condición de
reconsideración, y la revisión usa un checklist común además del de dominio.
Además, sacar la lista de gates de `quality/verify.mjs` a una configuración del
proyecto y corregir su voseo.

Trajano-Icarus es el piloto de un kit de proceso que se replicará en otros
proyectos con la misma base. Cada cambio separa visiblemente lo genérico
(portable) de lo específico de este proyecto, sin extraer todavía el kit.

- Spec: [`docs/superpowers/specs/2026-11-03-proceso-portable-evidencia-decisiones-design.md`](../../../superpowers/specs/2026-11-03-proceso-portable-evidencia-decisiones-design.md)
- Plan: [`docs/superpowers/plans/2026-11-03-proceso-portable-evidencia-decisiones.md`](../../../superpowers/plans/2026-11-03-proceso-portable-evidencia-decisiones.md)

## Alcance

- **Criterio con evidencia.** Formato de `tarea.md` con una línea `Evidencia:` por
  criterio (`test` con ruta y nombre, o `manual` con justificación), documentado en
  `docs/ai/tareas/README.md` y anunciado en la plantilla de `crear`. Lógica pura en
  `quality/lib/criterios.mjs`. El gate `Tareas` exige el formato desde
  `DESARROLLO` hasta `DOCUMENTACION`, y desde `VERIFICACION` además que cada prueba
  citada exista con su nombre y que cada criterio con prueba tenga su rojo
  registrado. La CLI niega `PLANIFICACION → DESARROLLO` sin criterios válidos.
  Las tareas cerradas no se revisan.
- **Registro de decisiones.** `docs/decisiones/` con `README.md` y `plantilla.md`
  portables y los registros 001 a 008 de este proyecto: 001 piloto del kit, 002
  errores por excepciones de dominio y 003 a 008 migradas de
  `docs/proyecto/arquitectura.md` §9. Motivo `PENDIENTE` salvo en 001.
- **Revisión.** `docs/ai/revision/comun.md` (portable) y
  `docs/ai/revision/dominio.md` (Trajano-Icarus); revisión ligera y profunda según
  la decisión 1.
- **Roles.** Planificador, desarrollador, verificador, revisor, orquestador y
  operaciones; adaptadores regenerados.
- **Gates configurables.** `quality/gates.json` con los diez gates vigentes;
  ejecutor genérico en `quality/verify.mjs` y `quality/lib/gates.mjs`, que falla
  cerrado ante una configuración ausente o inválida; prueba que congela la lista.
- **Voseo.** `quality/verify.mjs:68` y, según la decisión 3,
  `quality/check-adaptadores.mjs:36`, `quality/check-enlaces.mjs:90`,
  `quality/check-mojibake.mjs:84` y `docs/ai/README.md:26`.
- **Inventario.** `docs/ai/KIT_PROCESO.md`: qué es portable y qué es propio.
- **Documentación de proceso:** `AGENTS.md`, `docs/ai/README.md`,
  `docs/ai/WORKFLOW.md`, `docs/ai/PUERTA_CALIDAD.md`,
  `docs/ai/agentes/PROTOCOLO.md`, `docs/ai/agentes/README.md` y
  `docs/ai/tareas/README.md`.
- **Documentación de producto:** `docs/proyecto/arquitectura.md` §9 (enlaza los
  registros) y `docs/proyecto/operacion-calidad.md` §6 (lista de gates). Glosario:
  ninguno.
- **Decisiones nuevas a registrar:** las ocho de `docs/decisiones/` descritas
  arriba.

## Fuera de alcance

- Traer los gates de dev_Caserito (TDD, formato, complejidad, cobertura, Stryker):
  bloque 2.
- Extraer el kit a un repositorio propio o tocar otros repositorios: bloque 3.
- Migrar a Result pattern, feature flags o cualquier cambio de arquitectura del
  backend.
- Un gate que valide el formato de `docs/decisiones/`.
- Generalizar la lista `MODULOS` de `quality/lib/fichas.mjs`: queda anotada en el
  inventario como pieza específica.
- Reescribir los `tarea.md` de las tareas cerradas.
- Completar los motivos `PENDIENTE`: los pone el humano cuando quiera.

## Criterios de aceptación

Se comprueban en una terminal en la raíz del repositorio, sobre el commit en
prueba manual, y leyendo los documentos citados.

1. `node quality/tarea.mjs crear` deja en `tarea.md`, bajo «Criterios de
   aceptación», la indicación de que cada criterio lleva una línea «Evidencia:» y
   remite a `docs/ai/tareas/README.md`, que explica los dos tipos, el registro del
   rojo y qué comprueban la CLI y el gate.
   Evidencia: test `quality/__tests__/tarea-cli.test.mjs` «la plantilla de crear indica el formato de evidencia de los criterios»
   Evidencia: manual — la explicación del README es texto de proceso y se valida leyéndola.
2. Solo cuentan como criterios los numerados de la sección «Criterios de
   aceptación»; cada uno recoge sus líneas `Evidencia:` de tipo `test` o `manual`,
   y un encabezado cierra el criterio anterior.
   Evidencia: test `quality/__tests__/criterios.test.mjs` «lee los criterios numerados y sus evidencias de prueba y manuales»
   Evidencia: test `quality/__tests__/criterios.test.mjs` «un encabezado cierra el criterio anterior»
3. Son problemas un criterio sin «Evidencia:», una evidencia con formato
   desconocido (por ejemplo «dotnet test pasa»), una evidencia manual con menos de
   10 caracteres de justificación y un número de criterio repetido.
   Evidencia: test `quality/__tests__/criterios.test.mjs` «un criterio sin línea de evidencia es un problema»
   Evidencia: test `quality/__tests__/criterios.test.mjs` «una evidencia con formato desconocido es un problema»
   Evidencia: test `quality/__tests__/criterios.test.mjs` «una evidencia manual sin justificación suficiente es un problema»
   Evidencia: test `quality/__tests__/criterios.test.mjs` «un número de criterio repetido es un problema»
4. Con una tarea en `DESARROLLO` cuyo criterio no tiene evidencia,
   `node quality/check-tareas.mjs` falla y nombra la tarea y el número del criterio.
   Evidencia: test `quality/__tests__/check-tareas.test.mjs` «en DESARROLLO un criterio sin evidencia falla el gate»
5. Con una tarea en `VERIFICACION`, el gate falla si una prueba citada no existe,
   si su archivo no contiene el nombre o si falta el evento `comando` con
   «Rojo criterio N:»; con la prueba presente y el rojo registrado, pasa.
   Evidencia: test `quality/__tests__/criterios.test.mjs` «una prueba citada debe existir y contener el nombre»
   Evidencia: test `quality/__tests__/criterios.test.mjs` «un criterio con prueba necesita su rojo registrado»
   Evidencia: test `quality/__tests__/criterios.test.mjs` «solo cuentan como rojo los eventos comando con el prefijo exacto»
   Evidencia: test `quality/__tests__/check-tareas.test.mjs` «en VERIFICACION una evidencia que apunta a una prueba inexistente falla el gate»
   Evidencia: test `quality/__tests__/check-tareas.test.mjs` «en VERIFICACION un criterio con prueba sin rojo registrado falla el gate»
   Evidencia: test `quality/__tests__/check-tareas.test.mjs` «en VERIFICACION un criterio con prueba existente y rojo registrado pasa»
6. Las tareas cerradas y las que están en planificación no se revisan por
   criterios: `node quality/check-tareas.mjs` sigue en verde sobre el repositorio
   real sin que cambie ningún `tarea.md` de las cuatro tareas cerradas.
   Evidencia: test `quality/__tests__/check-tareas.test.mjs` «las tareas cerradas no se revisan por criterios»
   Evidencia: test `quality/__tests__/check-tareas.test.mjs` «una tarea en planificación con criterios incompletos no falla el gate»
   Evidencia: manual — ejecutar el gate sobre el repositorio real y comprobar con `git diff --stat -- docs/ai/tareas` que los cuatro `tarea.md` cerrados no cambiaron.
7. `node quality/tarea.mjs avanzar <id> DESARROLLO` desde `PLANIFICACION` se
   niega sin criterios («No hay criterios de aceptación») o con un criterio sin
   evidencia (nombra el criterio), y avanza cuando todos tienen evidencia válida.
   Evidencia: test `quality/__tests__/tareas.test.mjs` «PLANIFICACION a DESARROLLO exige criterios comprobados y sin problemas»
   Evidencia: test `quality/__tests__/tarea-cli.test.mjs` «avanzar de PLANIFICACION a DESARROLLO exige criterios con evidencia»
8. `quality/gates.json` declara los mismos diez gates de hoy, en el mismo orden y
   con los mismos comandos y argumentos; `quality/__tests__/verify.test.mjs` no
   cambia y sigue en verde.
   Evidencia: test `quality/__tests__/gates-proyecto.test.mjs` «la configuración del proyecto declara los diez gates vigentes en orden y con sus argumentos»
   Evidencia: manual — `git diff --stat -- quality/__tests__/verify.test.mjs` no muestra cambios; es la comprobación de que no se adaptó una prueba existente.
9. `quality/verify.mjs` y `quality/lib/gates.mjs` no nombran rutas ni productos de
   este proyecto; un gate `npm` se resuelve con el CLI de npm lanzado por node en
   Windows y con `npm` en POSIX (el ejecutor solo aplica shell en win32), y los gates `node` y `dotnet` con su
   comando y sus argumentos.
   Evidencia: test `quality/__tests__/gates.test.mjs` «el ejecutor no nombra rutas de este proyecto»
   Evidencia: test `quality/__tests__/gates.test.mjs` «un gate npm usa el CLI de npm con node en Windows y npm en POSIX (el ejecutor solo aplica shell en win32)»
   Evidencia: test `quality/__tests__/gates.test.mjs` «un gate node o dotnet se resuelve con su comando y sus argumentos»
10. Una configuración ausente, que no es JSON, vacía o con un gate inválido (tipo
    desconocido, sin argumentos, nombre repetido, clave no admitida, `cwd` absoluto
    o con `..`) hace fallar la puerta con un motivo por problema y sin ejecutar
    ningún gate.
    Evidencia: test `quality/__tests__/gates.test.mjs` «una configuración inválida se rechaza con un motivo por cada problema»
    Evidencia: test `quality/__tests__/gates.test.mjs` «un cwd absoluto o con .. se rechaza»
    Evidencia: test `quality/__tests__/gates.test.mjs` «cargarGates falla cerrado si el archivo falta o no es JSON»
    Evidencia: test `quality/__tests__/gates.test.mjs` «sin gates la puerta no da verde»
    Evidencia: manual — de extremo a extremo: cambiar temporalmente un «tipo» en `quality/gates.json` a «bash», ejecutar `node quality/verify.mjs`, ver el motivo, el código 1 y ningún gate ejecutado, y restaurar el archivo.
11. Los mensajes de la puerta están en español neutro: al cambiar temporalmente un
    argumento de un gate en `quality/gates.json`, `node quality/verify.mjs` falla en
    «Tests de la puerta» (la prueba que congela la lista) y termina con «Arregla el
    contenido, no el gate.»; `git grep -nE "Arreglá|Corré|Corregí|creá|Escribí|necesitás|ponelo|conocés"`
    no devuelve nada fuera de `docs/superpowers/` y `docs/ai/tareas/`.
    Evidencia: manual — es la salida de consola del proceso real y una búsqueda en el árbol; se restaura el archivo después.
12. `docs/decisiones/README.md` explica formato, quién escribe el motivo,
    inmutabilidad, sustitución y lectura obligatoria; `plantilla.md` tiene los seis
    apartados; existen los registros 001 a 008, cada uno con los seis apartados;
    001 trae el motivo dado por el usuario y 002 a 008 dicen `PENDIENTE`;
    `docs/proyecto/arquitectura.md` §9 enlaza los registros en lugar de listarlos.
    Evidencia: manual — son documentos sin gate de formato (fuera de alcance); se comprueban leyéndolos, y el gate Enlaces cubre los enlaces.
13. Los roles planificador, desarrollador y revisor exigen leer las decisiones
    aceptadas y pasar a `ESPERA_DECISION` ante una contradicción o una condición de
    reconsideración cumplida; planificador, desarrollador y verificador aplican la
    evidencia por criterio; los adaptadores de `.claude/agents/` y
    `.kimi-code/agents/` están regenerados.
    Evidencia: manual — es texto de los roles y se compara con la spec; el gate Adaptadores comprueba la regeneración.
14. `docs/ai/revision/comun.md` cubre criterios y evidencia, pruebas vacías o
    engañosas (aserción débil, prueba del doble, fórmula duplicada, `try`/`catch`
    que silencia, nombre engañoso, prueba omitida), pruebas existentes modificadas,
    lifetime de cada registro de DI, propagación de `CancellationToken`,
    prohibición de `.Result`, `.Wait()` y `!` para silenciar nulables, seguridad
    básica y puerta; `dominio.md` cubre tenant, autorización, anti-PII,
    migraciones, glosario, máquinas de estados y GestorCaisy, y lista las zonas que
    exigen revisión profunda; el rol revisor aplica las dos profundidades.
    Evidencia: manual — es texto de proceso; se comprueba leyéndolo contra esta lista.
15. `docs/ai/KIT_PROCESO.md` separa en una tabla lo portable de lo específico de
    cada pieza tocada, y cada documento nuevo declara al principio si es portable
    o propio de Trajano-Icarus (en los registros de decisiones, con su campo
    «Alcance»).
    Evidencia: manual — es una clasificación de documentos que se valida leyéndola.
16. `AGENTS.md` pide leer las decisiones aceptadas antes de una feature o bloque;
    `docs/ai/README.md` enlaza decisiones, revisión e inventario;
    `PROTOCOLO.md`, `WORKFLOW.md`, `PUERTA_CALIDAD.md` y
    `docs/proyecto/operacion-calidad.md` describen la evidencia, las decisiones, la
    revisión y la lista de gates configurable.
    Evidencia: manual — es documentación de proceso; se comprueba leyéndola, y los gates Adaptadores, Mojibake y Enlaces cubren su forma.
17. La evidencia `test` solo vale si la ruta es un archivo de pruebas reconocible
    (`__tests__/`, `tests/`, `*Tests/`, `*.test.*`, `*.spec.*` o `*Tests.cs`) y no está
    bajo `docs/ai/tareas/`; el nombre citado tiene al menos 8 caracteres.
    Evidencia: test `quality/__tests__/criterios.test.mjs` «una prueba citada debe ser un archivo de pruebas reconocible y fuera de las tareas»
    Evidencia: test `quality/__tests__/criterios.test.mjs` «el nombre de una prueba citada no puede ser trivial»
18. La comprobación de criterios de la CLI también se aplica en
    `ESPERA_DECISION` a `DESARROLLO`, no solo en `PLANIFICACION` a `DESARROLLO`.
    Evidencia: test `quality/__tests__/tareas.test.mjs` «ESPERA_DECISION a DESARROLLO exige criterios comprobados y sin problemas»
    Evidencia: test `quality/__tests__/tarea-cli.test.mjs` «avanzar de ESPERA_DECISION a DESARROLLO exige criterios con evidencia»
19. El `script` de un gate `npm` solo admite letras, números, `_`, `-` y `:`; otro
    valor se rechaza con un motivo y no se ejecuta ningún gate.
    Evidencia: test `quality/__tests__/gates.test.mjs` «el script de un gate npm solo admite letras, números, guion, guion bajo y dos puntos»
20. `docs/decisiones/README.md` admite, además de los dos cambios previos, corregir
    enlaces y erratas de un registro aceptado sin cambiar el sentido de la decisión.
    Evidencia: manual — es una regla de texto del registro de decisiones; se valida leyendo el README.

## Plan de pruebas

- Pruebas dirigidas durante TDD: `node --test quality/__tests__/<archivo>.test.mjs`.
  Cada criterio con prueba registra su rojo:
  `node quality/tarea.mjs registrar 2026-11-03-proceso-portable-evidencia-decisiones comando --texto "Rojo criterio <N>: <comando> — <motivo del fallo>" --rol desarrollador --proveedor <p>`.
- Al cerrar el desarrollo: `node --test "quality/__tests__/*.test.mjs"`,
  `node quality/generar-adaptadores.mjs`, `node quality/check-adaptadores.mjs`,
  `node quality/check-mojibake.mjs`, `node quality/check-enlaces.mjs`,
  `node quality/check-tareas.mjs` y `git diff --check`.
- Verificación: puerta completa con `node quality/tarea.mjs verificar`. El gate
  `Tareas` comprobará esta misma tarea en `VERIFICACION`: sus pruebas citadas y sus
  rojos registrados.
- Pruebas existentes: `verify.test.mjs` no se modifica; `check-tareas.test.mjs`,
  `tareas.test.mjs` y `tarea-cli.test.mjs` solo reciben pruebas nuevas al final.
- Revisor obligatorio, revisión profunda: el cambio toca `quality/` y los roles.

## Riesgos

- Un `tarea.md` con criterios en otra sección o sin numerar escapa del gate. Se
  acepta: el planificador escribe la sección y la CLI exige al menos un criterio al
  salir de `PLANIFICACION`.
- El rojo es autodeclarado (un evento `comando` con prefijo fijo). El gate exige
  que exista, no que sea cierto; el revisor lo contrasta con la prueba.
- La prueba que congela los gates obliga a tocarla en el bloque 2 al añadir gates.
  Es deliberado: quitar o debilitar un gate queda visible en el diff.
- Exportar `GATES` al importar `verify.mjs` lee `quality/gates.json` en cada
  importación; con una configuración rota, `GATES` queda vacío y la puerta falla
  sin ejecutar nada.
- Cambios en `quality/` y en roles: revisor obligatorio.

## Decisiones para aprobar

Solo las tres primeras necesitan al usuario. El resto las tomó el planificador; el
detalle y las alternativas están en la spec (D1 a D14).

1. **Revisión ligera en toda tarea con código** (spec D9). Recomendado: sí. Toda
   tarea que cambia código, pruebas o configuración pasa por `REVISION` con el
   checklist común sobre el diff; la profunda añade el de dominio y es obligatoria
   en las zonas que lista `dominio.md` (las de hoy más `quality/`). Solo una tarea
   exclusivamente documental va de `VERIFICACION` a `ENTORNO`. Motivo: los
   defectos del checklist común (pruebas vacías, pruebas existentes adaptadas al
   código, `.Result`) no dependen del riesgo del área y la puerta no los detecta.
   Coste: una lectura del diff por tarea con el nivel alto del revisor. Si la
   respuesta es no, el orquestador sigue decidiendo cuándo interviene el revisor y
   este aplica los dos checklists siempre que interviene.
2. **Ruta de las decisiones** (spec D5). Recomendado: `docs/decisiones/NNN-slug.md`.
   Queda fuera de `docs/proyecto/`, que describe el producto vigente revisado contra
   el código, y fuera de `docs/ai/`, que es proceso; en el kit, la carpeta, su
   README y su plantilla son portables y los registros numerados son de cada
   proyecto. Alternativa: `docs/proyecto/decisiones/`.
3. **Voseo restante en el kit** (spec D12). Recomendado: corregirlo también en
   `quality/check-adaptadores.mjs:36` («Corré»), `quality/check-enlaces.mjs:90`
   («Corregí», «creá»), `quality/check-mojibake.mjs:84` («Escribí», «necesitás»,
   «ponelo») y `docs/ai/README.md:26` («conocés»). Son las únicas apariciones
   fuera de specs, planes y tareas, ninguna prueba las cita, y viajarían al kit.
   Si la respuesta es no, el criterio 11 se limita a `verify.mjs`.
4. **Motivos pendientes** (no bloquea). Los registros 002 a 008 quedan con motivo
   `PENDIENTE`. Si quieres darlos ya, en especial el de 002 (excepciones de dominio
   frente a Result pattern), el desarrollador los escribe tal cual.
