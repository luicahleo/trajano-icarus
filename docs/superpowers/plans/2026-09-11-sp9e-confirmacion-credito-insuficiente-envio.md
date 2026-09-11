# SP9E — Confirmación explícita al enviar un pedido con crédito de huevo insuficiente — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cuando un pedido de alimento, al enviarse, dejaría el crédito por despachos de huevo del cliente en negativo, el backend exige que el cliente confirme explícitamente antes de aceptar el envío; sin confirmar, rechaza con un error distinguible y no persiste nada.

**Architecture:** El chequeo vive en `EnviarPedidoAlimentoHandler` (ya calcula el saldo hoy para SP9C), evaluado *antes* de mutar el agregado `PedidoAlimento`. Una excepción de dominio nueva, distinguible sin que `Icarus.BuildingBlocks.Observability` tenga que referenciar el módulo `GestionAvicola` (se usa una interfaz marker genérica en Building Blocks). El frontend reintenta con un flag tras el primer rechazo.

**Tech Stack:** .NET / MediatR / EF Core (backend), React + TanStack Query + MUI (frontend), xUnit + NSubstitute (tests backend), Vitest + Testing Library (tests frontend).

## Global Constraints

- Todo texto de negocio (mensajes, comentarios de dominio, nombres de test) en español correcto, con acentos, UTF-8 sin BOM.
- TDD real: cada test se corre en rojo antes de escribir el código que lo pone en verde. No saltear el paso de ver el fallo.
- Ningún commit usa `--no-verify` ni `-c commit.gpgsign=false`.
- Un commit por task, con el test dirigido en verde antes de comitear (no hace falta correr la suite completa en cada task intermedia — eso es la Task final).
- La rama de trabajo es `develop`; commits directos, sin PR (así funciona este repo).
- Nunca registrar en logs/registro de vuelo datos que no sean ids técnicos, estados o montos agregados (ya establecido en el resto del módulo; este plan no cambia esa regla, solo la respeta).

---

## Mapa de archivos

| Archivo | Acción |
|---|---|
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PedidoAlimento.cs` | Modificar: `EnviarACaisy` acepta motivo opcional |
| `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidoAlimentoTests.cs` | Modificar: test del motivo |
| `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs` | Modificar: nueva interfaz `IExcepcionConTituloPropio` |
| `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs` | Modificar: usa la interfaz para el título |
| `Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs` | Modificar: test del mecanismo genérico |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs` | Modificar: nueva excepción |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs` | Modificar: `EnviarPedidoAlimentoCommand` + `EnviarPedidoAlimentoHandler` |
| `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs` | Modificar: default del mock + 2 tests nuevos/ajustados |
| `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs` | Modificar: endpoint acepta body |
| `web/src/features/pedidos-alimento/api.ts` | Modificar: `enviarPedido` acepta el flag |
| `web/src/features/pedidos-alimento/api.test.ts` | Modificar: test del flag |
| `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx` | Modificar: diálogo de envío con confirmación |
| `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx` | Modificar: nuevo test del flujo |
| `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs` | Modificar: helper `EnviarAsync` + nuevo test |
| `Icarus/tests/Icarus.IntegrationTests/DocumentosNotaEndpointsTests.cs` | Modificar: una llamada a `/enviar` |

---

### Task 1: Dominio — `PedidoAlimento.EnviarACaisy` acepta un motivo opcional

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PedidoAlimento.cs:108-121`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidoAlimentoTests.cs`

**Interfaces:**
- Produces: `PedidoAlimento.EnviarACaisy(DateOnly fechaPedido, Guid actorId, IReadOnlyList<DatosPrecioEnvio> precios, string? motivo = null)` — el motivo (si no es null) queda en `TransicionPedidoAlimento.Motivo` de la transición Borrador→Solicitado. Task 3 lo consume.

- [ ] **Step 1: Escribir el test que falla**

En `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidoAlimentoTests.cs`, agregar este test después de `EnviarACongelaPreciosVigentesYPasaASolicitado` (o el primer test de envío exitoso que encuentres cerca de la línea 115-120; el orden exacto dentro del archivo no importa):

```csharp
    [Fact]
    public void EnviarACaisyConMotivoLoRegistraEnLaTransicion()
    {
        var pedido = BorradorDeBolsas(100);

        pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)],
            "Enviado con crédito insuficiente: saldo 0, pedido 18000, resultante -18000.");

        var transicion = Assert.Single(pedido.Historial);
        Assert.Equal(
            "Enviado con crédito insuficiente: saldo 0, pedido 18000, resultante -18000.",
            transicion.Motivo);
    }
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EnviarACaisyConMotivoLoRegistraEnLaTransicion"`
Expected: FAIL — error de compilación, `EnviarACaisy` no acepta un cuarto argumento.

- [ ] **Step 3: Implementar el cambio mínimo**

En `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PedidoAlimento.cs`, reemplazar el método `EnviarACaisy` completo (líneas 108-121):

```csharp
    public void EnviarACaisy(
        DateOnly fechaPedido, Guid actorId, IReadOnlyList<DatosPrecioEnvio> precios,
        string? motivo = null)
    {
        AsegurarEstado(EstadoPedidoAlimento.Borrador, "Solo un pedido en borrador se puede enviar.");
        AsegurarCantidadesGranel();
        var congelados = CongelarPrecios(precios);
        Estado = EstadoPedidoAlimento.Solicitado;
        FechaPedido = fechaPedido;
        foreach (var linea in _detalles)
            linea.CongelarPrecio(
                congelados[linea.TipoAlimento].PrecioFinalPor40Kg,
                congelados[linea.TipoAlimento].NotificacionPreciosAlimentosId);
        RegistrarTransicion(EstadoPedidoAlimento.Borrador, EstadoPedidoAlimento.Solicitado, actorId, motivo);
    }
```

También actualizar el comentario que precede al método (líneas 104-107) agregando una línea:

```csharp
    // Envío a CAISY (spec SP8): el servidor fija la fecha de negocio, congela
    // el precio vigente de todas las líneas dentro de la misma transacción y
    // registra la transición. Si falta precio para una línea, falla completo y
    // el borrador queda intacto. El motivo opcional (spec SP9E) es el aviso de
    // crédito insuficiente cuando el cliente confirmó el envío igual.
```

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EnviarACaisyConMotivoLoRegistraEnLaTransicion"`
Expected: PASS

- [ ] **Step 5: Correr toda la clase de test de dominio para confirmar que nada se rompió**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~PedidoAlimentoTests"`
Expected: todos PASS (el parámetro nuevo es opcional, ninguna llamada existente a `EnviarACaisy` se rompe).

- [ ] **Step 6: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PedidoAlimento.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidoAlimentoTests.cs
git commit -m "feat(avicola): permitir motivo opcional al enviar un pedido de alimento a CAISY"
```

---

### Task 2: Building Blocks — título propio para excepciones específicas + excepción de crédito insuficiente

**Contexto para quien implemente:** `ExceptionHandlingMiddleware` (en `Icarus.BuildingBlocks.Observability`) traduce excepciones a `ProblemDetails`. Hoy usa un switch por tipo: toda `ConflictException` recibe el mismo `title` genérico ("Conflicto con el estado actual"). Necesitamos que *una* excepción específica de `GestionAvicola` tenga su propio `title`, pero `Icarus.BuildingBlocks.Observability` **no puede referenciar `Icarus.GestionAvicola`** — Building Blocks es compartido por todos los módulos (Identity, Clientes, GestionAvicola) y no debe depender de ninguno de ellos, o los demás módulos arrastrarían esa dependencia transitiva sin sentido. La solución: una interfaz marker en `Icarus.BuildingBlocks.Domain` (que todos los módulos ya referencian) que cualquier excepción de cualquier módulo puede implementar para pedir su propio título; el middleware solo conoce la interfaz, nunca el tipo concreto.

**Files:**
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs`

**Interfaces:**
- Consumes: nada de tareas anteriores.
- Produces: `IExcepcionConTituloPropio.Titulo` (interfaz en `Icarus.BuildingBlocks.Domain`); `CreditoInsuficienteRequiereConfirmacionException` (en `Icarus.GestionAvicola.Application.CreditoHuevo`, hereda `ConflictException`, implementa la interfaz con `Titulo => "Crédito insuficiente"`). Task 3 lanza esta excepción.

- [ ] **Step 1: Escribir el test que falla (mecanismo genérico, aislado de GestionAvicola)**

En `Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs`, agregar (después de la clase `Ejecutar`, antes o después de cualquier `[Fact]` existente — el orden no importa):

```csharp
    private sealed class ExcepcionDePruebaConTituloPropio()
        : ConflictException("mensaje interno de la excepcion de prueba"), IExcepcionConTituloPropio
    {
        public string Titulo => "Título específico de prueba";
    }

    [Fact]
    public async Task UnaExcepcionConTituloPropioSobreescribeElTituloGenerico()
    {
        var (status, cuerpo) = await Ejecutar(new ExcepcionDePruebaConTituloPropio());

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("Título específico de prueba", cuerpo.GetProperty("title").GetString());
    }
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~UnaExcepcionConTituloPropioSobreescribeElTituloGenerico"`
Expected: FAIL — error de compilación, `IExcepcionConTituloPropio` no existe todavía.

- [ ] **Step 3: Agregar la interfaz en Building Blocks**

En `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs`, agregar al final del archivo (después de la clase `ConflictException`, dentro del mismo namespace, sin llave de cierre adicional — el archivo usa `namespace Icarus.BuildingBlocks.Domain;` de una sola línea, así que se agrega directamente a continuación):

```csharp

// Cualquier excepción de dominio de cualquier módulo puede implementar esto
// para pedirle al middleware un título de ProblemDetails distinto del
// genérico de su clase base (p. ej. distinguir un subtipo de
// ConflictException). Building Blocks define el contrato; el middleware
// nunca conoce el tipo concreto de ningún módulo vertical.
public interface IExcepcionConTituloPropio
{
    string Titulo { get; }
}
```

- [ ] **Step 4: Usar la interfaz en el middleware**

En `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs`, reemplazar el bloque (líneas 34-42):

```csharp
        var (status, titulo) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            DomainException => (StatusCodes.Status400BadRequest, "Error de negocio"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno"),
        };
```

por:

```csharp
        var (status, tituloGenerico) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            DomainException => (StatusCodes.Status400BadRequest, "Error de negocio"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno"),
        };
        var titulo = ex is IExcepcionConTituloPropio conTituloPropio ? conTituloPropio.Titulo : tituloGenerico;
```

- [ ] **Step 5: Correr el test y verificar que pasa**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~UnaExcepcionConTituloPropioSobreescribeElTituloGenerico"`
Expected: PASS

- [ ] **Step 6: Correr toda la clase de test del middleware para confirmar que nada se rompió**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ExceptionHandlingMiddlewareTests"`
Expected: todos PASS.

- [ ] **Step 7: Agregar la excepción de crédito insuficiente**

En `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs`, el contenido completo pasa a ser:

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Application.CreditoHuevo;

// Crédito por huevos despachados (spec SP9): disponible recién catorce días
// después de la recepción. Sin tabla de saldo persistida: se calcula por
// consulta (suma de ingresos disponibles menos egresos reales).
public interface IRepositorioBalanceCreditoHuevo
{
    Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default);
}

public static class ReglasCreditoHuevo
{
    public const int DiasDisponibilidadCredito = 14;
}

// Exige confirmación explícita del cliente antes de enviar un pedido que
// dejaría su crédito por despachos de huevo en negativo (spec SP9E). Hereda
// de ConflictException (409): la decisión final de aceptar el pedido sigue
// siendo de CAISY al aceptar/rechazar, así que esto no es un bloqueo duro
// sin salida — solo exige el paso consciente de confirmar.
public sealed class CreditoInsuficienteRequiereConfirmacionException()
    : ConflictException(
        "Este pedido dejaría el crédito del cliente en negativo. " +
        "Confirmá el envío para continuar."),
      IExcepcionConTituloPropio
{
    public string Titulo => "Crédito insuficiente";
}
```

No hay test unitario dedicado a esta clase por sí sola (es un constructor con mensaje fijo); Task 3 la ejercita end-to-end vía el Handler, y Task 7 la ejercita vía el endpoint HTTP real.

- [ ] **Step 8: Verificar que el proyecto compila**

Run: `dotnet build Icarus/Icarus.sln`
Expected: 0 errores.

- [ ] **Step 9: Commit**

```bash
git add Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs
git commit -m "feat(observability): permitir titulo propio de ProblemDetails sin acoplar building blocks a un modulo"
```

---

### Task 3: Aplicación — `EnviarPedidoAlimentoCommand` exige confirmación cuando el crédito queda negativo

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs:40-46,301-366`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs`

**Interfaces:**
- Consumes: `PedidoAlimento.EnviarACaisy(..., string? motivo = null)` (Task 1); `CreditoInsuficienteRequiereConfirmacionException` (Task 2); `IRepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync` (ya existente); `NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente` (ya existente); `DatosPrecioEnvio(TipoAlimento Tipo, PresentacionAlimento Presentacion, decimal PrecioFinalPor40Kg, Guid NotificacionPreciosAlimentosId)` (ya existente); `DetallePedidoAlimento.Equivalentes40Kg` (int, ya calculado sin depender del precio congelado).
- Produces: `EnviarPedidoAlimentoCommand(Guid PedidoId, bool ConfirmarCreditoInsuficiente = false)`. Task 4 (endpoint) y Task 6/7 (frontend/integración) lo consumen.

- [ ] **Step 1: Ajustar el default del mock de saldo en el archivo de test (antes de escribir los tests nuevos)**

En `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs`, en el constructor `PedidosAlimentoHandlerTests()` (líneas 62-71), cambiar:

```csharp
        _balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(0m);
```

por:

```csharp
        // Saldo suficiente por defecto: los tests que no versan sobre crédito
        // (cupo semanal, congelado de precios, etc.) no deben verse afectados
        // por SP9E. Los tests de crédito lo sobreescriben explícitamente.
        _balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(1_000_000m);
```

- [ ] **Step 2: Correr toda la clase de test para confirmar que sigue en verde con el nuevo default**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~PedidosAlimentoHandlerTests"`
Expected: todos PASS (nada más cambió todavía).

- [ ] **Step 3: Escribir el test que falla — rechazo sin confirmar**

En el mismo archivo, reemplazar el test `EnviarConSaldoInsuficienteAvisaACaisySinBloquearElEnvio` (líneas 299-321, incluyendo su comentario) por estos dos tests:

```csharp
    // SP9E (spec: "Confirmación explícita al enviar un pedido con crédito de
    // huevo insuficiente"): el primer intento sin confirmar se rechaza antes
    // de mutar el pedido ni gastar cupo — el cliente debe confirmar a
    // sabiendas. El saldo se lee antes de mutar el agregado, así que ya
    // excluye el pedido actual.
    [Fact]
    public async Task EnviarSinConfirmarConSaldoInsuficienteExigeConfirmacion()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(PublicacionVigente());
        _balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(0m);

        await Assert.ThrowsAsync<CreditoInsuficienteRequiereConfirmacionException>(() =>
            CrearEnviador().Handle(
                new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None));

        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
        Assert.Empty(pedido.Historial);
        _notificacionesDespachoHuevo.DidNotReceive()
            .Agregar(Arg.Any<NotificacionInternaDespachoHuevo>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _transaccion.DidNotReceive().ConfirmarAsync(Arg.Any<CancellationToken>());
    }

    // SP9E: confirmando explícitamente, el envío procede igual, la bandeja
    // global de CAISY se sigue avisando (sin cambios desde SP9C) y además
    // queda el motivo en el propio historial del pedido.
    [Fact]
    public async Task EnviarConfirmandoSaldoInsuficienteAvisaACaisyYDejaMotivoEnHistorial()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(PublicacionVigente());
        _balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
            ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(0m);

        await CrearEnviador().Handle(
            new EnviarPedidoAlimentoCommand(pedido.Id, ConfirmarCreditoInsuficiente: true),
            CancellationToken.None);

        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);
        _notificacionesDespachoHuevo.Received(1).Agregar(Arg.Is<NotificacionInternaDespachoHuevo>(n =>
            n.Tipo == TipoNotificacionDespachoHuevo.CreditoInsuficiente));
        var transicion = Assert.Single(pedido.Historial);
        Assert.Contains("crédito insuficiente", transicion.Motivo, StringComparison.OrdinalIgnoreCase);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _transaccion.Received(1).ConfirmarAsync(Arg.Any<CancellationToken>());
    }
```

- [ ] **Step 4: Correr los dos tests nuevos y verificar que fallan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EnviarSinConfirmarConSaldoInsuficienteExigeConfirmacion|FullyQualifiedName~EnviarConfirmandoSaldoInsuficienteAvisaACaisyYDejaMotivoEnHistorial"`
Expected: FAIL — `EnviarSinConfirmarConSaldoInsuficienteExigeConfirmacion` falla porque hoy no se lanza ninguna excepción (el envío se completa); `EnviarConfirmandoSaldoInsuficienteAvisaACaisyYDejaMotivoEnHistorial` falla en compilación porque `EnviarPedidoAlimentoCommand` todavía no tiene el parámetro `ConfirmarCreditoInsuficiente`.

- [ ] **Step 5: Extender el comando**

En `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs`, reemplazar (líneas 40-46):

```csharp
public sealed record EnviarPedidoAlimentoCommand(Guid PedidoId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.enviar",
        new Dictionary<string, DatoRegistroVuelo> { ["Lineas"] = DatoRegistroVuelo.Entero });
}
```

por:

```csharp
public sealed record EnviarPedidoAlimentoCommand(
    Guid PedidoId, bool ConfirmarCreditoInsuficiente = false)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.enviar",
        new Dictionary<string, DatoRegistroVuelo> { ["Lineas"] = DatoRegistroVuelo.Entero });
}
```

- [ ] **Step 6: Implementar la lógica en el Handler**

En el mismo archivo, reemplazar el método `Handle` completo de `EnviarPedidoAlimentoHandler` (líneas 313-365, es decir desde `public async Task Handle(EnviarPedidoAlimentoCommand request, CancellationToken cancellationToken)` hasta el `}` que cierra el método, sin tocar la declaración de la clase ni el constructor primario que la precede):

```csharp
    public async Task Handle(EnviarPedidoAlimentoCommand request, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Borrador)
            throw new ConflictException("Solo un pedido en borrador se puede enviar.");
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");

        var hoy = FechasNegocio.Hoy();
        await using var transaccion = await repositorio.IniciarTransaccionAsync(cancellationToken);
        var inicioSemana = SemanasIso.Inicio(hoy);
        var enviados = await repositorio.ContarEnviadosEnSemanaBloqueandoAsync(
            pedido.ClienteId, inicioSemana, inicioSemana.AddDays(6), cancellationToken);
        var maximo = opciones.MaximoPorSemana;
        if (enviados >= maximo)
            throw new ConflictException(
                $"Se alcanzó el límite semanal de {maximo.ToString(CultureInfo.InvariantCulture)} pedidos enviados.");

        var vigente = await repositorioPrecios.ObtenerVigenteAsync(hoy, cancellationToken)
            ?? throw new ValidationException("No hay una publicación de precios vigente.");
        var precios = vigente.Detalles
            .Select(d => new DatosPrecioEnvio(d.TipoAlimento, d.Presentacion, d.PrecioFinalPor40Kg, vigente.Id))
            .ToList();

        // Crédito (spec SP9E): se evalúa ANTES de mutar el agregado, con la
        // misma fórmula que EnviarACaisy va a congelar más abajo (precio ×
        // Equivalentes40Kg), así que el total coincide exactamente con
        // TotalSolicitado una vez enviado. Si falta precio para algún tipo,
        // no se evalúa el crédito acá: EnviarACaisy va a fallar más abajo con
        // su propio mensaje ("Falta precio vigente..."), que debe prevalecer
        // sobre cualquier mensaje de crédito.
        var preciosPorTipo = precios.ToDictionary(p => (p.Tipo, p.Presentacion), p => p.PrecioFinalPor40Kg);
        string? motivoCreditoInsuficiente = null;
        var haySaldoInsuficiente = false;
        if (pedido.Detalles.All(d => preciosPorTipo.ContainsKey((d.TipoAlimento, d.Presentacion))))
        {
            var totalEsperado = pedido.Detalles.Sum(
                d => preciosPorTipo[(d.TipoAlimento, d.Presentacion)] * d.Equivalentes40Kg);
            var saldoActual = await balanceCreditoHuevo.ObtenerSaldoDisponibleAsync(
                pedido.ClienteId, hoy, cancellationToken);
            var saldoResultante = saldoActual - totalEsperado;
            haySaldoInsuficiente = saldoResultante < 0;
            if (haySaldoInsuficiente)
            {
                if (!request.ConfirmarCreditoInsuficiente)
                    throw new CreditoInsuficienteRequiereConfirmacionException();
                motivoCreditoInsuficiente = string.Create(CultureInfo.InvariantCulture,
                    $"Enviado con crédito insuficiente: saldo {saldoActual}, pedido {totalEsperado}, resultante {saldoResultante}.");
            }
        }

        // El reenvío tras una devolución avisa a CAISY con su propio tipo: la
        // primera salida del borrador fija FechaPedido y la devolución la
        // conserva (el historial no se carga en este comando).
        var esReenvio = pedido.FechaPedido is not null;
        pedido.EnviarACaisy(hoy, actorId, precios, motivoCreditoInsuficiente);
        notificaciones.Agregar(NotificacionInterna.ParaCaisy(
            esReenvio ? TipoNotificacionPedido.PedidoReenviado : TipoNotificacionPedido.PedidoSolicitado,
            pedido.Id));

        // Notificación pasiva a la bandeja global de CAISY (spec SP9C, sin
        // cambios de condición ni de contenido): se genera exactamente cuando
        // el saldo resultante da negativo, confirmado o no.
        if (haySaldoInsuficiente)
            notificacionesDespachoHuevo.Agregar(
                NotificacionInternaDespachoHuevo.ParaCreditoInsuficiente(pedido.Id));

        registroVuelo.Decidir("avicola.pedidos.enviar", "envio", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = pedido.Detalles.Count,
                ["NotificacionPreciosId"] = vigente.Id,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        await transaccion.ConfirmarAsync(cancellationToken);
    }
```

También borrar el comentario viejo que quedó desactualizado sobre el método (líneas 294-300, el que dice "En una única transacción se comprueba el cupo... Los dobles clics y reintentos chocan con el estado...") y reemplazarlo por:

```csharp
// Envío a CAISY (spec SP8): en una única transacción se comprueba el cupo con
// una consulta bloqueable, se congela la publicación vigente resuelta por la
// fecha de negocio de Bolivia y se registra la transición junto con la
// notificación para la bandeja CAISY. Si falta precio para una línea o no hay
// publicación vigente, el envío falla completo y el borrador queda intacto.
// Los dobles clics y reintentos chocan con el estado y responden 409 sin
// gastar cupo ni repetir la transición ni la notificación. Si el crédito
// proyectado del cliente queda negativo y el comando no trae confirmación
// (spec SP9E), también falla completo sin persistir nada — el cliente debe
// reintentar confirmando explícitamente.
```

- [ ] **Step 7: Correr los dos tests nuevos y verificar que pasan**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EnviarSinConfirmarConSaldoInsuficienteExigeConfirmacion|FullyQualifiedName~EnviarConfirmandoSaldoInsuficienteAvisaACaisyYDejaMotivoEnHistorial"`
Expected: PASS

- [ ] **Step 8: Correr toda la clase de test del handler para confirmar que nada más se rompió**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~PedidosAlimentoHandlerTests"`
Expected: todos PASS. En particular `EnviarCongelaPreciosVigentesConFechaDeNegocio`, `ElCupoSeConsultaEnLaSemanaIsoActualDelTenant`, `EnviarFallaCompletoSiFaltaPrecioDeUnaLinea` y `EnviarConSaldoSuficienteNoAvisaYConfirmaElEnvio` deben seguir en verde sin haber sido tocados.

- [ ] **Step 9: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs
git commit -m "feat(avicola): exigir confirmacion explicita al enviar un pedido con credito de huevo insuficiente"
```

---

### Task 4: Host — el endpoint de envío acepta el flag de confirmación

**Files:**
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs:71-76,259-273`

**Interfaces:**
- Consumes: `EnviarPedidoAlimentoCommand(Guid PedidoId, bool ConfirmarCreditoInsuficiente = false)` (Task 3).
- Produces: `POST /pedidos-alimento/{id}/enviar` acepta body JSON opcional `{ "confirmarCreditoInsuficiente": boolean }`. Task 6 (frontend) y Task 7 (integración) lo consumen.

No hay test unitario dedicado a nivel de endpoint minimal API en este proyecto (se verifican vía integración, Task 7). Este paso no es TDD por sí solo; lo valida el test de integración de la Task 7.

- [ ] **Step 1: Modificar el endpoint**

En `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs`, reemplazar (líneas 71-76):

```csharp
        tenant.MapPost("/{id:guid}/enviar", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new EnviarPedidoAlimentoCommand(id), cancellationToken);
            return Results.NoContent();
        });
```

por:

```csharp
        tenant.MapPost("/{id:guid}/enviar", async (Guid id, EnviarPedidoRequest? cuerpo,
            ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(
                new EnviarPedidoAlimentoCommand(id, cuerpo?.ConfirmarCreditoInsuficiente ?? false),
                cancellationToken);
            return Results.NoContent();
        });
```

- [ ] **Step 2: Agregar el record de request**

En el mismo archivo, en el bloque de records privados al final de la clase (cerca de la línea 259-273, junto a `private sealed record MotivoRequest(string Motivo);`), agregar:

```csharp
    private sealed record EnviarPedidoRequest(bool ConfirmarCreditoInsuficiente = false);
```

- [ ] **Step 3: Verificar que el proyecto compila**

Run: `dotnet build Icarus/Icarus.sln`
Expected: 0 errores.

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs
git commit -m "feat(avicola): aceptar el flag de confirmacion de credito en el endpoint de envio"
```

---

### Task 5: Frontend — `enviarPedido` acepta el flag de confirmación

**Files:**
- Modify: `web/src/features/pedidos-alimento/api.ts`
- Test: `web/src/features/pedidos-alimento/api.test.ts`

**Interfaces:**
- Produces: `enviarPedido(id: string, confirmarCreditoInsuficiente = false): Promise<void>`. Task 6 lo consume.

- [ ] **Step 1: Escribir el test que falla**

En `web/src/features/pedidos-alimento/api.test.ts`, reemplazar el test existente (líneas 67-74):

```typescript
  test('enviarPedido hace POST al envío', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => sinCuerpo());
    vi.stubGlobal('fetch', f);
    await enviarPedido('p1');
    const q = solicitud(f);
    expect(q.method).toBe('POST');
    expect(q.url).toContain('/api/pedidos-alimento/p1/enviar');
  });
```

por:

```typescript
  test('enviarPedido hace POST al envío sin confirmar por defecto', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => sinCuerpo());
    vi.stubGlobal('fetch', f);
    await enviarPedido('p1');
    const q = solicitud(f);
    expect(q.method).toBe('POST');
    expect(q.url).toContain('/api/pedidos-alimento/p1/enviar');
    expect(await q.json()).toEqual({ confirmarCreditoInsuficiente: false });
  });

  test('enviarPedido manda el flag cuando se confirma', async () => {
    const f: ReturnType<typeof vi.fn> = vi.fn(async () => sinCuerpo());
    vi.stubGlobal('fetch', f);
    await enviarPedido('p1', true);
    const q = solicitud(f);
    expect(await q.json()).toEqual({ confirmarCreditoInsuficiente: true });
  });
```

**Nota para quien implemente:** revisar cómo `solicitud(f)` extrae el `Request` del mock en la parte superior de este archivo de test (busca la función `solicitud` definida antes de la línea 20) — si ya devuelve el objeto `Request`, `.json()` funciona directo; si devuelve otra forma, adaptar la lectura del cuerpo a lo que ese helper ya expone, sin cambiar su firma.

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `npm --prefix web test -- api.test.ts -t "enviarPedido"`
Expected: FAIL — el cuerpo enviado hoy es `undefined` (la función actual no manda body).

- [ ] **Step 3: Implementar el cambio**

En `web/src/features/pedidos-alimento/api.ts`, reemplazar (líneas 115-116):

```typescript
export const enviarPedido = (id: string) =>
  peticion<void>({ ruta: `/pedidos-alimento/${id}/enviar`, metodo: 'POST' });
```

por:

```typescript
export const enviarPedido = (id: string, confirmarCreditoInsuficiente = false) =>
  peticion<void>({
    ruta: `/pedidos-alimento/${id}/enviar`,
    metodo: 'POST',
    cuerpo: { confirmarCreditoInsuficiente },
  });
```

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `npm --prefix web test -- api.test.ts -t "enviarPedido"`
Expected: PASS

- [ ] **Step 5: Correr todo el archivo de test para confirmar que nada más se rompió**

Run: `npm --prefix web test -- api.test.ts`
Expected: todos PASS.

- [ ] **Step 6: Commit**

```bash
git add web/src/features/pedidos-alimento/api.ts web/src/features/pedidos-alimento/api.test.ts
git commit -m "feat(web): enviar el flag de confirmacion de credito al enviar un pedido"
```

---

### Task 6: Frontend — el diálogo de envío pide confirmación cuando hace falta

**Files:**
- Modify: `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx`
- Test: `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx`

**Interfaces:**
- Consumes: `enviarPedido(id, confirmarCreditoInsuficiente)` (Task 5); `obtenerBalanceCreditoHuevo(): Promise<{ saldoDisponible: number }>` (ya existe en `web/src/features/despacho-huevo/api.ts`); `ApiError` (ya existe en `web/src/lib/http.ts`, con la propiedad `code` = `title` del `ProblemDetails`).

- [ ] **Step 1: Escribir el test que falla**

En `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx`, agregar este test después de `'enviar pide confirmación con el total y reintenta sin duplicar'` (después de la línea 199, antes del test `'un borrador nuevo estima los precios vigentes...'`):

```tsx
  test('enviar con credito insuficiente pide confirmar y reintenta con el flag', async () => {
    const usuario = userEvent.setup();
    const cuerpos: unknown[] = [];
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const req = input instanceof Request ? input : new Request(String(input), init);
      const ruta = `${req.method} ${new URL(req.url).pathname}`;
      if (ruta === 'POST /api/pedidos-alimento/p1/enviar') {
        cuerpos.push(await req.json());
        return cuerpos.length === 1 ? respuesta(409, { title: 'Crédito insuficiente' }) : respuesta(204);
      }
      if (ruta === 'GET /api/pedidos-alimento/p1') return respuesta(200, pedidoBorradorDevuelto);
      if (ruta === 'GET /api/despachos-huevo/credito') return respuesta(200, { saldoDisponible: -5000 });
      return respuesta(404);
    });
    vi.stubGlobal('fetch', fetchMock);
    renderPagina();
    await usuario.click(await screen.findByRole('button', { name: 'Enviar a CAISY' }));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar envío' }));

    expect(await screen.findByText(/va a dejar tu crédito/i)).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Enviar de todas formas' }));

    expect(cuerpos).toEqual([
      { confirmarCreditoInsuficiente: false },
      { confirmarCreditoInsuficiente: true },
    ]);
  });
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `npm --prefix web test -- PedidoAlimentoDetallePage.test.tsx -t "credito insuficiente"`
Expected: FAIL — hoy el 409 dispara el `Alert` de error genérico de la página (`No se pudo enviar el pedido.`) y nunca aparece el texto "va a dejar tu crédito" ni el botón "Enviar de todas formas".

- [ ] **Step 3: Implementar el cambio**

En `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx`:

3a. Agregar imports (junto a los imports existentes, después de la línea 26 `import { DialogoConfirmacion } from '../../app/ui/DialogoConfirmacion';`):

```tsx
import { ApiError } from '../../lib/http';
import { obtenerBalanceCreditoHuevo } from '../despacho-huevo/api';
```

3b. Agregar la constante del título (justo antes de la declaración de la función del componente, después del comentario de la línea 73-75 `// Detalle del pedido...`):

```tsx
// Debe coincidir exactamente con el `title` que arma el backend para
// CreditoInsuficienteRequiereConfirmacionException (spec SP9E) — es el único
// mecanismo disponible hoy para distinguir este 409 de otros sin extender
// ApiError con campos específicos de un único caso de uso.
const TITULO_CREDITO_INSUFICIENTE = 'Crédito insuficiente';
```

3c. Agregar el estado nuevo (junto a los `useState` existentes, después de la línea 80 `const [confirmarEnvio, setConfirmarEnvio] = useState(false);`):

```tsx
  const [requiereConfirmacionCredito, setRequiereConfirmacionCredito] = useState(false);
```

3d. Agregar la query del saldo (después del bloque `preciosVigentes` que termina en la línea 102, antes de `const precioEstimadoDe = ...`):

```tsx
  // Saldo actual del cliente, consultado solo cuando el primer intento de
  // envío ya avisó que hace falta confirmar (spec SP9E): evita duplicar en
  // el frontend el mismo cálculo que ya hace el backend.
  const { data: creditoParaConfirmar } = useQuery({
    queryKey: ['despachos-huevo', 'credito'],
    queryFn: obtenerBalanceCreditoHuevo,
    enabled: requiereConfirmacionCredito,
  });
```

3e. Reemplazar la mutación `enviar` (líneas 121-129):

```tsx
  const enviar = useMutation({
    mutationFn: () => enviarPedido(id!),
    onSuccess: () => {
      setConfirmarEnvio(false);
      setError(null);
      refrescar();
    },
    onError: (e) => setError(e instanceof Error ? e.message : 'No se pudo enviar el pedido.'),
  });
```

por:

```tsx
  const enviar = useMutation({
    mutationFn: (confirmarCreditoInsuficiente: boolean) => enviarPedido(id!, confirmarCreditoInsuficiente),
    onSuccess: () => {
      setConfirmarEnvio(false);
      setRequiereConfirmacionCredito(false);
      setError(null);
      refrescar();
    },
    onError: (e) => {
      if (e instanceof ApiError && e.code === TITULO_CREDITO_INSUFICIENTE) {
        setRequiereConfirmacionCredito(true);
        return;
      }
      setError(e instanceof Error ? e.message : 'No se pudo enviar el pedido.');
    },
  });
```

3f. Agregar una función para cerrar el diálogo limpiando también el estado de crédito (junto a `const refrescar = ...`, después de la línea 119):

```tsx
  const cerrarDialogoEnvio = () => {
    setConfirmarEnvio(false);
    setRequiereConfirmacionCredito(false);
  };
```

3g. Reemplazar el `Dialog` de envío completo (líneas 490-518):

```tsx
      <Dialog open={confirmarEnvio} onClose={() => setConfirmarEnvio(false)}>
        <DialogTitle>Enviar pedido a CAISY</DialogTitle>
        <DialogContent>
          <DialogContentText component="div">
            <Typography variant="body2" sx={{ mb: 1 }}>
              Al enviar se fija la fecha de pedido (hoy) y se congelan los precios
              vigentes de todas las líneas. Esta acción consume el cupo semanal.
            </Typography>
            <Typography variant="body2">
              Total a enviar:{' '}
              <strong>
                {totalParaEnviar === null
                  ? 'sin precios vigentes para todas las líneas'
                  : `${formatoMoneda(totalParaEnviar)}${esEstimado ? ' (estimado, se congela al enviar)' : ''}`}
              </strong>
            </Typography>
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmarEnvio(false)}>Cancelar</Button>
          <Button
            variant="contained"
            onClick={() => enviar.mutate()}
            disabled={enviar.isPending || totalParaEnviar === null}
          >
            Confirmar envío
          </Button>
        </DialogActions>
      </Dialog>
```

por:

```tsx
      <Dialog open={confirmarEnvio} onClose={cerrarDialogoEnvio}>
        <DialogTitle>Enviar pedido a CAISY</DialogTitle>
        <DialogContent>
          <DialogContentText component="div">
            <Typography variant="body2" sx={{ mb: 1 }}>
              Al enviar se fija la fecha de pedido (hoy) y se congelan los precios
              vigentes de todas las líneas. Esta acción consume el cupo semanal.
            </Typography>
            <Typography variant="body2">
              Total a enviar:{' '}
              <strong>
                {totalParaEnviar === null
                  ? 'sin precios vigentes para todas las líneas'
                  : `${formatoMoneda(totalParaEnviar)}${esEstimado ? ' (estimado, se congela al enviar)' : ''}`}
              </strong>
            </Typography>
            {requiereConfirmacionCredito && (
              <Alert severity="warning" sx={{ mt: 2 }}>
                Este pedido va a dejar tu crédito por despachos de huevo en{' '}
                {formatoMoneda((creditoParaConfirmar?.saldoDisponible ?? 0) - (totalParaEnviar ?? 0))}{' '}
                negativo (saldo actual {formatoMoneda(creditoParaConfirmar?.saldoDisponible ?? 0)}, este
                pedido {formatoMoneda(totalParaEnviar ?? 0)}). ¿Confirmás el envío igual?
              </Alert>
            )}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={cerrarDialogoEnvio}>Cancelar</Button>
          <Button
            variant="contained"
            onClick={() => enviar.mutate(requiereConfirmacionCredito)}
            disabled={enviar.isPending || totalParaEnviar === null}
          >
            {requiereConfirmacionCredito ? 'Enviar de todas formas' : 'Confirmar envío'}
          </Button>
        </DialogActions>
      </Dialog>
```

- [ ] **Step 4: Correr el test nuevo y verificar que pasa**

Run: `npm --prefix web test -- PedidoAlimentoDetallePage.test.tsx -t "credito insuficiente"`
Expected: PASS

- [ ] **Step 5: Correr todo el archivo de test para confirmar que nada más se rompió**

Run: `npm --prefix web test -- PedidoAlimentoDetallePage.test.tsx`
Expected: todos PASS. En particular `'enviar pide confirmación con el total y reintenta sin duplicar'` y `'un borrador nuevo estima los precios vigentes y permite el primer envío'` deben seguir en verde (su primer intento ya es 204, nunca entran a la rama nueva).

- [ ] **Step 6: Correr lint y typecheck del frontend**

Run: `npm --prefix web run lint` y `npm --prefix web run typecheck`
Expected: 0 errores.

- [ ] **Step 7: Commit**

```bash
git add web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.test.tsx
git commit -m "feat(web): pedir confirmacion explicita al enviar un pedido con credito insuficiente"
```

---

### Task 7: Integración end-to-end — el endpoint real rechaza, informa y acepta al confirmar

**Contexto para quien implemente:** ningún test de integración existente siembra despachos de huevo antes de enviar un pedido de alimento, así que el saldo real de cualquier cliente de prueba es `0` (fórmula: `ingresos - recibidoReal - comprometidoPendiente + ajustes`, todos en cero sin datos sembrados). Con el cambio de esta feature, **cualquier pedido con total > 0 enviado sin el flag de confirmación ahora responde 409** en vez de 204. Dos archivos de integración ya existentes envían pedidos como parte de su *setup* (no están probando crédito) y se romperían si no se ajustan.

**Files:**
- Modify: `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`
- Modify: `Icarus/tests/Icarus.IntegrationTests/DocumentosNotaEndpointsTests.cs`

**Interfaces:**
- Consumes: `POST /pedidos-alimento/{id}/enviar` con body `{ confirmarCreditoInsuficiente: boolean }` (Task 4).

- [ ] **Step 1: Ajustar el helper `EnviarAsync` para no romper los tests existentes**

En `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`, reemplazar (líneas 73-75):

```csharp
    private static async Task<HttpStatusCode> EnviarAsync(HttpClient cliente, string token, Guid id) =>
        (await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{id}/enviar", token))).StatusCode;
```

por:

```csharp
    // El default confirma crédito insuficiente (spec SP9E): sin ningún
    // despacho de huevo sembrado, el saldo real de cualquier cliente de
    // prueba es 0, así que cualquier pedido con total > 0 dispara el
    // chequeo. Los tests de este archivo versan sobre cupo semanal y
    // transiciones, no sobre crédito, así que no deben verse afectados; el
    // único test que sí prueba crédito llama al endpoint directo, sin este
    // helper, para controlar el flag explícitamente.
    private static async Task<HttpStatusCode> EnviarAsync(
        HttpClient cliente, string token, Guid id, bool confirmarCreditoInsuficiente = true) =>
        (await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{id}/enviar", token,
            JsonContent.Create(new { confirmarCreditoInsuficiente })))).StatusCode;
```

- [ ] **Step 2: Correr la suite de integración de este archivo y verificar que sigue pasando (rojo esperado antes del fix del Step 1 se salta: este paso es una red de seguridad, no un test nuevo)**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~PedidosAlimentoEndpointsTests"`
Expected: PASS (requiere Docker corriendo para Testcontainers.MsSql — si no está disponible, avisar y detenerse acá; no continuar sin poder correr esta verificación).

- [ ] **Step 3: Escribir el test nuevo que falla**

En el mismo archivo, agregar después de `FlujoCompletoConTransicionesRechazoYNotificaciones` (al final de la clase, antes de la llave de cierre):

```csharp
    // SP9E (spec: "Confirmación explícita al enviar un pedido con crédito de
    // huevo insuficiente"): sin ningún despacho de huevo recibido, el saldo
    // del cliente es 0 y cualquier pedido con total > 0 exige confirmación.
    [Fact]
    public async Task EnviarSinConfirmarConCreditoInsuficienteExigeConfirmacionYElReintentoLoAcepta()
    {
        var cliente = _factory.CreateClient();
        var tokenCliente = await LoginComo(cliente, SemillaIdentidad.EmailClienteC1);
        var (caisy, tokenCaisy) = await CrearCuentaCaisyConFuncion();
        await ImportarYPublicarAsync(caisy, tokenCaisy);
        var pedidoId = await CrearBorradorAsync(cliente, tokenCliente);

        var primerIntento = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{pedidoId}/enviar", tokenCliente,
            JsonContent.Create(new { confirmarCreditoInsuficiente = false })));
        Assert.Equal(HttpStatusCode.Conflict, primerIntento.StatusCode);
        var cuerpoError = await primerIntento.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Crédito insuficiente", cuerpoError.GetProperty("title").GetString());

        var detalleSinEnviar = await ObtenerDetalleAsync(cliente, tokenCliente, pedidoId);
        Assert.Equal("Borrador", detalleSinEnviar.GetProperty("estado").GetString());
        Assert.Equal(0, detalleSinEnviar.GetProperty("historial").GetArrayLength());

        var reintento = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{pedidoId}/enviar", tokenCliente,
            JsonContent.Create(new { confirmarCreditoInsuficiente = true })));
        Assert.Equal(HttpStatusCode.NoContent, reintento.StatusCode);

        var detalleEnviado = await ObtenerDetalleAsync(cliente, tokenCliente, pedidoId);
        Assert.Equal("Solicitado", detalleEnviado.GetProperty("estado").GetString());
        var transicion = detalleEnviado.GetProperty("historial").EnumerateArray().Single();
        Assert.Contains("crédito insuficiente",
            transicion.GetProperty("motivo").GetString()!, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 4: Correr el test nuevo y verificar que falla**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~EnviarSinConfirmarConCreditoInsuficienteExigeConfirmacionYElReintentoLoAcepta"`
Expected: si las Tasks 1-4 ya están aplicadas (deberían estarlo, este plan es secuencial), este test debería compilar y pasar directo. Si por algún motivo el ambiente todavía tiene código viejo, FAIL con 204 en vez de 409 en el primer intento.

- [ ] **Step 5: Ajustar `DocumentosNotaEndpointsTests.cs`**

En `Icarus/tests/Icarus.IntegrationTests/DocumentosNotaEndpointsTests.cs`, reemplazar (líneas 116-118):

```csharp
        var enviar = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{idPedido}/enviar", tokenTenant));
        Assert.Equal(HttpStatusCode.NoContent, enviar.StatusCode);
```

por:

```csharp
        // Sin despachos de huevo sembrados el saldo es 0 (spec SP9E): confirma
        // directo, este archivo no prueba crédito, solo necesita el pedido
        // despachado para llegar a la recepción.
        var enviar = await cliente.SendAsync(Pedido(
            HttpMethod.Post, $"/api/pedidos-alimento/{idPedido}/enviar", tokenTenant,
            JsonContent.Create(new { confirmarCreditoInsuficiente = true })));
        Assert.Equal(HttpStatusCode.NoContent, enviar.StatusCode);
```

- [ ] **Step 6: Correr ambas suites de integración completas**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~PedidosAlimentoEndpointsTests|FullyQualifiedName~DocumentosNotaEndpointsTests"`
Expected: todos PASS.

- [ ] **Step 7: Commit**

```bash
git add Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs Icarus/tests/Icarus.IntegrationTests/DocumentosNotaEndpointsTests.cs
git commit -m "test(avicola): cubrir el rechazo y la confirmacion de credito insuficiente end-to-end"
```

---

### Task 8: Verificación completa y cierre

**Files:** ninguno (solo ejecución de la puerta de calidad completa).

- [ ] **Step 1: Correr la puerta de calidad completa**

Run (desde la raíz del repo, `C:\Users\lrcahuana\source\repos\Trajano-Icarus`): `./verify.ps1` (o `./verify.sh` si se ejecuta en un shell POSIX).
Expected: todo verde — frontend lint/build/tests, backend build 0 errores, Architecture, Unit, GestorCaisy e Integration tests, y los gates documentales (mojibake, enlaces, etc.).

- [ ] **Step 2: Si algo falla, diagnosticar y arreglar el contenido — nunca relajar el gate**

Si `verify.ps1` falla, leer el motivo exacto, volver a la task correspondiente de este plan, corregir y repetir el Step 1. No usar `--no-verify`, no bajar ninguna baseline de `quality/`.

- [ ] **Step 3: Actualizar `docs/ai/HANDOFF.md`**

Reemplazar el ítem 1 del backlog priorizado (la sección `## Backlog priorizado (crédito de huevo)`, punto `1. **Bloquear vs. solo advertir...**`) marcándolo como resuelto, y mover el `## Pendiente inmediato` para reflejar que el ítem 2 (desglose visible del crédito) es el siguiente a brainstormear. Redactar el resumen del estado actual siguiendo el mismo estilo ya usado en ese archivo (ver secciones existentes como referencia de tono y nivel de detalle).

- [ ] **Step 4: Commit y push**

```bash
git add docs/ai/HANDOFF.md
git commit -m "docs(dominio): cerrar item 1 del backlog de credito de huevo en el handoff"
git push
```
