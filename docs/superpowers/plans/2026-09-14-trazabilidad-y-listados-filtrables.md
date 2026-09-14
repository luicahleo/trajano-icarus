# Trazabilidad de granja y autor, y listados filtrables y paginados — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-09-14-trazabilidad-y-listados-filtrables-design.md`

**Goal:** El pedido de alimento registra granja y autor; los dos agregados ganan un folio legible; y los cuatro listados (dos del tenant, dos de CAISY) filtran y paginan sobre un contrato común reusable.

**Architecture:** GestionAvicola guarda **solo identificadores**, nunca nombres: la prueba de arquitectura le prohíbe depender de Clientes e Identity, y el nombre del trabajador vive en Clientes. La PWA del tenant resuelve el nombre cruzando `CreadoPorTrabajadorId` contra `/clientes/{clienteId}/trabajadores`, que ya consume. CAISY nunca recibe personas: recibe el folio, un correlativo que genera una `SEQUENCE` de SQL Server.

**Tech Stack:** .NET / MediatR / EF Core / SQL Server (backend), ASP.NET MVC (Trajano.GestorCaisy), React + TanStack Query + MUI (PWA), xUnit + NSubstitute + Testcontainers.MsSql, Vitest + Testing Library.

## Global Constraints

- Español correcto con acentos, UTF-8 sin BOM. **Español neutro, sin voseo.**
- TDD real: cada test se corre **en rojo** antes del código que lo pone en verde. Cuando una task solo actualiza cifras o firmas que una task anterior ya cambió, el rojo es imposible: esos casos están marcados abajo como **tests de guarda** y no hay que forzarlos.
- **Prohibido guardar nombres de personas en GestionAvicola**, y prohibido escribirlos en el registro de vuelo. Solo ids.
- **Sí hay migración** en este plan (a diferencia de los dos anteriores del crédito). Es una sola, del módulo GestionAvicola.
- Los datos existentes son de prueba y se descartan: la verificación manual arranca con `./iniciar-pc1.ps1 -RecrearDatos`.
- Ningún commit usa `--no-verify`. Un commit por task.
- `./verify.ps1` completo antes del push final, con Docker corriendo.
- Rama `develop`, commits directos, sin PR.

> **Nota de tamaño para el ejecutor:** son 8 tasks y tocan backend, MVC y PWA. Si el contexto aprieta, cortar limpio al terminar la Task 5 (backend completo y verde), dejar constancia en `docs/ai/HANDOFF.md` y seguir en sesión nueva desde la Task 6. **No** empezar una task sin poder terminarla.

---

## Mapa de archivos

| Archivo | Acción |
|---|---|
| `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/Paginacion.cs` | **Crear**: `PeticionPaginada` y `Pagina<T>` |
| `Icarus/tests/Icarus.UnitTests/BuildingBlocks/PaginacionTests.cs` | **Crear** |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PedidoAlimento.cs` | Modificar: `GranjaId`, `CreadoPorTrabajadorId`, `Numero` |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DespachoHuevo.cs` | Modificar: `CreadoPorTrabajadorId`, `Numero` |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionPedidoAlimento.cs` | Modificar: columnas + secuencia |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDespachoHuevo.cs` | Modificar: ídem |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/` | **Crear**: una migración |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs` | Modificar: filtros, folio, granja, autor |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/PuertosPedidosAlimento.cs` | Modificar: firmas de listado |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/ComandosDespachosHuevo.cs` | Modificar: ídem |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/DespachosHuevo/PuertosDespachosHuevo.cs` | Modificar: ídem |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioPedidosAlimento.cs` | Modificar: filtros en SQL |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioDespachosHuevo.cs` | Modificar: ídem |
| `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs` | Modificar: query string de filtros |
| `Icarus/src/Host/Icarus.Host/Endpoints/DespachosHuevoEndpoints.cs` | Modificar: ídem |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/SemillaDesarrolloAvicola.cs` | Modificar: poblar granja y autor |
| `web/src/lib/paginacion.ts` | **Crear**: tipos compartidos |
| `web/src/components/BarraFiltros.tsx` | **Crear**: reusable |
| `web/src/components/ControlesPaginacion.tsx` | **Crear**: reusable |
| `web/src/features/pedidos-alimento/PedidosAlimentoPage.tsx` | Modificar |
| `web/src/features/despacho-huevo/DespachosHuevoPage.tsx` | Modificar |
| `Icarus/src/Apps/Trajano.GestorCaisy/` | Modificar: filtros nuevos en las dos bandejas |
| `docs/ai/HANDOFF.md` | Modificar: cierre (**local, no versionado**, está en `.gitignore` — no comitearlo) |

---

## Task 1 — El contrato común de paginación

**Archivos:** `Paginacion.cs` (nuevo), `PaginacionTests.cs` (nuevo)

Los límites (página ≥ 1, tamaño 1–100) **se copian de `ListarPedidosCaisyValidator`**, que ya existe y está probado. El objetivo es que el sistema tenga una sola respuesta a «cuántos por página como máximo», no inventar otra.

### Paso 1.1 — Test en rojo

- [ ] Crear `Icarus/tests/Icarus.UnitTests/BuildingBlocks/PaginacionTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Xunit;

namespace Icarus.UnitTests.BuildingBlocks;

// Contrato único de paginación (spec 2026-09-14): normaliza acá, una vez, en
// vez de repetir Math.Max en cada handler como se venía haciendo.
public class PaginacionTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void LaPaginaNuncaEsMenorQueUno(int pedida, int esperada) =>
        Assert.Equal(esperada, new PeticionPaginada(pedida, 20).PaginaNormalizada);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(500, 100)]
    [InlineData(50, 50)]
    public void ElTamanoSeAcotaEntreUnoYCien(int pedido, int esperado) =>
        Assert.Equal(esperado, new PeticionPaginada(1, pedido).TamanoNormalizado);

    [Fact]
    public void ElSaltoDependeDeLaPaginaNormalizadaNoDeLaCruda()
    {
        // Una página 0 no debe producir un salto negativo, que en SQL Server
        // es un error de ejecución, no un cero silencioso.
        Assert.Equal(0, new PeticionPaginada(0, 20).Salto);
        Assert.Equal(40, new PeticionPaginada(3, 20).Salto);
    }

    [Fact]
    public void UnaPaginaVaciaSigueInformandoElTotal()
    {
        var pagina = new Pagina<string>([], 137, 9, 20);
        Assert.Empty(pagina.Items);
        Assert.Equal(137, pagina.Total);
    }
}
```

- [ ] Correr y **ver rojo** (no compila: el tipo no existe).

### Paso 1.2 — El contrato

- [ ] Crear `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/Paginacion.cs`:

```csharp
namespace Icarus.BuildingBlocks.Application;

// Contrato único de paginación (spec 2026-09-14). Los límites son los que ya
// usaba ListarPedidosCaisyValidator: no se inventaron acá, se unificaron.
// La normalización vive en el record y no en cada handler para que una página
// inválida no llegue nunca a SQL Server como un OFFSET negativo.
public sealed record PeticionPaginada(int Pagina = 1, int TamanoPagina = TamanoPorDefecto)
{
    public const int TamanoPorDefecto = 20;
    public const int TamanoMaximo = 100;

    public int PaginaNormalizada => Math.Max(Pagina, 1);

    public int TamanoNormalizado => TamanoPagina switch
    {
        < 1 => TamanoPorDefecto,
        > TamanoMaximo => TamanoMaximo,
        _ => TamanoPagina,
    };

    public int Salto => (PaginaNormalizada - 1) * TamanoNormalizado;
}

// Total es el conteo SIN paginar: la UI lo necesita para saber cuántas
// páginas hay, y es lo único que obliga a una segunda consulta.
public sealed record Pagina<T>(
    IReadOnlyList<T> Items, int Total, int NumeroPagina, int TamanoPagina);
```

> `TamanoPagina` fuera de rango **no lanza**: se acota. Un listado no es un comando; que la UI mande 500 no debe producir un 400, debe producir 100 filas.

- [ ] Correr y ver verde:

```
dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~PaginacionTests"
```

- [ ] Commit: `feat(bb): contrato comun de paginacion para los listados`

---

## Task 2 — Las columnas nuevas y la migración

**Archivos:** dominio, configuraciones EF, migración

### Paso 2.1 — Test en rojo

- [ ] Crear `Icarus/tests/Icarus.IntegrationTests/TrazabilidadRegistrosTests.cs`:

```csharp
// Trazabilidad de granja y autor (spec 2026-09-14). El folio lo genera una
// SEQUENCE de SQL Server, así que solo se puede comprobar contra una base
// real: un test unitario vería siempre cero.
[Collection(IntegracionCollection.Nombre)]
public class TrazabilidadRegistrosTests
{
    // ... mismo patrón de _factory y SembrarAsync que BalanceCreditoHuevoTests

    [Fact]
    public async Task UnPedidoGuardaGranjaAutorYRecibeFolioCorrelativo()
    {
        var clienteId = Guid.NewGuid();
        var granjaId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();

        var primero = new PedidoAlimento(clienteId, granjaId, Guid.NewGuid(), trabajadorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 10)]);
        var segundo = new PedidoAlimento(clienteId, granjaId, Guid.NewGuid(), trabajadorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 5)]);
        await SembrarAsync(primero, segundo);

        Assert.Equal(granjaId, primero.GranjaId);
        Assert.Equal(trabajadorId, primero.CreadoPorTrabajadorId);
        // Correlativo, no necesariamente consecutivo: una SEQUENCE puede dejar
        // huecos si una transacción se revierte. Lo que importa es el orden.
        Assert.True(primero.Numero > 0, $"El primero quedó con folio {primero.Numero}.");
        Assert.True(segundo.Numero > primero.Numero,
            $"El segundo ({segundo.Numero}) debía superar al primero ({primero.Numero}).");
    }

    [Fact]
    public async Task UnPedidoCreadoPorElClienteNoTieneTrabajador()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 10)]);
        await SembrarAsync(pedido);

        Assert.Null(pedido.CreadoPorTrabajadorId);
    }

    [Fact]
    public async Task UnDespachoGuardaAutorYRecibeFolioDeSuPropiaSerie()
    {
        // Series independientes: el primer despacho de la base no hereda el
        // contador de los pedidos.
        // ... crear dos DespachoHuevo con trabajadorId, sembrar
        // Assert: Numero > 0 y el segundo mayor que el primero
    }
}
```

- [ ] Correr y **ver rojo** (no compila: los constructores no tienen esos parámetros).

### Paso 2.2 — El dominio

- [ ] En `PedidoAlimento.cs`, agregar las tres propiedades y ampliar **ambos** constructores públicos (el de id generado y el de id explícito), con `granjaId` y `creadoPorTrabajadorId`:

```csharp
    // Granja de origen (spec 2026-09-14). Obligatoria: el handler la resuelve
    // desde la granja activa del cliente, no la recibe del frontend.
    public Guid GranjaId { get; private set; }

    // Quién lo creó, como ID y nunca como nombre: este módulo tiene prohibido
    // depender de Clientes e Identity (ReglasDeModulosTests). Nulo cuando lo
    // creó el propio Cliente. El nombre lo resuelve la PWA del tenant.
    public Guid? CreadoPorTrabajadorId { get; private set; }

    // Folio legible. Lo asigna una SEQUENCE de SQL Server al insertar: en
    // memoria vale 0 hasta que EF lo trae de vuelta.
    public int Numero { get; private set; }
```

- [ ] En `DespachoHuevo.cs`, agregar `CreadoPorTrabajadorId` y `Numero` con los mismos comentarios. **`GranjaId` y `CreadoPor` ya existen: no tocarlos.**

### Paso 2.3 — Persistencia

- [ ] En `ConfiguracionPedidoAlimento.cs`:

```csharp
        builder.Property(p => p.Numero)
            .HasDefaultValueSql("NEXT VALUE FOR gestion_avicola.secuencia_pedidos_alimento")
            .ValueGeneratedOnAdd();
        builder.HasIndex(p => p.Numero).IsUnique();
        // Índices de los filtros nuevos: sin esto, filtrar por granja sobre una
        // tabla grande es un scan completo, justo lo que este trabajo evita.
        builder.HasIndex(p => new { p.ClienteId, p.GranjaId });
        builder.HasIndex(p => p.CreadoPorTrabajadorId);
```

> `ValueGeneratedOnAdd()` no es opcional: sin él EF manda un 0 explícito y la secuencia nunca corre.

- [ ] En `ConfiguracionDespachoHuevo.cs`, lo mismo con `secuencia_despachos_huevo`.

- [ ] Generar la migración:

```
dotnet ef migrations add TrazabilidadGranjaAutorYFolio --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure --startup-project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure
```

- [ ] **Editar la migración a mano** para crear las dos secuencias *antes* de las columnas. EF no las genera solo:

```csharp
    migrationBuilder.CreateSequence<int>(
        name: "secuencia_pedidos_alimento", schema: "gestion_avicola", startValue: 1);
    migrationBuilder.CreateSequence<int>(
        name: "secuencia_despachos_huevo", schema: "gestion_avicola", startValue: 1);
```

y en `Down`, borrarlas con `DropSequence`.

- [ ] Verificar que el modelo quedó sincronizado:

```
dotnet ef migrations has-pending-model-changes --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure --startup-project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure
```

- [ ] Correr y ver verde:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~TrazabilidadRegistrosTests"
```

- [ ] Commit: `feat(avicola): granja, autor y folio en pedidos de alimento y despachos de huevo`

---

## Task 3 — Los handlers rellenan granja y autor

**Archivos:** `ComandosPedidosAlimento.cs`, `ComandosDespachosHuevo.cs`, tests unitarios

### Paso 3.1 — Tests en rojo

- [ ] En `PedidosAlimentoHandlerTests.cs`, agregar:

```csharp
    [Fact]
    public async Task CrearUnPedidoLoAsociaALaGranjaActivaDelCliente()
    {
        // Arrange: repositorio de granjas devuelve la granja activa
        // Assert: el pedido creado lleva ese GranjaId
    }

    [Fact]
    public async Task CrearUnPedidoComoTrabajadorGuardaSuIdComoAutor()
    {
        _usuarioActual.TrabajadorId.Returns(TrabajadorId);
        // Assert: CreadoPorTrabajadorId == TrabajadorId
    }

    [Fact]
    public async Task CrearUnPedidoComoClienteDejaElAutorEnNulo()
    {
        _usuarioActual.TrabajadorId.Returns((Guid?)null);
        // Assert: CreadoPorTrabajadorId es null
    }

    // Un cliente sin granja activa no puede pedir alimento: el pedido
    // quedaría sin origen y el filtro por granja lo perdería para siempre.
    [Fact]
    public async Task SinGranjaActivaElPedidoNoSeCrea()
    {
        // Assert: lanza la excepción de dominio correspondiente
    }
```

- [ ] Equivalente en los tests del handler de despachos, salvo la granja (el despacho ya la recibe).

- [ ] Correr y **ver rojo**.

### Paso 3.2 — Los handlers

- [ ] En `CrearPedidoAlimentoHandler`, resolver la granja activa y el autor:

```csharp
        var actorId = usuarioActual.UsuarioId ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        // La granja no viaja en el comando: hoy el sistema garantiza una
        // granja activa por cliente. El día que haya varias, el formulario
        // gana un selector y este handler un parámetro, sin tocar el esquema.
        var granjaId = await repositorioGranjas.ObtenerGranjaActivaIdAsync(clienteId, cancellationToken)
            ?? throw new NotFoundException("Granja activa", clienteId);
        var pedido = new PedidoAlimento(
            clienteId, granjaId, actorId, usuarioActual.TrabajadorId, request.Detalles);
```

> `usuarioActual.TrabajadorId` ya viaja en el token: **no** consultar Clientes para obtenerlo. Eso rompería la prueba de arquitectura.

- [ ] En el handler de creación de despacho, pasar `usuarioActual.TrabajadorId` al constructor.

- [ ] Si `IRepositorioGranjas` no expone `ObtenerGranjaActivaIdAsync`, agregarlo al puerto e implementarlo. Revisar primero: puede existir con otro nombre.

- [ ] Correr y ver verde. **Correr también las pruebas de arquitectura ahora, no al final** — es el punto del plan donde más fácil se rompen:

```
dotnet test Icarus/tests/Icarus.ArchitectureTests
dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~PedidosAlimentoHandlerTests"
```

- [ ] Commit: `feat(avicola): los handlers registran granja y autor al crear`

---

## Task 4 — Filtros y paginación en los pedidos

**Archivos:** puertos, comandos, repositorio, endpoints de pedidos

### Paso 4.1 — Tests en rojo

- [ ] En `PedidosAlimentoEndpointsTests.cs`, agregar tests HTTP:

```csharp
    // El listado del tenant no paginaba: devolvía la colección entera. Con
    // datos de un año eso es una respuesta inmanejable.
    [Fact] public async Task ElListadoDelTenantPagina() { /* ?pagina=2&tamanoPagina=1 → 1 item, total 2 */ }
    [Fact] public async Task FiltraPorGranja() { /* ?granjaId=... */ }
    [Fact] public async Task FiltraPorRangoDeFechas() { /* ?desde=&hasta= */ }
    [Fact] public async Task FiltraPorAutor() { /* ?creadoPorTrabajadorId=... */ }
    [Fact] public async Task BuscaPorFolio() { /* ?numero=123 → el pedido exacto */ }

    // CAISY ve el folio y NO ve al autor. Este test es la garantía
    // ejecutable de la decisión de la spec.
    [Fact]
    public async Task ElListadoDeCaisyExponeElFolioYNingunAutor()
    {
        // Assert: el JSON tiene "numero"/"folio" y NO tiene
        // "creadoPorTrabajadorId" ni ningún campo de nombre.
    }
```

- [ ] Correr y **ver rojo**.

### Paso 4.2 — Contrato de filtros

- [ ] En `ComandosPedidosAlimento.cs`:

```csharp
// Filtros del listado (spec 2026-09-14). CreadoPorTrabajadorId NO existe en el
// equivalente de CAISY: CAISY no filtra por personas porque no las ve.
public sealed record FiltrosPedidosTenant(
    Guid? GranjaId, string? Estado, string? Presentacion,
    DateOnly? Desde, DateOnly? Hasta, Guid? CreadoPorTrabajadorId, int? Numero);

public sealed record ListarPedidosAlimentoQuery(
    FiltrosPedidosTenant Filtros, PeticionPaginada Paginacion)
    : IRequest<Pagina<PedidoAlimentoResumen>>;
```

- [ ] Ampliar `PedidoAlimentoResumen` con `int Numero`, `string Folio`, `Guid GranjaId`, `Guid? CreadoPorTrabajadorId`.
- [ ] Ampliar `PedidoCaisyResumen` con `int Numero` y `string Folio`. **Sin autor.**
- [ ] El folio se compone al proyectar, no se persiste:

```csharp
// P-000123. El prefijo es presentación: guardar el texto compuesto duplicaría
// el dato y abriría la puerta a que diverja del número.
private static string FolioDe(int numero) =>
    string.Create(CultureInfo.InvariantCulture, $"P-{numero:D6}");
```

### Paso 4.3 — Repositorio y endpoints

- [ ] En `RepositorioPedidosAlimento`, aplicar los filtros como `Where` encadenados y condicionales, `OrderByDescending` por `Numero`, y devolver `(items, total)` con un `CountAsync` **antes** de `Skip/Take`.
- [ ] En `PedidosAlimentoEndpoints.cs`, leer los filtros del query string con `[AsParameters]` o parámetros sueltos, y mapear a la query.
- [ ] Correr y ver verde.
- [ ] Commit: `feat(avicola): filtros y paginacion en los listados de pedidos de alimento`

---

## Task 5 — Filtros y paginación en los despachos, y la semilla

**Archivos:** puertos/comandos/repositorio de despachos, endpoints, `SemillaDesarrolloAvicola.cs`

- [ ] Repetir la Task 4 sobre despachos de huevo, con `FiltrosDespachosTenant` (sin presentación) y folio `D-`.
- [ ] Actualizar `SemillaDesarrolloAvicola.cs` para que los pedidos sembrados lleven la granja del tenant y que **al menos dos** lleven `CreadoPorTrabajadorId` del trabajador del tenant, dejando el resto en nulo (creados por el Cliente). Sin eso no hay forma de probar el filtro por autor a mano.
- [ ] Ajustar `SemillaDesarrolloAvicolaTests.cs` si alguna aserción se rompe por las firmas nuevas. Los saldos **no deben cambiar**: si cambian, algo se rompió — el folio y el autor no tocan dinero.
- [ ] Agregar un test de guarda:

```csharp
    // Test de guarda (no ve rojo: la Task 2 ya puso el comportamiento). La
    // semilla tiene que ofrecer los dos casos de autoría, o el filtro por
    // autor no se puede probar a mano.
    [Fact]
    public async Task LaSemillaSiembraPedidosDeTrabajadorYDeCliente()
    {
        await SemillaDesarrolloAvicola.SembrarAsync(_servicios, Tenants);
        // Assert: existen pedidos con CreadoPorTrabajadorId != null y == null
    }
```

- [ ] `dotnet test Icarus/tests/Icarus.IntegrationTests` completo y verde.
- [ ] Commit: `feat(avicola): filtros y paginacion en despachos de huevo y semilla con autoria`

> **Punto de corte seguro.** El backend está completo. Si el contexto aprieta, cerrar sesión acá con handoff.

---

## Task 6 — Los componentes reusables de la PWA

**Archivos:** `web/src/lib/paginacion.ts`, `BarraFiltros.tsx`, `ControlesPaginacion.tsx` (los tres nuevos)

- [ ] Tests primero, con Testing Library: que `ControlesPaginacion` deshabilite «anterior» en la página 1 y «siguiente» en la última; que `BarraFiltros` emita el cambio al aplicar y limpie todos los campos al restablecer.
- [ ] Verlos en rojo.
- [ ] Implementar. `BarraFiltros` recibe una **declaración** de filtros (tipo, etiqueta, opciones) y no conoce pedidos ni despachos: es el punto entero del ejercicio, porque el resto de los listados del sistema va a reusarlo.
- [ ] Accesibilidad, en el mismo criterio que ya usa el chip «Negativo» del crédito: los controles llevan `aria-label`, y el estado de página se anuncia como texto («Página 2 de 7»), no solo con iconos.
- [ ] Commit: `feat(web): componentes reusables de filtros y paginacion`

---

## Task 7 — Los dos listados de la PWA

**Archivos:** `PedidosAlimentoPage.tsx`, `DespachosHuevoPage.tsx` y sus tests

- [ ] Tests en rojo: que la página pida `?pagina=1&tamanoPagina=20`; que al elegir una granja la vuelva a pedir con `granjaId`; que muestre el folio en cada fila; y que muestre el **nombre** del autor.
- [ ] Resolver el nombre cruzando contra `/clientes/{clienteId}/trabajadores` con TanStack Query:

```tsx
// El backend manda el id, nunca el nombre: GestionAvicola tiene prohibido
// depender del módulo Clientes. El cruce se hace acá, con la lista que esta
// misma app ya consume en la pantalla de Trabajadores — una consulta
// cacheada, no una por fila.
const nombreAutor = (id: string | null) =>
  id ? (trabajadores?.find((t) => t.id === id)?.nombre ?? 'Autor no disponible') : 'Cliente';
```

> Un id que no aparece en la lista (trabajador dado de baja) muestra «Autor no disponible». **Nunca** un error ni una fila rota.

- [ ] Verificar que el filtro por autor solo se ofrece cuando hay trabajadores que listar.
- [ ] `cd web; npm run lint; npm test; npm run build`
- [ ] Commit: `feat(web): los listados del tenant filtran, paginan y muestran granja y autor`

---

## Task 8 — GestorCaisy y cierre

**Archivos:** `Trajano.GestorCaisy/` (controladores, modelos, vistas, cliente HTTP) y cierre

- [ ] Agregar a las dos bandejas de CAISY los filtros nuevos: granja, rango de fechas y búsqueda por folio. El filtro de estado ya existe.
- [ ] Mostrar el folio como identificador visible de cada fila, en lugar del GUID.
- [ ] **Verificar que ninguna vista de CAISY muestra un autor.** Es la decisión central de la spec y la única que un descuido puede violar en silencio. Agregar un test en `Trajano.GestorCaisy.Tests` que lo afirme.
- [ ] `./verify.ps1` completo, con Docker corriendo.
- [ ] Push a `develop`.
- [ ] Actualizar `docs/ai/HANDOFF.md` (**local, no versionado**; el commit final es el de esta task).

---

## Verificación manual

Arrancar **recreando datos**, porque la migración cambia columnas obligatorias
y los datos viejos son de prueba:

```
./iniciar-pc1.ps1 -RecrearDatos
```

Contraseña de la semilla: `Admin123!` (no `Admin123456!`).

| Qué | Cómo |
|---|---|
| Folio visible | `cliente@`: la lista de pedidos muestra `P-000001`, `P-000002`… |
| Autor resuelto | `cliente@`: los pedidos del trabajador muestran su nombre; los propios, «Cliente» |
| Filtro por granja | `c3@`: elegir la granja y ver que la lista se acota |
| Filtro por fechas | Cualquier tenant: un rango que deje fuera los pedidos viejos |
| Filtro por autor | `cliente@`: filtrar por el trabajador y ver solo los suyos |
| Búsqueda por folio | Pegar `P-000002` y llegar a ese pedido |
| Paginación | Poner tamaño 1 y recorrer páginas; el total no cambia al avanzar |
| CAISY ve el folio | `gpa@`: la bandeja muestra `P-000123` |
| **CAISY no ve personas** | `gpa@`: ninguna pantalla muestra un nombre de trabajador |
| Trabajador | `t3@`: puede filtrar y paginar igual que el Cliente |
