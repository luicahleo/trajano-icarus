# SP9D — Corrección y eliminación de publicaciones de precio de huevo

Diseño validado mediante brainstorming con el usuario el 2026-09-10, a partir
de una pregunta directa: ¿qué pasa si `GestorRecepcionHuevos` publica un
precio de huevo con un error? Extiende el diseño de SP9
(`docs/superpowers/specs/2026-09-09-sp9-despacho-huevos-design.md`) y depende
de que SP9A, SP9B y SP9C ya estén implementados (lo están, a la fecha de este
documento).

## Objetivo

Cubrir los dos casos de error que el diseño original de SP9 no contemplaba:

1. Una publicación **futura** (`Publicada`, pero `FechaVigencia` todavía no
   llegó) resulta errónea. Nunca pudo haber sido usada por ningún despacho.
2. Una publicación **ya vigente** resulta errónea, y ya hay despachos que
   congelaron su precio con ella — algunos en tránsito (`Despachado`, todavía
   sin confirmar recepción) y otros ya confirmados (`Recibido`, con crédito ya
   potencialmente computado).

## Alcance y límites

Incluye:

- Renombrar en la UI de `Trajano.GestorCaisy` la acción ya existente para el
  caso 1 (futura y errónea) de "Anular publicación" a "Eliminar publicación".
  Sin cambios de lógica: reusa `AnularFutura` tal cual está implementado.
- Un nuevo flujo "Corregir" para el caso 2 (vigente y errónea): estado
  `Corregida`, entidad `AjusteCreditoHuevo`, notificación al cliente afectado,
  vista previa antes de confirmar, y reconciliación perezosa en
  `ConfirmarRecepcion` para despachos todavía en tránsito.

Queda fuera de esta versión:

- Corregir una publicación de precio de **alimento** (`NotificacionPreciosAlimentos`).
  El mismo problema puede existir ahí, pero no fue pedido y este documento no
  lo decide por su cuenta.
- Deshacer un `AjusteCreditoHuevo` ya creado (si el ajuste mismo estuviera
  mal, la corrección es una nueva corrección sobre la publicación correctiva,
  no una edición del ajuste).
- Cualquier límite de tiempo artificial sobre cuántos despachos puede alcanzar
  una corrección (se corrigen todos los `Recibido` que referencien la
  publicación errónea, sin importar cuán atrás estén).

## Vocabulario

| Término | Significado |
|---|---|
| Publicación corregida | Publicación que regía y fue reemplazada por error, distinta de una publicación simplemente superada por la siguiente en el tiempo. Estado `Corregida`. |
| Publicación correctiva | La nueva publicación que reemplaza a la errónea, con `FechaVigencia` hoy o anterior. |
| Ajuste de crédito | Registro que compensa, en el saldo disponible del cliente, la diferencia entre el precio erróneo y el correcto para un despacho ya `Recibido`. |

## Caso 1 — Futura y errónea (ya implementado, solo UI)

`PublicacionPrecioHuevo.AnularFutura(hoy)` ya existe: solo permite anular si
`Estado == Publicada` y `FechaVigencia > hoy`; pasa a `Anulada`. Como el
precio recién se congela en `Despachar` (lado tenant) contra la publicación
**vigente en ese momento**, una publicación todavía futura no puede haber sido
usada por ningún despacho — no hay nada que reconciliar.

Único cambio: en
`Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Detalles.cshtml`, el
botón que hoy dice "Anular publicación" pasa a decir "Eliminar publicación".
Mismo `asp-action="Anular"`, mismo `Model.PuedeAnularse`, mismo endpoint
`POST /{id}/Anular`. Sin cambios en `PreciosController.cs` (alimento, fuera de
alcance) ni en el backend.

## Caso 2 — Vigente y errónea, ya usada

### Dominio

**`EstadoPublicacionPrecioHuevo`** — agregar un valor al final, sin
renumerar:

```csharp
public enum EstadoPublicacionPrecioHuevo
{
    Borrador = 0,
    Publicada = 1,
    Anulada = 2,
    Corregida = 3,
}
```

`Corregida` es deliberadamente distinto de `Anulada`: `Anulada` es "nunca
llegó a regir, sin impacto"; `Corregida` es "regía y fue reemplazada por
error, con impacto que hay que reconciliar". Separar los dos estados evita
que el historial de publicaciones confunda un error real con el reemplazo
normal y esperado de una publicación vencida por la siguiente.

**`PublicacionPrecioHuevo`** — nuevos campos y método:

- `PublicacionCorrectivaId` (`Guid?`) — enlaza hacia la publicación que la
  reemplaza por corrección (no hacia el reemplazo normal por vencimiento, que
  no se enlaza).
- `Motivo` (`string?`) — por qué se corrigió.
- `CorregirVigente(DateOnly hoy, Guid publicacionCorrectivaId, string motivo)`
  — solo válido si `Estado == Publicada` (no se puede corregir algo que ya
  está `Anulada` o `Corregida`; si la propia correctiva resulta después
  también errónea, se corrige a *ella*, no a la original — esto forma una
  cadena de correcciones, nunca un grafo). No exige `FechaVigencia <= hoy` en
  el propio método — esa validación (que ya regía, y que la correctiva no es
  futura) la hace el handler de aplicación antes de invocarlo, porque
  requiere cargar ambos agregados.

**Nueva entidad `AjusteCreditoHuevo`** (aggregate root propio, en
`Icarus.GestionAvicola.Domain`, tabla propia — no una sub-entidad de
`DespachoHuevo`, porque `RepositorioBalanceCreditoHuevo` necesita sumarlo
igual que ya suma despachos y pedidos, con una consulta que cruza clientes):

- Propiedades: `ClienteId`, `DespachoHuevoId`, `PublicacionErroneaId`,
  `PublicacionCorrectivaId`, `Monto` (decimal con signo: positivo a favor del
  cliente, negativo en contra), `Motivo`, `CreadoEn` (UTC), `ActorId`.
- Inmutable tras crearse — es un registro histórico de lo que pasó, sin
  métodos de edición.
- Constructor rechaza `Monto == 0` (si la diferencia calculada para un
  despacho da cero, no se crea ajuste para ese despacho).

**`RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync`** — agrega un
cuarto término a la fórmula ya existente
(`ingresos - recibidoReal - comprometidoPendiente`): suma el total de
`AjusteCreditoHuevo.Monto` del cliente, sin filtro de fecha (un ajuste
corrige un error real, no es un ingreso sujeto al desfase de dos semanas).

### Reconciliación perezosa en `ConfirmarRecepcion`

Antes de sellar la recepción de un despacho (`DespachoHuevo.ConfirmarRecepcion`,
en el handler de aplicación que ya existe en SP9C), por cada línea cuya
`PublicacionPrecioHuevoId` apunte a una publicación en estado `Corregida`:

1. Cargar esa publicación.
2. Si está `Corregida`, seguir `PublicacionCorrectivaId` hacia la siguiente,
   repitiendo mientras la publicación encontrada siga estando `Corregida`
   (cadena de más de una corrección sucesiva antes de que este despacho
   confirmara recepción).
3. Volver a congelar la línea (`PrecioUnitarioCongelado`,
   `PublicacionPrecioHuevoId`) con el precio de esa última publicación activa
   para el mismo `Tamano`.

Esto solo alcanza a despachos todavía `Despachado`. Un despacho ya `Recibido`
nunca se re-congela — su camino de corrección es exclusivamente
`AjusteCreditoHuevo`, preservando la regla ya vigente en la UI ("el precio del
productor es el congelado al despacho y nunca se recalcula") para todo lo que
ya fue confirmado.

### Flujo de la acción "Corregir"

1. El gestor arma una publicación correctiva por el flujo normal ya existente
   (Borrador → editar o importar Excel), sin publicarla todavía.
2. Desde el detalle de la publicación errónea (ya vigente), un nuevo botón
   "Corregir con otra publicación" permite elegir esa correctiva en borrador.
3. **Vista previa** (query de solo lectura, no persiste nada): para cada
   `DespachoHuevo` en estado `Recibido` con alguna línea que referencia la
   errónea, calcula la diferencia por tamaño entre el precio de la correctiva
   (todavía en borrador) y el precio congelado, y muestra la lista de
   despachos afectados con su monto de ajuste y el total.
4. El gestor confirma → un único comando de aplicación, atómico:
   - Publica la correctiva, validando `FechaVigencia <= hoy` (si no cumple,
     rechaza con un mensaje claro: una corrección no puede tener efecto
     futuro, porque dejaría un vacío sin publicación vigente) y reusando la
     validación de unicidad de vigencia que ya existe para publicar
     normalmente.
   - Llama `errónea.CorregirVigente(hoy, correctiva.Id, motivo)`.
   - Por cada despacho `Recibido` afectado con diferencia ≠ 0: crea un
     `AjusteCreditoHuevo` y una `NotificacionInternaDespachoHuevo` nueva
     (ver más abajo).
   - Los despachos `Despachado` no se tocan en este paso — quedan para la
     reconciliación perezosa descrita arriba.

### Notificación al cliente

**`TipoNotificacionDespachoHuevo`** — agregar un valor al final, sin
renumerar:

```csharp
public enum TipoNotificacionDespachoHuevo
{
    DespachoRecibido = 0,
    CreditoInsuficiente = 1,
    AjusteCredito = 2,
}
```

Nuevo factory en `NotificacionInternaDespachoHuevo`, mismo patrón que
`ParaRecepcionConfirmada`:

```csharp
public static NotificacionInternaDespachoHuevo ParaAjusteCredito(
    Guid despachoHuevoId, Guid clienteId, string meta) =>
    new(TipoNotificacionDespachoHuevo.AjusteCredito, despachoHuevoId, clienteId, meta);
```

`ClienteId` va relleno (no es una notificación de bandeja global de CAISY,
sino del tenant afectado) — visible en la PWA con el mismo mecanismo de
notificaciones internas ya construido en SP8/SP9. `Meta` lleva el monto y el
motivo en texto, siguiendo la misma convención ya usada para
`CreditoInsuficiente`.

## Casos de borde ya resueltos por este diseño

- **Cadena de correcciones** (la correctiva misma resulta errónea después):
  cubierta por `CorregirVigente` solo operando sobre `Estado == Publicada`
  (no se puede corregir dos veces la misma publicación errónea; la segunda
  corrección se hace sobre la correctiva, que para entonces es la vigente) y
  por el `while` de la reconciliación perezosa, que sigue la cadena completa.
- **Despacho en `Borrador` al momento de la corrección**: no tiene precio
  congelado todavía: `Despachar()` usará la publicación vigente en ese
  momento (ya la correctiva, porque la errónea dejó de ser `Publicada`). Sin
  manejo especial.
- **Vacío de vigencia**: evitado exigiendo que la correctiva tenga
  `FechaVigencia <= hoy` antes de aceptar la corrección.

## Testing

- Dominio (`PublicacionPrecioHuevo`): `CorregirVigente` rechaza sobre
  `Borrador`, sobre `Anulada` y sobre una publicación ya `Corregida`; queda
  `Corregida` con el `PublicacionCorrectivaId` y `Motivo` correctos sobre una
  `Publicada` vigente.
- Dominio (`AjusteCreditoHuevo`): rechaza `Monto == 0` en el constructor.
- Aplicación: el comando "Corregir" crea un `AjusteCreditoHuevo` y una
  notificación solo para despachos `Recibido` con diferencia ≠ 0; no toca
  despachos `Despachado`; la vista previa no persiste nada; publicar la
  correctiva con `FechaVigencia` futura se rechaza.
- Aplicación (`ConfirmarRecepcion`): un despacho `Despachado` cuya línea
  referencia una publicación `Corregida` se re-congela con la publicación
  activa al final de la cadena antes de pasar a `Recibido`; uno que referencia
  una cadena de dos correcciones sucesivas también resuelve correctamente.
- Integración: `RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync`
  refleja el `AjusteCreditoHuevo` en el saldo, sin desfase de dos semanas.
- Trajano.GestorCaisy: el botón "Eliminar publicación" en el detalle de una
  publicación futura invoca el mismo endpoint `Anular` ya probado; la vista de
  vista previa de corrección muestra el total esperado.
