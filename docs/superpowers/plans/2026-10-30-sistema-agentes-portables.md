# Plan: sistema de agentes portables, fase 1

Spec: [sistema de agentes portables](../specs/2026-10-30-sistema-agentes-portables-design.md).

El plan se ejecutó en la misma sesión que el diseño. Queda como registro de lo hecho.

## Tareas

- [x] Spec con los nueve entregables.
- [x] Máquina de estados pura en `quality/lib/tareas.mjs`, con pruebas vistas en rojo.
- [x] CLI `quality/tarea.mjs` con cerrojo, evidencia por árbol y aprobación en terminal.
- [x] Guardia `quality/guardia-comandos.mjs` y su hook de proyecto en Claude Code.
- [x] Traducción de roles en `quality/adaptadores/roles.mjs`, integrada al manifiesto.
- [x] Gate `Tareas` en `quality/check-tareas.mjs`, agregado a la puerta.
- [x] Seis roles, protocolo y guía de uso en `docs/ai/agentes/`.
- [x] `AGENTS.md`, `docs/ai/README.md` y `docs/ai/PUERTA_CALIDAD.md` actualizados.
- [x] Arranque real de un rol en Claude Code y en Kimi Code.
- [x] Guardia probada como proceso real, con cerrojo y sin él.

## Pendiente, fuera de la fase 1

- [ ] Aprobación del diseño por el usuario.
- [ ] Piloto de la fase 2 con una tarea pequeña y un relevo entre proveedores.
- [ ] Prueba de `aprobar` en una terminal interactiva real.
- [ ] Hook de guardia en la configuración de usuario de Kimi.

## Verificación

- Dirigida: `node --test 'quality/__tests__/*.test.mjs'`.
- Integración: `./verify.ps1`.
