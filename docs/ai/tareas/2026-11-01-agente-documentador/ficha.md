---
id: 2026-11-01-agente-documentador
tipo: calidad
modulos: [quality, docs]
temas: [agentes, documentador, ficha-cierre, indice-tareas, gate-tareas]
resumen: Rol documentador, estado DOCUMENTACION, fichas de cierre de formato fijo e índice generado con gate.
aprobado: 4fdb638
commits: [cbf7c6b]
---

# Agente documentador: ficha de cierre, índice consultable y gate de documentación

## Qué cambió

- Nuevo rol documentador y estado `DOCUMENTACION` entre `APROBADA` y `CERRADA`.
- Ficha de cierre de formato fijo por tarea (60 líneas, 4000 bytes) e `INDICE.md`
  generado con una línea por ficha.
- El gate `Tareas` valida fichas e índice; comandos `ficha` e `indice` en la CLI.
- Fichas retroactivas de las tres tareas ya cerradas.

## Por qué

Las sesiones nuevas releían specs y planes enteros para orientarse. La ficha y el
índice dan memoria barata y buscable.

## Decisiones

- El documentador solo escribe en `docs/ai/tareas/`; no toca código ni `docs/proyecto/`.
- El índice se genera, nunca se edita a mano; un conflicto se resuelve regenerando.
- Las rutas de «Archivos clave» deben ser archivos regulares dentro del repo.

## Archivos clave

- `quality/lib/fichas.mjs` — formato, validación e índice de fichas
- `quality/lib/tareas.mjs` — máquina de estados con `DOCUMENTACION`
- `quality/check-tareas.mjs` — gate de fichas e índice
- `quality/tarea.mjs` — CLI con `ficha` e `indice`
- `docs/ai/agentes/roles/documentador.md` — fuente del rol
- `docs/ai/tareas/README.md` — formato de la ficha

## Cómo probarlo

- `node --test quality/__tests__/` y `node quality/check-tareas.mjs`.
- Criterios de aceptación en `tarea.md`.

## Pendientes

- No se exige `git ls-files` en `existe()` de las rutas citadas; las pruebas usan
  directorios sin repositorio git.
- `APROBADA` a `CERRADA` se acepta sin corte por fecha o id: una bitácora editada
  a mano puede saltar `DOCUMENTACION`.
- Menores de la revisión sin corregir: el documentador puede cerrar como dueño
  (M2), `ficha` no exige cerrojo (M3), `commits` sin comprobar existencia (M4),
  orden alfabético dentro del mismo día y resumen con barra vertical sin validar (M6).

## Enlaces

- [Tarea](tarea.md)
- [Spec](../../../superpowers/specs/2026-11-01-agente-documentador-design.md)
- [Plan](../../../superpowers/plans/2026-11-01-agente-documentador.md)
