# Corrección del bloque 9 — El crédito de huevo es privado del Cliente y no gobierna el pedido

Diseño validado mediante brainstorming con el usuario el 2026-09-14, como
**corrección** del bloque 9 ya implementado (SP9, SP9A–SP9F más los
documentos de crédito del 2026-09-11). No es una feature nueva: revierte
decisiones de diseño que el negocio descartó después de verlas funcionando.

## Por qué existe este documento

El bloque 9 se construyó sobre dos premisas que resultaron equivocadas:

1. **«Lo financiero es de las dos partes»** — que CAISY y el Cliente ven la
   misma información de crédito. Es falso: el saldo es un dato sensible del
   Cliente y ningún funcionario de CAISY debe verlo, ni el gestor de pedidos
   de alimento ni el de recepción de huevos.
2. **«El crédito es un límite de negocio real»** — que un saldo negativo
   debe frenar, advertir o al menos avisar. Es falso: el sistema no hace
   ninguna validación sobre el saldo. El pedido de alimento no depende del
   crédito. Un saldo negativo es un número más, no una anomalía.

De esas dos premisas salieron `2026-09-11-credito-huevo-vista-caisy-design`
(vista de CAISY del crédito) y
`2026-09-11-sp9e-confirmacion-credito-insuficiente-envio-design`
(confirmación obligatoria al enviar con saldo negativo). Este documento las
**supera**: ambas quedan obsoletas en la parte que este corrige.

## Objetivo

- El saldo y el desglose del crédito por despachos de huevo los ve **solo el
  Cliente**. Ni el Trabajador, ni `GestorPedidoAlimento`, ni
  `GestorRecepcionHuevos`, ni ningún otro rol.
- El crédito no valida, no bloquea, no advierte y no notifica nada. El envío
  de un pedido de alimento es independiente del saldo.
- El saldo refleja la realidad de la cuenta, no una proyección: deja de
  descontar pedidos que todavía no se recibieron.

## Decisiones del brainstorm

| Pregunta | Decisión |
|---|---|
| ¿Cómo se le cierra el crédito a CAISY? | Ocultar la tarjeta en las vistas **y** cerrar la API con 403. Sin borrar el endpoint, el handler ni la partial. |
| ¿Qué pasa con el bloqueo del envío? | Solo se quita el bloqueo. El envío nunca falla ni pide confirmación. |
| ¿El saldo sigue descontando los pedidos en tránsito? | No. Fuera el componente «comprometido pendiente». |
| ¿Dónde queda el rastro del envío con saldo negativo? | Marca en el historial del pedido **sin cifras**; el detalle con números va al registro de vuelo. |
| ¿Dónde ve el Cliente su saldo? | Al hacer un pedido de alimento (ya existe) y al hacer un despacho de huevo (falta). |

## Alcance

### 1. El saldo deja de ser visible para CAISY

Hoy `GET /pedidos-alimento-caisy/{id}/credito` responde el saldo del cliente
del pedido a cualquiera con rol `GestorCaisy` y la funcionalidad
`GestorPedidoAlimento`. El handler `ObtenerCreditoHuevoDePedidoCaisyHandler`
no tiene gate de rol a propósito: su comentario dice que la política del
grupo ya alcanza. Con la premisa corregida, esa política es precisamente el
problema: habilita al único rol que ya no debe ver el dato.

- El handler pasa a rechazar con `CreditoHuevoRequiereRolClienteException`
  (403) a todo el que no sea `Cliente`. Dado que el endpoint vive en el grupo
  de CAISY, eso significa que **nadie** lo obtiene: queda inerte a propósito,
  no borrado.
- `Trajano.GestorCaisy` deja de renderizar el bloque `_CreditoHuevo` en
  Detalles, Aceptar y Despachar, y deja de pedir el dato a la API.
- El archivo `_CreditoHuevo.cshtml`, `VistaConCredito`,
  `IFormularioConCredito`, la propiedad `Credito` de los modelos, el contrato
  `CreditoHuevoPedidoApi` y `ObtenerCreditoDePedidoAsync` **se conservan**,
  sin consumidor.

**Segunda puerta, menos obvia:** el motivo textual que SP9E escribía en la
transición Borrador→Solicitado —`"Enviado con crédito insuficiente: saldo X,
pedido Y, resultante Z"`— se muestra en el historial del pedido, que CAISY lee
en la vista Detalles. Ocultar la tarjeta sin tocar ese texto dejaría la fuga
abierta por la puerta de atrás.

### 2. El pedido de alimento no depende del saldo

`EnviarPedidoAlimentoHandler` deja de frenar el envío:

- No lanza `CreditoInsuficienteRequiereConfirmacionException`. El primer
  intento de envío siempre procede.
- No exige rol `Cliente` para enviar: enviar un pedido es una operación
  operativa que el Trabajador puede hacer, y ya no hay ninguna decisión
  financiera que tomar al enviar.
- No genera la notificación `NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente`
  hacia la bandeja global de CAISY. Ninguna alerta por saldo.
- Sí conserva el cálculo del saldo, con un único fin: dejar la marca
  `"Enviado con crédito insuficiente."` —sin cifras— en el historial del
  pedido, y las cifras en el registro de vuelo, que CAISY no consulta.

El flag `ConfirmarCreditoInsuficiente` del comando y del endpoint, y la
excepción `CreditoInsuficienteRequiereConfirmacionException`, **se conservan
inertes**, marcados en comentario como retirados por esta corrección.

La bandeja de CAISY (`Views/RecepcionesHuevo/Index.cshtml`) deja de tener
etiqueta para `CreditoInsuficiente`: al no producirse más, no hay nada que
etiquetar. El valor `CreditoInsuficiente = 1` del enum
`TipoNotificacionDespachoHuevo` no se renumera nunca (regla del enum) y las
filas históricas se conservan.

### 3. La fórmula del saldo refleja la cuenta real

Antes:

```
saldo = ingresos por huevo
      − alimento recibido real
      − comprometido pendiente   (pedidos Solicitado/Aceptado/Despachado)
      + ajustes
```

Después:

```
saldo = ingresos por huevo
      − alimento recibido real
      + ajustes
```

El componente «comprometido pendiente» existía únicamente para que la
advertencia de crédito insuficiente no se pudiera burlar enviando varios
pedidos seguidos antes de que CAISY recibiera el primero
(ver el comentario de `RepositorioBalanceCreditoHuevo`). Sin advertencia, no
tiene razón de ser, y además distorsiona la cifra: descuenta plata que el
Cliente todavía no debe, por alimento que no llegó y que CAISY todavía puede
rechazar o devolver. Un saldo que rebota hacia arriba cuando un pedido se
rechaza no es un saldo de cuenta.

Efecto colateral deseado: desaparecen los negativos que no correspondían a
una deuda real —la queja que originó esta corrección—. Los que queden
reflejan deuda efectiva, y se muestran tal cual, sin alerta.

### 4. El Cliente ve su crédito donde decide gastarlo

| Pantalla | Cliente | Trabajador | GestorCaisy |
|---|---|---|---|
| Formulario de pedido de alimento (`PedidoFormularioPage`) | ve saldo + ajustes (ya implementado) | no | — |
| Formulario de despacho de huevo (`DespachoHuevoFormularioPage`) | **se agrega** saldo + ajustes | no | — |
| Diálogo «Enviar a CAISY» (`PedidoAlimentoDetallePage`) | se quita el bloque de crédito junto con la confirmación | — | — |
| Detalles / Aceptar / Despachar de `Trajano.GestorCaisy` | — | — | se quita |

El gate del Trabajador ya existe en dos capas y se conserva sin cambios:
`ObtenerBalanceCreditoHuevoHandler` exige rol `Cliente` (403 aunque llame la
API directo) y la PWA condiciona el bloque con `tieneRol('Cliente')`. La
pantalla nueva de despacho usa exactamente el mismo par.

`VisibilidadNotificacionesDespachoHuevo` tampoco cambia: el Trabajador sigue
sin ver los tipos financieros, y `AjusteCredito` —el aviso al Cliente de que
una corrección de precio movió su crédito— se mantiene, porque es información
del Cliente sobre su propio crédito y no una alerta de saldo.

## Fuera de alcance

- Borrar código del camino de CAISY o del camino de confirmación. Decisión
  explícita del usuario: se oculta y se inhabilita, no se borra.
- Migración de datos. Las notificaciones `CreditoInsuficiente` ya emitidas se
  quedan en la base; simplemente dejan de producirse y de etiquetarse.
- Cambiar el desfase de 14 días, el catálogo de precios de huevo, la
  corrección de publicaciones (SP9D) o los ajustes de crédito.
- Una pantalla dedicada de «mi crédito» con ledger histórico. El Cliente lo
  ve en los dos formularios donde el dato le sirve para decidir.

## Deuda registrada

Por la decisión de ocultar en vez de borrar, quedan sin consumidor:
`ObtenerCreditoHuevoDePedidoCaisyQuery` y su handler, el endpoint
`/pedidos-alimento-caisy/{id}/credito`, `_CreditoHuevo.cshtml`,
`VistaConCredito`, `IFormularioConCredito`, `CreditoHuevoPedidoApi`,
`ObtenerCreditoDePedidoAsync`, `CreditoInsuficienteRequiereConfirmacionException`
y el flag `ConfirmarCreditoInsuficiente`. Todos quedan comentados como
retirados. Si en algún momento se decide limpiar, este documento es el
inventario.

## Documentos que quedan superados

- `2026-09-11-credito-huevo-vista-caisy-design.md` — su objetivo completo se
  revierte.
- `2026-09-11-sp9e-confirmacion-credito-insuficiente-envio-design.md` — su
  objetivo completo se revierte, salvo el mecanismo genérico
  `IExcepcionConTituloPropio` en Building Blocks, que sigue siendo útil.
- `2026-09-11-desglose-credito-huevo-design.md` — sigue vigente en cuanto al
  desglose de ajustes para el Cliente; queda superado solo en la parte que
  decía que CAISY ve lo mismo.
