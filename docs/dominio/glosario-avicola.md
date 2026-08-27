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
| Cliente (granjero) | El tenant del sistema. Granjero afiliado a CAISY. Puede registrar cualquier dato de su granja. |
| Trabajador recolector | Trabajador del cliente encargado de la recolección de huevos: **la recolección la registra él**, no el cliente (aunque el cliente también puede). Usa Icarus solo con las funcionalidades que el cliente le asigna (entitlement por funcionalidad); nunca tiene acceso al resto de lo que ve el cliente. |
| Permisos operativos del trabajador | El cliente con `GestionAvicola` administra la estructura de su única granja y opera todo el módulo. El trabajador solo recibe `ProduccionHuevos` y/o `Mortalidad`; esas funcionalidades le conceden lectura estructural implícita de la granja y sus galpones, pero nunca administración de estructura ni ajuste manual de inventario. Los permisos son efectivos únicamente mientras cliente y trabajador estén activos y el módulo siga habilitado. |

## Entidades de Gestión avícola

Definidas en el spec del subproyecto 5
(`docs/superpowers/specs/2026-08-17-sp5-gestion-avicola-granjas-galpones-design.md`).

| Término | Definición |
|---|---|
| Granja | La explotación avícola del cliente. **Un cliente tiene una sola granja activa**: las granjas reales son muy grandes y agrupan todos los galpones. En el legacy se llamaba `GestorAvicola` y arrastraba contadores derivados; es un error, la entidad es `Granja` y es limpia. |
| Galpón | Nave dentro de la granja que alberga **un lote** de gallinas ponedoras. Tiene capacidad máxima e inventario de gallinas vivas (`0 ≤ GallinasActuales ≤ CapacidadMaxima`). |
| Lote | Grupo de gallinas que puebla un galpón. No hay lotes mezclados en un galpón. La `FechaNacimientoLote` es la fecha en que se pobló el galpón. |

## Vacunación (SP7)

Definida en el spec del subproyecto 7
(`docs/superpowers/specs/2026-08-19-sp7-vacunacion-design.md`).

| Término | Definición |
|---|---|
| Programa de vacunación | Plan sanitario por edad del lote, emitido por CAISY. **Catálogo global** (sin tenant): lo sube el Administrador de plataforma y cada cliente lo asigna a sus galpones. No es solo vacunas: incluye manejos (paracetamol, recorte de pico, desparasitación, traslado). Un programa desactivado no es asignable y deja de ser vigente donde estaba: se desactivan sus tareas pendientes en todos los galpones (el historial completado/cancelado se conserva). |
| Ítem de plan | Una fila del cronograma: `EdadDia` (obligatoria, > 0, única por programa), `Vacuna` (texto libre: vacuna o manejo), `ModoAplicacion`, observaciones y `Fecha` (la fecha programada de la fila del Excel). |
| Asignación de plan | El cliente asigna un programa a un galpón: se materializa una tarea por ítem con `FechaProgramada = Fecha del ítem` (si el Excel la traía) o `FechaNacimientoLote + EdadDia` en su defecto. Al reasignar, las pendientes del plan anterior se desactivan y las completadas/canceladas quedan como historial. Nunca se borra físicamente. |
| Tarea de vacunación | La ejecución de un ítem sobre un galpón. Estados: `Pendiente` → `Completada` o `Cancelada`. Completar registra `FechaAplicacion` (informada por el usuario, nunca futura; por defecto hoy), aves vacunadas y quién la registró. Cancelar es decisión solo del cliente, con motivo opcional. No hay reprogramación individual. |
| Notificación de vacunación | Consulta de pendientes del tenant: vencidas y del día (`FechaProgramada <= hoy`) más próximas (7 días). Es el valor central de la feature: indica al trabajador qué toca vacunar. No hay jobs ni push. |

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

## Reglas de producción y mortalidad (SP6)

Validadas con el usuario el 2026-08-17 y 2026-08-18. Implementadas en el
subproyecto 6 (`docs/superpowers/specs/2026-08-18-sp6-produccion-mortalidad-design.md`).

1. **Eficiencia diaria por galpón** = huevos vendibles del día ÷ gallinas
   vivas del galpón. Se calcula al consultarla (nunca se persiste), con la
   población congelada por día: cada recogida y cada mortalidad guarda un
   **snapshot de gallinas vivas** en ese momento, y la eficiencia del día usa
   el último evento del día.
2. **Recogidas, no turnos.** El trabajador recoge cuando puede a lo largo del
   día (la gallina no tiene horario de producción): una o varias **recogidas**
   por galpón y día, cada una con su hora. El total del día es la suma de las
   recogidas.
3. **Quién registra**: la recolección la registra el **trabajador** (rol
   Trabajador con la funcionalidad asignada, p. ej. ProduccionHuevos); el
   cliente también puede registrar, pero el caso habitual es el trabajador. El
   trabajador solo accede a sus funcionalidades asignadas, nunca al resto de lo
   que ve el cliente.
4. **Ventana del día**: producción y mortalidad solo se registran con fecha de
   hoy. Mientras sea hoy, el registro se puede corregir (editar o desactivar).
   Pasada la medianoche, el día queda **sellado**: prohibido editar un registro
   pasado para agregar producción o mortalidad olvidada, porque distorsionaría
   la eficiencia histórica.
5. **Umbral de descarte de lote: 70 %.** Si la eficiencia de un galpón cae bajo
   ese umbral, el lote se considera para descarte y posterior venta como carne.
   Es una métrica derivada, no un estado persistido.
6. **Los huevos de descarte no cuentan para la eficiencia** ni entran en la
   contabilidad de huevos vendibles; se registran aparte, con el mismo conteo
   que el huevo bueno (maples y unidades sueltas por recogida).
7. **Idempotencia**: cada recogida o mortalidad acepta una `IdempotencyKey`
   generada por el cliente, para que los reintentos de la PWA offline no
   dupliquen registros.

## Pendiente

Las entidades de alimentación, despachos y precios se definen al migrar cada
bounded context, en los subproyectos siguientes (producción y mortalidad en
SP6, vacunación en SP7). Orden orientativo: SP8 alimentación → SP9 despachos →
SP10 precios. Cada subproyecto confirma su alcance en su propio spec y amplía
este documento ahí; no se anticipa acá.
