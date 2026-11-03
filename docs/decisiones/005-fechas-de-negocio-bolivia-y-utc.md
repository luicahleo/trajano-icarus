# 005. Fechas de negocio de Bolivia e instantes técnicos en UTC

- Estado: Aceptada
- Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
- Alcance: Trajano-Icarus

## Contexto

Las operaciones comerciales y la asistencia se razonan por día de calendario
local; los instantes técnicos necesitan una referencia sin zona. Antes del
registro estaba en `docs/proyecto/arquitectura.md` §9.

## Decisión

Las operaciones comerciales y la asistencia usan la fecha de negocio de
Bolivia. Los instantes técnicos se persisten en UTC donde el modelo lo
requiere.

## Motivo

PENDIENTE: lo completa el humano.

## Alternativas descartadas

- Todo en UTC, con la conversión solo en la interfaz.

## Reconsiderar cuándo

- Si se incorpora un cliente que opera en otra zona horaria.
