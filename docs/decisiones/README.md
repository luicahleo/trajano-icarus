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
