# SP9F — Bandeja de novedades del despacho de huevo en la PWA

Diseño validado mediante brainstorming con el usuario el 2026-09-11, a partir
del ítem 5 del backlog priorizado de crédito de huevo (`docs/ai/HANDOFF.md`):
*"Notificación de saldo negativo — reusar el patrón existente de
`NotificacionInternaDespachoHuevo` para avisar proactivamente en vez de que el
cliente tenga que entrar a mirar"*. Depende de SP9 (despacho de huevos), SP9C
(recepción y crédito) y SP9D (corrección de precio de huevo), ya
implementados y desplegados.

## Corrección de encuadre respecto al backlog

El ítem 5 está redactado como si no existiera ningún aviso proactivo. La
verificación mostró que existe y que ya se genera: lo que falta es que alguien
lo muestre.

`CorregirPublicacionPrecioHuevoVigenteHandler`
(`ComandosPreciosHuevo.cs:431`) ya agrega, por cada despacho afectado,
una `NotificacionInternaDespachoHuevo.ParaAjusteCredito(despacho.Id,
despacho.ClienteId, meta)` con el **`ClienteId` relleno** —es decir, dirigida
a la bandeja del tenant, no a la global de CAISY— y con el monto en Bs y el
motivo dentro de `Meta`. `ConfirmarRecepcionDespachoHuevoHandler` hace lo
mismo con `ParaRecepcionConfirmada`. Las filas están en la base de datos y
`GET /despachos-huevo/notificaciones` las devuelve con ETag y contador.

Del lado de CAISY esa bandeja **sí está construida**:
`RecepcionesHuevoController.cs:27` la consume y
`Views/RecepcionesHuevo/Index.cshtml:21` la muestra como "Novedades para
CAISY". Del lado de la PWA no hay una sola pantalla que llame al endpoint.

Y el hueco era conocido. El comentario de esa misma vista MVC
(`Index.cshtml:125-130`) dice literalmente:

> `DespachoRecibido` se crea con el `ClienteId` real del tenant emisor **(para
> su propia bandeja en la PWA)** y nunca puede aparecer acá.

O sea: el diseño de SP9C ya daba por hecho que la PWA iba a tener esta
bandeja. Nunca se construyó, y el ítem 5 la describió después como si fuera
una notificación nueva por inventar.

Este documento cubre exclusivamente ese hueco: construir en la PWA la bandeja
que ya se llena, y cerrar la brecha de rol que impide meter contenido
financiero en ella.

## Hallazgos de verificación que fijan el alcance

Cuatro hechos verificados durante el brainstorming. Los dos primeros
explican por qué el aviso explícito de "saldo negativo" se posterga; el
tercero es de por qué esta feature es obligatoria antes de cualquier otra
sobre la misma bandeja; el cuarto acota el diseño.

### 1. El comentario del código sobre `CreditoInsuficiente` dice más de lo que hace

`ComandosPedidosAlimento.cs:382-386` afirma que la notificación
`CreditoInsuficiente` "se genera exactamente cuando el saldo resultante da
negativo, confirmado o no". No es así. Si el comando llega sin
`ConfirmarCreditoInsuficiente`, la línea 363 lanza
`CreditoInsuficienteRequiereConfirmacionException` y la transacción explícita
se revierte completa, así que la ejecución nunca alcanza la línea 386. En la
práctica la bandeja global de CAISY solo ve envíos **confirmados** con
crédito insuficiente. El comentario queda como está: corregirlo es un cambio
ajeno a esta feature y no afecta su diseño, pero la afirmación no debe
reusarse como premisa.

### 2. El paso del tiempo nunca puede volver negativo un saldo

`RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync` usa el parámetro
`hoy` en un solo lugar: `fechaCorte = hoy - DiasDisponibilidadCredito` (14
días), que filtra qué despachos recibidos ya cuentan como ingreso. Al avanzar
la fecha, `fechaCorte` avanza y entran *más* despachos: el término `ingresos`
es monótono creciente. Los otros tres términos (`recibidoReal`,
`comprometidoPendiente`, `ajustes`) no dependen de `hoy`.

Consecuencia: el conjunto de comandos que pueden bajar el saldo es cerrado y
chico, y no existen cruces a negativo que ocurran sin que corra un comando.
Se verificó además que no hay ningún comando que anule o revierta un despacho
recibido ni una recepción de pedido, y que `new AjusteCreditoHuevo(` aparece
en un solo sitio de todo el código.

| Comando | Efecto en el saldo | ¿Puede cruzar a negativo? |
|---|---|---|
| Envío de pedido de alimento | `comprometidoPendiente` ↑ | Sí |
| Recepción de pedido de alimento | `−ΣSubtotalSolicitado`, `+TotalRecibido` | Sí, solo si se recibió de más |
| Corrección de publicación de precio de huevo vigente | `ajustes ±monto` | Sí, con ajuste negativo |
| Recepción de despacho de huevo | agrega un ingreso que recién cuenta 14 días después | No, nunca |

(`PedidoAlimento.ConfirmarRecepcion`, en `PedidoAlimento.cs:245`, admite
diferencia positiva: `CantidadRecibida` puede exceder `CantidadEntregada`, y
por eso la recepción puede bajar el saldo.)

Este análisis queda registrado acá para la sesión que encare el aviso
explícito de saldo negativo, y no se usa en esta feature.

### 3. La bandeja del tenant tiene una fuga de rol preexistente

`ListarNotificacionesDespachoHuevoHandler` filtra únicamente por
`usuarioActual.ClienteId`, y el grupo tenant del endpoint autoriza por
entitlement (`PoliticasClientes.Para(Funcionalidades.DespachoHuevo)`), no por
rol. Un **Trabajador** comparte el `ClienteId` del tenant —`ReglasRol.
RequiereCliente` confirma que Cliente y Trabajador son los dos únicos roles
con `ClienteId`—, así que hoy ya puede leer las notificaciones
`AjusteCredito`, que llevan el monto en Bs dentro de `Meta`.

Es la misma familia de brecha que cerró el ítem 2 del backlog. Hoy no se
manifiesta porque ninguna pantalla de la PWA muestra la bandeja; construirla
sin cerrar la brecha la volvería visible.

### 4. El mismo handler sirve a los dos alcances

`MapNotificacionesDespachoHuevo` se invoca dos veces en
`DespachosHuevoEndpoints.cs:123-124`, con el grupo `tenant` y con el grupo
`caisy`. Los dos alcances comparten handler y se separan solo por
`usuarioActual.ClienteId` (nulo para las cuentas de CAISY). Eso condiciona
cómo se puede escribir el gate de rol (ver decisión 2).

## Objetivo

Que el Cliente vea en la PWA, sin tener que entrar a buscar nada, las
novedades que ya se generan sobre sus despachos de huevo y sobre los ajustes
de su crédito; y que el Trabajador del mismo tenant no vea las que llevan
plata.

## Alcance y límites

Incluye:

- Un bloque de novedades en `DespachosHuevoPage.tsx` que consume
  `GET /despachos-huevo/notificaciones` y `POST
  /despachos-huevo/notificaciones/{id}/marcar-leida`, calcado del bloque
  "Novedades de CAISY" que ya funciona en `PedidosAlimentoPage.tsx`.
- Un gate de visibilidad por tipo de notificación y rol, aplicado de forma
  coherente en el listado, el contador y el marcado como leída.
- El monto del `Meta` del ajuste de crédito con cuatro decimales, consistente
  con la decisión del ítem 4 del backlog.

Queda fuera de esta versión:

- **Un tipo `SaldoNegativo` nuevo y la instrumentación de comandos para
  detectar el cruce a negativo.** Se descartó para esta feature: el caso que
  hoy es una sorpresa real para el cliente —el ajuste por corrección de
  precio, que le baja el saldo sin que él haga nada— ya genera notificación,
  y mostrarla cubre el objetivo del ítem 5 sin tocar ningún comando, sin
  valor de enum nuevo, sin migración y sin inventar una semántica
  anti-repetición. Vuelve al backlog como ítem nuevo, con el análisis de la
  sección "Hallazgos" ya hecho, para decidirse con la bandeja en uso.
- Replicar `CreditoInsuficiente` al Cliente. Le avisaría de algo que acaba de
  confirmar en el diálogo de SP9E, y no cubre el caso del ajuste.
- Retirar o unificar el tipo `CreditoInsuficiente`. Cambiaría el
  comportamiento ya desplegado de la bandeja de CAISY.
- Cualquier campana global o centro de notificaciones transversal de la PWA.
  Arrastra una decisión de producto (qué entra en esa campana, de qué
  módulos) que el ítem 5 no pide.
- Sondeo periódico, uso del parámetro `since` o manejo de ETag en el cliente.
  El precedente de `PedidosAlimentoPage` no los usa y esta bandeja no tiene
  un requisito de frescura que los justifique. El endpoint los conserva
  intactos.
- Cola offline o IndexedDB. `DespachosHuevoPage.tsx:13` declara la bandeja de
  despachos "deliberadamente online", igual que la de pedidos.
- Cambios en la bandeja de CAISY (`RecepcionesHuevo/Index.cshtml`) o en
  `Trajano.GestorCaisy`. Su pantalla ya existe y esta feature no la altera.
- La clave `Recibido` faltante en `ETIQUETAS_ESTADO` de
  `despacho-huevo/constantes.ts`, que hace caer ese estado al fallback con un
  chip gris. Observado y dejado fuera a propósito: es cosmético y ajeno al
  ítem.
- Corregir el comentario impreciso de `ComandosPedidosAlimento.cs:382-386`.

## Decisiones de diseño

### 1. El bloque va en `DespachosHuevoPage`

Es el espejo literal de `RecepcionesHuevo/Index.cshtml` en GestorCaisy y de
"Novedades de CAISY" en `PedidosAlimentoPage.tsx`. Los dos tipos que pueden
llegar al alcance tenant (`DespachoRecibido`, `AjusteCredito`) traen
`DespachoHuevoId` relleno, así que el enlace "Ver despacho" funciona para
ambos sin casos nulos que justificar.

Descartado ponerlo en `PedidosAlimentoPage` junto a las novedades de pedido:
obligaría a navegar cross-feature hacia `/despachos/{id}` y dejaría dos
bandejas distintas, de dos entidades distintas, apiladas con dos contadores
separados en la misma pantalla. Descartado ponerlo en las dos: duplica
bloque, query y tests, y obliga a resolver la invalidación cruzada al marcar
leída desde una de ellas.

### 2. El gate de rol se expresa como exclusión del Trabajador, no como inclusión del Cliente

Esta es la decisión menos intuitiva del diseño y la más fácil de implementar
mal.

El patrón establecido en el módulo es el gate positivo de
`ObtenerBalanceCreditoHuevoHandler`: `if (usuarioActual.Rol != "Cliente")
throw new CreditoHuevoRequiereRolClienteException(...)`. Ahí funciona porque
`GET /despachos-huevo/credito` es exclusivo del alcance tenant.

Acá no se puede copiar. Por el hallazgo 4, el mismo handler sirve al grupo
tenant y al grupo caisy, y los cuatro roles del sistema son `Administrador`,
`Cliente`, `Trabajador` y `GestorCaisy` (`Rol.cs`). Un gate positivo de
"solo Cliente" le sacaría a `GestorCaisy` y a `Administrador` sus
notificaciones `CreditoInsuficiente`, rompiendo la pantalla ya desplegada de
GestorCaisy y sus pruebas.

La regla correcta es: **los tipos financieros no se muestran al rol
`Trabajador`**; para cualquier otro rol nada cambia. Como en el alcance
tenant solo puede haber un Cliente o un Trabajador, la regla cubre
exactamente el caso que hay que cerrar, y la ausencia de cambios para los
otros dos roles se blinda con una prueba de regresión explícita.

Clasificación de los tipos:

| Tipo | Clase | Contenido de `Meta` |
|---|---|---|
| `DespachoRecibido` | Operativo | nulo |
| `CreditoInsuficiente` | Financiero | id del pedido de alimento |
| `AjusteCredito` | Financiero | monto en Bs y motivo |

`DespachoRecibido` se mantiene visible para el Trabajador: es él quien
registra los despachos en la PWA, y saber que CAISY los recibió es parte de
su trabajo. Se descartó cerrar la bandeja completa al Cliente (una línea
menos, pero le quita al Trabajador un aviso legítimamente operativo) y se
descartó mostrarle los tipos financieros con `Meta` vacío (un "hubo un ajuste
de crédito" que no se puede consultar es peor que no mostrarlo).

### 3. La regla vive en Application y usa el rol como literal

`Icarus.GestionAvicola.Application` referencia únicamente su propio `Domain` y
`BuildingBlocks.Application`, y la prueba de arquitectura
`ReglasDeModulosTests.GestionAvicolaNoSeReferenciaConOtrosModulos` lo blinda.
Por eso el rol se compara como literal `"Trabajador"`, igual que el
`"Cliente"` que ya usa `ObtenerBalanceCreditoHuevoHandler`. **No se puede
usar el enum `Icarus.Identity.Domain.Rol`**, aunque sea lo que uno querría
escribir: agregar esa referencia rompe el gate de arquitectura.

La regla queda en un único tipo estático, sin estado, en la carpeta
`NotificacionesDespachoHuevo/`, para que los tres handlers deriven de ella el
mismo conjunto y no haya dos fuentes de verdad.

### 4. El conjunto de tipos visibles baja al repositorio y se filtra en SQL

Se descartó filtrar en memoria dentro del handler tras un `ListarAsync` sin
filtro. Dos razones:

- **Coherencia por construcción.** El listado y el contador de no leídas
  tienen que coincidir; si el filtro vive solo en el listado, el contador
  cuenta filas que el usuario no puede ver. Pasando el conjunto por el puerto,
  los dos métodos filtran por lo mismo y la incoherencia deja de ser posible.
- No se traen del servidor filas que después se descartan.

El radio de impacto del cambio de firma es chico: los cuatro archivos de
prueba que crean un `Substitute.For<INotificacionesInternasDespachoHuevo>()`
(`ConfirmarRecepcionDespachoHuevoHandlerTests`,
`CorregirPublicacionPrecioHuevoHandlerTests`, `NotificacionesInternasTests`,
`PedidosAlimentoHandlerTests`) solo usan `Agregar`, así que no los toca.

### 5. El contador no usado recibe el mismo filtro, no se borra

`ContarNotificacionesDespachoHuevoNoLeidasQuery` y su handler existen pero no
los mapea ningún endpoint ni los cubre ninguna prueba. Se les aplica el mismo
conjunto de tipos visibles en vez de borrarlos: mantiene el puerto íntegro,
respeta la regla de preservar cambios ajenos, y evita la trampa de que el
próximo que mapee ese contador exponga al Trabajador el número de
notificaciones financieras sin darse cuenta.

### 6. El marcado como leída también valida el tipo

No estaba en el planteo original del ítem y se agrega por consistencia de la
regla. Hoy `MarcarNotificacionDespachoHuevoLeidaHandler` solo cruza
`ClienteId`. Un Trabajador que acertara un GUID podría marcar leída una
`AjusteCredito` y hacérsela desaparecer al Cliente antes de que la vea. No
filtra contenido, pero es la misma regla y son dos líneas: si el tipo no está
en el conjunto visible para el rol, se lanza el mismo `NotFoundException`
genérico que ya se usa para el cruce de tenant —sin revelar que la
notificación existe.

### 7. El monto del `Meta` del ajuste pasa a cuatro decimales

`ComandosPreciosHuevo.cs:434` compone el `Meta` con `{monto:0.00}`, pero el
ítem 4 del backlog decidió que las cifras del crédito de huevo van con cuatro
decimales, y `AjustesCreditoHuevo.tsx:23` ya muestra el mismo monto con
`formatoMonedaExacta`. Mostrar el `Meta` crudo sin corregirlo haría aparecer
la misma plata con dos decimales en la novedad y con cuatro en el desglose de
la pantalla vecina — exactamente la inconsistencia que el ítem 4 vino a
arreglar.

El cambio es `0.00` → `0.0000`. Las filas ya escritas conservan dos
decimales: son histórico y no se reescriben. No hace falta tocar la constante
de truncado del motivo (470): el assert existente de
`UnMotivoLargoNoDesbordaElMetaDeLaNotificacion` es `meta!.Length <= 500`, y
470 más `" Bs — "` deja 24 caracteres para el monto, de sobra para dos
decimales más.

Se descartó reformatear en el frontend: exigiría un regex sobre el texto
libre del `Meta`, que se rompe en silencio si el formato cambia.

### 8. No hay dominio, enum ni migración nuevos

`Tipo` se persiste con `HasConversion<int>()`
(`ConfiguracionNotificacionInternaDespachoHuevo.cs`). Esta feature no agrega
valores al enum ni columnas, así que no hay migración. La entidad
`NotificacionInternaDespachoHuevo`, sus factories y su configuración de EF
quedan intactas.

## Contrato

### Application

Un tipo estático nuevo en
`NotificacionesDespachoHuevo/VisibilidadNotificacionesDespachoHuevo.cs` que
resuelve, a partir del rol, el conjunto de tipos visibles. Los tipos
financieros se excluyen solo para `"Trabajador"`.

`INotificacionesInternasDespachoHuevo` cambia dos firmas para recibir el
conjunto; `Agregar` y `ObtenerPorIdAsync` no cambian:

```csharp
Task<IReadOnlyList<NotificacionInternaDespachoHuevo>> ListarAsync(
    Guid? clienteId,
    IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
    CancellationToken cancellationToken = default);

Task<int> ContarNoLeidasAsync(
    Guid? clienteId,
    IReadOnlyCollection<TipoNotificacionDespachoHuevo> tiposVisibles,
    CancellationToken cancellationToken = default);
```

Los tres handlers de `ComandosNotificacionesDespachoHuevo.cs` derivan el
conjunto de `usuarioActual.Rol` y lo pasan hacia abajo. El DTO
`NotificacionDespachoHuevoResumen` no cambia: ya expone `Tipo`,
`DespachoHuevoId`, `FechaUtc`, `Leida` y `Meta`, que es todo lo que la
bandeja necesita.

### Infrastructure

`RepositorioNotificacionesInternasDespachoHuevo` agrega la condición de tipo
a las dos consultas. El índice existente `(ClienteId, FechaUtc)` sigue
sirviendo: el filtro de tipo se aplica sobre un conjunto ya acotado por
tenant y no justifica un índice nuevo.

### Host

Sin cambios. `DespachosHuevoEndpoints.cs` ya mapea los dos endpoints en los
dos grupos, con ETag y `since`. El contador que el endpoint devuelve se
calcula desde el listado ya filtrado (`notificaciones.Count(n => !n.Leida)`),
así que queda consistente con el filtro sin tocar nada.

### Frontend

`web/src/features/despacho-huevo/api.ts` agrega el tipo
`NotificacionDespachoHuevo` (`id`, `tipo`, `despachoHuevoId: string | null`,
`fechaUtc`, `leida`, `meta: string | null`), la función de listado —que
devuelve `{ items, contador }`— y la de marcar leída.

`constantes.ts` agrega `mensajeNotificacionDespachoHuevo`, con una rama por
tipo y un default genérico, igual que `mensajeNotificacion` en
`pedidos-alimento/constantes.ts`. Sin regex: el `Meta` del ajuste es texto
plano y se muestra crudo.

## Presentación

Un `Paper variant="outlined"` sobre la tabla de despachos, con el título
"Novedades del despacho de huevo (N)" donde N es el contador de no leídas.
Lista densa con las no leídas, primeras cinco, cada una con el mensaje del
tipo como línea principal, el `Meta` —cuando existe— y la fecha local como
línea secundaria, un enlace al despacho cuando `despachoHuevoId` no es nulo,
y un `IconButton` con `aria-label="Marcar como leída"`. Calcado de
`PedidosAlimentoPage.tsx:138-168`.

El bloque no se renderiza cuando no hay no leídas.

Anti-PII: el `Meta` del ajuste lleva el monto y el motivo escrito por CAISY,
y ya se le muestra al Cliente en `AjustesCreditoHuevo.tsx:23` y en
`_CreditoHuevo.cshtml`, así que esta pantalla no expone nada nuevo. El `Meta`
de `CreditoInsuficiente` es un GUID de pedido y nunca alcanza el alcance
tenant, porque se crea con `ClienteId` nulo. Ninguna notificación lleva datos
nominales.

## Manejo de errores

La degradación sale del patrón del precedente sin escribir nada: la query se
consume como `const { data: notificaciones } = useQuery(...)` sin mirar
`isError`, y la guarda de renderizado es la longitud de las no leídas, que
con `notificaciones` en `undefined` da falso. Si la consulta falla, el bloque
no aparece y la tabla de despachos —lo principal de la pantalla— sigue
funcionando. Es la misma lección del fix de `PedidosController` que registra
el HANDOFF, obtenida por construcción en vez de por código defensivo.

`MarcarLeida(actorId)` ya es idempotente en el dominio, así que el doble clic
no necesita tratamiento propio.

## Pruebas

**Unit (`Icarus.UnitTests`):**

- La regla de visibilidad: el Trabajador no recibe los tipos financieros; el
  Cliente recibe los tres; `GestorCaisy` y `Administrador` reciben los tres
  —prueba de regresión explícita de la decisión 2.
- `ListarNotificacionesDespachoHuevoHandler` pasa al repositorio el alcance y
  el conjunto correctos según el rol. Hoy no existe ninguna prueba de este
  handler.
- `ContarNotificacionesDespachoHuevoNoLeidasHandler`, lo mismo.
- `MarcarNotificacionDespachoHuevoLeidaHandler` responde con el
  `NotFoundException` genérico cuando el tipo no es visible para el rol, y
  sigue marcando leída cuando sí lo es.
- El `Meta` del ajuste de crédito se compone con cuatro decimales.

**Integration (`Icarus.IntegrationTests`):** el endpoint tenant con sesión de
Trabajador no devuelve `AjusteCredito` y su contador no la cuenta; con sesión
de Cliente sí la devuelve.

**Frontend (`web`):** la bandeja renderiza el mensaje de cada tipo; marca
leída e invalida la query; no se renderiza cuando no hay no leídas.

## Verificación

Puerta de calidad completa (`./verify.ps1`) antes de cada commit y push, con
Docker corriendo para los tests de integración con Testcontainers.MsSql.
Prohibido `--no-verify` y prohibido relajar baselines, umbrales o exclusiones
de `quality/`.
