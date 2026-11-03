# 006. Online deliberado para pedidos y despachos

- Estado: Aceptada
- Fecha: 2026-11-03 (fecha del registro; la decisión es anterior)
- Alcance: Trajano-Icarus

## Contexto

La PWA trabaja sin conexión solo para la recogida y la mortalidad, con una cola
en IndexedDB ([recogida y mortalidad](../proyecto/recogida-huevos-mortalidad.md)).
La [spec de SP8](../superpowers/specs/2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md)
afirma que el pedido móvil de ICARUS no era realmente offline y que SP8 es
deliberadamente online; la
[bandeja de despachos](../superpowers/specs/2026-09-11-sp9f-bandeja-novedades-despacho-huevo-pwa-design.md)
sigue el mismo criterio. Antes del registro estaba en
`docs/proyecto/arquitectura.md` §9.

## Decisión

Los pedidos de alimento y los despachos de huevo solo funcionan con conexión:
sin cola offline ni IndexedDB. El modo offline queda limitado a la recogida y la
mortalidad.

## Motivo

PENDIENTE: lo completa el humano.

## Alternativas descartadas

- Cola offline también para pedidos y despachos.

## Reconsiderar cuándo

- Si los usuarios de galpones sin cobertura necesitan registrar pedidos o
  despachos sin red.
