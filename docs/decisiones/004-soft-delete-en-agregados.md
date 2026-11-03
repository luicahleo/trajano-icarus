# 004. Soft delete en los agregados de negocio

- Estado: Aceptada
- Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
- Alcance: Trajano-Icarus

## Contexto

La regla viene de ICARUS y se rescató al glosario al arrancar el proyecto
([spec del andamiaje](../superpowers/specs/2026-08-12-andamiaje-instrucciones-agentes-design.md)):
las entidades se desactivan con `EstaActivo = false`. Antes del registro
estaba en `docs/proyecto/arquitectura.md` §9.

## Decisión

Los agregados de negocio se desactivan, no se borran. Los historiales no se
borran físicamente.

## Motivo

PENDIENTE: lo completa el humano.

## Alternativas descartadas

- Borrado físico con tablas de auditoría aparte.

## Reconsiderar cuándo

- Si una norma de protección de datos exige borrar de verdad datos personales.
- Si el volumen de registros inactivos degrada las consultas.
