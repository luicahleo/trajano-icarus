# Desglose visible del crédito de despachos de huevo

Diseño validado mediante brainstorming con el usuario el 2026-09-11, a partir
del ítem 2 del backlog priorizado de crédito de huevo (`docs/ai/HANDOFF.md`):
*"Desglose visible del crédito"*. Depende de SP9 (despacho de huevos), SP9D
(ajustes de corrección) y SP9E (confirmación explícita de crédito
insuficiente), ya implementados.

## Motivación

Es una mejora proactiva de transparencia, no una respuesta a quejas
concretas de clientes: `RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync`
ya calcula cuatro componentes por separado (ingresos disponibles, recibido
real, comprometido pendiente, ajustes) pero los suma internamente y descarta
el desglose — el cliente solo ve el número final
(`BalanceCreditoHuevoResumen.SaldoDisponible`, vía `GET /despachos-huevo/credito`).

## Objetivo

Que el cliente pueda entender un cambio inesperado en su saldo sin tener que
preguntarle a nadie. De los cuatro componentes, el único que representa una
sorpresa real es el ajuste de corrección (`AjusteCreditoHuevo`, spec SP9D):
un movimiento retroactivo que CAISY aplicó sin que el cliente hiciera nada.
Los otros tres son flujos esperables por contexto (un pedido enviado
descuenta, un despacho recibido acredita a los 14 días) y no necesitan
explicación adicional.

## Alcance y límites

Incluye:

- Exponer la lista de ajustes de crédito del cliente (monto, motivo, fecha)
  junto al saldo, en el mismo endpoint que ya existe.
- Mostrar esa lista, cuando no está vacía, en los dos lugares donde hoy se ve
  el saldo del lado Cliente: `PedidoFormularioPage.tsx` (informativo) y el
  diálogo de confirmación de SP9E en `PedidoAlimentoDetallePage.tsx`.
- Corregir una brecha de rol preexistente en ese mismo diálogo: hoy no
  verifica rol antes de mostrar el saldo, así que un Trabajador que dispare
  el 409 de crédito insuficiente vería el saldo del cliente. Se corrige en
  ambas capas: el frontend deja de mostrarle el bloque financiero y de
  ofrecerle el botón de confirmación; el backend rechaza con 403 si de todos
  modos llega `confirmarCreditoInsuficiente: true` desde una cuenta que no
  es Cliente, para no depender únicamente del frontend.
- Corregir una segunda brecha de rol preexistente, más amplia: hoy
  `GET /despachos-huevo/credito` en sí mismo no filtra por rol, solo por
  entitlement de módulo — cualquier Trabajador con la funcionalidad
  `DespachoHuevo` habilitada puede llamarlo directo (curl, Postman) y recibir
  `saldoDisponible` (y, con este cambio, también `ajustes`) aunque ninguna
  pantalla se lo muestre. Se cierra en la raíz: el propio endpoint rechaza
  con 403 a cualquier cuenta que no sea Cliente.

Queda fuera de esta versión (decisiones ya descartadas en el brainstorm, o
ítems separados del mismo backlog):

- Exponer `IngresosDisponibles`, `RecibidoReal` o `ComprometidoPendiente`.
  Se evaluaron tres opciones (desglose completo de los 4 componentes; solo
  ajustes con motivo; híbrido con un tooltip de los 4 números) y se eligió
  la de solo ajustes: resuelve la sorpresa real sin exponer conceptos
  internos que hoy no tienen ninguna etiqueta pensada para un cliente no
  técnico, y sin superficie de API/UI adicional que no responde a ninguna
  necesidad planteada.
- Vista de CAISY del crédito por cliente (ítem 3 del backlog) — mismo
  backend (`ObtenerBalanceCreditoHuevoQuery`), pero nueva UI en
  `Trajano.GestorCaisy`, sesión aparte.
- Paginación o límite en la lista de ajustes: se esperan raros por cliente
  (correcciones puntuales de precio, spec SP9D), una lista simple alcanza.
- Notificación proactiva de saldo negativo (ítem 5) y ledger histórico
  (ítem 6): sin relación directa con este ítem.
- Cualquier cambio a `ObtenerSaldoDisponibleAsync` (el método que usa
  `EnviarPedidoAlimentoHandler` en el camino crítico de SP9E): no se toca,
  para no arriesgar ese flujo ya probado. El desglose se sirve desde un
  método nuevo y separado del repositorio.

## Diseño

### Backend — contrato de datos

`ComandosCreditoHuevo.cs` gana un tipo nuevo y `BalanceCreditoHuevoResumen`
se extiende:

```csharp
public sealed record AjusteCreditoHuevoResumen(Guid Id, decimal Monto, string Motivo, DateOnly Fecha);

public sealed record BalanceCreditoHuevoResumen(
    decimal SaldoDisponible, IReadOnlyList<AjusteCreditoHuevoResumen> Ajustes);
```

`Fecha` es la porción de fecha (sin hora) de `AjusteCreditoHuevo.CreadoEnUtc`:
es un timestamp técnico de auditoría, no una fecha de negocio, y mostrar solo
el día evita cualquier confusión de zona horaria al reutilizar el
`formatoFecha` ya existente en el frontend (que espera `yyyy-MM-dd`, igual
que el resto de las fechas de la aplicación).

### Backend — repositorio

`IRepositorioBalanceCreditoHuevo` (`PuertoCreditoHuevo.cs`) gana un método de
solo lectura nuevo, sin modificar la firma de `ObtenerSaldoDisponibleAsync`:

```csharp
public interface IRepositorioBalanceCreditoHuevo
{
    Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AjusteCreditoHuevoResumen>> ObtenerAjustesAsync(
        Guid clienteId, CancellationToken cancellationToken = default);
}
```

`RepositorioBalanceCreditoHuevo.ObtenerAjustesAsync` consulta
`db.AjustesCreditoHuevo` filtrando por `ClienteId`, ordenando por
`CreadoEnUtc` descendente (la corrección más reciente primero), proyectando
directamente a `AjusteCreditoHuevoResumen`. Es una segunda consulta contra la
misma tabla que ya suma `ajustes` dentro de `ObtenerSaldoDisponibleAsync` —
duplicación aceptada deliberadamente: separa el número (camino crítico de
SP9E, no tocar) de la lista para mostrar (camino nuevo, sin uso crítico), y
la tabla es chica porque los ajustes son raros.

No se agrega el método al puerto `IRepositorioAjustesCreditoHuevo`
(`PuertosPreciosHuevo.cs`) aunque ese es el repositorio del agregado
`AjusteCreditoHuevo`: su comentario existente ya documenta la decisión
("Solo se agregan, nunca se consultan por id desde este puerto: el saldo se
lee agregado en `RepositorioBalanceCreditoHuevo`"). Este desglose es una
lectura agregada por cliente, no por id, así que cae dentro de esa misma
decisión ya tomada — pertenece a `RepositorioBalanceCreditoHuevo`.

### Backend — handler de la consulta

`ObtenerBalanceCreditoHuevoHandler` (`ComandosCreditoHuevo.cs`) gana el
chequeo de rol (cierra la segunda brecha del alcance) y llama a los dos
métodos del repositorio para combinar el resultado:

```csharp
public async Task<BalanceCreditoHuevoResumen> Handle(
    ObtenerBalanceCreditoHuevoQuery request, CancellationToken cancellationToken)
{
    var clienteId = usuarioActual.ClienteId
        ?? throw new UnauthorizedAccessException("La sesión no es válida.");
    if (usuarioActual.Rol != "Cliente")
        throw new CreditoHuevoRequiereRolClienteException(
            "El crédito por despachos de huevo es exclusivo del Cliente.");
    var saldo = await repositorio.ObtenerSaldoDisponibleAsync(
        clienteId, DespachosHuevo.FechasNegocio.Hoy(), cancellationToken);
    var ajustes = await repositorio.ObtenerAjustesAsync(clienteId, cancellationToken);
    return new BalanceCreditoHuevoResumen(saldo, ajustes);
}
```

`CreditoHuevoRequiereRolClienteException` se define una sola vez y se
reutiliza en los dos puntos de este documento que necesitan la misma regla
("solo Cliente") — ver más abajo, "Backend — nueva excepción".

### Backend — endpoint

`GET /despachos-huevo/credito` (`DespachosHuevoEndpoints.cs`): sin cambios de
ruta ni de método — sigue exigiendo
`PoliticasClientes.Para(Funcionalidades.DespachoHuevo)` en la política de
autorización de ASP.NET (entitlement de módulo, primer filtro). El chequeo
de rol nuevo vive dentro del handler, no en la política: `PoliticasClientes`
es genérica por funcionalidad y no distingue Cliente de Trabajador, así que
agregar esa distinción ahí afectaría a todos los demás endpoints que la
usan. Con ambos filtros, un Trabajador con la funcionalidad habilitada pasa
la autorización de ASP.NET pero el handler igual rechaza con 403.

### Backend — brecha de rol en la confirmación de SP9E

Hallazgo del brainstorm: `PedidoAlimentoDetallePage.tsx` no filtra por rol
antes de mostrar el saldo en el diálogo de confirmación de crédito
insuficiente, a diferencia de `PedidoFormularioPage.tsx` (que sí calcula
`esCliente = tieneRol('Cliente')`). La política del endpoint de envío
(`PoliticasClientes.Para(Funcionalidades.PedidoAlimento)`) es por
entitlement de módulo, no por rol: un Trabajador con esa funcionalidad puede
disparar el mismo 409 `CreditoInsuficienteRequiereConfirmacionException` que
un Cliente.

La corrección de frontend (ver más abajo) evita que un Trabajador vea el
saldo o el botón de confirmación en el uso normal de la aplicación. Como
protección adicional en el backend — para no depender únicamente de que el
frontend se comporte bien, mismo principio que ya aplica SP9E para el flag
`confirmarCreditoInsuficiente` en general — se agrega un chequeo de rol en
`EnviarPedidoAlimentoHandler.Handle` (`ComandosPedidosAlimento.cs`), dentro
del bloque `if (haySaldoInsuficiente)`, **después** de comprobar
`request.ConfirmarCreditoInsuficiente` y **antes** de armar
`motivoCreditoInsuficiente`:

```csharp
if (haySaldoInsuficiente)
{
    if (!request.ConfirmarCreditoInsuficiente)
        throw new CreditoInsuficienteRequiereConfirmacionException();
    if (usuarioActual.Rol != "Cliente")
        throw new CreditoHuevoRequiereRolClienteException(
            "Solo el Cliente puede confirmar el envío con crédito insuficiente.");
    motivoCreditoInsuficiente = ...;
}
```

El primer rechazo (sin confirmar) sigue siendo idéntico para cualquier rol —
ese 409 solo informa que hace falta confirmar, no revela montos en el cuerpo
de la respuesta (ver spec SP9E, "Backend — contrato de error"). El chequeo
de rol nuevo solo entra en juego en el segundo intento, cuando ya viene
`confirmarCreditoInsuficiente: true`.

Se compara contra el string literal `"Cliente"`, no contra el enum
`Icarus.Identity.Domain.Rol`: `Icarus.GestionAvicola.Application` no
referencia `Icarus.Identity.Domain` en su `.csproj` (solo su propio Domain y
Building Blocks) y `ICurrentUser.Rol` ya es `string?` por diseño, para no
acoplar módulos — mismo patrón que evitó acoplar Observability a
GestionAvicola con `IExcepcionConTituloPropio` en SP9E. El frontend ya usa el
mismo literal vía `tieneRol('Cliente')`.

### Backend — nueva excepción y `ForbiddenException`

No existe hoy en `Icarus.BuildingBlocks.Domain` (`DomainException.cs`) un
caso de excepción para 403: el switch de `ExceptionHandlingMiddleware` solo
mapea `NotFoundException` (404), `ConflictException` (409),
`ValidationException`/`DomainException` (400) y `UnauthorizedAccessException`
(401). Se agrega, mismo patrón que las existentes:

```csharp
public class ForbiddenException : DomainException
{
    public ForbiddenException() { }

    public ForbiddenException(string mensaje) : base(mensaje) { }

    public ForbiddenException(string mensaje, Exception interna) : base(mensaje, interna) { }
}
```

Y en `ExceptionHandlingMiddleware.EscribirProblemDetails`, un caso más en el
switch (antes del catch-all `DomainException => (...)`, ya que
`ForbiddenException` hereda de `DomainException` y el pattern matching por
tipo captura el primer patrón que coincida en orden de declaración):

```csharp
ForbiddenException => (StatusCodes.Status403Forbidden, "Acción no permitida"),
```

Nueva excepción de dominio, junto a `CreditoInsuficienteRequiereConfirmacionException`
en `PuertoCreditoHuevo.cs` (mismo módulo, mismo archivo, mismo motivo: hereda
solo del tipo base genérico de Building Blocks). Una sola clase para las dos
brechas de rol que cierra este documento (la consulta del saldo y la
confirmación de envío): ambas son la misma regla de negocio ("esto es
exclusivo del Cliente"), solo cambia el mensaje según el punto donde se
dispara:

```csharp
public sealed class CreditoHuevoRequiereRolClienteException(string mensaje)
    : ForbiddenException(mensaje);
```

No implementa `IExcepcionConTituloPropio`: el título genérico "Acción no
permitida" alcanza, no hay ningún otro 403 en ninguno de los dos endpoints
del que distinguirlo, y el frontend no necesita reconocer este caso por
texto — un Trabajador nunca debería producirlo desde la UI normal (ver
diseño de frontend: `PedidoFormularioPage.tsx` ya gatea la consulta con
`esCliente`, y `PedidoAlimentoDetallePage.tsx` pasa a hacer lo mismo más
abajo), así que en ambos casos es una respuesta de "esto no debería pasar",
no un flujo que el frontend transforme en una pantalla amigable como sí hace
con `CreditoInsuficienteRequiereConfirmacionException`.

### Frontend — tipos

`web/src/features/despacho-huevo/api.ts`:

```ts
export interface AjusteCreditoHuevo {
  id: string;
  monto: number;
  motivo: string;
  fecha: string; // yyyy-MM-dd
}

export interface BalanceCreditoHuevo {
  saldoDisponible: number;
  ajustes: AjusteCreditoHuevo[];
}
```

### Frontend — componente compartido

Nuevo archivo `web/src/features/despacho-huevo/AjustesCreditoHuevo.tsx`,
presentacional puro (no hace fetch propio, recibe la lista ya cargada por la
query existente `obtenerBalanceCreditoHuevo` que ambas pantallas ya usan). No
renderiza nada si la lista está vacía:

```tsx
import { Stack, Typography } from '@mui/material';
import type { AjusteCreditoHuevo } from './api';
import { formatoFecha, formatoMoneda } from './constantes';

interface Props {
  ajustes: AjusteCreditoHuevo[];
}

export function AjustesCreditoHuevo({ ajustes }: Props) {
  if (ajustes.length === 0) return null;
  return (
    <Stack spacing={0.5} sx={{ mt: 1 }}>
      <Typography variant="caption" color="text.secondary">
        Correcciones aplicadas a tu crédito:
      </Typography>
      {ajustes.map((a) => (
        <Typography key={a.id} variant="body2" color={a.monto < 0 ? 'error' : 'text.secondary'}>
          {formatoFecha(a.fecha)} — {a.motivo} ({formatoMoneda(a.monto)})
        </Typography>
      ))}
    </Stack>
  );
}
```

Usa `formatoFecha`/`formatoMoneda` de `despacho-huevo/constantes.ts` (ya
existentes ahí, donde ya vive `BalanceCreditoHuevo`). Son una duplicación
literal de los mismos nombres en `pedidos-alimento/constantes.ts` —
preexistente a este cambio, fuera de alcance tocarla.

### Frontend — `PedidoFormularioPage.tsx`

Ya calcula `esCliente` y ya gatea el bloque de saldo con
`{esCliente && credito && (...)}`. Se agrega, dentro de ese mismo bloque,
justo debajo de la línea del saldo:

```tsx
<AjustesCreditoHuevo ajustes={credito.ajustes} />
```

Ningún chequeo nuevo: el gateo por rol ya existía y ya es correcto acá.

### Frontend — `PedidoAlimentoDetallePage.tsx`

Este archivo hoy no importa `useAuth` ni calcula ningún rol. Cambios:

1. Importar `useAuth` desde `../auth/AuthContext` y calcular
   `const esCliente = tieneRol('Cliente');`, mismo patrón que
   `PedidoFormularioPage.tsx`.
2. El bloque existente que hoy es
   `{requiereConfirmacionCredito && (<Alert severity="warning">...</Alert>)}`
   pasa a `{esCliente && requiereConfirmacionCredito && (...)}`, agregando
   `<AjustesCreditoHuevo ajustes={creditoParaConfirmar?.ajustes ?? []} />`
   dentro del `Alert`, después del texto del saldo.
3. Se agrega el caso contrario, mismo lugar del diálogo:
   ```tsx
   {!esCliente && requiereConfirmacionCredito && (
     <Alert severity="warning" sx={{ mt: 2 }}>
       Este pedido necesita confirmación del Cliente para continuar.
     </Alert>
   )}
   ```
   Sin montos, sin motivo, sin nada financiero.
4. El botón de acción contenido (`variant="contained"`, el que hoy siempre
   está y dispara `enviar.mutate(...)`) pasa a renderizarse condicionado a
   `esCliente || !requiereConfirmacionCredito`. Cuando esa condición es
   falsa (Trabajador + requiere confirmación), `DialogActions` solo muestra
   el botón "Cancelar" (`onClick={cerrarDialogoEnvio}`), que ya existe sin
   cambios. Para un Cliente, o cuando no hace falta confirmación, el botón
   contenido sigue exactamente igual que hoy (`enviar.mutate(requiereConfirmacionCredito)`,
   texto "Confirmar envío" / "Enviar de todas formas" según corresponda).
5. La query `creditoParaConfirmar` (`obtenerBalanceCreditoHuevo`) cambia su
   condición `enabled` de `requiereConfirmacionCredito` a
   `requiereConfirmacionCredito && esCliente`. Ya no es una simplificación
   opcional: como el endpoint ahora rechaza con 403 a quien no es Cliente
   (ver "Backend — endpoint"), dejar la query habilitada para un Trabajador
   dispararía un fetch que **falla en el uso normal de la aplicación** —no
   un caso límite de API saltada— así que hay que evitarlo.

El backend (`CreditoHuevoRequiereRolClienteException`, más arriba) es quien
realmente impide que un Trabajador consulte el saldo o complete el envío con
crédito insuficiente si de algún modo se saltea la UI; el frontend es la
capa de experiencia, no la de seguridad.

## Casos de borde

- **Cliente sin ajustes nunca**: `AjustesCreditoHuevo` no renderiza nada;
  la pantalla se ve exactamente igual que hoy.
- **Trabajador sin problema de crédito**: sin cambios de comportamiento — el
  diálogo de envío nunca entra en la rama de `requiereConfirmacionCredito`,
  así que ninguno de los cambios de este documento aplica.
- **Trabajador dispara el 409 de SP9E**: ve el mensaje genérico del punto 3,
  sin botón de confirmación; si cierra y reintenta, vuelve a pasar por el
  mismo camino — no tiene forma de completar el envío mientras el crédito
  siga insuficiente. Le corresponde al Cliente enviarlo.
- **Llamada directa a la API con `confirmarCreditoInsuficiente: true` desde
  una cuenta Trabajador** (saltando el frontend): `EnviarPedidoAlimentoHandler`
  responde 403 con `CreditoHuevoRequiereRolClienteException`, sin persistir
  ni mutar nada — mismo patrón transaccional que ya garantiza SP9E para el
  resto de sus rechazos (la excepción se lanza antes de `SaveChangesAsync`).
- **Llamada directa a `GET /despachos-huevo/credito` desde una cuenta
  Trabajador** con la funcionalidad `DespachoHuevo` habilitada (pasa la
  autorización de ASP.NET, saltando el frontend): `ObtenerBalanceCreditoHuevoHandler`
  responde 403 con el mismo tipo de excepción, sin llegar a consultar el
  repositorio.
- **Cuenta CAISY** (`Trajano.GestorCaisy`, roles fuera de `Cliente`/`Trabajador`):
  no llega a este flujo — `POST /pedidos-alimento/{id}/enviar` vive bajo la
  política de tenant, no la de CAISY; queda fuera de alcance de este
  documento (ítem 3 del backlog cubre la vista de CAISY, por separado).
- **Ajustes con `Monto` positivo y negativo mezclados**: cada línea se
  colorea según su propio signo (`color="error"` si `monto < 0`), sin
  relación con el signo del saldo total.

## Testing

A alto nivel (el detalle línea por línea, con TDD, lo define el plan de
implementación):

- **Unit (`GestionAvicola.Application`, clase nueva
  `ObtenerBalanceCreditoHuevoHandlerTests`)**: con rol Cliente, el handler
  combina saldo y ajustes de dos llamadas al repositorio (mock con
  `IRepositorioBalanceCreditoHuevo` de NSubstitute); con ajustes vacíos,
  `BalanceCreditoHuevoResumen.Ajustes` es una lista vacía, no `null`; con
  `_usuarioActual.Rol` distinto de `"Cliente"` lanza
  `CreditoHuevoRequiereRolClienteException` sin llamar a ningún método del
  repositorio (`_balanceCreditoHuevo.DidNotReceive().ObtenerSaldoDisponibleAsync(...)`).
- **Unit (`PedidosAlimentoHandlerTests.cs`)**: nuevo test — enviar
  confirmando (`ConfirmarCreditoInsuficiente: true`) con saldo negativo y
  `_usuarioActual.Rol` distinto de `"Cliente"` lanza
  `CreditoHuevoRequiereRolClienteException` sin llamar `SaveChangesAsync` ni
  `ConfirmarAsync` (mismo patrón que
  `EnviarSinConfirmarConSaldoInsuficienteExigeConfirmacion`). **Riesgo a
  cubrir en el plan**: el constructor de esta clase de test nunca configura
  `_usuarioActual.Rol` hoy, así que NSubstitute devuelve `null` por defecto;
  el test ya existente `EnviarConfirmandoSaldoInsuficienteAvisaACaisyYDejaMotivoEnHistorial`
  confirma con éxito hoy y **se rompería** con el chequeo nuevo si no se
  agrega `_usuarioActual.Rol.Returns("Cliente")` al setup compartido del
  constructor (o a ese test puntual) antes de escribir el test nuevo.
- **Repositorio (`Icarus.IntegrationTests`, contra SQL real vía
  Testcontainers)**: `ObtenerAjustesAsync` devuelve los ajustes del cliente
  ordenados por fecha descendente y no devuelve los de otro cliente.
- **Integración de endpoint (`Icarus.IntegrationTests`)**: `GET
  /despachos-huevo/credito` con un cliente que tiene un `AjusteCreditoHuevo`
  persistido (vía el flujo de corrección de SP9D ya existente en los tests)
  devuelve `ajustes` con ese registro; sin ajustes, devuelve lista vacía.
  Nuevos tests — el mismo `GET` desde una cuenta Trabajador con la
  funcionalidad `DespachoHuevo` habilitada responde 403 (a diferencia del
  test existente `CaisySinGestorRecepcionHuevosNoAccedeALaBandeja`, que hoy
  ya espera 403 para un Trabajador en este endpoint pero por falta de esa
  funcionalidad en su semilla, no por rol — verificar que ese test siga
  pasando sin cambios); reenviar con `confirmarCreditoInsuficiente: true`
  desde una cuenta Trabajador con la funcionalidad `PedidoAlimento`
  habilitada responde 403.
- **Frontend (`PedidoFormularioPage.test.tsx`)**: con `credito.ajustes` no
  vacío, se renderiza la lista con motivo y monto; con lista vacía, no se
  renderiza el bloque de correcciones.
- **Frontend (`PedidoAlimentoDetallePage.test.tsx`)**: para un usuario con
  rol Cliente, el diálogo de confirmación muestra los ajustes junto al saldo
  (comportamiento ya cubierto por SP9E, extendido); para un usuario con rol
  Trabajador, el mismo 409 produce el mensaje genérico sin montos y sin el
  botón "Enviar de todas formas" — solo "Cancelar" disponible, y la query
  `creditoParaConfirmar` no se dispara (mock de `obtenerBalanceCreditoHuevo`
  sin invocar).
