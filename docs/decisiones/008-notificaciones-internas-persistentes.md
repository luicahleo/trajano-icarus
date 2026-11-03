# 008. Notificaciones internas persistentes

- Estado: Aceptada
- Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
- Alcance: Trajano-Icarus

## Contexto

La [spec de SP8](../superpowers/specs/2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md),
en «Notificaciones internas», define las notificaciones como entidades
persistentes que cada destinatario lista, cuenta y marca como leídas; el aviso
en tiempo real es un añadido y el registro persistente es la fuente de verdad.
Antes del registro estaba en `docs/proyecto/arquitectura.md` §9.

## Decisión

Las notificaciones internas se persisten. Las notificaciones push o en tiempo
real nunca son la fuente de verdad.

## Motivo

PENDIENTE: lo completa el humano.

## Alternativas descartadas

- Push como único canal, sin registro persistente.

## Reconsiderar cuándo

- Si hace falta avisar con la aplicación cerrada.
