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
| Gestor CAISY | Usuario global de oficina, sin tenant, con funcionalidades CAISY explícitas. SP8 incorpora `GestorPedidoAlimento`; no equivale a Administrador de plataforma. |

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

## Pedidos de alimento (SP8)

Definidos en
`docs/superpowers/specs/2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md`.

| Término | Definición |
|---|---|
| Pedido de alimento | Solicitud compartida del tenant que Cliente o Trabajador con `PedidoAlimento` prepara como borrador y envía a CAISY. Solo el borrador se edita o se borra lógicamente. |
| Notificación de Precios de Alimentos | Publicación global de CAISY con vigencia desde una fecha y un precio final por cada tipo/presentación. Sigue vigente hasta la entrada en vigor de otra publicación. |
| Precio final por 40 kg | Unidad canónica del precio, tanto para bolsa como para granel. Incluye aporte CAISY, fondo y servicios; se congela al enviar el pedido. |
| Bolsa de alimento | Presentación cerrada de 40 kg. Se solicita en número entero de bolsas. |
| Alimento a granel | Presentación solicitada en toneladas enteras. Un pedido exige al menos 2 t por tipo y 6 t en total; una tonelada equivale a 25 unidades de 40 kg. |
| Devolución para corrección | Decisión no terminal de CAISY que devuelve el mismo pedido a `Borrador`, con motivo obligatorio, para que el tenant lo corrija y reenvíe. |
| Cupo semanal | Límite configurable de pedidos enviados por cliente y semana ISO (3 inicial). Cuentan los pedidos salidos del borrador y no borrados; los borradores no consumen cupo y la devolución más reenvío del mismo pedido no lo vuelve a consumir. |
| Nota de entrega de alimento | Documento que deja el distribuidor. SP8 admite una nota y una entrega por pedido, con datos manuales y varias imágenes privadas de respaldo. |
| Recepción de alimento | Confirmación por línea del tenant después del despacho. Termina como `RecibidoConforme` o `RecibidoConDiferencias`; ambos estados reconocen el gasto real. |
| Balance de alimento | Consulta del tenant por rango de fechas que suma el gasto real reconocido: equivalentes realmente recibidos × `PrecioFinalPor40Kg` congelado al envío, solo de pedidos en `RecibidoConforme` o `RecibidoConDiferencias`. El precio vigente posterior y el total manual de la nota no alteran un pedido recibido. |

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
2. **Un hecho no se fecha en el futuro.** Producción, mortalidad, vacunación,
   pedido realizado, despacho, recepción y acceso ocurren en el pasado o
   presente. Una fecha de planificación o vigencia (`VigenteDesde` de precios,
   entrega estimada) sí puede ser futura porque aún no afirma que el hecho
   ocurrió. La validación es de dominio, no solo de interfaz.
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

La planificación del alimento que debe suministrarse a las aves, los despachos
de huevos y otros precios ajenos al pedido se definirán en subproyectos futuros.
