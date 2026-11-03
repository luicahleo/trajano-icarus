# Agente documentador: ficha de cierre, índice consultable y gate de documentación

- Fecha: 2026-11-01
- Tarea: [`2026-11-01-agente-documentador`](../../ai/tareas/2026-11-01-agente-documentador/tarea.md)
- Plan: [`2026-11-01-agente-documentador.md`](../plans/2026-11-01-agente-documentador.md)
- Antecedente: [spec del sistema de agentes](2026-10-30-sistema-agentes-portables-design.md)

## Problema

Una sesión nueva que necesita entender un área tiene hoy dos caminos caros:
releer specs y planes (`docs/superpowers/` suma unos 3 MB) o explorar el código
hasta reconstruir qué se hizo y por qué. Solo la tarea del gestor veterinario
acumula 295 KB entre dos specs, dos planes y su `tarea.md`, unos 80 000 tokens si
se leyeran completos. `docs/proyecto/` describe el producto vigente, pero no dice
qué cambió en cada tarea, qué se decidió ni dónde está el código.

El objetivo es uno: **ahorrar tokens en las sesiones futuras**. Cada decisión de
esta spec se justifica por el coste de contexto que evita o que añade.

## Solución en una frase

Un rol nuevo, `documentador`, escribe tras la aprobación humana una ficha corta de
formato fijo por tarea; la CLI genera a partir de las fichas un índice de una
línea por tarea; el gate `Tareas` y la transición a `CERRADA` exigen ficha válida
e índice al día; y `AGENTS.md` ordena buscar en el índice antes de explorar.

## Decisiones

Cada decisión lleva su alternativa descartada. Las que el usuario debe confirmar
están numeradas igual en el informe del planificador.

### D1. Momento: estado `DOCUMENTACION` entre `APROBADA` y `CERRADA`

La ficha se escribe después de la aprobación humana, en un estado nuevo cuyo dueño
es el documentador.

- Tras la aprobación el contenido ya no cambia: la ficha se escribe una sola vez.
  Escribirla antes de `PRUEBA_MANUAL` obligaría a reescribirla tras cada rechazo,
  y la tarea del gestor veterinario tuvo una ampliación entera tras la prueba
  manual.
- La ficha y el índice viven en `docs/ai/tareas/`, que `arbolDeTrabajo` ya
  excluye del hash. Escribirlos después de aprobar no invalida la evidencia ni la
  aprobación, y `CERRADA` sigue exigiendo el árbol aprobado sin ninguna
  excepción nueva.
- El estado propio fija un responsable visible en `resumen`. Una sesión nueva sabe
  qué hacer sin leer la conversación, que es el principio del protocolo.
- Compatibilidad: `APROBADA → CERRADA` sigue existiendo en la tabla de
  transiciones para que `validarBitacora` acepte las tres bitácoras cerradas
  antes de este cambio, pero `validarTransicion` la prohíbe para transiciones
  nuevas. Ninguna bitácora existente se reescribe.

Descartado: producir la ficha antes de `PRUEBA_MANUAL`. Ganaba que el usuario la
viera en la prueba manual, pero cuesta retrabajo en cada rechazo y la ficha
entraría en el árbol verificado si viviera fuera de `docs/ai/tareas/`.

### D2. `docs/proyecto/` y el glosario se actualizan en `DESARROLLO`, no en `DOCUMENTACION`

La actualización de la documentación de producto sigue donde ya ocurre hoy (los
commits `feat(vacunacion)` la incluyen): la planifica el planificador en el
alcance de `tarea.md` y la hace el desarrollador junto con el código.

- El desarrollador ya tiene cargado el contexto del cambio; un documentador
  posterior tendría que volver a leer código para describir el comportamiento.
  Es el mismo trabajo pagado dos veces.
- Esos documentos forman parte del árbol aprobado. Editarlos después de la
  aprobación bloquearía `CERRADA` o exigiría relajar la comprobación del árbol.
- El documentador audita: si el dominio o el comportamiento cambiaron y
  `docs/proyecto/` o el glosario no lo reflejan, lo anota en «Pendientes» y en la
  bitácora. No los edita.

Descartado: permitir en `CERRADA` un árbol distinto del aprobado si la diferencia
se limita a `docs/proyecto/` y `docs/dominio/`. Debilita la garantía «lo cerrado
es lo aprobado» y documenta comportamiento que el usuario no vio.

### D3. La ficha vive en `docs/ai/tareas/<id>/ficha.md`

Junto a `tarea.md` y la bitácora. Hereda la exclusión del hash de evidencia (D1),
el gate `Tareas` ya recorre esa carpeta y el identificador de la tarea es la ruta.

Descartado: una carpeta `docs/fichas/`. Entraría en el árbol verificado y
separaría la ficha de su tarea.

### D4. Ficha obligatoria para toda tarea `CERRADA`

Sin excepciones por tipo. Las tareas canceladas no la necesitan.

- Toda tarea cerrada pasó por aprobación humana: algo cambió. Una ficha de una
  tarea pequeña cuesta unas 15 líneas.
- Una regla sin excepciones se comprueba sin git: el gate solo mira el estado
  reconstruido. Distinguir «tareas que cambian código» exigiría calcular diffs
  por rango de commits en cada ejecución del gate.
- El campo `tipo` (`funcionalidad`, `correccion`, `calidad`, `configuracion`,
  `documentacion`) permite filtrar en el índice.
- Las correcciones de bugs que merezcan memoria pasan por una tarea, aunque sea
  la ruta corta `RECIBIDA → DESARROLLO`. Un rechazo `funcional` dentro de una
  tarea queda en su propia ficha. No hay fichas sueltas sin tarea.

### D5. Formato fijo, corto y validado

Frontmatter de una línea por campo y siete secciones en orden fijo:

```markdown
---
id: 2026-10-30-gestor-veterinario-vacunacion
tipo: funcionalidad
modulos: [GestionAvicola, GestorCaisy, web]
temas: [vacunacion, gestor-veterinario, campana]
resumen: El Gestor Veterinario de CAISY publica los programas de vacunación.
aprobado: 1573cd5
commits: [3e4fffb, 494df97]
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

Topes, comprobados por el gate:

| Regla | Valor | Motivo |
|---|---|---|
| Tamaño total | 4000 bytes | Unos 1100 tokens: leer una ficha cuesta menos que un fragmento de plan |
| Líneas | 60 | Obliga a viñetas, no a prosa |
| `resumen` | 20 a 160 caracteres, una línea | Es lo que aparece en el índice |
| `temas` | 1 a 8, kebab-case ASCII sin acentos | La búsqueda no depende de escribir bien el acento |
| `modulos` | Lista cerrada | Filtro fiable: `Identity`, `Clientes`, `GestionAvicola`, `ControlAcceso`, `BuildingBlocks`, `Host`, `GestorCaisy`, `web`, `infra`, `deploy`, `quality`, `docs` |
| «Archivos clave» | Hasta 12, ruta entre comillas invertidas que exista | La ficha lleva directo al código, sin explorar |
| `aprobado` | Igual al commit de la aprobación en la bitácora | Fija la instantánea exacta para `git diff` |
| Marcador `COMPLETAR` | Prohibido | La plantilla no se puede cerrar sin rellenar |
| Credenciales | Prohibidas | Mismo detector que la bitácora |

Regla de no duplicar: la ficha **enlaza, no copia**. Las reglas viven en el
glosario y en `docs/proyecto/`, los criterios en `tarea.md`, el detalle en la spec
y el plan. «Cómo probarlo» nombra las pruebas dirigidas y remite a los criterios
de `tarea.md`.

La comprobación de existencia de «Archivos clave» tiene un coste: si una tarea
posterior renombra o borra un archivo citado, el gate falla y hay que corregir la
ficha antigua. Se acepta porque una ficha que apunta a rutas inexistentes engaña
a la sesión que la lee, que es justo lo que se quiere evitar.

### D6. Índice generado: `docs/ai/tareas/INDICE.md`

Una línea por ficha, de la más reciente a la más antigua:

```text
- 2026-10-30-gestor-veterinario-vacunacion | funcionalidad | GestionAvicola, GestorCaisy, web | vacunacion, gestor-veterinario, campana | El Gestor Veterinario de CAISY publica los programas de vacunación.
```

- Lo genera `node quality/tarea.mjs indice` desde el frontmatter de las fichas. No
  se edita a mano, así que no hay conflictos que resolver: ante cualquier duda se
  regenera. Además el cerrojo impide dos tareas activas a la vez.
- El gate falla si el archivo no coincide con lo que generaría la CLI.
- Lista y no tabla: sin relleno de columnas, unos 70 tokens por línea.
- Sin enlace markdown por línea: la ruta `docs/ai/tareas/<id>/ficha.md` se deduce
  del id y el enlace duplicaría unos 50 bytes por línea. La sincronía la garantiza
  el gate.
- Se consulta con búsqueda, no con lectura completa.

### D7. Gate dentro de `Tareas` y en la transición a `CERRADA`

- `quality/check-tareas.mjs` añade tres comprobaciones: toda tarea `CERRADA` tiene
  `ficha.md`; toda ficha existente es válida; `INDICE.md` está al día.
- `quality/tarea.mjs avanzar <id> CERRADA` calcula los mismos problemas y
  `validarTransicion` se niega a cerrar si hay alguno o si no se comprobaron.
- No se añade un gate a `verify.mjs`: las fichas son parte de las tareas y el gate
  `Tareas` ya recorre esa carpeta. Ninguna baseline, umbral ni exclusión cambia.

### D8. Comandos nuevos de la CLI

- `ficha <id> --rol documentador --proveedor <p>`: crea `ficha.md` desde la
  plantilla con `id`, título, `aprobado`, los commits del rango de la tarea y los
  enlaces a spec y plan que ya figuran en `tarea.md`. Funciona en `DOCUMENTACION`
  y en `CERRADA` (documentación retroactiva). No sobrescribe una ficha existente
  ni escribe en la bitácora. Ahorra al documentador leer el formato y buscar los
  commits.
- `indice`: regenera `INDICE.md`; idempotente.

### D9. Rol `documentador`

- Nivel intermedio: resume con criterio, pero no diseña.
- Herramientas: leer, buscar, shell y editar. No delega.
- Solo escribe en `docs/ai/tareas/<id>/ficha.md` e `INDICE.md` (este último, a
  través de la CLI). No hace commit, no avanza a `CERRADA`, no aprueba, no edita
  código, configuración, pruebas, `docs/proyecto/` ni el glosario.
- El orquestador lo añade a `delega_en`, pasa la tarea a `DOCUMENTACION` tras el
  push y la cierra cuando la ficha valida.

### D10. Consumo por una sesión nueva

`AGENTS.md`, en «Descubrimiento eficiente», incorpora un paso: antes de explorar un
área, buscar su término en `docs/ai/tareas/INDICE.md`, abrir solo las fichas que
coincidan y partir de sus «Archivos clave»; specs y planes, solo si la ficha no
basta. `docs/ai/README.md` añade el índice y el formato a su tabla. El coste fijo
son unas tres líneas más en `AGENTS.md`.

### D11. Grafo: segunda fase, fuera de alcance

graphify está instalado a nivel de usuario y no añade dependencias al
repositorio (`graphify-out/` ya está en `.gitignore` y en los `.*ignore`
generados), pero no conviene en esta fase:

- Construir el grafo usa el modelo para extraer relaciones del corpus completo:
  con 3 MB de specs y planes más el código, la primera construcción cuesta mucho
  más de lo que ahorran decenas de consultas a fichas.
- Solo Claude Code tiene la skill. Kimi, Codex y Gemini necesitarían instalar la
  CLI de Python por su cuenta; rompe la regla de que el proceso no dependa de un
  proveedor.
- El grafo no versionado se desactualiza sin aviso; las fichas tienen un gate.

Segunda fase propuesta, a decidir con datos: cuando existan unas 20 fichas,
probar `graphify docs/ai/tareas --no-viz` (solo fichas, corpus pequeño) y comparar
`graphify query "<pregunta>" --budget 1500` con una búsqueda en el índice para
tres preguntas reales. Adoptarlo solo si responde mejor con menos tokens, como
optimización opcional que nunca sea necesaria.

### D12. Piloto retroactivo

El documentador escribe las fichas de las tres tareas cerradas:
`2026-10-30-gestor-veterinario-vacunacion` (grande, con ampliación tras la prueba
manual), `2026-10-30-enlaces-sin-versionar` y `2026-10-30-mojibake-sin-versionar`
(pequeñas, con `tarea.md` vacío). Valida el formato en los dos extremos y es
requisito para que el gate pase: tras el cambio, toda tarea cerrada necesita
ficha. Esta misma tarea será la cuarta ficha, al cerrarse por el flujo nuevo.

## Estimación de ahorro

Supuesto: unos 3,5 bytes por token en markdown en español con código. Las cifras
son órdenes de magnitud, no mediciones; el piloto registra los tamaños reales.

| Lectura | Bytes | Tokens aprox. |
|---|---:|---:|
| Specs, planes y `tarea.md` del gestor veterinario, completos | 295 000 | 84 000 |
| Lectura realista hoy: `tarea.md`, las dos specs y búsquedas en los planes | 41 000 + búsquedas | 12 000 a 15 000 |
| Búsqueda en el índice (1 a 5 líneas) | 300 a 1500 | 100 a 400 |
| Una ficha | hasta 4000 | hasta 1100 |
| Coste fijo nuevo de `AGENTS.md` en cada sesión | unos 400 | unos 110 |

- Consulta sobre una tarea grande: de 12 000 a 15 000 tokens a unos 1500, más el
  ahorro de exploración porque «Archivos clave» lleva directo al código.
- Coste por tarea: el documentador lee `tarea.md`, las decisiones de la bitácora y
  `git diff --stat`; entre 6000 y 15 000 tokens de nivel intermedio, y unos 1200
  de salida.
- Equilibrio: una sola consulta futura amortiza la ficha de una tarea grande. En
  una tarea pequeña el ahorro por consulta es bajo (su bitácora ya es corta); lo
  que gana es poder encontrarla con una búsqueda en lugar de recorrer `git log`.
- El ahorro solo existe si las sesiones consultan el índice: por eso la regla va
  en `AGENTS.md`, que se carga siempre.

## Riesgos

- **Fichas que se desactualizan.** Mitigado: el gate comprueba rutas de «Archivos
  clave», `aprobado` y la sincronía del índice. El texto libre puede envejecer; la
  ficha remite a `docs/proyecto/` para el comportamiento vigente.
- **Coste del documentador mayor que el ahorro en tareas triviales.** Aceptado por
  simplicidad de la regla (D4); el tope de tamaño limita el coste de salida.
- **Mantenimiento por renombrados.** Un renombrado obliga a corregir fichas
  antiguas (D5). Se encuentran con una búsqueda de la ruta en `docs/ai/tareas/`.
- **Rotura de pruebas existentes.** `tareas.test.mjs` cierra hoy directamente
  desde `APROBADA`; esa prueba se reescribe al flujo nuevo y se añaden pruebas que
  garantizan que las bitácoras antiguas siguen siendo legales.
- **Rol nuevo en el manifiesto.** `manifiesto.test.mjs` fija la lista de roles: se
  amplía y el generador produce los dos adaptadores nuevos.
- **Datos personales en la ficha.** Mismo detector de credenciales que la
  bitácora; los datos personales siguen dependiendo del criterio del agente.

## Fuera de alcance

- El grafo de conocimiento (D11).
- Fichas para trabajo anterior al sistema de tareas (specs de octubre sin tarea).
- Editar `docs/proyecto/` o el glosario en el estado `DOCUMENTACION`.
- Cambios en `verify.mjs`, baselines o umbrales.
- Corregir el voseo que queda en `docs/ai/README.md` y `CONTEXT-EFFICIENCY.md`
  («conocés», «acá»): se anota para una tarea propia.
