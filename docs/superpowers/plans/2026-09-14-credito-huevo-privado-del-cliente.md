# Corrección del bloque 9 — El crédito de huevo es privado del Cliente y no gobierna el pedido — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-09-14-credito-huevo-privado-del-cliente-design.md`

**Goal:** El saldo de crédito por despachos de huevo lo ve únicamente el Cliente; el envío de un pedido de alimento deja de depender del saldo (sin bloqueo, sin confirmación, sin alerta); y el saldo deja de descontar pedidos que todavía no se recibieron.

**Architecture:** Cuatro cortes independientes entre sí. (1) `RepositorioBalanceCreditoHuevo` pierde el componente «comprometido pendiente». (2) `EnviarPedidoAlimentoHandler` deja de lanzar, de exigir rol y de notificar; solo deja una marca sin cifras en el historial y las cifras en el registro de vuelo. (3) `ObtenerCreditoHuevoDePedidoCaisyHandler` gana un gate de rol `Cliente` que, por estar el endpoint en el grupo de CAISY, lo deja inerte. (4) Las dos interfaces dejan de mostrar el crédito donde no corresponde y lo muestran donde sí.

**Tech Stack:** .NET / MediatR / EF Core (backend), ASP.NET MVC (Trajano.GestorCaisy), React + TanStack Query + MUI (PWA), xUnit + NSubstitute + Testcontainers.MsSql (tests backend), Vitest + Testing Library (tests frontend).

## Global Constraints

- Todo texto de negocio (mensajes, comentarios, nombres de test) en español correcto, con acentos, UTF-8 sin BOM. **Español neutro, sin voseo**: nunca «confirmá», «podés», «enviá».
- TDD real: cada test se corre en rojo antes de escribir el código que lo pone en verde.
- **No borrar código de producción del camino de CAISY ni del camino de confirmación.** Decisión explícita del usuario: se oculta e inhabilita, marcado en comentario como retirado. La única excepción son los tests que afirman exactamente lo contrario del nuevo requisito: esos se reemplazan, porque un test que afirma «CAISY ve el saldo» no puede convivir con el requisito «CAISY no ve el saldo».
- Ningún commit usa `--no-verify`. Un commit por task, con el test dirigido en verde.
- La rama de trabajo es `develop`; commits directos, sin PR.
- Docker corriendo: los tests de integración usan Testcontainers.MsSql.
- No registrar en el registro de vuelo nada que no sean ids técnicos, estados o montos agregados.

---

## Mapa de archivos

| Archivo | Acción |
|---|---|
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioBalanceCreditoHuevo.cs` | Modificar: fuera el componente «comprometido pendiente» |
| `Icarus/tests/Icarus.IntegrationTests/BalanceCreditoHuevoTests.cs` | Modificar: el test del comprometido se invierte |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs` | Modificar: envío sin bloqueo, sin gate de rol, sin notificación; marca sin cifras |
| `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs` | Modificar: 3 tests se invierten |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs` | Modificar: comentarios de retiro |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevoCaisy.cs` | Modificar: gate de rol `Cliente` |
| `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerCreditoHuevoDePedidoCaisyHandlerTests.cs` | Modificar: rol en el fake + 2 tests nuevos de 403 |
| `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs` | Modificar: 3 tests se invierten, helper simplificado |
| `Icarus/tests/Icarus.IntegrationTests/DocumentosNotaEndpointsTests.cs` | Modificar: una llamada a `/enviar` |
| `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs` | Modificar: deja de pedir el crédito a la API |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Detalles.cshtml` | Modificar: fuera el `<partial name="_CreditoHuevo">` |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Aceptar.cshtml` | Modificar: ídem |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Despachar.cshtml` | Modificar: ídem |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/RecepcionesHuevo/Index.cshtml` | Modificar: fuera la etiqueta de `CreditoInsuficiente` |
| `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs` | Modificar: los tests de visibilidad se invierten |
| `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPedidosTests.cs` | Modificar: ídem |
| `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx` | Modificar: diálogo de envío sin bloque de crédito |
| `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx` | Modificar: el test del flujo de confirmación se invierte |
| `web/src/features/despacho-huevo/DespachoHuevoFormularioPage.tsx` | Modificar: el Cliente ve su saldo |
| `web/src/features/despacho-huevo/DespachoHuevoFormularioPage.test.tsx` | Modificar: 2 tests nuevos |
| `web/src/features/despacho-huevo/constantes.ts` | Modificar: comentario de retiro en `CreditoInsuficiente` |
| `docs/ai/HANDOFF.md` | Modificar: cierre |

**Sin cambios, a propósito:** `VisibilidadNotificacionesDespachoHuevo` (el Trabajador ya está excluido), `ObtenerBalanceCreditoHuevoHandler` (ya exige rol `Cliente`), `TipoNotificacionDespachoHuevo` (el valor 1 no se renumera nunca), `PedidoFormularioPage.tsx` (ya muestra el saldo con gate de rol), `AjustesCreditoHuevo.tsx`.

---

### Task 1: El saldo deja de descontar los pedidos que todavía no llegaron

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioBalanceCreditoHuevo.cs:40-66`
- Test: `Icarus/tests/Icarus.IntegrationTests/BalanceCreditoHuevoTests.cs:116-135`

**Interfaces:**
- Produces: `ObtenerSaldoDisponibleAsync` devuelve `ingresos − recibidoReal + ajustes`. Las Tasks 2 y 3 lo consumen sin cambios de firma.

- [ ] **Step 1: Invertir el test, verlo en rojo**

En `BalanceCreditoHuevoTests.cs`, reemplazar el test
`PedidoSoloSolicitadoSinRecepcionRealRestaComoComprometidoPendiente` completo por:

```csharp
    // Corrección 2026-09-14: el saldo es la cuenta real, no una proyección.
    // Un pedido enviado y todavía no recibido no consumió nada — CAISY
    // todavía puede rechazarlo o devolverlo, y un saldo que rebota hacia
    // arriba cuando eso pasa no es un saldo. El componente «comprometido
    // pendiente» existía solo para que la advertencia de crédito
    // insuficiente no se pudiera burlar con envíos sucesivos; sin
    // advertencia, no tiene razón de ser.
    [Fact]
    public async Task PedidoSolicitadoSinRecepcionRealNoDescuentaDelSaldo()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var pedido = new PedidoAlimento(clienteId, actorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 100)]);
        pedido.EnviarACaisy(FechasNegocio.Hoy(), actorId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        await SembrarAsync(pedido);

        var saldo = await SaldoDeAsync(clienteId);

        Assert.Equal(0m, saldo);
    }
```

Correr solo este test y **ver el `-18000` fallando contra el `0` esperado**:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~PedidoSolicitadoSinRecepcionRealNoDescuentaDelSaldo"
```

- [ ] **Step 2: Quitar el componente del cálculo**

En `RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync`, borrar el
bloque completo del comentario `// Comprometido pendiente: ...` junto con la
consulta `var comprometidoPendiente = ...` (unas 20 líneas), y cambiar el
retorno a:

```csharp
        // Corrección 2026-09-14: el saldo es la cuenta real. No entra el
        // alimento pedido y todavía no recibido: ese alimento no llegó, no
        // consumió crédito, y el pedido todavía puede ser rechazado o
        // devuelto por CAISY. El componente «comprometido pendiente» que
        // vivía acá existía solo para blindar la advertencia de crédito
        // insuficiente al enviar, retirada por esta misma corrección.
        return ingresos - recibidoReal + ajustes;
```

- [ ] **Step 3: Verificar y comitear**

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~BalanceCreditoHuevoTests"
```

Los 5 tests restantes del archivo deben seguir en verde sin tocarlos.

Commit: `fix(avicola): el saldo de credito de huevo deja de descontar pedidos en transito`

---

### Task 2: El envío del pedido deja de depender del saldo

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs:38-48, 296-400`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs:303-402`

**Interfaces:**
- Produces: `EnviarPedidoAlimentoHandler` nunca lanza por crédito, nunca exige rol y nunca agrega una `NotificacionInternaDespachoHuevo`. El historial de la transición Borrador→Solicitado lleva `"Enviado con crédito insuficiente."` (sin cifras) cuando el saldo proyectado da negativo, y `null` en cualquier otro caso.
- Se conservan inertes: `EnviarPedidoAlimentoCommand.ConfirmarCreditoInsuficiente` y `CreditoInsuficienteRequiereConfirmacionException`.

- [ ] **Step 1: Invertir los tres tests, verlos en rojo**

En `PedidosAlimentoHandlerTests.cs`, reemplazar los tres tests
`EnviarSinConfirmarConSaldoInsuficienteExigeConfirmacion`,
`EnviarConfirmandoSaldoInsuficienteAvisaACaisyYDejaMotivoEnHistorial` y
`UnTrabajadorNoPuedeConfirmarElEnvioConCreditoInsuficiente` por estos dos:

```csharp
    // Corrección 2026-09-14: el envío NO depende del saldo. Sin bloqueo, sin
    // confirmación y sin alerta a CAISY. Queda solo la marca en el historial
    // del pedido, y sin cifras: ese historial lo lee CAISY en su vista de
    // Detalles, así que cualquier número ahí sería la misma fuga que esta
    // corrección cierra por el frente.
    [Fact]
    public async Task EnviarConSaldoInsuficienteProcedeYDejaLaMarcaSinCifras()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(PublicacionVigente());
        _balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(0m);

        await CrearEnviador().Handle(
            new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None);

        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);
        var transicion = Assert.Single(pedido.Historial);
        Assert.Equal("Enviado con crédito insuficiente.", transicion.Motivo);
        Assert.DoesNotContain("saldo", transicion.Motivo!, StringComparison.OrdinalIgnoreCase);
        _notificacionesDespachoHuevo.DidNotReceive()
            .Agregar(Arg.Any<NotificacionInternaDespachoHuevo>());
        await _transaccion.Received(1).ConfirmarAsync(Arg.Any<CancellationToken>());
    }

    // El Trabajador puede pedir alimento: enviar es una operación operativa.
    // La decisión financiera que antes exigía rol Cliente ya no existe, y el
    // Trabajador sigue sin poder VER el saldo (eso lo cierra
    // ObtenerBalanceCreditoHuevoHandler, que no cambia).
    [Fact]
    public async Task UnTrabajadorEnviaIgualConSaldoInsuficiente()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(PublicacionVigente());
        _balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(0m);
        _usuarioActual.Rol.Returns("Trabajador");

        await CrearEnviador().Handle(
            new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None);

        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);
        await _transaccion.Received(1).ConfirmarAsync(Arg.Any<CancellationToken>());
    }
```

El test existente `EnviarConSaldoSuficienteNoAvisaYConfirmaElEnvio` se
conserva sin cambios: sigue siendo válido.

Correr y ver los dos nuevos en rojo:

```
dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~PedidosAlimentoHandlerTests"
```

- [ ] **Step 2: Reescribir el bloque de crédito del handler**

En `ComandosPedidosAlimento.cs`, reemplazar el bloque que empieza en el
comentario `// Crédito (spec SP9E): ...` y termina en el cierre del `if
(haySaldoInsuficiente)` interno, por:

```csharp
        // Crédito (corrección 2026-09-14): el envío NO depende del saldo. No
        // hay validación, ni bloqueo, ni confirmación, ni alerta: el saldo es
        // información del Cliente, no una regla del pedido. Se sigue
        // calculando con un único fin — dejar rastro de que el pedido salió
        // con la cuenta en rojo: una marca SIN cifras en el historial del
        // pedido (que CAISY lee en su vista de Detalles) y las cifras
        // completas en el registro de vuelo, que CAISY no consulta. Si falta
        // precio para algún tipo no se evalúa nada: EnviarACaisy falla más
        // abajo con su propio mensaje, que debe prevalecer.
        var preciosPorTipo = precios.ToDictionary(p => (p.Tipo, p.Presentacion), p => p.PrecioFinalPor40Kg);
        string? motivoCreditoInsuficiente = null;
        decimal? saldoActual = null;
        decimal? totalEsperado = null;
        if (pedido.Detalles.All(d => preciosPorTipo.ContainsKey((d.TipoAlimento, d.Presentacion))))
        {
            totalEsperado = pedido.Detalles.Sum(
                d => preciosPorTipo[(d.TipoAlimento, d.Presentacion)] * d.Equivalentes40Kg);
            saldoActual = await balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
                pedido.ClienteId, hoy, cancellationToken);
            if (saldoActual - totalEsperado < 0)
                motivoCreditoInsuficiente = "Enviado con crédito insuficiente.";
        }
```

Borrar el bloque `if (haySaldoInsuficiente) notificacionesDespachoHuevo.Agregar(...)`
junto con su comentario. **No** borrar el parámetro
`INotificacionesInternasDespachoHuevo notificacionesDespachoHuevo` del
constructor primario: un parámetro de constructor primario sin uso no genera
warning y el registro en DI se conserva; agregarle encima este comentario:

```csharp
    // Retirado por la corrección 2026-09-14: ya no se emite ninguna
    // notificación por saldo negativo. El parámetro se conserva para no
    // alterar el registro en DI ni la firma que usan los tests.
```

Si el compilador con warnings-as-errors se queja del parámetro sin uso,
mantenerlo usándolo en nada no es opción: en ese caso **sí** quitarlo del
constructor y de `DependencyInjection.cs` si corresponde, y anotarlo en el
resumen de cierre. Verificar con `dotnet build Icarus/Icarus.sln`.

Extender el `Decidir` final para llevar las cifras al registro de vuelo:

```csharp
        registroVuelo.Decidir("avicola.pedidos.enviar", "envio", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = pedido.Detalles.Count,
                ["NotificacionPreciosId"] = vigente.Id,
                ["SaldoCreditoAntes"] = saldoActual,
                ["TotalPedido"] = totalEsperado,
            });
```

y declarar esas dos claves en el descriptor del comando (sin declararlas, el
registro las descarta en silencio):

```csharp
public sealed record EnviarPedidoAlimentoCommand(
    // `ConfirmarCreditoInsuficiente` quedó INERTE con la corrección
    // 2026-09-14: el envío ya no valida el saldo. Se conserva para no romper
    // el contrato del endpoint ni los clientes que todavía lo envían.
    Guid PedidoId, bool ConfirmarCreditoInsuficiente = false)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.enviar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["Lineas"] = DatoRegistroVuelo.Entero,
            ["SaldoCreditoAntes"] = DatoRegistroVuelo.Decimal,
            ["TotalPedido"] = DatoRegistroVuelo.Decimal,
        });
}
```

- [ ] **Step 3: Marcar la excepción como retirada**

En `PuertoCreditoHuevo.cs`, sustituir el comentario de
`CreditoInsuficienteRequiereConfirmacionException` por:

```csharp
// RETIRADA por la corrección 2026-09-14: el envío de un pedido de alimento ya
// no depende del saldo, así que nadie lanza esta excepción. Se conserva, sin
// borrar, por decisión explícita del usuario. Nota: su mensaje por defecto
// llevaba voseo («Confirmá»), prohibido en este proyecto — si alguna vez se
// revive, reescribirlo en español neutro.
```

y en el comentario de `CreditoHuevoRequiereRolClienteException`, cambiar la
última frase para que refleje los dos puntos vigentes:

```csharp
// Un solo tipo para los dos puntos que la disparan:
// ObtenerBalanceCreditoHuevoHandler y (desde la corrección 2026-09-14)
// ObtenerCreditoHuevoDePedidoCaisyHandler.
```

- [ ] **Step 4: Verificar y comitear**

```
dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~PedidosAlimentoHandlerTests"
dotnet build Icarus/Icarus.sln
```

Commit: `fix(avicola): el envio de pedido de alimento deja de depender del credito`

---

### Task 3: El crédito por pedido queda cerrado para CAISY

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevoCaisy.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerCreditoHuevoDePedidoCaisyHandlerTests.cs`

**Interfaces:**
- Produces: `ObtenerCreditoHuevoDePedidoCaisyHandler` lanza `CreditoHuevoRequiereRolClienteException` (403) para todo rol distinto de `Cliente`. Como el endpoint vive en el grupo `/pedidos-alimento-caisy`, en la práctica responde 403 siempre. Task 4 depende de esto.

- [ ] **Step 1: Escribir los tests que fallan**

En `ObtenerCreditoHuevoDePedidoCaisyHandlerTests.cs`, agregar un campo
`ICurrentUser` sustituto (mismo patrón que
`ObtenerBalanceCreditoHuevoHandlerTests`), pasarlo al constructor del handler
en todos los tests, con `Rol` devolviendo `"Cliente"` por defecto —los seis
tests de cálculo siguen documentando la fórmula y deben quedar en verde—, y
agregar estos dos:

```csharp
    // Corrección 2026-09-14: el saldo del Cliente es privado. Ningún
    // funcionario de CAISY lo ve, ni el gestor de pedidos de alimento ni el
    // de recepción de huevos. Como este endpoint vive en el grupo de CAISY,
    // el gate lo deja inerte a propósito: el handler no se borra, se cierra.
    [Fact]
    public async Task UnGestorDeCaisyNoPuedeVerElCreditoDelCliente()
    {
        var pedido = PedidoSolicitado();
        _repositorioPedidos.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _usuarioActual.Rol.Returns("GestorCaisy");

        await Assert.ThrowsAsync<CreditoHuevoRequiereRolClienteException>(() =>
            CrearHandler().Handle(
                new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None));

        await _repositorioCredito.DidNotReceive().ObtenerSaldoDisponibleAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnTrabajadorNoPuedeVerElCreditoDelCliente()
    {
        var pedido = PedidoSolicitado();
        _repositorioPedidos.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _usuarioActual.Rol.Returns("Trabajador");

        await Assert.ThrowsAsync<CreditoHuevoRequiereRolClienteException>(() =>
            CrearHandler().Handle(
                new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None));
    }
```

Adaptar `PedidoSolicitado()` / `CrearHandler()` a los helpers que ya tenga el
archivo; si no existen, extraerlos de los tests actuales.

- [ ] **Step 2: Poner el gate en el handler**

En `ComandosCreditoHuevoCaisy.cs`, reemplazar el párrafo del comentario de
clase que dice «Sin gate de rol dentro del handler...» por:

```csharp
// CERRADO por la corrección 2026-09-14: el saldo del Cliente es privado y
// ningún funcionario de CAISY lo ve. El gate de rol de acá abajo, sumado a
// que el endpoint vive en el grupo /pedidos-alimento-caisy, deja este camino
// inerte: nadie que pueda llegar al endpoint pasa el gate. La consulta, el
// handler y el endpoint se conservan sin borrar por decisión explícita del
// usuario; el inventario de lo que quedó inerte está en la spec.
```

Inyectar `ICurrentUser usuarioActual` en el constructor primario y abrir
`Handle` con:

```csharp
        if (usuarioActual.Rol != "Cliente")
            throw new CreditoHuevoRequiereRolClienteException(
                "El crédito por despachos de huevo es exclusivo del Cliente.");
```

antes de cualquier consulta al repositorio (el test lo verifica con
`DidNotReceive`). Agregar `using Icarus.BuildingBlocks.Application;`.

- [ ] **Step 3: Invertir los tests de integración del endpoint**

En `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`:

- `ElCreditoDelPedidoDevuelveLasTresCifrasCoherentes`,
  `ElCreditoDeUnBorradorInformaQueNoPesaEnElSaldo`,
  `ElCreditoDelPedidoSoloLoVeCaisyConGestorPedidoAlimento` y
  `ElCreditoDeUnPedidoInexistenteDevuelve404` se reemplazan por uno solo:

```csharp
    // Corrección 2026-09-14: este endpoint quedó cerrado. Nadie que pueda
    // llegar hasta él pasa el gate de rol Cliente del handler, así que
    // responde 403 incluso con el token de CAISY correcto y con un pedido
    // que existe. El endpoint no se borró: se cerró.
    [Fact]
    public async Task ElCreditoDelPedidoYaNoSeLeEntregaANingunGestorDeCaisy()
    {
        var (cliente, token) = await ClienteAutenticadoAsync();
        var id = await CrearBorradorAsync(cliente, token);
        var tokenCaisy = await TokenGestorCaisyAsync();

        var respuesta = await cliente.SendAsync(Peticion(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{id}/credito", tokenCaisy));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
```

  Adaptar los helpers (`ClienteAutenticadoAsync`, `CrearBorradorAsync`,
  `TokenGestorCaisyAsync`, `Peticion`) a los nombres reales del archivo.

- `EnviarSinConfirmarConCreditoInsuficienteExigeConfirmacionYElReintentoLoAcepta`
  y `UnTrabajadorNoPuedeConfirmarElEnvioConCreditoInsuficiente` se reemplazan
  por uno solo:

```csharp
    // Corrección 2026-09-14: el saldo no frena el envío. Un único intento,
    // sin cuerpo de confirmación, y el pedido sale.
    [Fact]
    public async Task EnviarConCreditoInsuficienteProcedeAlPrimerIntento()
    {
        var (cliente, token) = await ClienteAutenticadoAsync();
        var id = await CrearBorradorAsync(cliente, token);

        var respuesta = await cliente.SendAsync(Peticion(
            HttpMethod.Post, $"/api/pedidos-alimento/{id}/enviar", token));

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }
```

- El helper de la línea 81 pierde su parámetro:

```csharp
    private static async Task<HttpStatusCode> EnviarAsync(
        HttpClient cliente, string token, Guid id) =>
        (await cliente.SendAsync(Peticion(
            HttpMethod.Post, $"/api/pedidos-alimento/{id}/enviar", token))).StatusCode;
```

  Ajustar todas sus llamadas.

- En `DocumentosNotaEndpointsTests.cs:121`, quitar el
  `JsonContent.Create(new { confirmarCreditoInsuficiente = true })` de la
  llamada a `/enviar`.

- [ ] **Step 4: Verificar y comitear**

```
dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ObtenerCreditoHuevoDePedidoCaisyHandlerTests"
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~PedidosAlimentoEndpointsTests"
```

Commit: `fix(avicola): el credito del cliente queda cerrado para los gestores de caisy`

---

### Task 4: `Trajano.GestorCaisy` deja de mostrar y de pedir el crédito

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs:332-360`
- Modify: `Views/Pedidos/Detalles.cshtml:60`, `Views/Pedidos/Aceptar.cshtml:16`, `Views/Pedidos/Despachar.cshtml:18`
- Modify: `Views/RecepcionesHuevo/Index.cshtml:124-142`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs:320-445`, `Integracion/FlujoPedidosTests.cs:278-360`

**Interfaces:**
- Produces: ninguna de las tres pantallas de decisión renderiza crédito, y el controlador no llama a la API de crédito ni una vez.

- [ ] **Step 1: Invertir los tests, verlos en rojo**

En `PedidosControllerTests.cs`, reemplazar los siete tests del bloque de
crédito (`DetallesLlevaElCreditoDelClienteAlModelo`,
`SiElCreditoFallaElDetalleSigueRenderizandoSinCredito`,
`SiLaRedFallaElDetalleSigueRenderizandoSinCredito`,
`SiElCuerpoDelCreditoEsIlegibleElDetalleSigueRenderizandoSinCredito`,
`AceptarLlevaElCreditoAlFormulario`, `DespacharLlevaElCreditoAlFormulario`,
`ElReRenderPorFechaPasadaConservaElCredito`,
`SiElCreditoFallaLaPantallaDeAceptacionSigueRenderizando`) por:

```csharp
    // Corrección 2026-09-14: el saldo del cliente es privado. GestorCaisy no
    // lo ve ni lo pide: el controlador ni siquiera llama a la API, así que
    // tampoco hay degradación que probar.
    [Theory]
    [InlineData("Detalles")]
    [InlineData("Aceptar")]
    [InlineData("Despachar")]
    public async Task NingunaPantallaDeDecisionPideNiLlevaElCreditoDelCliente(string accion)
    {
        var id = Guid.NewGuid();
        _api.CreditoDePedido = new CreditoHuevoPedidoApi(-5000m, 14120m, 9120m, true, []);

        var resultado = await EjecutarAccionAsync(accion, id);

        Assert.Equal(0, _api.VecesObtenerCredito);
        Assert.Null(CreditoDelModelo(resultado));
    }
```

Escribir `EjecutarAccionAsync` y `CreditoDelModelo` como helpers locales del
archivo, reutilizando exactamente el arranque que hoy usan los tests
borrados (`_api.Pedido = ...`, `PrepararControlador()`, etc.);
`CreditoDelModelo` devuelve `PedidoDetalleVista.Credito` o
`IFormularioConCredito.Credito` según el tipo del modelo.

En `FlujoPedidosTests.cs`, reemplazar
`DetallesMuestraElCreditoDelClienteConLasTresCifras`,
`DetallesConElCreditoCaidoAvisaYMantieneLasAcciones` y
`AceptarYDespacharMuestranElCreditoDelCliente` por un test de HTML que
verifica la ausencia:

```csharp
    // El HTML renderizado no debe contener el bloque de crédito por ninguna
    // de sus tres puertas: el título de la tarjeta, el aviso de degradación
    // y el rótulo del saldo.
    [Theory]
    [InlineData("detalles")]
    [InlineData("aceptar")]
    [InlineData("despachar")]
    public async Task NingunaPantallaDeDecisionRenderizaElCreditoDelCliente(string ruta)
    {
        await using var aplicacion = NuevaAplicacion();
        var id = Guid.NewGuid();
        aplicacion.Api.Pedido = PedidoSolicitado(id);
        aplicacion.Api.CreditoDePedido = new CreditoHuevoPedidoApi(-5000m, 14120m, 9120m, true, []);

        var html = await (await aplicacion.ClienteAutenticado()
            .GetAsync($"/Pedidos/{ruta}/{id}")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Crédito por despachos de huevo", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Saldo disponible", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Crédito no disponible", html, StringComparison.Ordinal);
        Assert.Equal(0, aplicacion.Api.VecesObtenerCredito);
    }
```

Adaptar `NuevaAplicacion`, `ClienteAutenticado`, `PedidoSolicitado` y las
rutas reales a lo que ya use el archivo.

- [ ] **Step 2: Quitar el bloque de las tres vistas**

Borrar la línea `<partial name="_CreditoHuevo" model="new BloqueCreditoVista(Model.Credito)" />`
de `Detalles.cshtml` (línea 60), `Aceptar.cshtml` (línea 16) y
`Despachar.cshtml` (línea 18). `_CreditoHuevo.cshtml` **no se borra**:
agregarle arriba del `@model` el comentario de retiro:

```
@* RETIRADA por la corrección 2026-09-14: el saldo del cliente es privado y
   GestorCaisy no lo ve. Esta partial ya no la incluye ninguna vista y el
   controlador ya no pide el dato. Se conserva sin borrar por decisión
   explícita del usuario. *@
```

- [ ] **Step 3: El controlador deja de pedir el crédito**

En `PedidosController.CreditoDelPedidoONullAsync`, reemplazar el cuerpo
completo (los cuatro `catch` incluidos) por:

```csharp
    // RETIRADO por la corrección 2026-09-14: el saldo del cliente es privado
    // y GestorCaisy no lo ve. No se llama a la API —que además responde 403
    // desde esta misma corrección—, así que no hay ida y vuelta inútil por
    // cada render. El método, `VistaConCredito`, `IFormularioConCredito` y
    // `ObtenerCreditoDePedidoAsync` se conservan sin borrar por decisión
    // explícita del usuario: el inventario está en la spec.
    private static Task<CreditoHuevoPedidoApi?> CreditoDelPedidoONullAsync(
        Guid id, CancellationToken token) =>
        Task.FromResult<CreditoHuevoPedidoApi?>(null);
```

Si el compilador se queja por los parámetros sin uso o por `using`s que
quedan huérfanos (`System.Text.Json`), resolverlo sin relajar ningún
analizador: descartar con `_ = id;` no es aceptable, preferir mantener la
firma y, si el analizador insiste, convertir el método en una propiedad
estática `CreditoRetirado`. Verificar con `dotnet build`.

- [ ] **Step 4: La bandeja de CAISY deja de etiquetar `CreditoInsuficiente`**

En `Views/RecepcionesHuevo/Index.cshtml`, dentro de `@functions`, dejar
`EtiquetaNotificacion` y `EtiquetaChip` sin la rama de `CreditoInsuficiente`
y actualizar el comentario:

```csharp
    // Esta bandeja es global de CAISY (ClienteId nulo en la consulta). El
    // único tipo que se creaba con ese alcance era CreditoInsuficiente, y la
    // corrección 2026-09-14 dejó de emitirlo: no se manda ninguna alerta por
    // saldo. Las filas ya existentes en base se conservan y caen en la rama
    // por defecto. El valor 1 del enum queda reservado y no se renumera.
    private static string EtiquetaNotificacion(string tipo) => tipo;

    private static string EtiquetaChip(string tipo) => "borrador";
```

Si algún analizador marca el parámetro sin uso en `EtiquetaChip`, dejar el
`switch` con solo la rama por defecto en vez de la expresión constante.

- [ ] **Step 5: Verificar y comitear**

```
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests
```

Commit: `fix(caisy): la oficina deja de mostrar y de pedir el credito del cliente`

---

### Task 5: La PWA deja de pedir confirmación por crédito al enviar

**Files:**
- Modify: `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx:78-86, 118-125, 145-165, 542-577`
- Modify: `web/src/features/despacho-huevo/constantes.ts:41`
- Test: `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx:225-240`

**Interfaces:**
- Produces: el diálogo «Enviar pedido a CAISY» tiene un único botón «Confirmar envío» y no menciona crédito. `enviarPedido` se sigue llamando con el flag en `false` (el contrato del endpoint no cambia).

- [ ] **Step 1: Invertir el test, verlo en rojo**

En `PedidoAlimentoDetallePage.test.tsx`, reemplazar el test que hoy verifica
los dos envíos (`{ confirmarCreditoInsuficiente: false }` seguido de `true`)
por:

```tsx
  // Corrección 2026-09-14: el envío no depende del crédito. Un solo intento,
  // sin segundo diálogo y sin cifras de saldo en pantalla.
  it('envía el pedido de una sola vez, sin pedir confirmación por crédito', async () => {
    // …montar la página con un borrador enviable (mismo arranque que el test
    // borrado) y responder 204 al POST de /enviar…
    await usuario.click(pantalla.getByRole('button', { name: 'Enviar a CAISY' }));
    await usuario.click(pantalla.getByRole('button', { name: 'Confirmar envío' }));

    expect(cuerposEnviados).toEqual([{ confirmarCreditoInsuficiente: false }]);
    expect(pantalla.queryByText(/crédito/i)).toBeNull();
    expect(pantalla.queryByRole('button', { name: 'Enviar de todas formas' })).toBeNull();
  });
```

Correr en rojo: `npm --prefix web run test -- PedidoAlimentoDetallePage`

- [ ] **Step 2: Limpiar la página**

En `PedidoAlimentoDetallePage.tsx`:

- Borrar la constante `TITULO_CREDITO_INSUFICIENTE` y su comentario.
- Borrar el estado `requiereConfirmacionCredito` y su `setState`.
- Borrar la consulta `creditoParaConfirmar` completa.
- En `cerrarDialogoEnvio` y en el `onSuccess` de `enviar`, quitar las líneas
  `setRequiereConfirmacionCredito(false)`.
- En `onError` de `enviar`, borrar la rama `if (e instanceof ApiError && e.code === ...)`.
- `mutationFn` pasa a `() => enviarPedido(id!)` y el botón a
  `onClick={() => enviar.mutate()}`; `enviarPedido` conserva su parámetro con
  valor por defecto `false`, así que `api.ts` y `api.test.ts` no se tocan.
- Borrar los dos `<Alert>` del diálogo (el del cliente con las cifras y el de
  «necesita confirmación del Cliente»), y dejar el botón siempre visible con
  la etiqueta fija `Confirmar envío`.
- Quitar los imports que queden huérfanos: `AjustesCreditoHuevo`,
  `obtenerBalanceCreditoHuevo`, `formatoMonedaExacta` y `ApiError` si ya no
  se usan. ESLint lo marca.

Agregar arriba del componente:

```tsx
// Corrección 2026-09-14: el envío no consulta ni menciona el crédito. El
// saldo es información privada del Cliente y se muestra donde decide gastar
// —formulario de pedido y formulario de despacho de huevo—, no como freno.
```

- [ ] **Step 3: Marcar el mensaje retirado en `constantes.ts`**

En `web/src/features/despacho-huevo/constantes.ts`, dejar la rama
`'CreditoInsuficiente'` de `mensajeNotificacionDespachoHuevo` **en su lugar**
(hay filas históricas en base que todavía se listan) y agregarle encima:

```ts
    // Retirado como emisión por la corrección 2026-09-14: ya no se genera
    // ninguna notificación por saldo negativo. La rama se conserva para las
    // filas históricas que sigan en base.
```

- [ ] **Step 4: Verificar y comitear**

```
npm --prefix web run lint
npm --prefix web run test
```

Commit: `fix(web): el envio de pedido deja de pedir confirmacion por credito`

---

### Task 6: El Cliente ve su crédito al preparar un despacho de huevo

**Files:**
- Modify: `web/src/features/despacho-huevo/DespachoHuevoFormularioPage.tsx`
- Test: `web/src/features/despacho-huevo/DespachoHuevoFormularioPage.test.tsx`

**Interfaces:**
- Produces: el formulario de despacho muestra, solo para el rol `Cliente`, el saldo con `formatoMonedaExacta` y el desglose `AjustesCreditoHuevo`. Mismo par de gates que `PedidoFormularioPage`: `tieneRol('Cliente')` en la PWA y el 403 del backend.

- [ ] **Step 1: Escribir los dos tests que fallan**

En `DespachoHuevoFormularioPage.test.tsx`:

```tsx
  // El crédito nace de los despachos de huevo: el Cliente lo ve justo donde
  // decide cuánto despachar. Cuatro decimales, igual que en el formulario de
  // pedido: el crédito sale de precios por huevo de cuatro decimales y
  // redondear haría que la cuenta no cierre.
  it('muestra el saldo y los ajustes al Cliente', async () => {
    // …responder /despachos-huevo/credito con
    //   { saldoDisponible: -5000, ajustes: [{ id, monto: 900, motivo: 'Corrección…', fecha }] }
    //   y montar con rol Cliente…
    expect(await pantalla.findByText(/Crédito por despachos de huevo/)).toBeInTheDocument();
    expect(pantalla.getByText('Negativo')).toBeInTheDocument();
    expect(pantalla.getByText(/Corrección/)).toBeInTheDocument();
  });

  it('no muestra ni consulta el crédito para el Trabajador', async () => {
    // …montar con rol Trabajador…
    expect(pantalla.queryByText(/Crédito por despachos de huevo/)).toBeNull();
    expect(llamadasA('/despachos-huevo/credito')).toHaveLength(0);
  });
```

Adaptar el arranque (mock de `useAuth`/`tieneRol`, servidor de peticiones) a
lo que ya use el archivo; si no mockea rol todavía, copiar el patrón de
`PedidoFormularioPage.test.tsx`.

- [ ] **Step 2: Agregar el bloque al formulario**

En `DespachoHuevoFormularioPage.tsx`, agregar los imports:

```tsx
import { Chip } from '@mui/material';
import { useAuth } from '../../app/auth/useAuth';
import { AjustesCreditoHuevo } from './AjustesCreditoHuevo';
import { obtenerBalanceCreditoHuevo } from './api';
import { formatoPrecioUnitario } from './constantes';
```

(`formatoPrecioUnitario` ya es `formatoMonedaExacta` reexportado; `useAuth`
debe importarse desde la misma ruta que usa `PedidoFormularioPage.tsx`
—verificarla antes de escribirla.)

Dentro del componente, junto a las demás consultas:

```tsx
  // Crédito por despachos de huevo: informativo, solo para el Cliente. El
  // crédito nace acá, así que es donde más sentido tiene verlo. Nunca para el
  // Trabajador: el backend responde 403 y esta consulta ni se dispara.
  const { tieneRol } = useAuth();
  const esCliente = tieneRol('Cliente');
  const { data: credito } = useQuery({
    queryKey: ['despachos-huevo', 'credito'],
    queryFn: obtenerBalanceCreditoHuevo,
    enabled: esCliente,
  });
```

Y en el `<Stack spacing={2}>` del render, justo después del bloque de
`{precios && (...)}`:

```tsx
          {esCliente && credito && (
            <Box>
              {/* El saldo negativo no se distingue solo por color: lleva la
                  etiqueta textual "Negativo", para que se perciba sin visión
                  de color. Mismo bloque que PedidoFormularioPage. */}
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                <Typography
                  variant="body2"
                  color={credito.saldoDisponible < 0 ? 'error' : 'text.secondary'}
                >
                  Crédito por despachos de huevo: {formatoPrecioUnitario(credito.saldoDisponible)}
                </Typography>
                {credito.saldoDisponible < 0 && (
                  <Chip size="small" color="error" label="Negativo" />
                )}
              </Stack>
              <AjustesCreditoHuevo ajustes={credito.ajustes} />
            </Box>
          )}
```

- [ ] **Step 3: Verificar y comitear**

```
npm --prefix web run lint
npm --prefix web run test -- DespachoHuevoFormularioPage
```

Commit: `feat(web): el cliente ve su credito al preparar un despacho de huevo`

---

### Task 7: Verificación completa y cierre

- [ ] **Step 1: Revisar la semilla de desarrollo**

`SemillaDesarrolloAvicola.cs` construye el escenario de crédito. Con la
fórmula de la Task 1, el saldo sembrado cambia (los pedidos solicitados,
aceptados y despachados ya no restan). Correr:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~SemillaDesarrolloAvicolaTests"
```

Si algún test afirma un saldo concreto, **ajustar el número esperado al
nuevo cálculo, no la fórmula**. Si el escenario dejó de mostrar un saldo
negativo y ese era su punto, agregar al escenario un pedido **recibido** de
monto mayor al huevo despachado —deuda real— en vez de reponer el
comprometido.

- [ ] **Step 2: Buscar referencias huérfanas**

```
grep -rn "comprometido\|ConfirmarCreditoInsuficiente\|CreditoInsuficienteRequiereConfirmacion" --include=*.cs --include=*.ts --include=*.tsx --include=*.cshtml Icarus/src web/src Icarus/tests
```

Cada aparición restante debe ser una de las conservadas a propósito y llevar
su comentario de retiro. Cualquier otra cosa es un cabo suelto.

- [ ] **Step 3: Puerta de calidad completa**

Con Docker corriendo:

```
./verify.ps1
```

Verde de punta a punta. Prohibido relajar un gate, una baseline o un umbral.

- [ ] **Step 4: Actualizar el handoff y comitear**

En `docs/ai/HANDOFF.md`, dejar constancia de que el bloque 9 quedó corregido:
el crédito es privado del Cliente, no gobierna el pedido y la fórmula es de
cuenta real. Anotar el inventario de código inerte remitiendo a la spec, y
eliminar del backlog los ítems que esta corrección volvió obsoletos
(advertencia al enviar, vista de CAISY, alerta proactiva de saldo negativo).

Commit final: `docs(ai): handoff de la correccion del credito de huevo`

- [ ] **Step 5: Push**

```
git push origin develop
```
