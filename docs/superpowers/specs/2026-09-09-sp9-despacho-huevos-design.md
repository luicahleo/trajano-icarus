# SP9 — Despacho de huevos a CAISY

Diseño validado mediante brainstorming con el usuario el 2026-09-09, después
de revisar el ICARUS legacy (documentado en
`docs/superpowers/specs/2026-09-09-despacho-huevos-legacy-analisis.md`), la
arquitectura ya implementada de SP8 (pedidos de alimento) como patrón a
replicar, el glosario de dominio y tres documentos reales: una nota de
entrega física del productor a CAISY, una notificación de cambio de precio de
huevo (Excel) emitida por CAISY, y una boleta de recepción de huevos del
sistema contable interno de CAISY.

## Objetivo

Permitir que un cliente o un trabajador autorizado registre y envíe desde la
PWA el despacho de los huevos ya recolectados y empacados hacia CAISY, y que
una persona de CAISY con la funcionalidad `GestorRecepcionHuevos` confirme la
recepción desde Trajano.GestorCaisy — la misma aplicación de oficina que ya
usa `GestorPedidoAlimento`, pero con una funcionalidad y una cuenta CAISY
distintas. El monto recibido se convierte en crédito del cliente, visible
como referencia frente a sus pedidos de alimento.

Es el flujo inverso a SP8: aquí el tenant envía a CAISY (no al revés), y es
CAISY quien confirma recepción (no el tenant).

## Aprendizajes del sistema anterior

El legacy ("Despacho de Huevo", en el módulo ContabilidadAvicola) aporta
vocabulario y varias reglas útiles, documentadas en detalle en el análisis
legacy. No se migra literalmente:

- Nombraba a la cooperativa "CAICI"; el nombre correcto, confirmado por el
  glosario de dominio, es **CAISY**.
- Usaba "amarre" (180 huevos) como unidad de despacho, ya vigente en el
  glosario de Trajano-Icarus como **"amarra"**.
- Máquina de estados `Preparado → Despachado → Verificado`, sin descuento de
  inventario ni cruce con la producción diaria — se mantiene la ausencia de
  cruce con inventario (decisión explícita, ver más abajo), pero se
  renombra a `Borrador → Despachado → Recibido` y el paso final pasa a ser
  responsabilidad de CAISY, no una autoverificación del productor.
- El precio se modelaba como `PrecioUnitario - Servicio = PrecioProductor`.
  La notificación real de CAISY confirma esta relación pero desde el otro
  origen: CAISY define **Precio Al Productor** y **Servicio** por
  publicación, y `Precio Unitario` es el valor derivado solo para mostrar.
  SP9 adopta el origen real: precio al productor y servicio son los campos
  que carga `GestorRecepcion`.
- El balance semanal del legacy reconocía el ingreso de una semana como el
  despacho de **dos semanas antes**. SP9 conserva ese desfase para el
  crédito disponible.
- La compresión de imagen duplicada en dos handlers y la falta de
  `FluentValidation` dedicado no se repiten: SP9 sigue el patrón ya limpio
  de SP8 (compresión client-side reutilizada, validators explícitos).

## Alcance y límites

SP9 incluye:

- catálogo y publicación de precios de huevo por tamaño, con `Servicio` único
  por publicación e importación desde Excel;
- creación, edición y borrado lógico de despachos en estado Borrador;
- envío del despacho con congelamiento de precio y evidencia fotográfica de
  la nota de entrega;
- confirmación de recepción por `GestorRecepcion`, con generación de un
  recibo imprimible/reimprimible en PDF y notificación interna al emisor;
- crédito disponible por despacho recibido, con desfase de dos semanas;
- advertencia (no bloqueante) al cliente y notificación a `GestorRecepcion`
  cuando el saldo proyectado de crédito queda negativo al enviar un pedido de
  alimento;
- notificaciones internas persistentes, siguiendo el mecanismo ya construido
  en SP8.

Queda fuera de esta primera versión:

- bloqueo real del envío de pedidos de alimento por crédito insuficiente;
- desglose del despacho por galpón (queda a nivel granja, como el legacy);
- recuento o registro de diferencias por línea en la recepción;
- transportista como actor o cuenta del sistema;
- cálculo del "Servicio Logístico" tal como aparece en la boleta interna de
  CAISY (nuestro monto congelado ya es neto, sin ese paso adicional);
- conciliación automática con el sistema contable externo de CAISY
  (DEPHUEVO / boleta de recepción de huevos);
- edición del despacho después de Despachado.

## Vocabulario

| Término | Significado |
|---|---|
| Amarra | Unidad de despacho a CAISY. 180 huevos (6 maples). Ya definida en el glosario de dominio. |
| Despacho de huevo | Registro que el cliente o trabajador envía a CAISY con lo que el transportista se lleva de la granja, por tamaño de huevo. |
| Nota de entrega | Documento (foto) que el trabajador emite y sube como constancia al despachar; el transportista la verifica visualmente, sin recuento formal en el sistema. |
| Publicación de precio de huevo | Emitida por `GestorRecepcion`: precio al productor y servicio vigentes por tamaño. Solo una vigente a la vez. |
| GestorRecepcion | Funcionalidad de una cuenta CAISY en Trajano.GestorCaisy, análoga a `GestorPedidoAlimento` pero para huevos: publica precios y confirma recepciones. |
| Crédito por despacho | Monto que un despacho `Recibido` aporta al saldo del cliente, disponible recién dos semanas después de la recepción. |

## Dominio

**`DespachoHuevo`** (agregado raíz, `Icarus.GestionAvicola.Domain`), asociado
a la Granja del cliente (no al galpón).

Propiedades: `ClienteId`, `GranjaId`, `FechaDespacho`, `Estado`
(`EstadoDespachoHuevo`), `TotalAmarras`, `TotalHuevos`, `TotalBs`,
`DocumentoNotaEntregaId`, `Historial` (`IReadOnlyList<TransicionDespachoHuevo>`),
`Version` (rowversion, mismo patrón de concurrencia optimista que
`PedidoAlimento`).

Métodos explícitos, sin setters públicos, cada uno con guard de estado y
registro de transición (mismo patrón que `PedidoAlimento`):

- `AgregarDetalle` / `QuitarDetalle` / `ReemplazarDetalles` — solo en
  Borrador.
- `Despachar(precios, documentoNotaEntregaId)` — válida que exista al menos
  un detalle con cantidad y que haya precio vigente para cada tamaño usado
  (todo o nada, igual que `EnviarACaisy`); congela `PrecioProductor` por
  línea; exige el id del documento de evidencia ya guardado; pasa a
  Despachado.
- `ConfirmarRecepcion(actorId)` — solo desde Despachado; pasa a Recibido. No
  recibe cantidades ni ajustes: el monto ya congelado al despachar es el
  monto final.
- `Desactivar()` — solo en Borrador (borrado lógico).

**`DetalleDespachoHuevo`** (`Entity`): `Tamano` (`TamanoHuevo`, reutilizado
del vocabulario legacy: Extra, Primera, Segunda, Tercera, Cuarta, Quinta),
`CantidadAmarras` (entero), `UnidadesSueltas` (0-179, huevos que no completan
una amarra), `CantidadHuevos` (calculado: `CantidadAmarras * 180 +
UnidadesSueltas`), `PrecioProductorCongelado` (nulo en Borrador, fijado al
Despachar), `Subtotal` (calculado). Índice único `(DespachoHuevoId, Tamano)`.

**`EstadoDespachoHuevo`** (enum estable, nunca renumerar):
`Borrador = 0, Despachado = 1, Recibido = 2`.

**`TransicionDespachoHuevo`**: fila de historial inmutable — origen, destino,
`ActorId`, `FechaUtc` (mismo patrón que `TransicionPedidoAlimento`; sin
motivo obligatorio porque no hay devolución ni rechazo en este flujo).

**`DocumentoNotaEntrega`**: foto de la nota de entrega física, subida por el
trabajador/cliente al despachar. Reutiliza el patrón de almacenamiento de
documentos privados ya construido para `EntregaPedidoAlimento`
(`IAlmacenDocumentosPedido`, generalizado o extendido con un nuevo tipo de
documento).

**`PublicacionPrecioHuevo`** (catálogo global, sin tenant): por tamaño,
`PrecioAlProductor` [Bs.] (input directo de `GestorRecepcion`); un único
`Servicio` [Bs.] por publicación (no por tamaño); `PrecioUnitario` calculado
= `PrecioAlProductor + Servicio`, solo para mostrar. `FechaPublicacion`,
`FechaVigencia`. Solo una publicación vigente a la vez (`EsVigente`); al
publicar una nueva, la anterior deja de estar vigente.

Sin cruce con `RegistroProduccion`: el despacho no descuenta ni valida contra
huevos vendibles recolectados, igual que el legacy — la producción diaria
sigue existiendo únicamente para calcular eficiencia.

## Precios: captura y congelamiento

`GestorRecepcion` publica `PublicacionPrecioHuevo` desde Trajano.GestorCaisy,
manualmente o importando el mismo formato Excel de la notificación real
("CAMBIO DE PRECIO DE HUEVOS": fecha de notificación, fecha de vigencia, y
una fila por tamaño con precio al productor y servicio), reutilizando la
infraestructura de importación ya construida para precios de alimento
(`docs/superpowers/specs/2026-09-05-importacion-excel-precios-alimento-design.md`).

La PWA muestra el precio vigente por tamaño al armar el despacho, para que el
trabajador sepa cuánto vale antes de enviarlo. Al `Despachar`, cada línea con
cantidad congela el `PrecioAlProductor` vigente de ese tamaño en ese momento;
el total del despacho es la suma de `CantidadHuevos * PrecioProductorCongelado`
por línea. Ese monto es directamente el abono al productor: no se resta un
"servicio logístico" aparte, porque el servicio ya está descontado en el
precio al productor congelado.

## Flujo de recepción y notificaciones

`GestorRecepcion` ve en su bandeja los despachos en estado Despachado y
confirma la recepción (`ConfirmarRecepcion`). Al confirmar:

1. El despacho pasa a Recibido.
2. Se genera un recibo en PDF (reutilizando el patrón `ReciboPedidoAlimento`
   / QuestPDF), imprimible y reimprimible desde Trajano.GestorCaisy.
3. Se registra una `NotificacionInterna` para el cliente emisor (mismo
   mecanismo ya construido en SP8: alcance por `ClienteId`, persistida en la
   misma transacción, sondeo con ETag desde la PWA).
4. El cliente ve la notificación y su saldo de crédito actualizado.

No hay recuento por línea ni estado de diferencias: se confirma tal cual se
declaró al despachar, porque el transportista solo verifica visualmente y no
hay reconteo formal en el sistema (a diferencia de la recepción de alimento
en SP8, donde el tenant sí reporta diferencias por línea).

## Crédito y su relación con pedidos de alimento

Cada despacho `Recibido` aporta su `TotalBs` como crédito, disponible recién
**dos semanas después** de la fecha de recepción (mismo desfase que el
balance semanal del legacy). El saldo se calcula por consulta (suma de
créditos ya disponibles menos lo ya consumido), sin tabla de saldo
persistida — evita duplicar estado que puede derivarse.

Al enviar un pedido de alimento (SP8), si el saldo proyectado del cliente
queda negativo, no se bloquea el envío: se muestra una advertencia al
cliente y se dispara una `NotificacionInterna` a `GestorRecepcion` (alcance
CAISY, `ClienteId = null`) para que decida si actúa. Este es el único punto
de integración entre SP9 y el pedido de alimento existente de SP8.

## Capas técnicas

Sigue el mismo patrón por capas que SP8, referenciado en detalle en el
análisis de arquitectura de PedidoAlimento (memoria de esta sesión):

- **Domain**: entidades descritas arriba en
  `Icarus.GestionAvicola.Domain`.
- **Application** (`Icarus.GestionAvicola.Application/DespachosHuevo/`):
  archivo único `ComandosDespachosHuevo.cs` (records + `AbstractValidator` +
  `IRequestHandler`, mismo patrón que `ComandosPedidosAlimento.cs`),
  `PuertosDespachosHuevo.cs` (repositorio, transacción), `ReciboDespachoHuevo.cs`
  (query + `IReciboDespachoRenderer` para el PDF), y reutilización de
  `Notificaciones/` ya existente con un `TipoNotificacionDespachoHuevo`
  nuevo.
- **Infrastructure**: `ConfiguracionDespachoHuevo.cs` y configs EF
  relacionadas, `RepositorioDespachosHuevo.cs`, una migración nueva.
- **API** (`Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs`, minimal API):
  dos grupos — `/despachos-huevo` (tenant, requiere una funcionalidad nueva
  `DespachoHuevo` en el enum `Funcionalidades` de Clientes, bit siguiente sin
  renumerar) y `/despachos-huevo-caisy` (CAISY,
  `FuncionalidadesCaisy.GestorRecepcionHuevos`, bit siguiente sin
  renumerar). El registro de políticas es automático en el backend (itera el
  enum), como ya ocurre para `PedidoAlimento` y `GestorPedidoAlimento`.
- **Trajano.GestorCaisy**: nuevo controller (bandeja de recepciones,
  publicación de precios) protegido por una política nueva registrada a mano
  en `Program.cs` más una constante de bit en
  `ConstantesAutorizacion.cs` — ese registro no es automático en esta app, a
  diferencia del backend.
- **PWA** (`web/src/features/despacho-huevos/`): mismo patrón que
  `pedidos-alimento/` — React Query, deliberadamente **online**, sin cola
  offline ni IndexedDB, porque el despacho se completa en el momento de
  cargar el camión con conectividad disponible. Reutiliza la compresión
  client-side de imagen ya construida para subir la foto de la nota de
  entrega.
- **Tests**: unit (agregado + handlers), integración de endpoints en
  `Icarus.IntegrationTests`, integración de controller en
  `Trajano.GestorCaisy.Tests`, y verificación de que no se violan las reglas
  de `Icarus.ArchitectureTests`.

## Riesgos y preguntas abiertas para el plan

- El nombre exacto de la nueva funcionalidad tenant (`DespachoHuevo`
  propuesto) y de la funcionalidad CAISY (`GestorRecepcionHuevos` propuesto)
  se puede ajustar en el plan si hay un nombre de negocio más preciso.
- El cálculo del saldo de crédito por consulta (sin tabla persistida) debe
  revisarse en el plan para asegurar que no se vuelva costoso a medida que
  crezca el historial de despachos; si hace falta, se puede introducir un
  snapshot periódico más adelante, pero no en esta primera versión.
