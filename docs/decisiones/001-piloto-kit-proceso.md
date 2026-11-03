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
