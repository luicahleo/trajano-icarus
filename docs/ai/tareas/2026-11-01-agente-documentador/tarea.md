# Agente documentador: ficha de cierre, índice consultable y gate de documentación

## Pedido

Un agente que documente cada tarea al cerrarla para que las sesiones futuras
consulten fichas e índices en lugar de releer specs, planes y código. Criterio
rector: ahorrar tokens en las sesiones futuras. El usuario delegó las decisiones
de diseño en el planificador y las revisará (lista «Decisiones para aprobar»).

- Spec: [`docs/superpowers/specs/2026-11-01-agente-documentador-design.md`](../../../superpowers/specs/2026-11-01-agente-documentador-design.md)
- Plan: [`docs/superpowers/plans/2026-11-01-agente-documentador.md`](../../../superpowers/plans/2026-11-01-agente-documentador.md)

## Alcance

- Rol portable `documentador` en `docs/ai/agentes/roles/` y sus adaptadores
  generados para Claude Code y Kimi Code; el orquestador lo añade a su delegación.
- Estado `DOCUMENTACION` entre `APROBADA` y `CERRADA`, con el documentador como
  dueño. `APROBADA → CERRADA` queda solo como historia válida de las bitácoras
  anteriores.
- `quality/lib/fichas.mjs`: formato fijo de la ficha, validación con topes e
  índice generado.
- CLI: comandos `ficha` (plantilla prellenada) e `indice` (regenera
  `docs/ai/tareas/INDICE.md`); `CERRADA` exige ficha válida e índice al día.
- Gate `Tareas`: toda tarea cerrada tiene ficha válida y el índice coincide con
  las fichas.
- Planificador y desarrollador: `docs/proyecto/` y el glosario se planifican y se
  actualizan en `DESARROLLO`.
- Documentación de proceso: `AGENTS.md`, `docs/ai/README.md`,
  `docs/ai/agentes/PROTOCOLO.md`, `docs/ai/agentes/README.md`,
  `docs/ai/tareas/README.md` y `docs/ai/PUERTA_CALIDAD.md`.
- Piloto: fichas retroactivas de las tres tareas cerradas y medición de tamaños.
- Documentación de producto (`docs/proyecto/`, glosario): ninguna; el cambio es
  de proceso.

## Fuera de alcance

- Grafo de conocimiento con graphify: segunda fase, según la decisión D11 de la
  spec.
- Fichas para el trabajo anterior al sistema de tareas.
- Que el documentador edite `docs/proyecto/` o el glosario tras la aprobación.
- Cambios en `quality/verify.mjs`, baselines, umbrales o exclusiones.
- Corregir el voseo que queda en `docs/ai/README.md` y
  `docs/ai/CONTEXT-EFFICIENCY.md`.

## Criterios de aceptación

Se comprueban en una terminal en la raíz del repositorio, sobre el commit en
prueba manual.

1. `docs/ai/tareas/INDICE.md` existe, dice que no se edita a mano y tiene
   exactamente tres líneas de fichas, de la más reciente a la más antigua, con la
   forma `- <id> | <tipo> | <módulos> | <temas> | <resumen>`.
2. Cada una de las tres tareas cerradas del 2026-10-30 tiene `ficha.md` con las
   secciones «Qué cambió», «Por qué», «Decisiones», «Archivos clave», «Cómo
   probarlo», «Pendientes» y «Enlaces», en ese orden, sin `COMPLETAR`, con 60
   líneas o menos. Leída sola, la ficha del gestor veterinario permite saber qué
   cambió y dónde está el código sin abrir la spec ni el plan.
3. Cada ruta de «Archivos clave» de esas fichas existe en el repositorio.
4. `node quality/check-tareas.mjs` termina en verde e informa cuatro tareas
   legales con fichas e índice al día.
5. Si se cambia a mano una línea de `INDICE.md`, `node quality/check-tareas.mjs`
   falla con «INDICE.md no está al día»; `node quality/tarea.mjs indice` lo
   corrige y el gate vuelve a verde.
6. Si se cambia en una ficha una ruta de «Archivos clave» por una inexistente, el
   gate falla nombrando esa ruta; al restaurarla vuelve a verde.
7. `node quality/tarea.mjs ficha 2026-11-01-agente-documentador --rol documentador --proveedor anthropic`,
   con la tarea en `PRUEBA_MANUAL`, se niega e indica que la ficha se escribe en
   `DOCUMENTACION`.
8. `node quality/tarea.mjs ficha 2026-10-30-enlaces-sin-versionar --rol documentador --proveedor anthropic`
   se niega porque la ficha ya existe.
9. Existen `.claude/agents/documentador.md` y `.kimi-code/agents/documentador.md`,
   y el agente aparece al listar los agentes de Claude Code.
10. `AGENTS.md` indica, en «Descubrimiento eficiente», buscar en
    `docs/ai/tareas/INDICE.md` antes de explorar un área, y nombra siete roles.
11. La bitácora de esta tarea tiene una nota de medición por ficha piloto con su
    tamaño frente al de la spec, el plan y `tarea.md` que sustituye.
12. Tras la aprobación (lo comprueba el orquestador, no la prueba manual):
    `avanzar ... CERRADA` desde `APROBADA` se niega; la tarea pasa por
    `DOCUMENTACION`, `resumen` muestra «Responsable: documentador», se escribe su
    ficha y solo entonces se cierra.

## Plan de pruebas

- `quality/__tests__/tareas.test.mjs`: dueño de `DOCUMENTACION`, cierre directo
  prohibido, guarda de ficha en `CERRADA`, bitácoras antiguas legales.
- `quality/__tests__/fichas.test.mjs` (nuevo): formato, topes, listas cerradas,
  rutas, credenciales, CRLF y generación del índice.
- `quality/__tests__/check-tareas.test.mjs`: tarea cerrada sin ficha, ficha
  inválida, `aprobado` distinto de la bitácora e índice desactualizado.
- `quality/__tests__/tarea-cli.test.mjs`: `ficha`, `indice` y cierre completo con
  un remoto temporal.
- `quality/__tests__/manifiesto.test.mjs`: el rol nuevo tiene sus dos adaptadores.
- Puerta: `./verify.ps1` completo.
- Prueba manual: criterios 1 a 11. El 12 lo verifica el orquestador al cerrar.

## Riesgos

- Una tarea posterior que renombre o borre un archivo citado en una ficha hace
  fallar el gate y obliga a corregir la ficha antigua. Es deliberado (decisión 8).
- La ficha del gestor veterinario puede no caber en 4000 bytes. Si no cabe, se
  recorta; el tope no se sube sin decisión del usuario.
- El ahorro depende de que las sesiones consulten el índice. Mitigado con la regla
  en `AGENTS.md`, que cuesta unos 110 tokens por sesión.
- En tareas triviales el coste del documentador puede superar el ahorro. Se acepta
  por simplicidad de la regla (decisión 4).
- `tareas.test.mjs` cierra hoy directamente desde `APROBADA`: esa prueba se
  reescribe al flujo nuevo, sin relajar ninguna comprobación.
- Cambios en `quality/` y en roles: el revisor es obligatorio.

## Decisiones para aprobar

Detalle y alternativas en la spec (D1 a D12).

1. **Momento:** estado nuevo `DOCUMENTACION` entre `APROBADA` y `CERRADA`, dueño
   el documentador. Recomendado: evita reescribir la ficha tras cada rechazo y no
   toca el árbol aprobado.
2. **Documentación de producto:** `docs/proyecto/` y el glosario los actualiza el
   desarrollador en `DESARROLLO`, según el alcance del planificador; el
   documentador solo anota lo que falte. Recomendado: el desarrollador ya tiene el
   contexto cargado y editar tras aprobar invalidaría el cierre.
3. **Ubicación:** `docs/ai/tareas/<id>/ficha.md`. Recomendado: queda fuera del
   hash de evidencia y junto a su tarea.
4. **Obligatoriedad:** ficha para toda tarea `CERRADA`, sin excepción por tipo;
   las canceladas no la necesitan; un bug con memoria pasa por una tarea.
   Recomendado: regla comprobable sin git y de coste bajo.
5. **Topes:** 4000 bytes y 60 líneas; resumen de 20 a 160 caracteres; hasta 12
   archivos clave y 8 temas. Recomendado: leer una ficha cuesta unos 1100 tokens
   como máximo.
6. **Índice:** `docs/ai/tareas/INDICE.md`, generado por la CLI, una línea por
   ficha sin enlace markdown, de la más reciente a la más antigua. Recomendado: sin
   conflictos ni desincronía, unos 70 tokens por línea.
7. **Vocabulario:** módulos de una lista cerrada y temas en kebab-case ASCII sin
   acentos. Recomendado: la búsqueda no depende de escribir bien el acento.
8. **Rutas comprobadas:** el gate exige que existan las rutas de «Archivos
   clave». Recomendado: una ficha con rutas muertas engaña; el coste es corregir
   fichas antiguas al renombrar.
9. **Dónde se comprueba:** en el gate `Tareas` existente y en la transición a
   `CERRADA`, sin gate nuevo en `verify.mjs`. Recomendado: misma carpeta, mismo
   gate.
10. **Nivel del documentador:** intermedio. Recomendado: resume con criterio pero
    no diseña.
11. **Grafo:** fuera de alcance; segunda fase con datos cuando haya unas 20
    fichas. Recomendado: construirlo cuesta más de lo que ahorra hoy y solo
    Claude Code tiene la skill.
12. **Piloto:** las tres tareas cerradas del 2026-10-30, como último paso antes de
    la puerta completa. Es requisito del gate: tras el cambio, toda tarea cerrada
    necesita ficha.
13. **Nivel de quien implementa:** alto, aunque el plan trae el código completo,
    porque `ECONOMIA_TOKENS.md` no permite bajarlo en cambios de la puerta.
