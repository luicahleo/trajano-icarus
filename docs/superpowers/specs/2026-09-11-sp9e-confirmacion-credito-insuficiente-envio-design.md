# SP9E — Confirmación explícita al enviar un pedido con crédito de huevo insuficiente

Diseño validado mediante brainstorming con el usuario el 2026-09-11, a partir
del ítem 1 del backlog priorizado de crédito de huevo
(`docs/ai/HANDOFF.md`): *"Bloquear vs. solo advertir en el envío de
pedido"*. Depende de SP9 (despacho de huevos) y SP9C (confirmar recepción y
crédito), ya implementados.

## Corrección de encuadre respecto al backlog

El backlog decía que el envío "no consulta ni bloquea" el crédito. Es
impreciso: `EnviarPedidoAlimentoHandler` **ya consulta**
`RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync` desde SP9C y, si
el saldo proyectado queda negativo, agrega una
`NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente` a la bandeja
**global de CAISY**. Lo que falta es:

- Cualquier bloqueo o freno del envío.
- Cualquier aviso al **cliente** en el momento de enviar: CAISY se entera
  después del hecho por su bandeja; el cliente nunca se entera de nada. El
  diálogo "Enviar a CAISY" (`PedidoAlimentoDetallePage.tsx`) no menciona
  crédito. El saldo solo se ve, sin comparar contra el total del pedido,
  como texto suelto en la pantalla de creación del borrador
  (`PedidoFormularioPage.tsx`).

Este documento cubre exclusivamente esa brecha: avisar al cliente y exigirle
una confirmación explícita antes de dejarlo enviar con crédito insuficiente.

## Objetivo

Cuando un pedido de alimento, al enviarse, dejaría el crédito por despachos
de huevo del cliente en negativo, el cliente debe verlo y confirmarlo
explícitamente antes de que el envío se concrete. El crédito es un límite de
negocio real (representa plata que el cliente ya generó con huevo), pero la
decisión final de aceptar o rechazar el pedido sigue siendo de CAISY —que ya
tiene esa potestad hoy al aceptar/rechazar—, así que no se bloquea el envío
sin salida: se exige un paso consciente de más.

## Alcance y límites

Incluye:

- Rechazar el primer intento de envío cuando el saldo proyectado queda
  negativo y el cliente todavía no confirmó nada, devolviendo un error
  distinguible del resto de los 409 ya existentes en este mismo endpoint.
- Aceptar el envío si el cliente ya confirmó, dejando rastro en el propio
  historial del pedido (visible para CAISY al abrir el pedido, sin depender
  de que revisen la bandeja de notificaciones aparte).
- Actualizar el diálogo "Enviar a CAISY" del frontend para mostrar el
  desglose (saldo actual, total del pedido, saldo resultante) y ofrecer
  reintentar confirmando.
- La confirmación es vinculante en el backend: el comando de envío exige el
  flag cuando corresponde. Un cliente que llame la API directo, saltándose
  el frontend, no puede evitar el rechazo.

Queda fuera de esta versión (decisiones ya descartadas en el brainstorm, o
ítems separados del mismo backlog):

- Bloqueo duro sin ninguna salida. Descartado: el crédito es un límite real,
  pero CAISY sigue siendo quien decide en última instancia al aceptar o
  rechazar el pedido — igual que hoy.
- Una válvula de escape con autorización de un tercero (p. ej. que CAISY
  preapruebe el exceso antes del envío). No se pidió y añade una decisión de
  "quién autoriza" que no hace falta para resolver este ítem.
- Desglose completo del crédito en sus 4 componentes (ingresos, recibido
  real, comprometido pendiente, ajustes) — ítem 2 del backlog, sesión
  aparte. Este documento solo expone los 3 números derivados que ya calcula
  hoy `ObtenerSaldoDisponibleAsync` más el total del pedido.
- Vista de CAISY del crédito por cliente (ítem 3), notificación proactiva de
  saldo negativo fuera del flujo de envío (ítem 5) y ledger histórico
  (ítem 6): sin relación directa con este ítem.
- Cambios a la notificación pasiva `NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente`
  que ya existe: sigue generándose exactamente igual que hoy, tanto si el
  cliente confirmó como si el chequeo nunca hizo falta (saldo ya negativo
  desde antes, no por este pedido). Este documento le agrega, no le quita.

## Diseño

### Flujo — envío normal (sin problema de crédito)

Sin cambios: el cliente entra al detalle de un pedido en borrador, hace clic
en "Enviar a CAISY", confirma en el diálogo existente, el pedido pasa a
Solicitado.

### Flujo — envío con crédito insuficiente

1. Mismo diálogo, mismo clic en "Confirmar envío". El frontend llama
   `POST /pedidos-alimento/{id}/enviar` con
   `{ "confirmarCreditoInsuficiente": false }` (valor por defecto: el cliente
   nunca decidió nada todavía).
2. El backend calcula lo que ya calcula hoy: saldo disponible del cliente y
   total del pedido a congelar. Si el resultado da negativo y el comando no
   trae la confirmación, rechaza con `409 Conflict` y un `title` propio
   (`"Crédito insuficiente"`, distinto del resto de los 409 de este mismo
   endpoint) que el frontend usa para diferenciarlo de un error genérico. No
   se persiste ni se muta nada: el pedido sigue en Borrador.
3. El frontend reconoce ese `title` específico y, en vez de mostrar el error
   genérico rojo que ya usa para cualquier otro fallo, transforma el mismo
   diálogo: pide de nuevo el saldo actual (`GET /despachos-huevo/credito`,
   endpoint que ya existe) y muestra: *"Este pedido va a dejar tu crédito en
   $(resultante) negativo (saldo actual $X, este pedido $Y). ¿Confirmás el
   envío igual?"*. El botón pasa de "Confirmar envío" a "Enviar de todas
   formas".
4. Clic ahí → se reintenta el mismo POST, ahora con
   `{ "confirmarCreditoInsuficiente": true }`.
5. El backend deja pasar el envío: el pedido pasa a Solicitado igual que
   cualquier otro. Además de la notificación pasiva que ya se genera hoy a
   la bandeja global de CAISY (sin cambios), el historial del pedido —el que
   CAISY ya ve al abrir el detalle del pedido— queda con un motivo en la
   transición Borrador→Solicitado: *"Enviado con crédito insuficiente: saldo
   $X, pedido $Y, resultante $(resultante)."*.

### Backend — contrato del comando

`EnviarPedidoAlimentoCommand` (`ComandosPedidosAlimento.cs`) gana un campo:

```csharp
public sealed record EnviarPedidoAlimentoCommand(
    Guid PedidoId, bool ConfirmarCreditoInsuficiente = false)
    : IRequest, IOperacionRegistrable
```

Endpoint `POST /pedidos-alimento/{id}/enviar`
(`PedidosAlimentoEndpoints.cs`): hoy no recibe body; pasa a aceptar un body
opcional `{ confirmarCreditoInsuficiente: boolean }` (ausente o `false` por
defecto), igual de tolerante que el resto de los endpoints del módulo.

### Backend — excepción y contrato de error

Nueva excepción en `Icarus.GestionAvicola.Application.CreditoHuevo` (junto a
`PuertoCreditoHuevo.cs`), heredando de `ConflictException`
(`Icarus.BuildingBlocks.Domain`) para no tocar reglas de arquitectura de
capas (Building Blocks no puede depender de un módulo vertical, así que la
excepción vive en el módulo y solo hereda del tipo base genérico):

```csharp
public sealed class CreditoInsuficienteRequiereConfirmacionException()
    : ConflictException(
        "Este pedido dejaría el crédito del cliente en negativo. " +
        "Confirmá el envío para continuar.");
```

`ExceptionHandlingMiddleware` gana un caso más en el switch existente (mismo
mecanismo que ya distingue `NotFoundException`/`ConflictException`/etc. por
tipo, sin acoplarse al módulo — el switch ya vive en Building Blocks y ya
conoce `ConflictException` genérica; el caso nuevo es solo un `title` más
específico para este subtipo):

```csharp
CreditoInsuficienteRequiereConfirmacionException => (StatusCodes.Status409Conflict, "Crédito insuficiente"),
```

Ese caso nuevo debe quedar **antes** del `ConflictException => (...)` genérico ya
existente en el switch: como hereda de `ConflictException`, el pattern
matching por tipo captura el primer patrón que coincide en orden de
declaración — si quedara después del genérico, el caso genérico lo atraparía
primero y el `title` nunca sería el específico.

Nota deliberada: **no** se extiende `ProblemDetails.Extensions` con los tres
montos (saldo actual, total del pedido, resultante) para este caso, a
diferencia de cómo `ValidationException` ya expone `errors`. El frontend
obtiene esos números reconsultando `GET /despachos-huevo/credito` (que ya
expone el saldo) más el total que ya tiene calculado en pantalla, en el
momento del rechazo. Esto evita extender `ApiError`/`http.ts` —código
compartido de alto impacto, usado por toda la aplicación— con campos
específicos de un único caso de uso. Contra: el frontend depende de que el
`title` del error no cambie de texto sin que se actualice la comparación en
el componente; se documenta con un comentario en el código, mismo nivel de
fragilidad que ya acepta hoy el patrón `code = title` en `ApiError`.

### Backend — dónde vive el chequeo

Dentro de `EnviarPedidoAlimentoHandler.Handle` (`ComandosPedidosAlimento.cs`),
en el punto donde hoy ya calcula `saldoActual` y compara contra
`pedido.TotalSolicitado` (líneas 351-355 actuales) — mismo cálculo, mismo
lugar. La única diferencia de comportamiento: si el resultado da negativo y
`request.ConfirmarCreditoInsuficiente` es `false`, lanza la excepción en vez
de (o antes de) agregar la notificación pasiva. Si el resultado da negativo
y viene confirmado, agrega la notificación pasiva igual que hoy, y además
dispone que el motivo del historial quede anotado (ver siguiente punto).

Requisito no negociable para el plan de implementación, sin fijar el
mecanismo exacto: si hace falta confirmación y no llegó, no debe quedar
ninguna transición nueva en el historial ni ningún cambio persistido — el
patrón transaccional ya existente lo garantiza mientras la excepción se
lance antes de `unidadTrabajoGestionAvicola.SaveChangesAsync(...)` (como ya
ocurre hoy con cualquier otro `throw` dentro de este mismo método).

### Backend — dominio (motivo en el historial)

`TransicionPedidoAlimento` ya admite un `Motivo` (`string?`) opcional —hoy
usado por `DevolverParaCorreccion`/`Rechazar`— vía el `RegistrarTransicion`
privado de `PedidoAlimento`. `EnviarACaisy` es el único método de transición
que hoy siempre pasa `motivo: null`. El plan de implementación decide el
mecanismo concreto para que, cuando corresponda, la transición
Borrador→Solicitado quede con el motivo compuesto por el Handler de
aplicación (mismo patrón de texto invariante ya usado en
`ActualizarEntregaEstimadaPedidoHandler.Meta()`), sin duplicar en el Handler
la validación de precios que hoy vive exclusivamente dentro de
`EnviarACaisy` (`CongelarPrecios`, `AsegurarCantidadesGranel`).

### Frontend — `PedidoAlimentoDetallePage.tsx`

- `enviarPedido(id, confirmarCreditoInsuficiente)` en
  `pedidos-alimento/api.ts` gana el segundo parámetro (`= false`), enviado
  como cuerpo JSON.
- Nuevo estado local `requiereConfirmacionCredito: boolean`. La mutación
  `enviar` recibe el flag como argumento de `mutate()`; en `onError`, si
  `error instanceof ApiError && error.code === 'Crédito insuficiente'`, en
  vez de setear el error genérico, pone `requiereConfirmacionCredito = true`.
- Nueva query, habilitada solo cuando `requiereConfirmacionCredito` es
  `true`, reusando `obtenerBalanceCreditoHuevo` (ya importado en
  `despacho-huevo/api.ts`, mismo `queryKey` que ya usa
  `PedidoFormularioPage.tsx` — comparte caché).
- El `Dialog` de "Enviar pedido a CAISY" cambia su contenido cuando
  `requiereConfirmacionCredito` es `true`: agrega un `Alert severity="warning"`
  con los tres números, y el botón de acción cambia de "Confirmar envío" a
  "Enviar de todas formas", llamando `enviar.mutate(true)` en vez de
  `enviar.mutate(false)`.
- Cerrar el diálogo (botón "Cancelar" o `onClose`) resetea
  `requiereConfirmacionCredito` a `false` también, para que un envío
  posterior arranque limpio.

### Notificación a CAISY (sin cambios de comportamiento)

`NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente` se sigue
generando exactamente en el mismo punto y condición que hoy: saldo
resultante negativo, sin importar si hizo falta pedir confirmación al
cliente o si el saldo ya estaba negativo desde antes. La única fuente nueva
de información para CAISY es el motivo en el historial del propio pedido
(punto anterior), que complementa —no reemplaza— la notificación de la
bandeja global.

## Casos de borde

- **Reenvío tras una devolución** (`esReenvio` ya existe en el Handler):
  mismo chequeo, recalculado con el saldo de ese momento. Si mejoró desde el
  primer envío, no vuelve a pedir confirmación.
- **Saldo resultante exactamente en cero**: no dispara nada — se mantiene la
  regla ya vigente (`< 0` estricto), sin cambiar el umbral.
- **Doble clic o reintento después de confirmar y enviar**: el pedido ya
  pasó a Solicitado; cualquier segundo intento choca con
  `"Solo un pedido en borrador se puede enviar"`, igual que cualquier otra
  transición repetida hoy.
- **Sin cupo semanal y con crédito insuficiente a la vez**: el chequeo de
  cupo semanal (`ContarEnviadosEnSemanaBloqueandoAsync`) sigue ocurriendo
  primero, sin cambios; si no hay cupo, ni se llega a calcular el crédito.
- **El cliente confirma, pero entre el rechazo y el reintento el saldo
  cambió** (otro pedido concurrente, una recepción que liberó crédito): el
  backend vuelve a calcular el saldo en el reintento — es el mismo cálculo,
  no un valor cacheado del primer intento — así que el resultado final
  siempre refleja el estado real al momento del envío efectivo.

## Testing

A alto nivel (el detalle línea por línea, con TDD, lo define el plan de
implementación):

- **Unit (`PedidosAlimentoHandlerTests.cs`)**: enviar sin confirmar y con
  saldo negativo lanza `CreditoInsuficienteRequiereConfirmacionException`
  sin llamar `SaveChangesAsync` ni `ConfirmarAsync` (mismo patrón que
  `EnviarUnPedidoYaEnviadoDevuelveConflictoSinGastarCupo`); enviar
  confirmando y con saldo negativo deja el pedido Solicitado, agrega la
  notificación pasiva (test ya existente
  `EnviarConSaldoInsuficienteAvisaACaisySinBloquearElEnvio` se ajusta para
  enviar `ConfirmarCreditoInsuficiente: true`) y el motivo queda en el
  historial con los tres montos; con saldo suficiente el comportamiento no
  cambia (`EnviarConSaldoSuficienteNoAvisaYConfirmaElEnvio` sigue pasando
  sin tocar el flag).
- **Integración (Testcontainers)**: primer intento de envío responde 409 con
  `title: "Crédito insuficiente"`; reintento con el flag responde 204 y el
  pedido queda Solicitado con el motivo persistido.
- **Frontend (`PedidoAlimentoDetallePage.test.tsx`)**: el diálogo muestra el
  segundo estado (mensaje con los tres números y botón "Enviar de todas
  formas") tras un 409 con ese `title`; el segundo clic reintenta con el
  flag en `true`; un error distinto (p. ej. sin cupo semanal) sigue
  mostrando el mensaje genérico de siempre, sin activar este flujo.
