# Vista de CAISY del crédito de huevo por pedido — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que el gestor de CAISY vea el crédito por despachos de huevo del cliente en las tres pantallas donde decide sobre un pedido de alimento, sin poder interpretar mal el número ni consultar clientes arbitrarios.

**Architecture:** Un query nuevo de alcance de pedido (`ObtenerCreditoHuevoDePedidoCaisyQuery`) deriva el `ClienteId` del pedido y reusa los dos métodos del repositorio de crédito, que ya reciben `clienteId` explícito. Se expone en `GET /pedidos-alimento-caisy/{id}/credito`, dentro del grupo que ya exige rol `GestorCaisy` más funcionalidad `GestorPedidoAlimento`. El handler resuelve en el backend cuánto pesa el pedido en el saldo según su estado. `Trajano.GestorCaisy` consume el endpoint y muestra un partial en detalle, aceptación y despacho, degradando a un aviso si la consulta falla.

**Tech Stack:** .NET 10, MediatR, EF Core 9 (SQL Server), xUnit, NSubstitute, Testcontainers.MsSql, ASP.NET Core MVC con Razor.

**Spec:** `docs/superpowers/specs/2026-09-11-credito-huevo-vista-caisy-design.md`

## Global Constraints

- No modificar `ComandosCreditoHuevo.cs`, `ObtenerBalanceCreditoHuevoHandler`, su gate de rol Cliente, ni `IRepositorioBalanceCreditoHuevo`, ni `RepositorioBalanceCreditoHuevo`. La feature no toca el camino del Cliente.
- No modificar nada bajo `web/` (la PWA). No hay cambios de frontend React.
- Identificadores de dominio, comentarios y textos de interfaz en español correcto, con acentos, UTF-8 sin BOM.
- Anti-PII: no registrar en logs montos, saldos, motivos de ajuste ni datos del cliente.
- El crédito es informativo: ninguna falla de la consulta de crédito puede impedir aceptar o despachar un pedido.
- Formato de montos en la app MVC: `ToString("0.00")` sin `CultureInfo` explícito, igual que el resto de `Views/Pedidos/`.
- Prohibido `--no-verify`. Prohibido relajar baselines o umbrales de `quality/`.
- La puerta de calidad completa (`./verify.ps1`) exige Docker corriendo: los tests de integración usan Testcontainers.MsSql.

## Estructura de archivos

**Crear:**

| Archivo | Responsabilidad |
|---|---|
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevoCaisy.cs` | Query, DTO y handler del crédito con alcance de pedido para CAISY |
| `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerCreditoHuevoDePedidoCaisyHandlerTests.cs` | La tabla de estados, los ajustes y el 404 |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/_CreditoHuevo.cshtml` | Partial del bloque de crédito, incluido el caso degradado |

**Modificar:**

| Archivo | Cambio |
|---|---|
| `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs` | Un `MapGet` en el grupo `caisy` |
| `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs` | Autorización y cifras del endpoint nuevo; helper de cuenta CAISY parametrizado |
| `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs` | `CreditoHuevoPedidoApi`, `AjusteCreditoHuevoApi` |
| `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/IApiIcarusClient.cs` | `ObtenerCreditoDePedidoAsync` |
| `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs` | Implementación del GET |
| `Icarus/tests/Trajano.GestorCaisy.Tests/Servicios/ApiIcarusClientTests.cs` | La ruta exacta pedida |
| `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs` | Doble de la operación nueva |
| `Icarus/src/Apps/Trajano.GestorCaisy/Models/PedidosVistas.cs` | `BloqueCreditoVista`, `IFormularioConCredito`, `Credito` en tres modelos |
| `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs` | Helper de degradación, helper de vista con crédito, seis puntos de render |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Detalles.cshtml` | Incluir el partial |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Aceptar.cshtml` | Incluir el partial |
| `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Despachar.cshtml` | Incluir el partial |
| `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs` | Crédito en el modelo de las tres pantallas y degradación |
| `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPedidosTests.cs` | HTML del bloque y del aviso |
| `docs/ai/HANDOFF.md` | Cierre del ítem 3 del backlog |

**No se toca:** ningún puerto, repositorio, entidad de dominio, migración ni configuración de EF. El cálculo, el `DbContext` y sus filtros de tenant ya sirven el caso: `GestionAvicolaDbContext` no filtra por tenant cuando la cuenta no tiene `ClienteId`.

---

### Task 1: Query, DTO y handler del crédito por pedido

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevoCaisy.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerCreditoHuevoDePedidoCaisyHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioPedidosAlimento.ObtenerPorIdAsync(Guid, CancellationToken)` (ya incluye `Detalles` y `Recepcion`); `IRepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync(Guid, DateOnly, CancellationToken)` y `.ObtenerAjustesAsync(Guid, CancellationToken)`; `AjusteCreditoHuevoResumen(Guid Id, decimal Monto, string Motivo, DateOnly Fecha)`; `DespachosHuevo.FechasNegocio.Hoy()`; `NotFoundException(string entidad, Guid id)`.
- Produces: `ObtenerCreditoHuevoDePedidoCaisyQuery(Guid PedidoId)` y `CreditoHuevoDePedidoCaisy(decimal SaldoDisponible, decimal MontoDelPedido, decimal SaldoSinEstePedido, bool PedidoComputadoEnElSaldo, IReadOnlyList<AjusteCreditoHuevoResumen> Ajustes)`. Los nombres de propiedad se serializan en camelCase y los consume la Task 3.

- [ ] **Step 1: Escribir el test que falla**

Crear `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerCreditoHuevoDePedidoCaisyHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// Vista de CAISY del crédito (spec 2026-09-11-credito-huevo-vista-caisy):
// el handler deriva el cliente del pedido y resuelve cuánto pesa ese pedido
// en el saldo según su estado. No verifica rol: la política del grupo
// /pedidos-alimento-caisy ya exige rol GestorCaisy más la funcionalidad
// GestorPedidoAlimento (ver ObtenerBalanceCreditoHuevoHandlerTests.cs para
// el gate del lado Cliente, que existe porque la política del tenant no
// distingue Cliente de Trabajador).
public class ObtenerCreditoHuevoDePedidoCaisyHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 9, 1);

    private readonly IRepositorioPedidosAlimento _pedidos =
        Substitute.For<IRepositorioPedidosAlimento>();
    private readonly IRepositorioBalanceCreditoHuevo _credito =
        Substitute.For<IRepositorioBalanceCreditoHuevo>();

    private ObtenerCreditoHuevoDePedidoCaisyHandler CrearHandler() => new(_pedidos, _credito);

    private void ConSaldo(decimal saldo, params AjusteCreditoHuevoResumen[] ajustes)
    {
        _credito.ObtenerSaldoDisponibleAsync(
                ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(saldo);
        _credito.ObtenerAjustesAsync(ClienteId, Arg.Any<CancellationToken>())
            .Returns(ajustes.ToList());
    }

    private void ConPedido(PedidoAlimento pedido) =>
        _pedidos.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

    private static PedidoAlimento PedidoBorrador() =>
        new(ClienteId, ActorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 100)]);

    // 100 bolsas = 100 equivalentes de 40 kg a 180 = 18 000 congelados.
    private static PedidoAlimento PedidoEnviado()
    {
        var pedido = PedidoBorrador();
        pedido.EnviarACaisy(Hoy, ActorId,
        [
            new DatosPrecioEnvio(
                TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid()),
        ]);
        return pedido;
    }

    // Recibe 80 de las 100 despachadas: 80 × 180 = 14 400. Distinto de los
    // 18 000 comprometidos a propósito, para que el test pruebe que se usó
    // Recepcion.TotalRecibido y no la suma de subtotales.
    private static PedidoAlimento PedidoRecibidoConDiferencias()
    {
        var pedido = PedidoEnviado();
        pedido.Aceptar(Hoy.AddDays(1), Hoy, ActorId);
        pedido.RegistrarDespacho("NOTA-1", Hoy, null,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, 100)], Hoy, ActorId);
        pedido.ConfirmarRecepcion(
            [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 80)],
            new DatosDocumentoNota(
                Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512,
                "hash-sha256", "recepcion.jpg"),
            ActorId);
        return pedido;
    }

    [Fact]
    public async Task PedidoSolicitadoAportaSusSubtotalesCongelados()
    {
        var pedido = PedidoEnviado();
        ConPedido(pedido);
        ConSaldo(-5000m);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        Assert.Equal(-5000m, resultado.SaldoDisponible);
        Assert.Equal(18000m, resultado.MontoDelPedido);
        Assert.Equal(13000m, resultado.SaldoSinEstePedido);
        Assert.True(resultado.PedidoComputadoEnElSaldo);
    }

    [Fact]
    public async Task PedidoRecibidoAportaElTotalRealmenteRecibido()
    {
        var pedido = PedidoRecibidoConDiferencias();
        ConPedido(pedido);
        ConSaldo(1000m);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        Assert.Equal(14400m, resultado.MontoDelPedido);
        Assert.Equal(15400m, resultado.SaldoSinEstePedido);
        Assert.True(resultado.PedidoComputadoEnElSaldo);
    }

    [Fact]
    public async Task PedidoBorradorNoPesaEnElSaldo()
    {
        var pedido = PedidoBorrador();
        ConPedido(pedido);
        ConSaldo(2500m);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        Assert.Equal(0m, resultado.MontoDelPedido);
        Assert.Equal(2500m, resultado.SaldoSinEstePedido);
        Assert.False(resultado.PedidoComputadoEnElSaldo);
    }

    [Fact]
    public async Task PedidoRechazadoNoPesaEnElSaldo()
    {
        var pedido = PedidoEnviado();
        pedido.Rechazar("Cantidad fuera de lo acordado.", ActorId);
        ConPedido(pedido);
        ConSaldo(2500m);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        Assert.Equal(0m, resultado.MontoDelPedido);
        Assert.Equal(2500m, resultado.SaldoSinEstePedido);
        Assert.False(resultado.PedidoComputadoEnElSaldo);
    }

    [Fact]
    public async Task LosAjustesDelClienteDelPedidoViajanTalCual()
    {
        var pedido = PedidoEnviado();
        var ajuste = new AjusteCreditoHuevoResumen(
            Guid.NewGuid(), 45m, "Corrección de precio Extra.", new DateOnly(2026, 9, 1));
        ConPedido(pedido);
        ConSaldo(0m, ajuste);

        var resultado = await CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(pedido.Id), CancellationToken.None);

        var unico = Assert.Single(resultado.Ajustes);
        Assert.Equal("Corrección de precio Extra.", unico.Motivo);
        Assert.Equal(45m, unico.Monto);
    }

    [Fact]
    public async Task PedidoInexistenteFallaSinConsultarElCredito()
    {
        var id = Guid.NewGuid();
        _pedidos.ObtenerPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((PedidoAlimento?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => CrearHandler().Handle(
            new ObtenerCreditoHuevoDePedidoCaisyQuery(id), CancellationToken.None));

        await _credito.DidNotReceive().ObtenerSaldoDisponibleAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _credito.DidNotReceive().ObtenerAjustesAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter FullyQualifiedName~ObtenerCreditoHuevoDePedidoCaisyHandlerTests`

Expected: FAIL de compilación — `ObtenerCreditoHuevoDePedidoCaisyHandler` y `ObtenerCreditoHuevoDePedidoCaisyQuery` no existen.

- [ ] **Step 3: Escribir la implementación mínima**

Crear `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevoCaisy.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

// Vista de CAISY del crédito del cliente de un pedido (spec
// 2026-09-11-credito-huevo-vista-caisy-design). El alcance es el pedido, no
// el cliente: el ClienteId se deriva del pedido y nunca viaja como
// parámetro, así que no hay enumeración posible de clientes desde la API.
//
// Sin gate de rol dentro del handler, a diferencia de
// ObtenerBalanceCreditoHuevoHandler: la política del grupo
// /pedidos-alimento-caisy exige rol GestorCaisy más la funcionalidad
// GestorPedidoAlimento, mientras la política del tenant no distingue
// Cliente de Trabajador — de ahí que solo ese otro handler necesite el if.
public sealed record ObtenerCreditoHuevoDePedidoCaisyQuery(Guid PedidoId)
    : IRequest<CreditoHuevoDePedidoCaisy>;

// Tres cifras en vez de una: SaldoDisponible ya tiene descontado este
// pedido cuando está comprometido, así que quien lea solo ese número está
// viendo el saldo DESPUÉS del pedido, no antes de decidir.
public sealed record CreditoHuevoDePedidoCaisy(
    decimal SaldoDisponible,
    decimal MontoDelPedido,
    decimal SaldoSinEstePedido,
    bool PedidoComputadoEnElSaldo,
    IReadOnlyList<AjusteCreditoHuevoResumen> Ajustes);

public sealed class ObtenerCreditoHuevoDePedidoCaisyHandler(
    IRepositorioPedidosAlimento repositorioPedidos,
    IRepositorioBalanceCreditoHuevo repositorioCredito)
    : IRequestHandler<ObtenerCreditoHuevoDePedidoCaisyQuery, CreditoHuevoDePedidoCaisy>
{
    public async Task<CreditoHuevoDePedidoCaisy> Handle(
        ObtenerCreditoHuevoDePedidoCaisyQuery request, CancellationToken cancellationToken)
    {
        var pedido = await repositorioPedidos.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        var saldo = await repositorioCredito.ObtenerSaldoDisponibleAsync(
            pedido.ClienteId, DespachosHuevo.FechasNegocio.Hoy(), cancellationToken);
        var ajustes = await repositorioCredito.ObtenerAjustesAsync(
            pedido.ClienteId, cancellationToken);
        var monto = MontoQueElPedidoAportaAlSaldo(pedido);
        return new CreditoHuevoDePedidoCaisy(
            saldo, monto ?? 0m, saldo + (monto ?? 0m), monto is not null, ajustes);
    }

    // Cuánto pesa este pedido en el saldo, según su estado; null cuando no
    // pesa. Los dos componentes que lo incluyen se RESTAN en
    // RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync, así que el
    // saldo sin el pedido es siempre saldo + monto.
    private static decimal? MontoQueElPedidoAportaAlSaldo(PedidoAlimento pedido) =>
        pedido.Estado switch
        {
            // comprometidoPendiente suma los subtotales NO NULOS. No se usa
            // pedido.TotalSolicitado porque esa propiedad devuelve null si
            // algún subtotal es nulo, y daría un número distinto del que el
            // saldo realmente descontó.
            EstadoPedidoAlimento.Solicitado
                or EstadoPedidoAlimento.Aceptado
                or EstadoPedidoAlimento.Despachado =>
                pedido.Detalles
                    .Where(d => d.SubtotalSolicitado is not null)
                    .Sum(d => d.SubtotalSolicitado!.Value),
            // recibidoReal usa el snapshot Recepcion.TotalRecibido: el
            // pedido ya consumió crédito real y dejó de estar comprometido.
            EstadoPedidoAlimento.RecibidoConforme
                or EstadoPedidoAlimento.RecibidoConDiferencias =>
                pedido.Recepcion?.TotalRecibido ?? 0m,
            // Borrador y Rechazado no entran en ningún componente.
            _ => null,
        };
}
```

- [ ] **Step 4: Correr el test para verificar que pasa**

Run: `dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter FullyQualifiedName~ObtenerCreditoHuevoDePedidoCaisyHandlerTests`

Expected: PASS, 6 tests.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevoCaisy.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerCreditoHuevoDePedidoCaisyHandlerTests.cs
git commit -m "feat(avicola): query del credito de huevo con alcance de pedido para CAISY"
```

---

### Task 2: Endpoint `GET /pedidos-alimento-caisy/{id}/credito`

**Files:**
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs` (grupo `caisy`, junto al `MapGet("/{id:guid}/recibo.pdf")`)
- Test: `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`

**Interfaces:**
- Consumes: `ObtenerCreditoHuevoDePedidoCaisyQuery` de la Task 1.
- Produces: la ruta HTTP `pedidos-alimento-caisy/{id}/credito` con cuerpo `{ saldoDisponible, montoDelPedido, saldoSinEstePedido, pedidoComputadoEnElSaldo, ajustes[] }`, que consume la Task 3.

- [ ] **Step 1: Escribir los tests que fallan**

En `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`, primero parametrizar el helper existente (línea ~94) para poder crear una cuenta CAISY con otra funcionalidad. Reemplazar su firma y el arreglo de funcionalidades:

```csharp
    private async Task<(HttpClient Cliente, string Token)> CrearCuentaCaisyConFuncion(
        string funcionalidad = "GestorPedidoAlimento")
    {
        var anonimo = _factory.CreateClient();
        var tokenAdmin = await LoginComo(anonimo, SemillaIdentidad.EmailAdmin);
        var emailCaisy = $"pedidos-{Guid.NewGuid():N}@icarus.test";
        var alta = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { funcionalidad },
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var token = await LoginComo(anonimo, emailCaisy, "Clave-Caisy-123");
        return (anonimo, token);
    }
```

El valor por defecto deja intactas todas las llamadas existentes del archivo.

Agregar al final de la clase:

```csharp
    // Vista de CAISY del crédito (spec 2026-09-11-credito-huevo-vista-caisy):
    // el cliente sale del pedido, así que la ruta no lleva ningún ClienteId.
    // El tenant de prueba no tiene despachos de huevo sembrados, así que sus
    // ingresos y ajustes son cero y el saldo es exactamente el negativo de lo
    // comprometido por este pedido. No se fija el precio en la aserción: el
    // monto se contrasta contra el total solicitado que informa la API, que
    // es el mismo snapshot congelado al envío.
    [Fact]
    public async Task ElCreditoDelPedidoDevuelveLasTresCifrasCoherentes()
    {
        var (cliente, tokenCliente, _) = await CrearClienteConGestionAvicolaAsync();
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        await ImportarYPublicarAsync(caisy, tokenCaisy);
        var id = await CrearBorradorAsync(cliente, tokenCliente);
        Assert.Equal(HttpStatusCode.NoContent, await EnviarAsync(cliente, tokenCliente, id));
        var detalle = await ObtenerDetalleAsync(cliente, tokenCliente, id);
        var totalSolicitado = detalle.GetProperty("totalSolicitado").GetDecimal();

        var respuesta = await caisy.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{id}/credito", tokenCaisy));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(totalSolicitado, cuerpo.GetProperty("montoDelPedido").GetDecimal());
        Assert.Equal(-totalSolicitado, cuerpo.GetProperty("saldoDisponible").GetDecimal());
        Assert.Equal(0m, cuerpo.GetProperty("saldoSinEstePedido").GetDecimal());
        Assert.True(cuerpo.GetProperty("pedidoComputadoEnElSaldo").GetBoolean());
        Assert.Empty(cuerpo.GetProperty("ajustes").EnumerateArray());
    }

    // Un borrador no pesa en el saldo: no entra en ningún componente del
    // cálculo, así que las dos cifras derivadas quedan neutras.
    [Fact]
    public async Task ElCreditoDeUnBorradorInformaQueNoPesaEnElSaldo()
    {
        var (cliente, tokenCliente, _) = await CrearClienteConGestionAvicolaAsync();
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        var id = await CrearBorradorAsync(cliente, tokenCliente);

        var respuesta = await caisy.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{id}/credito", tokenCaisy));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0m, cuerpo.GetProperty("montoDelPedido").GetDecimal());
        Assert.Equal(0m, cuerpo.GetProperty("saldoDisponible").GetDecimal());
        Assert.False(cuerpo.GetProperty("pedidoComputadoEnElSaldo").GetBoolean());
    }

    // El crédito de CAISY es financiero y exclusivo de las dos partes de la
    // relación comercial: solo una cuenta GestorCaisy con la funcionalidad
    // GestorPedidoAlimento lo consulta por esta ruta. El Cliente tiene su
    // propio endpoint (/despachos-huevo/credito) y el Trabajador no tiene
    // ninguno.
    [Fact]
    public async Task ElCreditoDelPedidoSoloLoVeCaisyConGestorPedidoAlimento()
    {
        var (cliente, tokenCliente, clienteId) = await CrearClienteConGestionAvicolaAsync();
        var tokenTrabajador = await CrearTrabajadorConFuncionAsync(
            cliente, tokenCliente, clienteId, "PedidoAlimento");
        var (_, tokenOtraFuncion) = await CrearCuentaCaisyConFuncion("GestorRecepcionHuevos");
        var id = await CrearBorradorAsync(cliente, tokenCliente);
        var ruta = $"/api/pedidos-alimento-caisy/{id}/credito";

        var sinToken = await cliente.GetAsync(ruta);
        var comoCliente = await cliente.SendAsync(Pedido(HttpMethod.Get, ruta, tokenCliente));
        var comoTrabajador = await cliente.SendAsync(Pedido(HttpMethod.Get, ruta, tokenTrabajador));
        var comoOtraFuncion = await cliente.SendAsync(Pedido(HttpMethod.Get, ruta, tokenOtraFuncion));

        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, comoCliente.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, comoTrabajador.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, comoOtraFuncion.StatusCode);
    }

    [Fact]
    public async Task ElCreditoDeUnPedidoInexistenteDevuelve404()
    {
        var (cliente, tokenCaisy) = await CrearCuentaCaisyConFuncion();

        var respuesta = await cliente.SendAsync(Pedido(
            HttpMethod.Get, $"/api/pedidos-alimento-caisy/{Guid.NewGuid()}/credito", tokenCaisy));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
```

- [ ] **Step 2: Correr los tests para verificar que fallan**

Docker debe estar corriendo (Testcontainers.MsSql).

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj --filter FullyQualifiedName~PedidosAlimentoEndpointsTests`

Expected: los cuatro tests nuevos FAIL con 404 (la ruta no existe todavía). El resto de la clase sigue en verde.

- [ ] **Step 3: Escribir la implementación mínima**

En `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs`, agregar el `using` del namespace del crédito si falta:

```csharp
using Icarus.GestionAvicola.Application.CreditoHuevo;
```

Y en el grupo `caisy`, justo después del `MapGet("/{id:guid}/recibo.pdf", …)`:

```csharp
        // Crédito por despachos de huevo del cliente de este pedido (spec
        // 2026-09-11-credito-huevo-vista-caisy-design): el gestor lo necesita
        // para decidir. Cuelga del pedido, no del cliente, así que la
        // funcionalidad que ya exige el grupo (GestorPedidoAlimento) es la
        // correcta y no hace falta ningún ClienteId en la ruta.
        caisy.MapGet("/{id:guid}/credito", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(
                new ObtenerCreditoHuevoDePedidoCaisyQuery(id), cancellationToken)));
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj --filter FullyQualifiedName~PedidosAlimentoEndpointsTests`

Expected: PASS, toda la clase.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs
git commit -m "feat(avicola): endpoint del credito de huevo por pedido en el grupo de CAISY"
```

---

### Task 3: Contrato y cliente HTTP en Trajano.GestorCaisy

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs` (al final, después de `BandejaNotificacionesDespachoHuevoApi`)
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/IApiIcarusClient.cs` (junto a `DespacharPedidoAsync`)
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs` (junto a `ObtenerPedidoAsync`)
- Modify: `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ApiIcarusFalsa.cs`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Servicios/ApiIcarusClientTests.cs`

**Interfaces:**
- Consumes: la ruta de la Task 2. Del propio proyecto: `EnviarConSesionAsync`, `PeticionJson(HttpMethod, string ruta, string? accessToken)`, `AsegurarExitoAsync`, `Json`, `ErrorApiException(int, string)`.
- Produces: `CreditoHuevoPedidoApi(decimal SaldoDisponible, decimal MontoDelPedido, decimal SaldoSinEstePedido, bool PedidoComputadoEnElSaldo, IReadOnlyList<AjusteCreditoHuevoApi> Ajustes)`; `AjusteCreditoHuevoApi(Guid Id, decimal Monto, string Motivo, DateOnly Fecha)`; `IApiIcarusClient.ObtenerCreditoDePedidoAsync(Guid, CancellationToken)`; y en el doble de pruebas `ApiIcarusFalsa.CreditoDePedido`, `.ErrorDeObtenerCredito`, `.VecesObtenerCredito`, `.UltimoCreditoPedido`, `ApiIcarusFalsa.CrearCredito(...)`. Los consume la Task 4.

- [ ] **Step 1: Escribir el test que falla**

En `Icarus/tests/Trajano.GestorCaisy.Tests/Servicios/ApiIcarusClientTests.cs`, agregar:

```csharp
    [Fact]
    public async Task ObtenerCreditoDePedidoPideLaRutaDelPedidoYParseaLasTresCifras()
    {
        _manejador.Responder(HttpStatusCode.OK,
            """
            {"saldoDisponible":-5000.00,"montoDelPedido":14120.00,
             "saldoSinEstePedido":9120.00,"pedidoComputadoEnElSaldo":true,
             "ajustes":[{"id":"11111111-1111-1111-1111-111111111111","monto":45.00,
                         "motivo":"Corrección de precio Extra.","fecha":"2026-09-01"}]}
            """);
        var id = Guid.NewGuid();

        var credito = await _cliente.ObtenerCreditoDePedidoAsync(id);

        Assert.Equal(-5000m, credito.SaldoDisponible);
        Assert.Equal(14120m, credito.MontoDelPedido);
        Assert.Equal(9120m, credito.SaldoSinEstePedido);
        Assert.True(credito.PedidoComputadoEnElSaldo);
        var ajuste = Assert.Single(credito.Ajustes);
        Assert.Equal("Corrección de precio Extra.", ajuste.Motivo);
        Assert.Equal(new DateOnly(2026, 9, 1), ajuste.Fecha);
        var peticion = _manejador.Peticiones[0];
        Assert.Equal(HttpMethod.Get, peticion.Metodo);
        Assert.Equal($"{BaseApi}pedidos-alimento-caisy/{id}/credito", peticion.Uri.ToString());
        Assert.Equal("Bearer token-actual", peticion.Autorizacion);
    }

    [Fact]
    public async Task UnErrorDelCreditoLlegaComoErrorApiException()
    {
        _manejador.Responder(HttpStatusCode.InternalServerError,
            """{"title":"Error interno"}""");

        var error = await Assert.ThrowsAsync<ErrorApiException>(
            () => _cliente.ObtenerCreditoDePedidoAsync(Guid.NewGuid()));

        Assert.Equal(500, error.Estado);
    }
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests/Trajano.GestorCaisy.Tests.csproj --filter FullyQualifiedName~ApiIcarusClientTests`

Expected: FAIL de compilación — `ObtenerCreditoDePedidoAsync` y `CreditoHuevoPedidoApi` no existen.

- [ ] **Step 3: Escribir la implementación mínima**

En `ContratosApi.cs`, al final del archivo:

```csharp
// Crédito por despachos de huevo del cliente de un pedido, para la decisión
// de CAISY (spec 2026-09-11-credito-huevo-vista-caisy-design). Espejo de
// CreditoHuevoDePedidoCaisy: el saldo ya tiene descontado el pedido cuando
// está comprometido, y por eso viajan también el monto que aporta y el saldo
// sin él. Cliente y CAISY ven exactamente la misma información, incluidos los
// ajustes con su motivo.
public sealed record AjusteCreditoHuevoApi(Guid Id, decimal Monto, string Motivo, DateOnly Fecha);

public sealed record CreditoHuevoPedidoApi(
    decimal SaldoDisponible, decimal MontoDelPedido, decimal SaldoSinEstePedido,
    bool PedidoComputadoEnElSaldo, IReadOnlyList<AjusteCreditoHuevoApi> Ajustes);
```

En `IApiIcarusClient.cs`, después de `DespacharPedidoAsync`:

```csharp
    // El crédito viaja por la ruta del pedido: el cliente lo deriva la API
    // del propio pedido. Lanza ErrorApiException como el resto; la
    // degradación cuando no hay crédito es decisión del controller.
    Task<CreditoHuevoPedidoApi> ObtenerCreditoDePedidoAsync(
        Guid id, CancellationToken token = default);
```

En `ApiIcarusClient.cs`, después de `ObtenerPedidoAsync`:

```csharp
    public async Task<CreditoHuevoPedidoApi> ObtenerCreditoDePedidoAsync(
        Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(
                HttpMethod.Get, $"pedidos-alimento-caisy/{id}/credito", accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        return await respuesta.Content.ReadFromJsonAsync<CreditoHuevoPedidoApi>(Json, token)
            ?? throw new ErrorApiException((int)respuesta.StatusCode, "Respuesta ilegible");
    }
```

En `ApiIcarusFalsa.cs`, junto al resto de los miembros de pedidos (después de `ObtenerPedidoAsync`):

```csharp
    public Exception? ErrorDeObtenerCredito { get; set; }
    public CreditoHuevoPedidoApi? CreditoDePedido { get; set; }
    public int VecesObtenerCredito { get; private set; }
    public Guid? UltimoCreditoPedido { get; private set; }

    public Task<CreditoHuevoPedidoApi> ObtenerCreditoDePedidoAsync(
        Guid id, CancellationToken token = default)
    {
        VecesObtenerCredito++;
        UltimoCreditoPedido = id;
        if (ErrorDeObtenerCredito is not null) throw ErrorDeObtenerCredito;
        return Task.FromResult(CreditoDePedido ?? CrearCredito());
    }
```

Y junto a los demás constructores estáticos (`CrearPedido`, `CrearEntrega`):

```csharp
    // Saldo negativo por defecto: es el caso interesante para la decisión de
    // CAISY. Las cifras coinciden con el pedido de CrearPedido (14 120 de la
    // única línea congelada).
    public static CreditoHuevoPedidoApi CrearCredito(
        decimal saldo = -5000m, decimal montoDelPedido = 14120m, bool computado = true) =>
        new(saldo, montoDelPedido, saldo + montoDelPedido, computado,
            [
                new AjusteCreditoHuevoApi(
                    Guid.NewGuid(), 45m, "Corrección de precio Extra.", new(2026, 9, 1)),
            ]);
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests/Trajano.GestorCaisy.Tests.csproj`

Expected: PASS, la suite completa del proyecto (los 185 previos más los 2 nuevos).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Servicios Icarus/tests/Trajano.GestorCaisy.Tests
git commit -m "feat(gestorcaisy): cliente HTTP del credito de huevo por pedido"
```

---

### Task 4: Bloque de crédito en el detalle del pedido

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/PedidosVistas.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/_CreditoHuevo.cshtml`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Detalles.cshtml`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPedidosTests.cs`

**Interfaces:**
- Consumes: `CreditoHuevoPedidoApi`, `ObtenerCreditoDePedidoAsync`, `ApiIcarusFalsa.CrearCredito` de la Task 3.
- Produces: `BloqueCreditoVista(CreditoHuevoPedidoApi? Credito)`; `VistaPedidoDetalle(…, CreditoHuevoPedidoApi? Credito = null)`; el partial `_CreditoHuevo`; y en el controller el método privado `CreditoDelPedidoONullAsync(Guid, CancellationToken)`, que reusa la Task 5.

- [ ] **Step 1: Escribir los tests que fallan**

En `PedidosControllerTests.cs`:

```csharp
    [Fact]
    public async Task DetallesLlevaElCreditoDelClienteAlModelo()
    {
        var id = Guid.NewGuid();
        _api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        _api.CreditoDePedido = new CreditoHuevoPedidoApi(-5000m, 14120m, 9120m, true, []);

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaPedidoDetalle>(((ViewResult)vista).Model);
        Assert.Equal(-5000m, modelo.Credito!.SaldoDisponible);
        Assert.Equal(9120m, modelo.Credito.SaldoSinEstePedido);
        Assert.Equal(id, _api.UltimoCreditoPedido);
        Assert.Equal(1, _api.VecesObtenerCredito);
    }

    // El crédito es informativo (spec SP9E y
    // 2026-09-11-credito-huevo-vista-caisy): una falla del cálculo no puede
    // paralizar la decisión sobre el pedido.
    [Fact]
    public async Task SiElCreditoFallaElDetalleSigueRenderizandoSinCredito()
    {
        var id = Guid.NewGuid();
        _api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        _api.ErrorDeObtenerCredito = new ErrorApiException(500, "Error interno");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaPedidoDetalle>(((ViewResult)vista).Model);
        Assert.Null(modelo.Credito);
        Assert.True(modelo.PuedeProcesarse);
    }

    [Fact]
    public async Task SiLaRedFallaElDetalleSigueRenderizandoSinCredito()
    {
        var id = Guid.NewGuid();
        _api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        _api.ErrorDeObtenerCredito = new HttpRequestException("La API no responde.");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaPedidoDetalle>(((ViewResult)vista).Model);
        Assert.Null(modelo.Credito);
    }
```

En `FlujoPedidosTests.cs`:

```csharp
    [Fact]
    public async Task DetallesMuestraElCreditoDelClienteConLasTresCifras()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        aplicacion.Api.CreditoDePedido = new CreditoHuevoPedidoApi(
            -5000m, 14120m, 9120m, true,
            [
                new AjusteCreditoHuevoApi(
                    Guid.NewGuid(), 45m, "Corrección de precio Extra.", new(2026, 9, 1)),
            ]);

        var html = await cliente.GetStringAsync($"/Pedidos/{id}");

        Assert.Contains("Crédito por despachos de huevo", html);
        Assert.Contains("-5000.00", html);
        Assert.Contains("14120.00", html);
        Assert.Contains("9120.00", html);
        Assert.Contains("Negativo", html);
        Assert.Contains("Corrección de precio Extra.", html);
    }

    [Fact]
    public async Task DetallesConUnBorradorAclaraQueElPedidoNoPesaEnElSaldo()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Borrador");
        aplicacion.Api.CreditoDePedido = new CreditoHuevoPedidoApi(2500m, 0m, 2500m, false, []);

        var html = await cliente.GetStringAsync($"/Pedidos/{id}");

        Assert.Contains("2500.00", html);
        Assert.Contains("Este pedido todavía no pesa en el saldo.", html);
        Assert.DoesNotContain("Saldo sin este pedido", html);
    }

    [Fact]
    public async Task DetallesConElCreditoCaidoAvisaYMantieneLasAcciones()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        aplicacion.Api.ErrorDeObtenerCredito = new ErrorApiException(500, "Error interno");

        var html = await cliente.GetStringAsync($"/Pedidos/{id}");

        Assert.Contains("Crédito no disponible por ahora.", html);
        Assert.Contains($"/Pedidos/{id}/Aceptar", html);
    }
```

- [ ] **Step 2: Correr los tests para verificar que fallan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests/Trajano.GestorCaisy.Tests.csproj --filter "FullyQualifiedName~PedidosControllerTests|FullyQualifiedName~FlujoPedidosTests"`

Expected: FAIL de compilación — `VistaPedidoDetalle` no tiene `Credito`.

- [ ] **Step 3: Escribir la implementación mínima**

En `Models/PedidosVistas.cs`, reemplazar el record `VistaPedidoDetalle` por:

```csharp
public sealed record VistaPedidoDetalle(
    PedidoDetalleApi Pedido,
    bool PuedeProcesarse,
    bool PuedeActualizarEntrega,
    bool PuedeDespacharse,
    // Nulo cuando la consulta de crédito falló: el bloque avisa y la
    // decisión sigue habilitada (spec 2026-09-11-credito-huevo-vista-caisy).
    CreditoHuevoPedidoApi? Credito = null);
```

Y agregar al final del archivo:

```csharp
// Modelo del partial del crédito. Envuelve un valor anulable en vez de
// pasarle null al partial: así el modelo del partial nunca es nulo y el
// propio partial decide qué mostrar.
public sealed record BloqueCreditoVista(CreditoHuevoPedidoApi? Credito);
```

En `Controllers/PedidosController.cs`, agregar el helper privado al final de la clase:

```csharp
    // El crédito es informativo: si el cálculo no está disponible, la
    // pantalla carga igual y la decisión sigue habilitada (spec
    // 2026-09-11-credito-huevo-vista-caisy-design). No se degrada
    // TaskCanceledException: una cancelación real del token significa que la
    // petición se abortó y no hay pantalla que renderizar. Sin logs: el
    // saldo es dato financiero del cliente (anti-PII).
    private async Task<CreditoHuevoPedidoApi?> CreditoDelPedidoONullAsync(
        Guid id, CancellationToken token)
    {
        try
        {
            return await api.ObtenerCreditoDePedidoAsync(id, token);
        }
        catch (ErrorApiException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
```

Y en la acción `Detalles`, cambiar el `return View(...)` por:

```csharp
        return View(new VistaPedidoDetalle(
            pedido,
            PuedeProcesarse: pedido.Estado == "Solicitado",
            PuedeActualizarEntrega: pedido.Estado == "Aceptado",
            PuedeDespacharse: pedido.Estado == "Aceptado",
            Credito: await CreditoDelPedidoONullAsync(id, token)));
```

Crear `Views/Pedidos/_CreditoHuevo.cshtml`:

```razor
@model BloqueCreditoVista
@{
    var credito = Model.Credito;
}
@* Crédito por despachos de huevo del cliente del pedido (spec
   2026-09-11-credito-huevo-vista-caisy-design). Tres cifras y no una: el
   saldo disponible ya tiene descontado este pedido cuando está
   comprometido. Un saldo negativo no se distingue solo por color: lleva
   la etiqueta textual "Negativo". *@
<section class="tarjeta">
    <h2>Crédito por despachos de huevo</h2>
    @if (credito is null)
    {
        <p class="vacio__texto">
            Crédito no disponible por ahora. La decisión sobre el pedido sigue habilitada.
        </p>
    }
    else
    {
        <dl class="ficha">
            <div>
                <dt>Saldo disponible</dt>
                <dd>
                    @credito.SaldoDisponible.ToString("0.00")
                    @if (credito.SaldoDisponible < 0)
                    {
                        <span class="chip chip--rechazado">Negativo</span>
                    }
                </dd>
            </div>
            @if (credito.PedidoComputadoEnElSaldo)
            {
                <div>
                    <dt>Este pedido</dt>
                    <dd>@credito.MontoDelPedido.ToString("0.00")</dd>
                </div>
                <div>
                    <dt>Saldo sin este pedido</dt>
                    <dd>@credito.SaldoSinEstePedido.ToString("0.00")</dd>
                </div>
            }
        </dl>
        @if (credito.PedidoComputadoEnElSaldo)
        {
            <p class="pagina-cabecera__detalle">
                El saldo disponible ya tiene descontado este pedido.
            </p>
        }
        else
        {
            <p class="pagina-cabecera__detalle">
                Este pedido todavía no pesa en el saldo.
            </p>
        }
        @if (credito.Ajustes.Count > 0)
        {
            <h3>Ajustes de corrección</h3>
            <table class="tabla">
                <thead>
                    <tr>
                        <th scope="col">Fecha</th>
                        <th scope="col">Monto</th>
                        <th scope="col">Motivo</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var ajuste in credito.Ajustes)
                    {
                        <tr>
                            <td>@ajuste.Fecha.ToString("dd/MM/yyyy")</td>
                            <td>@ajuste.Monto.ToString("0.00")</td>
                            <td>@ajuste.Motivo</td>
                        </tr>
                    }
                </tbody>
            </table>
        }
    }
</section>
```

En `Views/Pedidos/Detalles.cshtml`, insertar el partial inmediatamente después del `</section>` que cierra la tarjeta "Resumen" (antes de la sección "Líneas congeladas al envío"):

```razor
<partial name="_CreditoHuevo" model="new BloqueCreditoVista(Model.Credito)" />
```

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests/Trajano.GestorCaisy.Tests.csproj`

Expected: PASS, la suite completa.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy Icarus/tests/Trajano.GestorCaisy.Tests
git commit -m "feat(gestorcaisy): mostrar el credito del cliente en el detalle del pedido"
```

---

### Task 5: Bloque de crédito en aceptación y despacho

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/PedidosVistas.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Aceptar.cshtml`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Despachar.cshtml`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PedidosControllerTests.cs`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPedidosTests.cs`

**Interfaces:**
- Consumes: `CreditoDelPedidoONullAsync` y `BloqueCreditoVista` de la Task 4.
- Produces: `IFormularioConCredito { CreditoHuevoPedidoApi? Credito { get; set; } }`, implementada por `FormularioEntregaVista` y `FormularioDespachoVista`; y el helper privado `VistaConCredito<T>(string, Guid, T, CancellationToken)`.

- [ ] **Step 1: Escribir los tests que fallan**

En `PedidosControllerTests.cs`:

```csharp
    [Fact]
    public async Task AceptarLlevaElCreditoAlFormulario()
    {
        var id = Guid.NewGuid();
        _api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        _api.CreditoDePedido = new CreditoHuevoPedidoApi(-5000m, 14120m, 9120m, true, []);

        var vista = await _controlador.ConfirmarAceptacion(id, default);

        var modelo = Assert.IsType<FormularioEntregaVista>(((ViewResult)vista).Model);
        Assert.Equal(-5000m, modelo.Credito!.SaldoDisponible);
    }

    [Fact]
    public async Task DespacharLlevaElCreditoAlFormulario()
    {
        var id = Guid.NewGuid();
        _api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Aceptado");
        _api.CreditoDePedido = new CreditoHuevoPedidoApi(-5000m, 14120m, 9120m, true, []);

        var vista = await _controlador.ConfirmarDespacho(id, default);

        var modelo = Assert.IsType<FormularioDespachoVista>(((ViewResult)vista).Model);
        Assert.Equal(9120m, modelo.Credito!.SaldoSinEstePedido);
    }

    // El bloque no puede desaparecer a mitad de la decisión: el re-render
    // por error de validación lo vuelve a cargar.
    [Fact]
    public async Task ElReRenderPorFechaPasadaConservaElCredito()
    {
        var id = Guid.NewGuid();
        _api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        _api.CreditoDePedido = new CreditoHuevoPedidoApi(-5000m, 14120m, 9120m, true, []);

        var vista = await _controlador.Aceptar(id, new FormularioEntregaVista
        {
            Id = id,
            FechaEntregaEstimada = FechasDeOficina.Hoy().AddDays(-1),
        }, default);

        var modelo = Assert.IsType<FormularioEntregaVista>(((ViewResult)vista).Model);
        Assert.Equal(-5000m, modelo.Credito!.SaldoDisponible);
        Assert.False(_controlador.ModelState.IsValid);
    }

    [Fact]
    public async Task SiElCreditoFallaLaPantallaDeAceptacionSigueRenderizando()
    {
        var id = Guid.NewGuid();
        _api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        _api.ErrorDeObtenerCredito = new ErrorApiException(500, "Error interno");

        var vista = await _controlador.ConfirmarAceptacion(id, default);

        var modelo = Assert.IsType<FormularioEntregaVista>(((ViewResult)vista).Model);
        Assert.Null(modelo.Credito);
    }
```

Nota para el implementador: `FechasDeOficina` vive en el namespace `Trajano.GestorCaisy`; agregar `using Trajano.GestorCaisy;` al archivo de tests si el compilador lo pide.

En `FlujoPedidosTests.cs`:

```csharp
    [Fact]
    public async Task AceptarYDespacharMuestranElCreditoDelCliente()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.CreditoDePedido = new CreditoHuevoPedidoApi(
            -5000m, 14120m, 9120m, true, []);

        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        var htmlAceptar = await cliente.GetStringAsync($"/Pedidos/{id}/Aceptar");
        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Aceptado");
        var htmlDespachar = await cliente.GetStringAsync($"/Pedidos/{id}/Despachar");

        Assert.Contains("Crédito por despachos de huevo", htmlAceptar);
        Assert.Contains("9120.00", htmlAceptar);
        Assert.Contains("Crédito por despachos de huevo", htmlDespachar);
        Assert.Contains("9120.00", htmlDespachar);
    }
```

- [ ] **Step 2: Correr los tests para verificar que fallan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests/Trajano.GestorCaisy.Tests.csproj --filter "FullyQualifiedName~PedidosControllerTests|FullyQualifiedName~FlujoPedidosTests"`

Expected: FAIL de compilación — `FormularioEntregaVista` no tiene `Credito`.

- [ ] **Step 3: Escribir la implementación mínima**

En `Models/PedidosVistas.cs`, agregar el `using` que hace falta y la interfaz, y sumar la propiedad a los dos formularios:

```csharp
using Microsoft.AspNetCore.Mvc.ModelBinding;
```

```csharp
// Los formularios de decisión muestran el crédito como dato de referencia,
// nunca como campo enviado: no se envuelven en un modelo contenedor porque
// eso cambiaría el prefijo de los campos del POST. Hay precedente de dato de
// solo referencia dentro de un formulario: LineaDespachoVista.CantidadSolicitada.
public interface IFormularioConCredito
{
    CreditoHuevoPedidoApi? Credito { get; set; }
}
```

En `FormularioEntregaVista`, agregar la implementación y la propiedad:

```csharp
public sealed class FormularioEntregaVista : IFormularioConCredito
{
    [JsonRequired]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "La fecha de entrega estimada es obligatoria.")]
    [JsonRequired]
    public DateOnly FechaEntregaEstimada { get; set; }

    [BindNever]
    public CreditoHuevoPedidoApi? Credito { get; set; }
}
```

Lo mismo en `FormularioDespachoVista`: cambiar la declaración a
`public sealed class FormularioDespachoVista : IFormularioConCredito` y agregar al final de la clase:

```csharp
    [BindNever]
    public CreditoHuevoPedidoApi? Credito { get; set; }
```

En `Controllers/PedidosController.cs`, agregar el helper junto a `CreditoDelPedidoONullAsync`:

```csharp
    // Todo render de un formulario de decisión pasa por acá, incluidos los
    // re-render por error de validación: el bloque de crédito no puede
    // desaparecer a mitad de la decisión.
    private async Task<IActionResult> VistaConCredito<T>(
        string vista, Guid id, T formulario, CancellationToken token)
        where T : IFormularioConCredito
    {
        formulario.Credito = await CreditoDelPedidoONullAsync(id, token);
        return View(vista, formulario);
    }
```

Y reemplazar los seis render de `Aceptar` y `Despachar`:

1. En `ConfirmarDespacho` (GET):
```csharp
        return await VistaConCredito("Despachar", id, FormularioDespachoDesde(pedido), token);
```
2. En `Despachar` (POST), los tres `return View("Despachar", formulario);`:
```csharp
        return await VistaConCredito("Despachar", id, formulario, token);
```
3. En `ConfirmarAceptacion` (GET), el `return View(new FormularioEntregaVista { … });`:
```csharp
        return await VistaConCredito("Aceptar", id, new FormularioEntregaVista
        {
            Id = id,
            FechaEntregaEstimada = FechasDeOficina.Hoy(),
        }, token);
```
4. En `Aceptar` (POST), los tres `return View("Aceptar", formulario);`:
```csharp
        return await VistaConCredito("Aceptar", id, formulario, token);
```

No se toca `EntregaEstimada`: su formulario comparte el tipo, pero esa pantalla no muestra el bloque y su `Credito` queda nulo.

En `Views/Pedidos/Aceptar.cshtml`, insertar después de `<partial name="_Avisos" />`:

```razor
<partial name="_CreditoHuevo" model="new BloqueCreditoVista(Model.Credito)" />
```

Lo mismo en `Views/Pedidos/Despachar.cshtml`, después de `<partial name="_Avisos" />`.

- [ ] **Step 4: Correr los tests para verificar que pasan**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests/Trajano.GestorCaisy.Tests.csproj`

Expected: PASS, la suite completa. Verificar en particular que los tests previos de `Aceptar` y `Despachar` (binding del POST y antiforgery) siguen verdes: los nombres de los campos no cambiaron.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy Icarus/tests/Trajano.GestorCaisy.Tests
git commit -m "feat(gestorcaisy): mostrar el credito del cliente al aceptar y al despachar"
```

---

### Task 6: Puerta de calidad y cierre del backlog

**Files:**
- Modify: `docs/ai/HANDOFF.md`

**Interfaces:**
- Consumes: todo lo anterior. No produce contrato nuevo.

- [ ] **Step 1: Correr la puerta de calidad completa**

Con Docker corriendo.

Run: `./verify.ps1`

Expected: todos los gates verdes — mojibake, enlaces, adaptadores, frontend lint/build/tests, backend build sin errores, Architecture, Unit, GestorCaisy e Integration. Los conteos deben subir respecto de la última corrida (Unit +6, GestorCaisy +9, Integration +4).

Si algún gate falla, arreglar el contenido. Prohibido relajar el gate, la baseline o el umbral.

- [ ] **Step 2: Actualizar el handoff**

En `docs/ai/HANDOFF.md`, marcar el ítem 3 del backlog como resuelto, siguiendo el formato de los ítems 1 y 2 (tachado con `~~…~~` y el resumen de la decisión). Texto a usar:

```markdown
3. ~~**Vista de CAISY del crédito por cliente**~~ — **resuelto el 2026-09-11**:
   el alcance quedó en el pedido, no en el cliente. CAISY no podía reusar
   `GET /despachos-huevo/credito` (exige rol Cliente), así que se agregó
   `GET /pedidos-alimento-caisy/{id}/credito` con
   `ObtenerCreditoHuevoDePedidoCaisyQuery`: el `ClienteId` se deriva del
   pedido y no hay enumeración posible. El handler resuelve cuánto pesa el
   pedido en el saldo según su estado (comprometido, recibido real o nada) y
   devuelve tres cifras, porque el saldo ya tiene descontado el pedido
   abierto. CAISY ve lo mismo que el Cliente, ajustes con motivo incluidos:
   las dos son partes de la relación comercial, el Trabajador no. El bloque
   aparece en el detalle, la aceptación y el despacho, y degrada a un aviso
   si el cálculo no responde. Spec y plan en `docs/superpowers/`.
```

Actualizar además la sección "Estado" con los conteos reales de la corrida de `verify.ps1` y la sección "Pendiente inmediato" para que apunte al ítem 4 del backlog.

- [ ] **Step 3: Verificar los gates documentales**

Run: `node quality/check-mojibake.mjs; node quality/check-enlaces.mjs; git diff --check`

Expected: sin hallazgos.

- [ ] **Step 4: Commit y push**

```bash
git add docs/ai/HANDOFF.md
git commit -m "docs(ai): cerrar el item 3 del backlog de credito de huevo"
git push origin develop
```

---

## Self-Review

**Cobertura de la spec:**

| Requisito de la spec | Task |
|---|---|
| Query con alcance de pedido, `ClienteId` derivado | 1 |
| DTO con las tres cifras y el flag | 1 |
| Aritmética por estado en el backend | 1 (tabla completa, un test por rama) |
| No usar `TotalSolicitado` sino la suma de subtotales no nulos | 1 (implementación y comentario) |
| Sin gate de rol en el handler | 1 (comentario) y 2 (test del 403) |
| Endpoint bajo `/pedidos-alimento-caisy` con `GestorPedidoAlimento` | 2 |
| 401 / 403 Cliente / 403 Trabajador / 403 otra funcionalidad / 404 | 2 |
| Contrato espejo y cliente HTTP | 3 |
| Degradación ante falla, sin swallow de cancelación | 4 |
| Bloque en detalle | 4 |
| Bloque en aceptación y despacho, sin romper el binding | 5 |
| Negativo con etiqueta textual, no solo color | 4 (partial) y test de HTML |
| Ajustes con monto, motivo y fecha | 4 (partial) y 1 (test del handler) |
| Caso `PedidoComputadoEnElSaldo == false` | 2 (endpoint) y 4 (HTML) |
| Anti-PII: sin logs de montos | 4 (comentario del helper, sin `ILogger`) |
| Puerta de calidad completa | 6 |

Sin huecos. No se toca el camino del Cliente, la PWA ni ningún repositorio, como exige la spec.

**Consistencia de tipos:** `CreditoHuevoDePedidoCaisy` (backend) y `CreditoHuevoPedidoApi` (GestorCaisy) tienen las mismas cinco propiedades en el mismo orden; `AjusteCreditoHuevoResumen` y `AjusteCreditoHuevoApi` tienen `(Id, Monto, Motivo, Fecha)`. El nombre del método es `ObtenerCreditoDePedidoAsync` en la interfaz, la implementación, el doble y los tests. El helper es `CreditoDelPedidoONullAsync` en las Tasks 4 y 5. El modelo del partial es `BloqueCreditoVista` en las tres vistas.

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-09-11-credito-huevo-vista-caisy.md`.**

Está escrito para ejecutarse en una sesión aparte con un modelo más económico: cada task trae el código completo, el comando exacto y el mensaje de commit.
