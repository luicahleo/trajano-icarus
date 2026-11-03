# 002. Errores por excepciones de dominio, no Result pattern

- Estado: Aceptada
- Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
- Alcance: Trajano-Icarus

## Contexto

El dominio y la aplicación señalan los errores esperados lanzando excepciones
que derivan de `DomainException`: `NotFoundException`, `ConflictException` y
`ForbiddenException` en
`Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs`, y
`ReglaNegocioException` en `ReglaNegocioException.cs`, en la misma carpeta.
`ExceptionHandlingMiddleware`, en
`Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/`, las traduce a
respuestas HTTP.

## Decisión

Los errores de regla de negocio, de existencia, de conflicto y de permiso se
comunican con esas excepciones. Los handlers no devuelven `Result<T>` ni tipos
equivalentes.

## Motivo

PENDIENTE: lo completa el humano.

## Alternativas descartadas

- Result pattern (`Result<T>` con errores tipados): hace explícito el error en
  la firma, a cambio de propagarlo a mano en cada capa. Por qué se descartó es
  parte del motivo pendiente.

## Reconsiderar cuándo

- Si una operación necesita devolver varios errores de negocio a la vez.
- Si los errores esperados se vuelven frecuentes y su coste aparece en la
  telemetría.
