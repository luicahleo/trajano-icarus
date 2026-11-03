# 007. Sin procesos programados para vigencias o crédito

- Estado: Aceptada
- Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
- Alcance: Trajano-Icarus

## Contexto

Las vigencias de precios y el crédito cambian con el tiempo. La
[campana de tareas de vacunación](../superpowers/specs/2026-11-01-campana-tareas-vacunacion-trabajador-design.md)
aplica el mismo criterio: la consulta es la fuente de verdad, sin jobs ni push.
Antes del registro estaba en `docs/proyecto/arquitectura.md` §9.

## Decisión

No hay procesos programados para vigencias ni crédito: se resuelven al
consultar o al ejecutar el comando correspondiente.

## Motivo

PENDIENTE: lo completa el humano.

## Alternativas descartadas

- Un proceso en segundo plano que actualice vigencias y saldos a hora fija.

## Reconsiderar cuándo

- Si una regla tiene que actuar sin que nadie consulte, por ejemplo un aviso de
  vencimiento a una hora fija.
- Si calcular al consultar se vuelve lento.
