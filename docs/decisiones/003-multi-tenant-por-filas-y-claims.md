# 003. Multi-tenant por filas y claims

- Estado: Aceptada
- Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
- Alcance: Trajano-Icarus

## Contexto

El Cliente es el tenant: sus datos operativos no se mezclan con los de otros
clientes. Antes del registro estaba en `docs/proyecto/arquitectura.md` §9.
Detalle en [identidad, clientes y permisos](../proyecto/identidad-clientes-permisos.md)
y en el [modelo de datos](../proyecto/datos.md).

## Decisión

Todos los clientes comparten una base de datos. Cada fila de datos de un
cliente lo identifica, y los filtros de los contextos de EF Core toman el
cliente activo de los claims del token. No hay una base ni un esquema por
cliente.

## Motivo

PENDIENTE: lo completa el humano.

## Alternativas descartadas

- Una base de datos por cliente.
- Un esquema por cliente en la misma base.

## Reconsiderar cuándo

- Si un cliente exige aislamiento físico de sus datos por contrato o por norma.
- Si el volumen de un cliente degrada el servicio de los demás.
