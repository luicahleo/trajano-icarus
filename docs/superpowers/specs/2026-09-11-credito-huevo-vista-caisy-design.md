# Vista de CAISY del crédito de huevo por pedido

Diseño validado mediante brainstorming con el usuario el 2026-09-11, a partir
del ítem 3 del backlog priorizado de crédito de huevo (`docs/ai/HANDOFF.md`):
*"Vista de CAISY del crédito por cliente"*. Depende de SP8 (pedidos de
alimento e integración con CAISY), SP9 (despacho de huevos), SP9D (ajustes de
corrección) y SP9E (confirmación explícita de crédito insuficiente), todos ya
implementados.

## Motivación

El gestor de CAISY decide sobre los pedidos de alimento entrantes —los acepta,
los devuelve, los rechaza y registra su despacho— sin ver el crédito por
despachos de huevo del cliente que los envió. Hoy solo recibe una señal
binaria: `NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente(pedidoId)`
le avisa a su bandeja global que un envío dejó el saldo en negativo, pero no
le dice **cuánto**. Lo que falta es el número, no el aviso.

Del otro lado del mostrador, el Cliente sí ve su saldo y el desglose de
ajustes desde la feature anterior (ítem 2 del backlog).

## Principio de diseño que fija el alcance

El eje de la información financiera **no** es *tenant vs. CAISY*, sino
**quién es parte de la relación comercial**. Cliente y CAISY son las dos
partes y deben tener la misma información para poder entenderse; el
Trabajador no es parte y nunca ve montos.

Esto tiene una consecuencia concreta de diseño: CAISY ve exactamente lo mismo
que el Cliente —saldo **y** desglose de ajustes con su motivo—, no una versión
recortada. Y descarta de entrada la alternativa de embeber el crédito en
`PedidoAlimentoDetalle`: ese DTO también se sirve en la ruta del tenant,
donde lo lee un Trabajador.

## Objetivo

Que el gestor de CAISY, en la pantalla donde decide sobre un pedido, vea el
crédito del cliente de ese pedido, sin poder interpretarlo mal y sin poder
consultar el crédito de clientes arbitrarios.

## Alcance y límites

Incluye:

- Un endpoint nuevo de CAISY con alcance de pedido:
  `GET /pedidos-alimento-caisy/{id}/credito`.
- El bloque de crédito en tres pantallas de `Trajano.GestorCaisy`: el detalle
  del pedido, la pantalla de aceptación y la de despacho.
- Tres cifras explícitas (saldo disponible, monto que aporta este pedido y
  saldo sin él) más la lista de ajustes de corrección.

Excluye:

- Una columna de crédito en la bandeja paginada: implicaría N cálculos por
  página, cada uno cruzando cuatro tablas.
- Una pantalla propia de crédito por cliente con selector: CAISY hoy no tiene
  ningún selector de clientes (la bandeja los identifica con los primeros
  ocho caracteres del `ClienteId`), y habilitarla exigiría un endpoint con
  `ClienteId` libre que permitiría enumerar clientes.
- Cambios en la PWA (`web/`) y en el camino del Cliente. La feature no toca
  `ObtenerBalanceCreditoHuevoQuery`, su handler ni su gate de rol.
- El bloque en las pantallas de devolución y rechazo: el saldo no cambia la
  decisión de decir que no.
- Los ítems 4, 5 y 6 del backlog (decimales de la PWA, notificación de saldo
  negativo, ledger histórico).

## Restricción de partida: el gate de rol Cliente

`ObtenerBalanceCreditoHuevoHandler` exige `Rol == "Cliente"` y lanza
`CreditoHuevoRequiereRolClienteException` (403) para cualquier otra cuenta,
CAISY incluida. Ese gate se agregó a propósito en la feature anterior para
cerrar la brecha del Trabajador, así que no se toca: CAISY no puede reusar
`GET /despachos-huevo/credito` tal cual.

## Decisiones de diseño

### 1. Alcance de pedido, no de cliente

El query nuevo recibe el `PedidoId`, no el `ClienteId`. El cliente se deriva
del pedido dentro del handler.

Ventajas sobre un `?clienteId=` libre:

- No hay enumeración posible de clientes desde la API.
- El parámetro que el gestor ya tiene en la mano (el pedido que está mirando)
  es el que viaja.
- Cuelga del grupo `/pedidos-alimento-caisy`, cuya política exige la
  funcionalidad correcta.

### 2. La funcionalidad correcta es `GestorPedidoAlimento`

Las funcionalidades de CAISY son componibles: `RequisitoFuncionalidadCaisy`
exige rol `GestorCaisy` **más** el flag concreto. Colgar el crédito de
`/despachos-huevo-caisy` exigiría `GestorRecepcionHuevos`, que el gestor de
pedidos puede no tener. El caso de uso es decidir sobre un pedido de
alimento, así que la funcionalidad es `GestorPedidoAlimento`.

### 3. Sin gate de rol dentro del handler

Los handlers de CAISY existentes (`ListarPedidosCaisyHandler` y compañía) no
verifican rol: confían en la política del endpoint. El gate del lado Cliente
existe porque la política del tenant **no distingue** Cliente de Trabajador;
la política de CAISY sí distingue con precisión. Duplicar la verificación
daría una simetría falsa.

Costo asumido y explícito: si en el futuro alguien mapeara este query en una
ruta de tenant, no habría red de seguridad en el handler. Se cubre con un
test de integración que prueba el 403 en la ruta de CAISY, no con un `if`
redundante.

### 4. La aritmética por estado vive en el backend

El saldo que devuelve el cálculo **ya tiene descontado el pedido abierto**:
`comprometidoPendiente` resta los pedidos en `Solicitado`, `Aceptado` y
`Despachado`. Un gestor que lea "saldo disponible" sobre un pedido solicitado
está viendo el saldo *después* de ese pedido, no antes. Por eso la vista
muestra tres cifras y no una.

Cuánto pesa el pedido depende de su estado:

| Estado del pedido | Cómo entra en el cálculo | `MontoDelPedido` |
|---|---|---|
| `Solicitado`, `Aceptado`, `Despachado` | `comprometidoPendiente` | suma de `SubtotalSolicitado` no nulos |
| `RecibidoConforme`, `RecibidoConDiferencias` | `recibidoReal` | `Recepcion.TotalRecibido` |
| `Borrador`, `Rechazado` | no entra | `0` |

Los dos componentes se restan en el cálculo, así que
`SaldoSinEstePedido = SaldoDisponible + MontoDelPedido` vale para ambas ramas.

Esta tabla se resuelve en el handler, no en una vista Razor: es una regla de
negocio y se prueba una vez.

### 5. El monto comprometido no usa `TotalSolicitado`

`PedidoAlimento.TotalSolicitado` devuelve `null` si **algún**
`SubtotalSolicitado` es nulo, mientras `RepositorioBalanceCreditoHuevo`
calcula `comprometidoPendiente` con `.Where(d => d.SubtotalSolicitado != null)`
—suma los que hay—. Usar la propiedad del dominio daría un número distinto
del que el saldo realmente descontó en ese caso de borde.

El handler replica la fórmula del repositorio: suma de subtotales no nulos.
Los dos números coinciden siempre.

### 6. La falla del crédito no bloquea la decisión

Si la consulta de crédito falla (500, endpoint caído, error de red), la
pantalla del pedido carga igual con un aviso en el bloque, y Aceptar y
Despachar siguen habilitados. Es la misma postura que ya tomó SP9E: el
crédito es informativo y la decisión final es de CAISY. Una caída del cálculo
no puede paralizar el despacho.

Se degradan `ErrorApiException` y `HttpRequestException`. **No** se degrada
`TaskCanceledException`: una cancelación real del token significa que la
petición se abortó y no hay pantalla que renderizar.

## Contrato

### Application

Archivo nuevo `Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevoCaisy.cs`.
No se modifica `ComandosCreditoHuevo.cs`.

```csharp
public sealed record ObtenerCreditoHuevoDePedidoCaisyQuery(Guid PedidoId)
    : IRequest<CreditoHuevoDePedidoCaisy>;

public sealed record CreditoHuevoDePedidoCaisy(
    decimal SaldoDisponible,
    decimal MontoDelPedido,
    decimal SaldoSinEstePedido,
    bool PedidoComputadoEnElSaldo,
    IReadOnlyList<AjusteCreditoHuevoResumen> Ajustes);
```

`AjusteCreditoHuevoResumen` se reusa tal cual de la feature anterior: mismo
tipo para las dos partes, que es justo el principio de diseño de arriba.

El handler depende de `IRepositorioPedidosAlimento` (para
`ObtenerPorIdAsync`, que ya incluye `Detalles` y `Recepcion`) y de
`IRepositorioBalanceCreditoHuevo`. Pedido inexistente →
`NotFoundException("Pedido de alimento", id)`, igual que el detalle.

Ningún puerto ni repositorio cambia: `ObtenerSaldoDisponibleAsync` y
`ObtenerAjustesAsync` ya reciben el `clienteId` explícito, y
`GestionAvicolaDbContext` no filtra por tenant cuando la cuenta no tiene
`ClienteId` (caso CAISY).

### Host

```csharp
caisy.MapGet("/{id:guid}/credito", async (Guid id, ISender mediator, CancellationToken ct) =>
    Results.Ok(await mediator.Send(new ObtenerCreditoHuevoDePedidoCaisyQuery(id), ct)));
```

Dentro del grupo `/pedidos-alimento-caisy` ya existente, sin política propia.

### Trajano.GestorCaisy

- `ContratosApi.cs`: `CreditoHuevoPedidoApi` y `AjusteCreditoHuevoApi`, espejo
  del DTO.
- `IApiIcarusClient.ObtenerCreditoDePedidoAsync(Guid id, CancellationToken)`,
  que lanza `ErrorApiException` como el resto de las operaciones: la
  degradación es decisión del controller, no del cliente HTTP.
- `IFormularioConCredito { CreditoHuevoPedidoApi? Credito { get; set; } }`,
  implementada por `FormularioEntregaVista` y `FormularioDespachoVista` con
  `[BindNever]`. No se envuelven los formularios en un modelo contenedor
  porque cambiaría el prefijo de los campos del POST y rompería el binding y
  los tests existentes. Hay precedente de dato de solo referencia dentro de un
  formulario: `FormularioDespachoVista.Lineas` ya lleva `CantidadSolicitada`.
- `VistaPedidoDetalle` gana la propiedad `Credito`.
- Partial `Views/Pedidos/_CreditoHuevo.cshtml`, modelo `CreditoHuevoPedidoApi?`.

## Presentación

El bloque muestra, con formato `"0.00"` (el que ya usa la app MVC para
montos):

- **Saldo disponible** del cliente.
- **Este pedido** y **saldo sin este pedido**, solo si
  `PedidoComputadoEnElSaldo` es `true`. Si es `false` (borrador o rechazado)
  se omiten las dos cifras y se aclara que el pedido no pesa en el saldo.
- La **lista de ajustes** con monto, motivo y fecha, cuando no está vacía.
- Si el modelo es `null`: "Crédito no disponible por ahora" y la aclaración
  de que la decisión sigue habilitada.

Un saldo negativo no se distingue solo por color: lleva prefijo textual
explícito. La accesibilidad del color es el ítem 4 del backlog para la PWA;
esta pantalla no nace con el mismo defecto.

Anti-PII: no se registran montos ni motivos en los logs, igual que ya hace
`PedidosController` con los motivos de decisión.

## Pruebas

1. **Unitarias del handler** (`Icarus.UnitTests`): una por rama de la tabla de
   estados, el saldo sin el pedido, los ajustes pasados tal cual y el
   `NotFoundException`.
2. **Integración del endpoint** (`PedidosAlimentoEndpointsTests`): 200 para
   una cuenta CAISY con `GestorPedidoAlimento`, 403 para Cliente y para
   Trabajador, 401 sin token, 404 con pedido inexistente.
3. **`ApiIcarusClientTests`**: la ruta pedida es
   `pedidos-alimento-caisy/{id}/credito`.
4. **`ApiIcarusFalsa`**: `CreditoDePedido`, `ErrorDeObtenerCredito`,
   `VecesObtenerCredito`, `UltimoCreditoPedido`.
5. **`PedidosControllerTests`**: las tres pantallas llevan el crédito al
   modelo; con la API caída el modelo trae `Credito == null` y la acción
   sigue devolviendo `ViewResult`; los re-render por error de validación
   también lo rellenan.
6. **`FlujoPedidosTests`**: el HTML del detalle contiene el bloque con las
   tres cifras.

## Verificación

`./verify.ps1` completo antes del commit, con Docker corriendo (los tests de
integración usan Testcontainers.MsSql).
