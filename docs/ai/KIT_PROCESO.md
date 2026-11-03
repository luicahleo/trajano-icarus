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
