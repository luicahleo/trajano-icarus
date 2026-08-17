# Glosario del dominio avícola

Vocabulario y reglas de negocio de Trajano-Icarus. Es conocimiento del negocio,
no convención de estilo: las convenciones viven en `AGENTS.md` y en la puerta de
calidad.

Consultar este documento **antes de nombrar una entidad o inventar una regla**.
Los identificadores de dominio van en español, igual que el resto del proyecto.

## Módulos

| Módulo | Alcance |
|---|---|
| Control de acceso | Trabajadores, zonas, registros biométricos, entradas y salidas |
| Gestión avícola | Granjas, galpones, producción de huevos, mortalidad, vacunación, alimentación, despachos, precios |

## Actores

| Término | Definición |
|---|---|
| CAISY | Cooperativa Agropecuaria San Juan de Yapacaní. La cooperativa a la que el granjero vende huevos y compra alimento. En el código legacy aparece como "CAICI": es un error, el nombre correcto es CAISY. |
| Cliente (granjero) | El tenant del sistema. Granjero afiliado a CAISY. |

## Entidades de Gestión avícola

Definidas en el spec del subproyecto 5
(`docs/superpowers/specs/2026-08-17-sp5-gestion-avicola-granjas-galpones-design.md`).

| Término | Definición |
|---|---|
| Granja | La explotación avícola del cliente. **Un cliente tiene una sola granja activa**: las granjas reales son muy grandes y agrupan todos los galpones. En el legacy se llamaba `GestorAvicola` y arrastraba contadores derivados; es un error, la entidad es `Granja` y es limpia. |
| Galpón | Nave dentro de la granja que alberga **un lote** de gallinas ponedoras. Tiene capacidad máxima e inventario de gallinas vivas (`0 ≤ GallinasActuales ≤ CapacidadMaxima`). |
| Lote | Grupo de gallinas que puebla un galpón. No hay lotes mezclados en un galpón. La `FechaNacimientoLote` es la fecha en que se pobló el galpón. |

## Unidades

| Término | Definición |
|---|---|
| Maple | Unidad estándar de empaque de huevos. **Un maple son 30 huevos.** |
| Unidades incompletas | Huevos sueltos que no completan un maple. Siempre menos de 30. |
| Amarra | Unidad de despacho a la cooperativa. **Una amarra son 180 huevos** (6 maples). |
| Huevo de descarte | Huevo rajado o con falta de calcio: no se vende con el resto ni cuenta para la eficiencia, pero se comercializa aparte en un mercado más barato. El legacy no lo registra; Trajano-Icarus sí (a partir de SP6). |

Cálculo del total, sin excepciones:

```
Total Huevos = (CantidadMaples * 30) + UnidadesIncompletas
```

La constante 30 pertenece al dominio y se declara una sola vez. Nunca repetirla
como número suelto en el código.

## Reglas transversales

1. **Soft delete en todas las entidades.** Nunca se hace un borrado físico: se
   marca `EstaActivo = false`. Las consultas normales filtran por `EstaActivo`.
   El motivo es trazabilidad: registros de acceso y de producción no se borran.
2. **Ninguna fecha del dominio admite futuro.** Una producción, una mortalidad,
   una vacunación o un registro de acceso ocurren en el pasado o en el presente.
   La validación es de dominio, no de interfaz.
3. **Los datos biométricos y los registros nominales de acceso son sensibles.**
   Nunca aparecen en logs, mensajes de error ni trazas. Ver la regla anti-PII en
   `AGENTS.md`.

## Reglas de producción y mortalidad (base del SP6)

Validadas con el usuario el 2026-08-17. Se implementan en el subproyecto 6; se
registran aquí porque son reglas del negocio, no del código.

1. **Eficiencia diaria por galpón** = huevos producidos del día ÷ gallinas
   vivas del galpón. La recogida la hacen los trabajadores en distintos turnos,
   así que hay varios registros de producción por galpón y día.
2. **Umbral de descarte de lote: 70 %.** Si la eficiencia de un galpón cae bajo
   ese umbral, el lote se considera para descarte y posterior venta como carne.
   Es una métrica derivada, no un estado persistido.
3. **Los huevos de descarte no cuentan para la eficiencia** ni entran en la
   contabilidad de huevos vendibles; se registran aparte.
4. **La mortalidad no es retroactiva**: si ya se registró la recogida de huevos
   de un día, no se puede editar ese registro días después para agregar
   mortalidad olvidada, porque distorsionaría la eficiencia histórica. La
   mortalidad se registra en su momento o no se registra.

## Pendiente

Las entidades de producción, mortalidad, vacunación, alimentación, despachos y
precios se definen al migrar cada bounded context, en los subproyectos 6 y
siguientes. Orden orientativo: SP6 producción + mortalidad → SP7 vacunación →
SP8 alimentación → SP9 despachos → SP10 precios. Cada subproyecto confirma su
alcance en su propio spec y amplía este documento ahí; no se anticipa acá.
