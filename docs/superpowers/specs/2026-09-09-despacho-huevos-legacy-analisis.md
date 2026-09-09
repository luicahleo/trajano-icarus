# Despacho de huevos — análisis del ICARUS legacy

Investigación previa al brainstorming de la nueva funcionalidad de envío de
huevos del productor hacia CAISY. No es un diseño: es la memoria de lo que
existe en `c:\Users\lrcahuana\source\repos\dev\ICARUS`, para no perder el
contexto entre sesiones. El diseño real vive en un spec posterior, cuando el
brainstorming esté cerrado.

## Nombre en el legacy

El flujo se llama **"Despacho de Huevo"** (`DespachoHuevo`), no "EnvioHuevos".
Vive en un módulo separado llamado **ContabilidadAvicola**, distinto de
`GestionAvicola` (donde está la recolección diaria). La empresa central se
llama **CAICI** en el código legacy (verificar si es el mismo ente que CAISY
en la nomenclatura nueva, o un nombre a corregir).

## Entidades y enums (namespace `ICARUS.Domain.Entities.GestionAvicola`)

- **`DespachoHuevo`** (agregado raíz): `GestorAvicolaId`, `ClienteId`
  (desnormalizado "para optimizar consultas"), `FechaDespacho`, `TotalAmarres`,
  `TotalHuevos`, `TotalBs`, `Estado` (`EstadoDespacho`), `Observaciones`,
  `ReciboFotoUrl`. Constante `HUEVOS_POR_AMARRA = 180`.
- **`DetalleDespachoHuevo`**: línea por tamaño de huevo — `Tamano`
  (`TamanoHuevo`), `CantidadAmarres`, `CantidadHuevos` (calculado),
  `PrecioUnitario`, `PrecioProductor`, `Subtotal`. Índice único
  `(DespachoHuevoId, Tamano)`.
- **`PrecioHuevo`**: precio vigente por tamaño — `PrecioUnitario`, `Servicio`
  (comisión), `PrecioProductor = PrecioUnitario - Servicio`, `FechaEfectiva`,
  `Activo`, FK opcional a `PublicacionPrecioHuevoId`.
- **`PublicacionPrecioHuevo`**: publicación de precios emitida por CAICI, con
  6 precios unitarios (uno por tamaño) + un `Servicio` único y sus 6
  `PrecioProductor*` calculados. Solo una puede tener `EsVigente = true`.
  `FechaPublicacion`, `FechaVigencia`, `FechaFinVigencia`.
- **`GestorAvicola`**: en este legacy **no existe una entidad "Granja"
  separada** — `GestorAvicola` cumple ese rol (1 por cliente), con
  `Galpones` y `DespachosHuevo`. Difiere del nuevo proyecto, donde `Granja` y
  `Galpon` ya están separadas.
- **`BalanceCuenta`**: resumen semanal de ingresos (despachos) vs. egresos
  (pedidos de alimento).

```csharp
public enum TamanoHuevo { Extra=0, Primera=1, Segunda=2, Tercera=3, Cuarta=4, Quinta=5 }
public enum EstadoDespacho { Preparado=0, Despachado=1, Verificado=2 }
```

## Máquina de estados

`Preparado → Despachado → Verificado` (sin "Borrador" explícito; `Preparado`
cumple ese rol).

1. **Creación (Preparado)**: valida fecha no futura y al menos un detalle con
   cantidad. Busca precios activos por tamaño; si no hay precio configurado
   para un tamaño, ese detalle se **descarta silenciosamente** (solo warning
   en log). Si no queda ningún detalle válido, falla. Calcula totales
   (`TotalAmarres`, `TotalHuevos`, `TotalBs`) sumando detalles activos.
2. **Edición (solo en Preparado)**: borra todos los detalles y los recrea con
   los precios activos **actuales**, no con los que existían al crear el
   despacho — pérdida de trazabilidad histórica si el precio cambió entre
   creación y edición.
3. **Marcar como Despachado**: exige **foto obligatoria** del recibo firmado
   en Base64, solo desde estado `Preparado`. La imagen se comprime/redimensiona
   (ImageSharp, máx. 800px de ancho, JPEG calidad 80%) y se guarda en disco en
   `wwwroot/fotos/recibos/recibos_despacho/{clienteId}/{trabajadorId}/...`.
   Requiere al menos un detalle activo con cantidad. Hay una operación
   separada para reemplazar la foto (`ActualizarFotoRecibo`), solo en estado
   `Despachado`, que borra el archivo anterior antes de guardar el nuevo.
   **Bug/gap detectado**: el endpoint API sí exige la foto; el controller Web
   MVC llama al mismo comando **sin pasar foto**, por lo que ese botón en la
   Web parece roto o fue reemplazado por el flujo móvil — confirmar en
   runtime si aplica.
4. **Verificar**: solo permitido desde `Despachado`. Es una confirmación
   manual del productor de que coincide con el extracto de CAICI — no hay
   integración automática con un extracto real.
5. **Eliminar**: soft delete (`EstaActivo = false`), solo en `Preparado`.

**No hay descuento de inventario ni de stock.** `DespachoHuevo` no toca
`GestorAvicola.ContadorHuevos` ni ningún registro de producción — es
puramente un registro contable de la salida física de huevos, desconectado en
código de `RegistroProduccionDiario`. No valida que lo despachado exista como
producción recolectada.

## Cálculo de negocio y unidades de empaque

- Unidad de empaque del despacho: **amarre = 180 huevos** (constante
  hardcodeada). Distinta de la unidad del registro diario de producción,
  **maple = 30 huevos** (`CantidadMaples * 30 + UnidadesIncompletas`). Es
  decir, el legacy mezcla dos unidades de empaque en el mismo dominio: 1
  amarre = 6 maples.
- `CantidadHuevos = CantidadAmarres * 180`; `Subtotal = CantidadHuevos *
  PrecioProductor` — el subtotal usa siempre el precio al productor, nunca el
  precio unitario (que queda solo como referencia histórica en el detalle).
- `PrecioProductor = PrecioUnitario - Servicio` (comisión que cobra CAICI),
  calculado tanto en `PublicacionPrecioHuevo` como replicado en `PrecioHuevo`.

## Capas (legacy)

- **Domain**: `DespachoHuevo.cs`, `DetalleDespachoHuevo.cs`, `PrecioHuevo.cs`,
  `PublicacionPrecioHuevo.cs`, `ContabilidadEnums.cs`. La máquina de estados y
  los cálculos viven en la entidad (`MarcarDespachado`, `Verificar`,
  `ActualizarReciboFoto`, `CalcularTotales`, `AgregarDetalle`,
  `CalcularSubtotal`).
- **Application** (`Features/ContabilidadAvicola/`): comandos MediatR
  (`CreateDespachoHuevoCommand`, `UpdateDespachoHuevoCommand`,
  `MarcarDespachadoCommand`, `ActualizarFotoReciboCommand`,
  `VerificarDespachoCommand`, `DeleteDespachoHuevoCommand`) y sus handlers,
  incluida la compresión de imágenes (mezclada en el handler, no en un
  servicio de infraestructura — deuda técnica a no repetir). Queries para
  listados paginados, detalle, precios activos, resumen semanal y export a
  Excel. **Sin FluentValidation dedicado** para estos comandos (a diferencia
  de `RegistroProduccionDiario`, que sí lo tiene): las validaciones están
  inline en handlers y entidad.
- **Infrastructure**: `DespachoHuevoConfiguration.cs` (EF Fluent config),
  `IDespachoHuevoRepository`. Tablas creadas en una sola migración
  (`AddContabilidadAvicolaTables`) junto con `BalanceCuenta`,
  `PedidoAlimento`, `PublicacionesPreciosAlimento`,
  `PublicacionesPreciosHuevo`, `DetallePedidoAlimento`, `PrecioAlimento`,
  `PrecioHuevo`. FK `GestorAvicola → DespachoHuevo` con
  `DeleteBehavior.Restrict`; `DespachoHuevo → Detalles` con `Cascade`.
- **API móvil**: `DespachoHuevoController` bajo
  `api/mobile/contabilidad/despachos`, `[Authorize(Roles = "Trabajador")]`.
  `ClienteId`/`GestorAvicolaId`/`TrabajadorId` se obtienen de claims del JWT,
  nunca de query params.
- **Web MVC**: `ContabilidadController` (1896 líneas, mezcla Pedidos de
  Alimento, Publicaciones de Precios, Balance y Despachos). Sección Despachos
  de Huevo entre líneas 653-1041. Gestión de precios/publicaciones requiere
  rol `Administrador`; las acciones de despacho no tienen restricción de rol
  adicional más allá de estar autenticado con un gestor avícola asociado.
- **Vistas** (Razor + JS vanilla): `Despachos.cshtml` (listado con filtro de
  fechas), `CrearDespacho.cshtml` (resumen "sticky" calculado en vivo por
  tamaño), `EditarDespacho.cshtml` (solo en `Preparado`),
  `DetalleDespacho.cshtml` (estado + foto de recibo), `ReportesDespachos.cshtml`
  (export a Excel).

## Detalles de negocio no obvios

- **Amarre = 180 huevos**, hardcodeado, no configurable.
- **Ciclo de pago desfasado 2 semanas**: en el cálculo del balance semanal, el
  "ingreso" de una semana dada **no** es el despacho de esa semana sino el
  despacho de **hace 2 semanas** (`semanaIngreso = semana - 2`), reflejando el
  ciclo real de pago de CAICI. El egreso de alimento, en cambio, se computa
  sin desfase, en la semana actual. Regla crítica a no perder si se migra el
  balance.
- **Fotos guardadas en disco** (`wwwroot`), no en blob storage; la Web accede
  a ellas vía un proxy HTTP que llama de vuelta a la API interna
  (`IHttpClientFactory` con cliente nombrado `"IcarusApi"`).
- **Compresión de imagen duplicada literalmente** en dos handlers distintos
  (`MarcarDespachadoCommandHandler` y `ActualizarFotoReciboCommandHandler`).
- **Sin notificaciones ni eventos de dominio** disparados por cambios de
  estado del despacho — todo es síncrono, sin colas.

## Comparación con Trajano-Icarus (estado actual, antes de esta feature)

| Legacy ICARUS | Trajano-Icarus (nuevo) |
|---|---|
| `GestorAvicola` combina el rol de "Granja" (1:1 con Cliente) | `Granja` y `Galpon` son entidades separadas |
| `RegistroProduccionDiario`: maples (30 huevos), sin concepto de descarte | `RegistroProduccion`: ya separa `TotalHuevosVendibles()` de `TotalHuevosDescarte()` |
| `DespachoHuevo`: amarres (180 huevos), totalmente desconectado del inventario/producción | No existe todavía equivalente — confirmado sin coincidencias de "DespachoHuevo" ni "envio.*huevo" en el código nuevo |

Preguntas abiertas para el diseño nuevo (a resolver en el brainstorming, no
aquí):

- ¿El nuevo envío de huevos debería descontar contra los huevos vendibles ya
  recolectados por galpón/granja (mejora real de negocio sobre el legacy, que
  no validaba nada contra inventario)?
- ¿Debería permitir declarar también huevos de descarte enviados, dado que el
  nuevo dominio ya diferencia descarte de vendibles?
- ¿Se mantiene la dualidad maple/amarre o se unifica la unidad de empaque?
- ¿Se replica el desfase de 2 semanas en el cálculo de balance, o se
  simplifica?
