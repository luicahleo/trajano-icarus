# SP5 — Gestión avícola: Granjas y Galpones — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Crear el primer bounded context de negocio avícola (`GestionAvicola`) con los agregados `Granja` y `Galpon`, sus endpoints y tests, siguiendo el patrón del módulo Clientes.

**Architecture:** Tres proyectos nuevos (Domain/Application/Infrastructure) bajo `Icarus/src/GestionAvicola/`, schema `gestion_avicola`, filtros globales de tenant + soft delete, endpoints minimal en el Host protegidos por las políticas de entitlement ya existentes (`Funcionalidad:Granjas`, `Funcionalidad:Galpones`). El módulo NO referencia Clientes ni Identity; la composición vive en el Host.

**Tech Stack:** .NET 10, EF Core 10 (SqlServer), MediatR 12.4.1, FluentValidation 11.10, xUnit + NSubstitute, Testcontainers.MsSql 4.13 (exige Docker corriendo), NetArchTest.

**Spec:** `docs/superpowers/specs/2026-08-17-sp5-gestion-avicola-granjas-galpones-design.md` (leerlo primero; es la fuente de las reglas de negocio).

## Global Constraints

- Idioma: identificadores, mensajes y tests en español correcto; UTF-8 sin BOM; nunca mojibake.
- Anti-PII: errores genéricos; el nombre de la granja nunca va a logs (la lista `NombresProhibidos` del registro de vuelo filtra "Nombre").
- Anti-enumeración: un id de otro tenant se comporta igual que uno inexistente (404 vía `NotFoundException`).
- TDD: cada test se ve en rojo antes de implementar (para tipos nuevos, el rojo es el error de compilación). Nombres de test en español estilo frase.
- `TreatWarningsAsErrors=true` con Roslynator y SonarAnalyzer: el build queda sin warnings.
- `sealed` en todo; `sealed record` para commands/queries/DTOs; `sealed class` para entidades, handlers, validators y repositorios.
- Filtros globales EF **sin `.Value`** sobre el `Guid?` del tenant (trampa documentada en `ClientesDbContext`).
- `IUnitOfWork` genérica YA resuelve al `ClientesDbContext` (registro global en DI). Este módulo usa su propia `IUnidadTrabajoGestionAvicola` (Tarea 4). No cambiar el registro de Clientes.
- `GestionAvicola` no referencia `Icarus.Clientes` ni `Icarus.Identity` (los tests de arquitectura lo fuerzan en la Tarea 8).
- Rutas relativas a la raíz del repo (`Trajano-Icarus/`).
- Los tests de integración exigen Docker corriendo (Testcontainers.MsSql).
- Prohibido `--no-verify`; prohibido relajar baselines o umbrales de la puerta.
- Commits por tarea con el test dirigido en verde; puerta completa (`./verify.ps1`) antes del push final. Convención de mensajes: `feat(avicola): ...`, `test(avicola): ...`, `docs(...)`, en español.

---

### Task 1: Andamiaje de los tres proyectos

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/Icarus.GestionAvicola.Domain.csproj`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Icarus.GestionAvicola.Application.csproj`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Icarus.GestionAvicola.Infrastructure.csproj`
- Modify: `Icarus/Icarus.sln` (vía `dotnet sln add`)
- Modify: `Icarus/src/Host/Icarus.Host/Icarus.Host.csproj` (añadir referencia)
- Modify: `Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj` (añadir 3 referencias)
- Modify: `Icarus/tests/Icarus.ArchitectureTests/Icarus.ArchitectureTests.csproj` (añadir 3 referencias)

**Interfaces:**
- Produces: los tres proyectos vacíos con el grafo de referencias del spec, para que las tareas siguientes solo creen archivos `.cs`.

- [x] **Step 1: Crear los tres csproj**

`Icarus.GestionAvicola.Domain.csproj` (copia el patrón de `Icarus.Clientes.Domain.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\BuildingBlocks\Icarus.BuildingBlocks.Domain\Icarus.BuildingBlocks.Domain.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

`Icarus.GestionAvicola.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\Icarus.GestionAvicola.Domain\Icarus.GestionAvicola.Domain.csproj" />
    <ProjectReference Include="..\..\BuildingBlocks\Icarus.BuildingBlocks.Application\Icarus.BuildingBlocks.Application.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

`Icarus.GestionAvicola.Infrastructure.csproj` (copia el patrón de `Icarus.Clientes.Infrastructure.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Icarus.GestionAvicola.Application\Icarus.GestionAvicola.Application.csproj" />
    <ProjectReference Include="..\..\BuildingBlocks\Icarus.BuildingBlocks.Observability\Icarus.BuildingBlocks.Observability.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

Las versiones de paquetes NO van en los csproj: están centralizadas en `Icarus/Directory.Packages.props` (CPM). Los analizadores (Roslynator, SonarAnalyzer) se aplican solos vía `GlobalPackageReference`.

- [x] **Step 2: Añadir los proyectos a la solución y a los csproj consumidores**

```bash
dotnet sln Icarus/Icarus.sln add \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/Icarus.GestionAvicola.Domain.csproj \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Icarus.GestionAvicola.Application.csproj \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Icarus.GestionAvicola.Infrastructure.csproj
```

En `Icarus/src/Host/Icarus.Host/Icarus.Host.csproj`, añadir dentro del `ItemGroup` de `ProjectReference` (el Host referencia solo los Infrastructure de los módulos):

```xml
<ProjectReference Include="..\..\GestionAvicola\Icarus.GestionAvicola.Infrastructure\Icarus.GestionAvicola.Infrastructure.csproj" />
```

En `Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj`, añadir al `ItemGroup` de referencias:

```xml
<ProjectReference Include="..\..\src\GestionAvicola\Icarus.GestionAvicola.Domain\Icarus.GestionAvicola.Domain.csproj" />
<ProjectReference Include="..\..\src\GestionAvicola\Icarus.GestionAvicola.Application\Icarus.GestionAvicola.Application.csproj" />
<ProjectReference Include="..\..\src\GestionAvicola\Icarus.GestionAvicola.Infrastructure\Icarus.GestionAvicola.Infrastructure.csproj" />
```

En `Icarus/tests/Icarus.ArchitectureTests/Icarus.ArchitectureTests.csproj`, añadir las mismas tres referencias (ajustando la ruta relativa si difiere; copiar el estilo de las referencias a Clientes ya presentes).

- [x] **Step 3: Verificar el build**

Run: `dotnet build Icarus/Icarus.sln --nologo`
Expected: BUILD succeeded, 0 warnings (TreatWarningsAsErrors).

- [x] **Step 4: Commit**

```bash
git add Icarus
git commit -m "chore(avicola): andamiaje de proyectos del modulo GestionAvicola"
```

---

### Task 2: Agregado `Granja` (dominio, TDD)

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/GranjaTests.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/Granja.cs`

**Interfaces:**
- Consumes: `AggregateRoot`, `ReglaNegocioException` de `Icarus.BuildingBlocks.Domain`.
- Produces: `Granja(Guid clienteId, string nombre)`, `Granja(Guid id, Guid clienteId, string nombre)` (semillas/tests), propiedades `ClienteId`, `Nombre`, `EstaActivo`, métodos `Renombrar(string)` y `Desactivar()`.

- [x] **Step 1: Escribir el test que falla**

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class GranjaTests
{
    [Fact]
    public void CtorSinClienteLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new Granja(Guid.Empty, "Granja Norte"));
        Assert.Equal("La granja debe pertenecer a un cliente.", ex.Message);
    }

    [Fact]
    public void CtorNombreVacioLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() => new Granja(Guid.NewGuid(), "  "));
        Assert.Equal("El nombre de la granja es obligatorio.", ex.Message);
    }

    [Fact]
    public void CtorValidoRecortaNombreYNaceActiva()
    {
        var granja = new Granja(Guid.NewGuid(), "  Granja Norte  ");
        Assert.Equal("Granja Norte", granja.Nombre);
        Assert.True(granja.EstaActivo);
    }

    [Fact]
    public void RenombrarVacioLanzaReglaNegocio()
    {
        var granja = new Granja(Guid.NewGuid(), "Granja Norte");
        Assert.Throws<ReglaNegocioException>(() => granja.Renombrar(""));
    }

    [Fact]
    public void RenombrarValidoRecorta()
    {
        var granja = new Granja(Guid.NewGuid(), "Granja Norte");
        granja.Renombrar("  Granja Sur ");
        Assert.Equal("Granja Sur", granja.Nombre);
    }

    [Fact]
    public void DesactivarMarcaInactivaSinBorrar()
    {
        var granja = new Granja(Guid.NewGuid(), "Granja Norte");
        granja.Desactivar();
        Assert.False(granja.EstaActivo);
    }
}
```

- [x] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~GranjaTests"`
Expected: FALLA la compilación (el tipo `Granja` no existe). Ese es el rojo.

- [x] **Step 3: Implementación mínima**

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Agregado raíz (spec SP5). Un cliente tiene a lo sumo una granja activa: la
// regla se fuerza en el handler de creación y con un índice único filtrado en
// BD. EstaActivo es el soft delete transversal del glosario: desactivar nunca
// borra la fila. Sin contadores: ContadorHuevos/TotalGallinas/BajasGallinas del
// legacy (GestorAvicola) eran datos derivados, no estado; se calcularán por
// consulta cuando exista producción (SP6).
public sealed class Granja : AggregateRoot
{
    private Granja()
    {
    }

    public Granja(Guid clienteId, string nombre)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La granja debe pertenecer a un cliente.");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre de la granja es obligatorio.");

        ClienteId = clienteId;
        Nombre = nombre.Trim();
        EstaActivo = true;
    }

    // Para semillas y tests que necesitan ids fijos (el claim clienteId del
    // usuario semilla debe coincidir con el ClienteId sembrado).
    public Granja(Guid id, Guid clienteId, string nombre)
        : this(clienteId, nombre) => Id = id;

    public Guid ClienteId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public bool EstaActivo { get; private set; }

    public void Renombrar(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre de la granja es obligatorio.");
        Nombre = nombre.Trim();
    }

    // Soft delete (glosario): nunca borrado físico. La cascada a galpones la
    // orquesta el handler de Application, no el agregado.
    public void Desactivar() => EstaActivo = false;
}
```

- [x] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~GranjaTests"`
Expected: PASS (6 tests).

- [x] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/Granja.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/GranjaTests.cs
git commit -m "feat(avicola): agregado Granja con invariantes de dominio"
```

---

### Task 3: Agregado `Galpon` (dominio, TDD)

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/GalponTests.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/Galpon.cs`

**Interfaces:**
- Consumes: `AggregateRoot`, `ReglaNegocioException`.
- Produces: `Galpon(Guid granjaId, Guid clienteId, string numero, int capacidadMaxima, int gallinasActuales, DateOnly fechaNacimientoLote, string? descripcion)` (+ sobrecarga con `Guid id` primero para semillas/tests), propiedades `GranjaId`, `ClienteId`, `Numero`, `CapacidadMaxima`, `GallinasActuales`, `FechaNacimientoLote`, `Descripcion`, `EstaActivo`, métodos `ActualizarDatos(string numero, string? descripcion, int capacidadMaxima)`, `AjustarInventarioGallinas(int nuevoTotal)` (total absoluto, NO delta) y `Desactivar()`.

- [x] **Step 1: Escribir el test que falla**

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class GalponTests
{
    private static readonly DateOnly Ayer =
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    private static Galpon GalponValido() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, Ayer, "Norte");

    [Fact]
    public void CtorValidoRecortaYNaceActivo()
    {
        var galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), " A ", 5000, 4800, Ayer, "  ");
        Assert.Equal("A", galpon.Numero);
        Assert.Null(galpon.Descripcion);
        Assert.True(galpon.EstaActivo);
    }

    [Fact]
    public void CtorFechaFuturaLanzaReglaNegocio()
    {
        var manana = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 0, manana, null));
        Assert.Equal("La fecha de nacimiento del lote no puede ser futura.", ex.Message);
    }

    [Fact]
    public void CtorCapacidadCeroLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 0, 0, Ayer, null));
        Assert.Equal("La capacidad máxima debe ser mayor que cero.", ex.Message);
    }

    [Fact]
    public void CtorInventarioNegativoLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, -1, Ayer, null));
        Assert.Equal("Las gallinas actuales no pueden ser negativas.", ex.Message);
    }

    [Fact]
    public void CtorInventarioSuperaCapacidadLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 5001, Ayer, null));
        Assert.Equal("Las gallinas actuales no pueden superar la capacidad máxima.", ex.Message);
    }

    [Fact]
    public void ActualizarDatosCapacidadMenorQueInventarioLanzaReglaNegocio()
    {
        var galpon = GalponValido();
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            galpon.ActualizarDatos("1", null, 4000));
        Assert.Equal("La capacidad máxima no puede ser menor que las gallinas actuales.", ex.Message);
    }

    [Fact]
    public void ActualizarDatosValidoRecorta()
    {
        var galpon = GalponValido();
        galpon.ActualizarDatos(" B ", " Sur ", 6000);
        Assert.Equal("B", galpon.Numero);
        Assert.Equal("Sur", galpon.Descripcion);
        Assert.Equal(6000, galpon.CapacidadMaxima);
    }

    [Fact]
    public void AjustarInventarioSuperaCapacidadLanzaReglaNegocio()
    {
        var galpon = GalponValido();
        Assert.Throws<ReglaNegocioException>(() => galpon.AjustarInventarioGallinas(5001));
    }

    [Fact]
    public void AjustarInventarioValido()
    {
        var galpon = GalponValido();
        galpon.AjustarInventarioGallinas(4500);
        Assert.Equal(4500, galpon.GallinasActuales);
    }

    [Fact]
    public void DesactivarMarcaInactivoSinBorrar()
    {
        var galpon = GalponValido();
        galpon.Desactivar();
        Assert.False(galpon.EstaActivo);
    }
}
```

- [x] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~GalponTests"`
Expected: FALLA la compilación (el tipo `Galpon` no existe).

- [x] **Step 3: Implementación mínima**

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Agregado raíz PROPIO (spec SP5): no es hijo de Granja, porque los registros
// diarios de producción y mortalidad (SP6) lo actualizarán con alta frecuencia
// y por turnos, sin arrastrar a la granja. ClienteId va desnormalizado de la
// granja para que el filtro global de tenant no necesite join. FechaNacimientoLote
// es la fecha en que se pobló el galpón con el lote (glosario); ninguna fecha
// del dominio admite futuro.
public sealed class Galpon : AggregateRoot
{
    private Galpon()
    {
    }

    public Galpon(
        Guid granjaId, Guid clienteId, string numero, int capacidadMaxima, int gallinasActuales,
        DateOnly fechaNacimientoLote, string? descripcion)
    {
        if (granjaId == Guid.Empty)
            throw new ReglaNegocioException("El galpón debe pertenecer a una granja.");
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("El galpón debe pertenecer a un cliente.");
        if (string.IsNullOrWhiteSpace(numero))
            throw new ReglaNegocioException("El número del galpón es obligatorio.");
        if (capacidadMaxima <= 0)
            throw new ReglaNegocioException("La capacidad máxima debe ser mayor que cero.");
        if (fechaNacimientoLote > Hoy())
            throw new ReglaNegocioException("La fecha de nacimiento del lote no puede ser futura.");

        GranjaId = granjaId;
        ClienteId = clienteId;
        Numero = numero.Trim();
        CapacidadMaxima = capacidadMaxima;
        FechaNacimientoLote = fechaNacimientoLote;
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        EstaActivo = true;
        AjustarInventarioGallinas(gallinasActuales);
    }

    // Para semillas y tests que necesitan ids fijos.
    public Galpon(
        Guid id, Guid granjaId, Guid clienteId, string numero, int capacidadMaxima,
        int gallinasActuales, DateOnly fechaNacimientoLote, string? descripcion)
        : this(granjaId, clienteId, numero, capacidadMaxima, gallinasActuales, fechaNacimientoLote, descripcion)
        => Id = id;

    public Guid GranjaId { get; private set; }

    public Guid ClienteId { get; private set; }

    public string Numero { get; private set; } = string.Empty;

    public int CapacidadMaxima { get; private set; }

    public int GallinasActuales { get; private set; }

    public DateOnly FechaNacimientoLote { get; private set; }

    public string? Descripcion { get; private set; }

    public bool EstaActivo { get; private set; }

    public void ActualizarDatos(string numero, string? descripcion, int capacidadMaxima)
    {
        if (string.IsNullOrWhiteSpace(numero))
            throw new ReglaNegocioException("El número del galpón es obligatorio.");
        if (capacidadMaxima <= 0)
            throw new ReglaNegocioException("La capacidad máxima debe ser mayor que cero.");
        if (capacidadMaxima < GallinasActuales)
            throw new ReglaNegocioException(
                "La capacidad máxima no puede ser menor que las gallinas actuales.");

        Numero = numero.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        CapacidadMaxima = capacidadMaxima;
    }

    // Total absoluto, no delta (spec). La invariante 0 <= actuales <= capacidad
    // se fuerza aquí y como check constraint en BD.
    public void AjustarInventarioGallinas(int nuevoTotal)
    {
        if (nuevoTotal < 0)
            throw new ReglaNegocioException("Las gallinas actuales no pueden ser negativas.");
        if (nuevoTotal > CapacidadMaxima)
            throw new ReglaNegocioException(
                "Las gallinas actuales no pueden superar la capacidad máxima.");
        GallinasActuales = nuevoTotal;
    }

    // Soft delete (glosario): nunca borrado físico.
    public void Desactivar() => EstaActivo = false;

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
```

- [x] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~GalponTests"`
Expected: PASS (10 tests).

- [x] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/Galpon.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/GalponTests.cs
git commit -m "feat(avicola): agregado Galpon con invariante de inventario y fecha sin futuro"
```

---

### Task 4: Application de Granjas (handlers, TDD)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/IUnidadTrabajoGestionAvicola.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/IRepositorioGranjas.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/CrearGranjaCommand.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/CrearGranjaHandler.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/CrearGranjaValidator.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/RenombrarGranjaCommand.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/RenombrarGranjaHandler.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/RenombrarGranjaValidator.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/DesactivarGranjaCommand.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/DesactivarGranjaHandler.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/ObtenerGranjaQuery.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/ObtenerGranjaHandler.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/ListarGranjasQuery.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Granjas/ListarGranjasHandler.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/CrearGranjaHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/RenombrarGranjaHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/DesactivarGranjaHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerGranjaHandlerTests.cs`

**Interfaces:**
- Consumes: `Granja` (Task 2); `ICurrentUser`, `IUnitOfWork` de BB.Application; `IOperacionRegistrable`, `DescriptorOperacionRegistroVuelo`, `DatoRegistroVuelo`, `IRegistroVuelo` de BB.Application.Observability; `NotFoundException`, `ConflictException` de BB.Domain.
- Produces (las usa la Tarea 5 y la Infraestructura):
  - `IUnidadTrabajoGestionAvicola : IUnitOfWork` (marcador de este módulo).
  - `IRepositorioGranjas`: `void Agregar(Granja)`, `Task<Granja?> ObtenerPorIdAsync(Guid, CancellationToken)`, `Task<Granja?> ObtenerActivaDelTenantAsync(CancellationToken)`, `Task<IReadOnlyList<GranjaResumen>> ListarDelTenantAsync(CancellationToken)`, `Task<bool> ExisteNombreAsync(Guid clienteId, string nombre, CancellationToken)`.
  - `GranjaResumen(Guid Id, string Nombre)`.
  - Commands: `CrearGranjaCommand(string Nombre) : IRequest<Guid>`, `RenombrarGranjaCommand(Guid GranjaId, string Nombre) : IRequest`, `DesactivarGranjaCommand(Guid GranjaId) : IRequest`; queries: `ObtenerGranjaQuery(Guid GranjaId) : IRequest<GranjaResumen>`, `ListarGranjasQuery : IRequest<IReadOnlyList<GranjaResumen>>`.

- [x] **Step 1: Escribir los tests que fallan**

`CrearGranjaHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class CrearGranjaHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly CrearGranjaHandler _handler;

    public CrearGranjaHandlerTests()
    {
        _usuarioActual.ClienteId.Returns(ClienteId);
        _handler = new CrearGranjaHandler(_granjas, _usuarioActual, _unidadTrabajo);
    }

    [Fact]
    public async Task SinClienteEnElClaimLanzaUnauthorized()
    {
        _usuarioActual.ClienteId.Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(new CrearGranjaCommand("Granja Norte"), CancellationToken.None));
        _granjas.DidNotReceive().Agregar(Arg.Any<Granja>());
    }

    [Fact]
    public async Task GranjaActivaExistenteLanzaConflictGenerico()
    {
        _granjas.ObtenerActivaDelTenantAsync(Arg.Any<CancellationToken>())
            .Returns(new Granja(ClienteId, "Granja Vieja"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(new CrearGranjaCommand("Granja Norte"), CancellationToken.None));

        Assert.Equal("No se pudo registrar la granja.", ex.Message);
        _granjas.DidNotReceive().Agregar(Arg.Any<Granja>());
    }

    [Fact]
    public async Task NombreDuplicadoLanzaConflictGenerico()
    {
        _granjas.ObtenerActivaDelTenantAsync(Arg.Any<CancellationToken>()).Returns((Granja?)null);
        _granjas.ExisteNombreAsync(ClienteId, "Granja Norte", Arg.Any<CancellationToken>())
            .Returns(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(new CrearGranjaCommand("  Granja Norte "), CancellationToken.None));

        Assert.Equal("No se pudo registrar la granja.", ex.Message);
        _granjas.DidNotReceive().Agregar(Arg.Any<Granja>());
    }

    [Fact]
    public async Task DatosValidosCreanYGuardan()
    {
        _granjas.ObtenerActivaDelTenantAsync(Arg.Any<CancellationToken>()).Returns((Granja?)null);
        _granjas.ExisteNombreAsync(ClienteId, "Granja Norte", Arg.Any<CancellationToken>())
            .Returns(false);

        var id = await _handler.Handle(new CrearGranjaCommand(" Granja Norte "), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _granjas.Received(1).Agregar(Arg.Is<Granja>(g =>
            g.ClienteId == ClienteId && g.Nombre == "Granja Norte" && g.EstaActivo));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

`RenombrarGranjaHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class RenombrarGranjaHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly RenombrarGranjaHandler _handler;

    public RenombrarGranjaHandlerTests() =>
        _handler = new RenombrarGranjaHandler(_granjas, _unidadTrabajo);

    [Fact]
    public async Task GranjaInexistenteLanzaNotFound()
    {
        _granjas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Granja?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new RenombrarGranjaCommand(Guid.NewGuid(), "Nuevo"), CancellationToken.None));

        Assert.Equal("Granja no encontrado.", ex.Message);
    }

    [Fact]
    public async Task MismoNombreNoConsultaUnicidad()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);

        await _handler.Handle(
            new RenombrarGranjaCommand(granja.Id, " Granja Norte "), CancellationToken.None);

        await _granjas.DidNotReceive().ExisteNombreAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NombreDuplicadoLanzaConflictGenerico()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        _granjas.ExisteNombreAsync(ClienteId, "Granja Sur", Arg.Any<CancellationToken>())
            .Returns(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(new RenombrarGranjaCommand(granja.Id, "Granja Sur"), CancellationToken.None));

        Assert.Equal("No se pudo renombrar la granja.", ex.Message);
        Assert.Equal("Granja Norte", granja.Nombre);
    }

    [Fact]
    public async Task NombreNuevoRenombraYGuarda()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        _granjas.ExisteNombreAsync(ClienteId, "Granja Sur", Arg.Any<CancellationToken>())
            .Returns(false);

        await _handler.Handle(
            new RenombrarGranjaCommand(granja.Id, " Granja Sur "), CancellationToken.None);

        Assert.Equal("Granja Sur", granja.Nombre);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

`DesactivarGranjaHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class DesactivarGranjaHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRegistroVuelo _registroVuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly DesactivarGranjaHandler _handler;

    public DesactivarGranjaHandlerTests() =>
        _handler = new DesactivarGranjaHandler(_granjas, _galpones, _registroVuelo, _unidadTrabajo);

    [Fact]
    public async Task GranjaInexistenteLanzaNotFound()
    {
        _granjas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Granja?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new DesactivarGranjaCommand(Guid.NewGuid()), CancellationToken.None));
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesactivaGalponesActivosYNarraLaCascada()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        var galpones = new List<Galpon>
        {
            new(granja.Id, ClienteId, "1", 5000, 100, DateOnly.FromDateTime(DateTime.UtcNow), null),
            new(granja.Id, ClienteId, "2", 5000, 200, DateOnly.FromDateTime(DateTime.UtcNow), null),
        };
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        _galpones.ListarActivosDeGranjaAsync(granja.Id, Arg.Any<CancellationToken>())
            .Returns(galpones);

        await _handler.Handle(new DesactivarGranjaCommand(granja.Id), CancellationToken.None);

        Assert.False(granja.EstaActivo);
        Assert.All(galpones, g => Assert.False(g.EstaActivo));
        _registroVuelo.Received(1).Decidir(
            "avicola.granjas.desactivar", "cascada_galpones", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(d => (int)d["GalponesDesactivados"] == 2));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SinGalponesNoNarraCascada()
    {
        var granja = new Granja(ClienteId, "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);
        _galpones.ListarActivosDeGranjaAsync(granja.Id, Arg.Any<CancellationToken>())
            .Returns(new List<Galpon>());

        await _handler.Handle(new DesactivarGranjaCommand(granja.Id), CancellationToken.None);

        Assert.False(granja.EstaActivo);
        _registroVuelo.DidNotReceive().Decidir(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>?>());
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

`ObtenerGranjaHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ObtenerGranjaHandlerTests
{
    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly ObtenerGranjaHandler _handler;

    public ObtenerGranjaHandlerTests() => _handler = new ObtenerGranjaHandler(_granjas);

    [Fact]
    public async Task GranjaInexistenteLanzaNotFound()
    {
        _granjas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Granja?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new ObtenerGranjaQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GranjaExistenteDevuelveResumen()
    {
        var granja = new Granja(Guid.NewGuid(), "Granja Norte");
        _granjas.ObtenerPorIdAsync(granja.Id, Arg.Any<CancellationToken>()).Returns(granja);

        var resumen = await _handler.Handle(new ObtenerGranjaQuery(granja.Id), CancellationToken.None);

        Assert.Equal(granja.Id, resumen.Id);
        Assert.Equal("Granja Norte", resumen.Nombre);
    }
}
```

- [x] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~Granja"`
Expected: FALLA la compilación (no existen los tipos de Application). `DesactivarGranjaHandlerTests` además referencia `IRepositorioGalpones`, que se crea en la Tarea 5: para ver el rojo de esta tarea, crear ya el archivo `Galpones/IRepositorioGalpones.cs` con la interfaz completa de la Tarea 5 (su contenido exacto está ahí); la Tarea 5 lo dará por existente.

- [x] **Step 3: Implementación mínima**

`IUnidadTrabajoGestionAvicola.cs`:

```csharp
using Icarus.BuildingBlocks.Application;

namespace Icarus.GestionAvicola.Application;

// IUnitOfWork genérica ya resuelve al contexto de Clientes (registro global en
// el Host). Esta interfaz marca la unidad de trabajo de ESTE módulo: la
// implementa GestionAvicolaDbContext y se registra aparte en DI.
public interface IUnidadTrabajoGestionAvicola : IUnitOfWork
{
}
```

`Granjas/IRepositorioGranjas.cs`:

```csharp
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Granjas;

public interface IRepositorioGranjas
{
    void Agregar(Granja granja);

    // Respeta los filtros globales (tenant + activos): una granja de otro
    // tenant o inactiva devuelve null, igual que una inexistente
    // (anti-enumeración).
    Task<Granja?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Respeta los filtros globales: la granja activa del tenant actual, si
    // existe. Un cliente tiene a lo sumo una granja activa (spec SP5).
    Task<Granja?> ObtenerActivaDelTenantAsync(CancellationToken cancellationToken = default);

    // Respeta los filtros globales (tenant + activos).
    Task<IReadOnlyList<GranjaResumen>> ListarDelTenantAsync(CancellationToken cancellationToken = default);

    // Ignora los filtros globales y acota por clienteId explícito: la unicidad
    // del nombre es por cliente, también contra granjas inactivas (el soft
    // delete no libera el nombre).
    Task<bool> ExisteNombreAsync(
        Guid clienteId, string nombre, CancellationToken cancellationToken = default);
}

public sealed record GranjaResumen(Guid Id, string Nombre);
```

`Granjas/CrearGranjaCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Granjas;

// Registro de vuelo (spec SP5): el nombre de la granja es dato del negocio del
// tenant y la lista NombresProhibidos filtra "Nombre"; el descriptor queda sin
// campos.
public sealed record CrearGranjaCommand(string Nombre) : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.granjas.crear", new Dictionary<string, DatoRegistroVuelo>());
}
```

`Granjas/CrearGranjaHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class CrearGranjaHandler : IRequestHandler<CrearGranjaCommand, Guid>
{
    private readonly IRepositorioGranjas _granjas;
    private readonly ICurrentUser _usuarioActual;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public CrearGranjaHandler(
        IRepositorioGranjas granjas, ICurrentUser usuarioActual,
        IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _granjas = granjas;
        _usuarioActual = usuarioActual;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task<Guid> Handle(CrearGranjaCommand request, CancellationToken cancellationToken)
    {
        // El tenant sale del claim, nunca del body. Las cuentas de plataforma
        // (ClienteId nulo) no registran granjas.
        var clienteId = _usuarioActual.ClienteId
            ?? throw new UnauthorizedAccessException("Solo una cuenta de cliente puede registrar granjas.");

        // Normaliza igual que el ctor del agregado, para que la unicidad se
        // compare contra el valor que realmente se persiste.
        var nombre = request.Nombre.Trim();

        // Anti-PII: conflicto genérico, sin revelar el dato duplicado. Un
        // cliente tiene a lo sumo una granja activa (spec SP5).
        if (await _granjas.ObtenerActivaDelTenantAsync(cancellationToken) is not null)
            throw new ConflictException("No se pudo registrar la granja.");
        if (await _granjas.ExisteNombreAsync(clienteId, nombre, cancellationToken))
            throw new ConflictException("No se pudo registrar la granja.");

        var granja = new Granja(clienteId, nombre);
        _granjas.Agregar(granja);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
        return granja.Id;
    }
}
```

`Granjas/CrearGranjaValidator.cs`:

```csharp
using FluentValidation;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class CrearGranjaValidator : AbstractValidator<CrearGranjaCommand>
{
    public CrearGranjaValidator() => RuleFor(c => c.Nombre).NotEmpty().MaximumLength(200);
}
```

`Granjas/RenombrarGranjaCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed record RenombrarGranjaCommand(Guid GranjaId, string Nombre)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.granjas.renombrar", new Dictionary<string, DatoRegistroVuelo>());
}
```

`Granjas/RenombrarGranjaHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class RenombrarGranjaHandler : IRequestHandler<RenombrarGranjaCommand>
{
    private readonly IRepositorioGranjas _granjas;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public RenombrarGranjaHandler(
        IRepositorioGranjas granjas, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _granjas = granjas;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(RenombrarGranjaCommand request, CancellationToken cancellationToken)
    {
        // Respeta filtros globales: id de otro tenant = inexistente (404).
        var granja = await _granjas.ObtenerPorIdAsync(request.GranjaId, cancellationToken)
            ?? throw new NotFoundException("Granja", request.GranjaId);

        var nombre = request.Nombre.Trim();
        if (!string.Equals(granja.Nombre, nombre, StringComparison.Ordinal)
            && await _granjas.ExisteNombreAsync(granja.ClienteId, nombre, cancellationToken))
            throw new ConflictException("No se pudo renombrar la granja.");

        granja.Renombrar(nombre);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

`Granjas/RenombrarGranjaValidator.cs`:

```csharp
using FluentValidation;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class RenombrarGranjaValidator : AbstractValidator<RenombrarGranjaCommand>
{
    public RenombrarGranjaValidator() => RuleFor(c => c.Nombre).NotEmpty().MaximumLength(200);
}
```

`Granjas/DesactivarGranjaCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed record DesactivarGranjaCommand(Guid GranjaId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.granjas.desactivar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["GalponesDesactivados"] = DatoRegistroVuelo.Entero,
        });
}
```

`Granjas/DesactivarGranjaHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class DesactivarGranjaHandler : IRequestHandler<DesactivarGranjaCommand>
{
    private readonly IRepositorioGranjas _granjas;
    private readonly IRepositorioGalpones _galpones;
    private readonly IRegistroVuelo _registroVuelo;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public DesactivarGranjaHandler(
        IRepositorioGranjas granjas, IRepositorioGalpones galpones,
        IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _granjas = granjas;
        _galpones = galpones;
        _registroVuelo = registroVuelo;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(DesactivarGranjaCommand request, CancellationToken cancellationToken)
    {
        var granja = await _granjas.ObtenerPorIdAsync(request.GranjaId, cancellationToken)
            ?? throw new NotFoundException("Granja", request.GranjaId);

        // Cascada (spec SP5): una granja inactiva no admite galpones activos.
        // Los filtros globales ya acotan al tenant y a los activos.
        var galpones = await _galpones.ListarActivosDeGranjaAsync(granja.Id, cancellationToken);
        foreach (var galpon in galpones)
            galpon.Desactivar();

        if (galpones.Count > 0)
        {
            _registroVuelo.Decidir(
                "avicola.granjas.desactivar", "cascada_galpones", "aplicada",
                new Dictionary<string, object?> { ["GalponesDesactivados"] = galpones.Count });
        }

        granja.Desactivar();
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

`Granjas/ObtenerGranjaQuery.cs`:

```csharp
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed record ObtenerGranjaQuery(Guid GranjaId) : IRequest<GranjaResumen>;
```

`Granjas/ObtenerGranjaHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class ObtenerGranjaHandler : IRequestHandler<ObtenerGranjaQuery, GranjaResumen>
{
    private readonly IRepositorioGranjas _granjas;

    public ObtenerGranjaHandler(IRepositorioGranjas granjas) => _granjas = granjas;

    public async Task<GranjaResumen> Handle(
        ObtenerGranjaQuery request, CancellationToken cancellationToken)
    {
        var granja = await _granjas.ObtenerPorIdAsync(request.GranjaId, cancellationToken)
            ?? throw new NotFoundException("Granja", request.GranjaId);
        return new GranjaResumen(granja.Id, granja.Nombre);
    }
}
```

`Granjas/ListarGranjasQuery.cs`:

```csharp
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed record ListarGranjasQuery : IRequest<IReadOnlyList<GranjaResumen>>;
```

`Granjas/ListarGranjasHandler.cs`:

```csharp
using MediatR;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class ListarGranjasHandler
    : IRequestHandler<ListarGranjasQuery, IReadOnlyList<GranjaResumen>>
{
    private readonly IRepositorioGranjas _granjas;

    public ListarGranjasHandler(IRepositorioGranjas granjas) => _granjas = granjas;

    public Task<IReadOnlyList<GranjaResumen>> Handle(
        ListarGranjasQuery request, CancellationToken cancellationToken) =>
        _granjas.ListarDelTenantAsync(cancellationToken);
}
```

- [x] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~Granja"`
Expected: PASS. Ojo: los tests de esta tarea compilan solo cuando existe `IRepositorioGalpones` (Tarea 5, Step 1 ya lo creó si se siguió la nota del Step 2).

- [x] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application Icarus/tests/Icarus.UnitTests/GestionAvicola
git commit -m "feat(avicola): handlers de granjas con una granja activa por cliente y cascada"
```

---

### Task 5: Application de Galpones (handlers, TDD)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Galpones/IRepositorioGalpones.cs` (si no se creó ya en la Tarea 4)
- Create: `.../Galpones/CrearGalponCommand.cs`, `CrearGalponHandler.cs`, `CrearGalponValidator.cs`
- Create: `.../Galpones/ActualizarGalponCommand.cs`, `ActualizarGalponHandler.cs`, `ActualizarGalponValidator.cs`
- Create: `.../Galpones/AjustarInventarioGalponCommand.cs`, `AjustarInventarioGalponHandler.cs`
- Create: `.../Galpones/DesactivarGalponCommand.cs`, `DesactivarGalponHandler.cs`
- Create: `.../Galpones/ObtenerGalponQuery.cs`, `ObtenerGalponHandler.cs`
- Create: `.../Galpones/ListarGalponesPorGranjaQuery.cs`, `ListarGalponesPorGranjaHandler.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/CrearGalponHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ActualizarGalponHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/AjustarInventarioGalponHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/DesactivarGalponHandlerTests.cs`

**Interfaces:**
- Consumes: `Galpon` (Task 3), `Granja`, `IRepositorioGranjas` (Task 4), `IUnidadTrabajoGestionAvicola` (Task 4).
- Produces:
  - `IRepositorioGalpones`: `void Agregar(Galpon)`, `Task<Galpon?> ObtenerPorIdAsync(Guid, CancellationToken)`, `Task<IReadOnlyList<GalponResumen>> ListarPorGranjaAsync(Guid granjaId, CancellationToken)`, `Task<IReadOnlyList<Galpon>> ListarActivosDeGranjaAsync(Guid granjaId, CancellationToken)`, `Task<bool> ExisteNumeroAsync(Guid granjaId, string numero, CancellationToken)`.
  - `GalponResumen(Guid Id, string Numero, int CapacidadMaxima, int GallinasActuales, DateOnly FechaNacimientoLote, string? Descripcion)`.
  - Commands: `CrearGalponCommand(Guid GranjaId, string Numero, int CapacidadMaxima, int GallinasActuales, DateOnly FechaNacimientoLote, string? Descripcion) : IRequest<Guid>`, `ActualizarGalponCommand(Guid GalponId, string Numero, string? Descripcion, int CapacidadMaxima) : IRequest`, `AjustarInventarioGalponCommand(Guid GalponId, int GallinasActuales) : IRequest`, `DesactivarGalponCommand(Guid GalponId) : IRequest`; queries: `ObtenerGalponQuery(Guid GalponId) : IRequest<GalponResumen>`, `ListarGalponesPorGranjaQuery(Guid GranjaId) : IRequest<IReadOnlyList<GalponResumen>>`.

- [x] **Step 1: Crear la interfaz de repositorio**

`Galpones/IRepositorioGalpones.cs`:

```csharp
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Galpones;

public interface IRepositorioGalpones
{
    void Agregar(Galpon galpon);

    // Respeta los filtros globales (tenant + activos): un id ajeno o inactivo
    // devuelve null, igual que uno inexistente (anti-enumeración).
    Task<Galpon?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Respeta los filtros globales (tenant + activos).
    Task<IReadOnlyList<GalponResumen>> ListarPorGranjaAsync(
        Guid granjaId, CancellationToken cancellationToken = default);

    // Respeta los filtros globales (tenant + activos), con tracking: la usa la
    // cascada al desactivar la granja.
    Task<IReadOnlyList<Galpon>> ListarActivosDeGranjaAsync(
        Guid granjaId, CancellationToken cancellationToken = default);

    // Ignora los filtros globales y acota por granjaId explícito: el número es
    // único por granja también contra galpones inactivos (el soft delete no
    // libera el número).
    Task<bool> ExisteNumeroAsync(
        Guid granjaId, string numero, CancellationToken cancellationToken = default);
}

public sealed record GalponResumen(
    Guid Id, string Numero, int CapacidadMaxima, int GallinasActuales,
    DateOnly FechaNacimientoLote, string? Descripcion);
```

- [x] **Step 2: Escribir los tests que fallan**

`CrearGalponHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class CrearGalponHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly DateOnly Ayer =
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    private readonly IRepositorioGranjas _granjas = Substitute.For<IRepositorioGranjas>();
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly CrearGalponHandler _handler;
    private readonly Granja _granja = new(ClienteId, "Granja Norte");

    public CrearGalponHandlerTests() =>
        _handler = new CrearGalponHandler(_granjas, _galpones, _unidadTrabajo);

    private CrearGalponCommand ComandoValido() =>
        new(_granja.Id, "1", 5000, 4800, Ayer, "Norte");

    [Fact]
    public async Task GranjaInexistenteOAjenaLanzaNotFound()
    {
        _granjas.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Granja?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(ComandoValido(), CancellationToken.None));

        Assert.Equal("Granja no encontrado.", ex.Message);
        _galpones.DidNotReceive().Agregar(Arg.Any<Galpon>());
    }

    [Fact]
    public async Task NumeroDuplicadoLanzaConflictGenerico()
    {
        _granjas.ObtenerPorIdAsync(_granja.Id, Arg.Any<CancellationToken>()).Returns(_granja);
        _galpones.ExisteNumeroAsync(_granja.Id, "1", Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(ComandoValido(), CancellationToken.None));

        Assert.Equal("No se pudo registrar el galpón.", ex.Message);
        _galpones.DidNotReceive().Agregar(Arg.Any<Galpon>());
    }

    [Fact]
    public async Task DatosValidosCreanConElTenantDeLaGranjaYGuardan()
    {
        _granjas.ObtenerPorIdAsync(_granja.Id, Arg.Any<CancellationToken>()).Returns(_granja);
        _galpones.ExisteNumeroAsync(_granja.Id, "1", Arg.Any<CancellationToken>()).Returns(false);

        var id = await _handler.Handle(ComandoValido(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _galpones.Received(1).Agregar(Arg.Is<Galpon>(g =>
            g.GranjaId == _granja.Id && g.ClienteId == ClienteId
            && g.Numero == "1" && g.CapacidadMaxima == 5000 && g.GallinasActuales == 4800));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

`ActualizarGalponHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ActualizarGalponHandlerTests
{
    private static readonly DateOnly Ayer =
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly ActualizarGalponHandler _handler;
    private readonly Galpon _galpon =
        new(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, Ayer, null);

    public ActualizarGalponHandlerTests() =>
        _handler = new ActualizarGalponHandler(_galpones, _unidadTrabajo);

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Galpon?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(
                new ActualizarGalponCommand(Guid.NewGuid(), "2", null, 6000), CancellationToken.None));

        Assert.Equal("Galpon no encontrado.", ex.Message);
    }

    [Fact]
    public async Task NumeroDuplicadoLanzaConflictGenerico()
    {
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);
        _galpones.ExisteNumeroAsync(_galpon.GranjaId, "2", Arg.Any<CancellationToken>())
            .Returns(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(
                new ActualizarGalponCommand(_galpon.Id, "2", null, 6000), CancellationToken.None));

        Assert.Equal("No se pudo actualizar el galpón.", ex.Message);
        Assert.Equal("1", _galpon.Numero);
    }

    [Fact]
    public async Task DatosValidosActualizanYGuardan()
    {
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);
        _galpones.ExisteNumeroAsync(_galpon.GranjaId, "2", Arg.Any<CancellationToken>())
            .Returns(false);

        await _handler.Handle(
            new ActualizarGalponCommand(_galpon.Id, " 2 ", "Sur", 6000), CancellationToken.None);

        Assert.Equal("2", _galpon.Numero);
        Assert.Equal("Sur", _galpon.Descripcion);
        Assert.Equal(6000, _galpon.CapacidadMaxima);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

`AjustarInventarioGalponHandlerTests.cs` y `DesactivarGalponHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class AjustarInventarioGalponHandlerTests
{
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly AjustarInventarioGalponHandler _handler;

    public AjustarInventarioGalponHandlerTests() =>
        _handler = new AjustarInventarioGalponHandler(_galpones, _unidadTrabajo);

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Galpon?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(
                new AjustarInventarioGalponCommand(Guid.NewGuid(), 100), CancellationToken.None));
    }

    [Fact]
    public async Task AjusteValidoGuarda()
    {
        var galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), null);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);

        await _handler.Handle(
            new AjustarInventarioGalponCommand(galpon.Id, 4500), CancellationToken.None);

        Assert.Equal(4500, galpon.GallinasActuales);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public class DesactivarGalponHandlerTests
{
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly DesactivarGalponHandler _handler;

    public DesactivarGalponHandlerTests() =>
        _handler = new DesactivarGalponHandler(_galpones, _unidadTrabajo);

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Galpon?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new DesactivarGalponCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task DesactivaYGuarda()
    {
        var galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), null);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);

        await _handler.Handle(new DesactivarGalponCommand(galpon.Id), CancellationToken.None);

        Assert.False(galpon.EstaActivo);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

- [x] **Step 3: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~Galpon"`
Expected: FALLA la compilación (no existen los tipos de Application de galpones).

- [x] **Step 4: Implementación mínima**

`Galpones/CrearGalponCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Galpones;

// Registro de vuelo (spec SP5): numero, capacidad e inventario no son PII.
public sealed record CrearGalponCommand(
    Guid GranjaId, string Numero, int CapacidadMaxima, int GallinasActuales,
    DateOnly FechaNacimientoLote, string? Descripcion) : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.galpones.crear",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["Numero"] = DatoRegistroVuelo.Texto,
            ["CapacidadMaxima"] = DatoRegistroVuelo.Entero,
            ["GallinasActuales"] = DatoRegistroVuelo.Entero,
        });
}
```

`Galpones/CrearGalponHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed class CrearGalponHandler : IRequestHandler<CrearGalponCommand, Guid>
{
    private readonly IRepositorioGranjas _granjas;
    private readonly IRepositorioGalpones _galpones;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public CrearGalponHandler(
        IRepositorioGranjas granjas, IRepositorioGalpones galpones,
        IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _granjas = granjas;
        _galpones = galpones;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task<Guid> Handle(CrearGalponCommand request, CancellationToken cancellationToken)
    {
        // La granja debe existir, estar activa y ser del tenant actual; el
        // filtro global lo garantiza y un id ajeno da 404 (anti-enumeración).
        var granja = await _granjas.ObtenerPorIdAsync(request.GranjaId, cancellationToken)
            ?? throw new NotFoundException("Granja", request.GranjaId);

        var numero = request.Numero.Trim();
        if (await _galpones.ExisteNumeroAsync(granja.Id, numero, cancellationToken))
            throw new ConflictException("No se pudo registrar el galpón.");

        // ClienteId desnormalizado de la granja (spec SP5): el filtro de
        // tenant del galpón no necesita join.
        var galpon = new Galpon(
            granja.Id, granja.ClienteId, numero, request.CapacidadMaxima,
            request.GallinasActuales, request.FechaNacimientoLote, request.Descripcion);
        _galpones.Agregar(galpon);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
        return galpon.Id;
    }
}
```

`Galpones/CrearGalponValidator.cs`:

```csharp
using FluentValidation;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed class CrearGalponValidator : AbstractValidator<CrearGalponCommand>
{
    public CrearGalponValidator()
    {
        RuleFor(c => c.Numero).NotEmpty().MaximumLength(10);
        RuleFor(c => c.CapacidadMaxima).GreaterThan(0);
        RuleFor(c => c.GallinasActuales).GreaterThanOrEqualTo(0);
        RuleFor(c => c.FechaNacimientoLote)
            .Must(f => f <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("La fecha de nacimiento del lote no puede ser futura.");
        RuleFor(c => c.Descripcion).MaximumLength(500);
    }
}
```

`Galpones/ActualizarGalponCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed record ActualizarGalponCommand(
    Guid GalponId, string Numero, string? Descripcion, int CapacidadMaxima)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.galpones.actualizar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["Numero"] = DatoRegistroVuelo.Texto,
            ["CapacidadMaxima"] = DatoRegistroVuelo.Entero,
        });
}
```

`Galpones/ActualizarGalponHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed class ActualizarGalponHandler : IRequestHandler<ActualizarGalponCommand>
{
    private readonly IRepositorioGalpones _galpones;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public ActualizarGalponHandler(
        IRepositorioGalpones galpones, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _galpones = galpones;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(ActualizarGalponCommand request, CancellationToken cancellationToken)
    {
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);

        var numero = request.Numero.Trim();
        if (!string.Equals(galpon.Numero, numero, StringComparison.Ordinal)
            && await _galpones.ExisteNumeroAsync(galpon.GranjaId, numero, cancellationToken))
            throw new ConflictException("No se pudo actualizar el galpón.");

        galpon.ActualizarDatos(numero, request.Descripcion, request.CapacidadMaxima);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

`Galpones/ActualizarGalponValidator.cs`:

```csharp
using FluentValidation;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed class ActualizarGalponValidator : AbstractValidator<ActualizarGalponCommand>
{
    public ActualizarGalponValidator()
    {
        RuleFor(c => c.Numero).NotEmpty().MaximumLength(10);
        RuleFor(c => c.CapacidadMaxima).GreaterThan(0);
        RuleFor(c => c.Descripcion).MaximumLength(500);
    }
}
```

`Galpones/AjustarInventarioGalponCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed record AjustarInventarioGalponCommand(Guid GalponId, int GallinasActuales)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.galpones.ajustar-inventario",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["GallinasActuales"] = DatoRegistroVuelo.Entero,
        });
}
```

`Galpones/AjustarInventarioGalponHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed class AjustarInventarioGalponHandler : IRequestHandler<AjustarInventarioGalponCommand>
{
    private readonly IRepositorioGalpones _galpones;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public AjustarInventarioGalponHandler(
        IRepositorioGalpones galpones, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _galpones = galpones;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(
        AjustarInventarioGalponCommand request, CancellationToken cancellationToken)
    {
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);

        galpon.AjustarInventarioGallinas(request.GallinasActuales);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

`Galpones/DesactivarGalponCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed record DesactivarGalponCommand(Guid GalponId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.galpones.desactivar", new Dictionary<string, DatoRegistroVuelo>());
}
```

`Galpones/DesactivarGalponHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed class DesactivarGalponHandler : IRequestHandler<DesactivarGalponCommand>
{
    private readonly IRepositorioGalpones _galpones;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public DesactivarGalponHandler(
        IRepositorioGalpones galpones, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _galpones = galpones;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(DesactivarGalponCommand request, CancellationToken cancellationToken)
    {
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);

        galpon.Desactivar();
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

`Galpones/ObtenerGalponQuery.cs` y `ObtenerGalponHandler.cs`:

```csharp
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed record ObtenerGalponQuery(Guid GalponId) : IRequest<GalponResumen>;
```

```csharp
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed class ObtenerGalponHandler : IRequestHandler<ObtenerGalponQuery, GalponResumen>
{
    private readonly IRepositorioGalpones _galpones;

    public ObtenerGalponHandler(IRepositorioGalpones galpones) => _galpones = galpones;

    public async Task<GalponResumen> Handle(
        ObtenerGalponQuery request, CancellationToken cancellationToken)
    {
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);
        return new GalponResumen(
            galpon.Id, galpon.Numero, galpon.CapacidadMaxima, galpon.GallinasActuales,
            galpon.FechaNacimientoLote, galpon.Descripcion);
    }
}
```

`Galpones/ListarGalponesPorGranjaQuery.cs` y `ListarGalponesPorGranjaHandler.cs`:

```csharp
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed record ListarGalponesPorGranjaQuery(Guid GranjaId)
    : IRequest<IReadOnlyList<GalponResumen>>;
```

```csharp
using MediatR;

namespace Icarus.GestionAvicola.Application.Galpones;

public sealed class ListarGalponesPorGranjaHandler
    : IRequestHandler<ListarGalponesPorGranjaQuery, IReadOnlyList<GalponResumen>>
{
    private readonly IRepositorioGalpones _galpones;

    public ListarGalponesPorGranjaHandler(IRepositorioGalpones galpones) => _galpones = galpones;

    public Task<IReadOnlyList<GalponResumen>> Handle(
        ListarGalponesPorGranjaQuery request, CancellationToken cancellationToken) =>
        _galpones.ListarPorGranjaAsync(request.GranjaId, cancellationToken);
}
```

- [x] **Step 5: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~GestionAvicola"`
Expected: PASS (toda la carpeta GestionAvicola).

- [x] **Step 6: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application Icarus/tests/Icarus.UnitTests/GestionAvicola
git commit -m "feat(avicola): handlers de galpones con unicidad de numero y tenant desnormalizado"
```

---

### Task 6: Infraestructura (DbContext, repositorios, DI, migración)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs`
- Create: `.../Persistencia/ConfiguracionGranja.cs`
- Create: `.../Persistencia/ConfiguracionGalpon.cs`
- Create: `.../Repositorios/RepositorioGranjas.cs`
- Create: `.../Repositorios/RepositorioGalpones.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DesignTimeGestionAvicolaDbContextFactory.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/` (generada por `dotnet ef`)

**Interfaces:**
- Consumes: todo lo de las Tasks 4-5; `SaveChangesRegistroVueloInterceptor`, `TransaccionesRegistroVueloInterceptor`, `DescriptorContextoPersistencia` de BB.Observability.
- Produces: `GestionAvicolaDbContext` (implementa `IUnidadTrabajoGestionAvicola`), `AddGestionAvicolaInfraestructura(this IServiceCollection, IConfiguration)`, migración `InicialGestionAvicola`.

- [x] **Step 1: DbContext con filtros globales**

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

// Filtros globales de EF Core (spec SP5): soft delete (EstaActivo, regla
// transversal del glosario) y tenant (ClienteId del claim, vía ICurrentUser).
// El rol de plataforma (Administrador) lleva ClienteId nulo y ve todos los
// tenants. Galpon aplica el filtro sobre su ClienteId desnormalizado: no hace
// falta join con la granja.
public sealed class GestionAvicolaDbContext : DbContext, IUnidadTrabajoGestionAvicola
{
    private readonly Guid? _clienteIdActual;

    public GestionAvicolaDbContext(
        DbContextOptions<GestionAvicolaDbContext> opciones, ICurrentUser usuarioActual)
        : base(opciones) => _clienteIdActual = usuarioActual.ClienteId;

    public DbSet<Granja> Granjas => Set<Granja>();

    public DbSet<Galpon> Galpones => Set<Galpon>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("gestion_avicola");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GestionAvicolaDbContext).Assembly);

        // Sin ".Value" sobre el nullable: EF evalúa los valores capturados al
        // extraer los parámetros del filtro, y ".Value" lanza
        // InvalidOperationException cuando ClienteId es null (trampa ya
        // documentada en ClientesDbContext).
        modelBuilder.Entity<Granja>().HasQueryFilter(g =>
            g.EstaActivo && (_clienteIdActual == null || g.ClienteId == _clienteIdActual));
        modelBuilder.Entity<Galpon>().HasQueryFilter(g =>
            g.EstaActivo && (_clienteIdActual == null || g.ClienteId == _clienteIdActual));
    }
}
```

- [x] **Step 2: Configuraciones EF**

`ConfiguracionGranja.cs`:

```csharp
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionGranja : IEntityTypeConfiguration<Granja>
{
    public void Configure(EntityTypeBuilder<Granja> builder)
    {
        builder.ToTable("granjas");
        builder.Property(g => g.Nombre).HasMaxLength(200).IsRequired();

        // Nombre único por cliente, incluyendo inactivas: el soft delete no
        // libera el nombre (spec SP5).
        builder.HasIndex(g => new { g.ClienteId, g.Nombre }).IsUnique();

        // Un cliente tiene a lo sumo una granja activa (spec SP5): índice
        // único filtrado, última línea de defensa tras el handler.
        builder.HasIndex(g => g.ClienteId).IsUnique().HasFilter("[EstaActivo] = 1");
    }
}
```

`ConfiguracionGalpon.cs`:

```csharp
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionGalpon : IEntityTypeConfiguration<Galpon>
{
    public void Configure(EntityTypeBuilder<Galpon> builder)
    {
        builder.ToTable("galpones", t =>
        {
            // Las invariantes del agregado, reflejadas como última línea de
            // defensa en BD (spec SP5).
            t.HasCheckConstraint("CK_galpones_capacidad", "[CapacidadMaxima] > 0");
            t.HasCheckConstraint("CK_galpones_inventario",
                "[GallinasActuales] >= 0 AND [GallinasActuales] <= [CapacidadMaxima]");
        });
        builder.Property(g => g.Numero).HasMaxLength(10).IsRequired();
        builder.Property(g => g.Descripcion).HasMaxLength(500);

        // Número único por granja, incluyendo inactivos: el soft delete no
        // libera el número (spec SP5).
        builder.HasIndex(g => new { g.GranjaId, g.Numero }).IsUnique();
        builder.HasIndex(g => g.ClienteId);
    }
}
```

- [x] **Step 3: Repositorios**

`RepositorioGranjas.cs`:

```csharp
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioGranjas : IRepositorioGranjas
{
    private readonly GestionAvicolaDbContext _db;

    public RepositorioGranjas(GestionAvicolaDbContext db) => _db = db;

    public void Agregar(Granja granja) => _db.Granjas.Add(granja);

    public async Task<Granja?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Granjas.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<Granja?> ObtenerActivaDelTenantAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Granjas.FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<GranjaResumen>> ListarDelTenantAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Granjas.AsNoTracking().OrderBy(g => g.Nombre)
            .Select(g => new GranjaResumen(g.Id, g.Nombre))
            .ToListAsync(cancellationToken);

    public async Task<bool> ExisteNombreAsync(
        Guid clienteId, string nombre, CancellationToken cancellationToken = default) =>
        await _db.Granjas.IgnoreQueryFilters()
            .AnyAsync(g => g.ClienteId == clienteId && g.Nombre == nombre, cancellationToken);
}
```

`RepositorioGalpones.cs`:

```csharp
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioGalpones : IRepositorioGalpones
{
    private readonly GestionAvicolaDbContext _db;

    public RepositorioGalpones(GestionAvicolaDbContext db) => _db = db;

    public void Agregar(Galpon galpon) => _db.Galpones.Add(galpon);

    public async Task<Galpon?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Galpones.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GalponResumen>> ListarPorGranjaAsync(
        Guid granjaId, CancellationToken cancellationToken = default) =>
        await _db.Galpones.AsNoTracking()
            .Where(g => g.GranjaId == granjaId)
            .OrderBy(g => g.Numero)
            .Select(g => new GalponResumen(
                g.Id, g.Numero, g.CapacidadMaxima, g.GallinasActuales,
                g.FechaNacimientoLote, g.Descripcion))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Galpon>> ListarActivosDeGranjaAsync(
        Guid granjaId, CancellationToken cancellationToken = default) =>
        await _db.Galpones.Where(g => g.GranjaId == granjaId).ToListAsync(cancellationToken);

    public async Task<bool> ExisteNumeroAsync(
        Guid granjaId, string numero, CancellationToken cancellationToken = default) =>
        await _db.Galpones.IgnoreQueryFilters()
            .AnyAsync(g => g.GranjaId == granjaId && g.Numero == numero, cancellationToken);
}
```

- [x] **Step 4: DependencyInjection y factory de diseño**

`DependencyInjection.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Icarus.GestionAvicola.Infrastructure.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.GestionAvicola.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGestionAvicolaInfraestructura(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddDbContext<GestionAvicolaDbContext>((sp, opciones) =>
        {
            opciones.UseSqlServer(configuracion.GetConnectionString("Icarus"));
            opciones.AddInterceptors(
                new SaveChangesRegistroVueloInterceptor(sp.GetRequiredService<IRegistroVuelo>(),
                    new DescriptorContextoPersistencia("GestionAvicola")),
                new TransaccionesRegistroVueloInterceptor(sp.GetRequiredService<IRegistroVuelo>(),
                    new DescriptorContextoPersistencia("GestionAvicola")));
        });

        servicios.AddScoped<IRepositorioGranjas, RepositorioGranjas>();
        servicios.AddScoped<IRepositorioGalpones, RepositorioGalpones>();

        // IUnitOfWork genérica ya resuelve al contexto de Clientes; este
        // módulo usa su propia unidad de trabajo (spec SP5).
        servicios.AddScoped<IUnidadTrabajoGestionAvicola>(
            sp => sp.GetRequiredService<GestionAvicolaDbContext>());

        return servicios;
    }
}
```

`DesignTimeGestionAvicolaDbContextFactory.cs`:

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Icarus.GestionAvicola.Infrastructure;

// Permite correr dotnet ef sin levantar el Host (patrón de Clientes). La
// cadena es ficticia: solo se usa para generar migraciones, nunca conecta.
public sealed class DesignTimeGestionAvicolaDbContextFactory
    : IDesignTimeDbContextFactory<GestionAvicolaDbContext>
{
    public GestionAvicolaDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<GestionAvicolaDbContext>()
            .UseSqlServer("Server=localhost;Database=IcarusDiseno;TrustServerCertificate=True")
            .Options;
        return new GestionAvicolaDbContext(opciones, new UsuarioActualDiseno());
    }

    // Sin usuario en tiempo de diseño: ClienteId nulo deja los filtros de
    // tenant abiertos, igual que un rol de plataforma.
    private sealed class UsuarioActualDiseno : ICurrentUser
    {
        public bool EstaAutenticado => false;

        public Guid? UsuarioId => null;

        public string? Rol => null;

        public Guid? ClienteId => null;

        public Guid? TrabajadorId => null;
    }
}
```

- [x] **Step 5: Generar la migración**

El manifiesto de herramientas ya fija `dotnet-ef` 10.0.11 en `Icarus/.config/dotnet-tools.json`.

```bash
cd Icarus && dotnet tool restore
dotnet ef migrations add InicialGestionAvicola \
  --project src/GestionAvicola/Icarus.GestionAvicola.Infrastructure \
  --startup-project src/GestionAvicola/Icarus.GestionAvicola.Infrastructure \
  --context GestionAvicolaDbContext
```

Expected: se generan `Migrations/<timestamp>_InicialGestionAvicola.cs`, `.Designer.cs` y `GestionAvicolaDbContextModelSnapshot.cs`. Revisar el archivo generado: tablas `gestion_avicola.granjas` y `gestion_avicola.galpones`, índice único `(ClienteId, Nombre)`, índice único filtrado `[EstaActivo] = 1` en `granjas.ClienteId`, índice único `(GranjaId, Numero)`, los dos check constraints, columna `FechaNacimientoLote` de tipo `date`.

- [x] **Step 6: Verificar build y tests**

Run: `dotnet build Icarus/Icarus.sln --nologo` y `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~GestionAvicola"`
Expected: BUILD succeeded, 0 warnings; tests PASS.

- [x] **Step 7: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure
git commit -m "feat(avicola): persistencia de granjas y galpones con filtros de tenant y migracion inicial"
```

---

### Task 7: Host (endpoints, composición y semilla)

**Files:**
- Create: `Icarus/src/Host/Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/SemillaGestionAvicola.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs`

**Interfaces:**
- Consumes: `PoliticasClientes.Para(...)` y `Funcionalidades` de `Icarus.Clientes` (el Host sí puede referenciarlos: él compone), `SemillaIdentidad.ClienteDemoId`, todos los commands/queries de las Tasks 4-5.
- Produces: endpoints `/granjas` y `/galpones` protegidos por entitlement; semilla demo en Dev/Testing.

- [x] **Step 1: Endpoints**

`GestionAvicolaEndpoints.cs`:

```csharp
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Autorizacion;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Granjas;
using MediatR;

namespace Icarus.Host.Endpoints;

// Endpoints del módulo Gestión avícola (spec SP5). La protección es por
// funcionalidad (entitlement): el rol Cliente tiene todas las de sus módulos;
// el rol Trabajador solo las asignadas (glosario: el recolector solo ve su
// funcionalidad). GestionAvicola no referencia Clientes: el Host compone.
public static class GestionAvicolaEndpoints
{
    public static IEndpointRouteBuilder MapGestionAvicola(this IEndpointRouteBuilder app)
    {
        var politicaGranjas = PoliticasClientes.Para(Funcionalidades.Granjas);
        var politicaGalpones = PoliticasClientes.Para(Funcionalidades.Galpones);

        var granjas = app.MapGroup("/granjas");

        granjas.MapPost("/", async (CrearGranjaRequest cuerpo, ISender mediator) =>
        {
            var id = await mediator.Send(new CrearGranjaCommand(cuerpo.Nombre));
            return Results.Created($"/granjas/{id}", new { id });
        }).RequireAuthorization(politicaGranjas);

        granjas.MapGet("/", async (ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarGranjasQuery())))
            .RequireAuthorization(politicaGranjas);

        granjas.MapGet("/{id:guid}", async (Guid id, ISender mediator) =>
            Results.Ok(await mediator.Send(new ObtenerGranjaQuery(id))))
            .RequireAuthorization(politicaGranjas);

        granjas.MapPut("/{id:guid}", async (Guid id, RenombrarGranjaRequest cuerpo, ISender mediator) =>
        {
            await mediator.Send(new RenombrarGranjaCommand(id, cuerpo.Nombre));
            return Results.NoContent();
        }).RequireAuthorization(politicaGranjas);

        granjas.MapDelete("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new DesactivarGranjaCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(politicaGranjas);

        granjas.MapPost("/{granjaId:guid}/galpones",
            async (Guid granjaId, CrearGalponRequest cuerpo, ISender mediator) =>
            {
                var id = await mediator.Send(new CrearGalponCommand(
                    granjaId, cuerpo.Numero, cuerpo.CapacidadMaxima, cuerpo.GallinasActuales,
                    cuerpo.FechaNacimientoLote, cuerpo.Descripcion));
                return Results.Created($"/galpones/{id}", new { id });
            }).RequireAuthorization(politicaGalpones);

        granjas.MapGet("/{granjaId:guid}/galpones", async (Guid granjaId, ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarGalponesPorGranjaQuery(granjaId))))
            .RequireAuthorization(politicaGalpones);

        var galpones = app.MapGroup("/galpones");

        galpones.MapGet("/{id:guid}", async (Guid id, ISender mediator) =>
            Results.Ok(await mediator.Send(new ObtenerGalponQuery(id))))
            .RequireAuthorization(politicaGalpones);

        galpones.MapPut("/{id:guid}", async (Guid id, ActualizarGalponRequest cuerpo, ISender mediator) =>
        {
            await mediator.Send(new ActualizarGalponCommand(
                id, cuerpo.Numero, cuerpo.Descripcion, cuerpo.CapacidadMaxima));
            return Results.NoContent();
        }).RequireAuthorization(politicaGalpones);

        galpones.MapPut("/{id:guid}/inventario",
            async (Guid id, InventarioGalponRequest cuerpo, ISender mediator) =>
            {
                await mediator.Send(new AjustarInventarioGalponCommand(id, cuerpo.GallinasActuales));
                return Results.NoContent();
            }).RequireAuthorization(politicaGalpones);

        galpones.MapDelete("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new DesactivarGalponCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(politicaGalpones);

        return app;
    }

    private sealed record CrearGranjaRequest(string Nombre);

    private sealed record RenombrarGranjaRequest(string Nombre);

    private sealed record CrearGalponRequest(
        string Numero, int CapacidadMaxima, int GallinasActuales,
        DateOnly FechaNacimientoLote, string? Descripcion);

    private sealed record ActualizarGalponRequest(
        string Numero, string? Descripcion, int CapacidadMaxima);

    private sealed record InventarioGalponRequest(int GallinasActuales);
}
```

- [x] **Step 2: Semilla demo**

`SemillaGestionAvicola.cs`:

```csharp
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.GestionAvicola.Infrastructure;

// Datos de prueba, SOLO entornos dev/test (anti-PII: nombres ficticios). El
// ClienteId lo pasa el Host desde SemillaIdentidad: GestionAvicola no
// referencia Identity (aislamiento de módulos forzado por los tests de
// arquitectura). Las fechas de poblado son pasadas (glosario: ninguna fecha
// admite futuro).
public static class SemillaGestionAvicola
{
    public static readonly Guid GranjaDemoId = new("aa000000-0000-0000-0000-000000000001");
    public static readonly Guid GalponDemoNorteId = new("aa000000-0000-0000-0000-000000000011");
    public static readonly Guid GalponDemoSurId = new("aa000000-0000-0000-0000-000000000012");

    public static async Task SembrarAsync(IServiceProvider servicios, Guid clienteDemoId)
    {
        var db = servicios.GetRequiredService<GestionAvicolaDbContext>();
        if (await db.Granjas.IgnoreQueryFilters().AnyAsync(g => g.Id == GranjaDemoId))
            return;

        db.Granjas.Add(new Granja(GranjaDemoId, clienteDemoId, "Granja Demo"));
        db.Galpones.Add(new Galpon(
            GalponDemoNorteId, GranjaDemoId, clienteDemoId, "1", 5000, 4800,
            new DateOnly(2025, 9, 1), "Galpón norte"));
        db.Galpones.Add(new Galpon(
            GalponDemoSurId, GranjaDemoId, clienteDemoId, "2", 5000, 5000,
            new DateOnly(2026, 2, 2), null));

        await db.SaveChangesAsync();
    }
}
```

- [x] **Step 3: Composición en `Program.cs`**

Cinco ediciones en `Icarus/src/Host/Icarus.Host/Program.cs`:

a) Usings (junto a los de Clientes):

```csharp
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Infrastructure;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
```

b) Registrar los ensamblados de MediatR y validadores (añadir `typeof(CrearGranjaCommand).Assembly` a AMBAS llamadas existentes):

```csharp
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(IniciarSesionCommand).Assembly, typeof(CrearClienteCommand).Assembly,
    typeof(CrearGranjaCommand).Assembly));
builder.Services.AddValidatorsFromAssemblies([
    typeof(IniciarSesionCommand).Assembly, typeof(CrearClienteCommand).Assembly,
    typeof(CrearGranjaCommand).Assembly]);
```

c) Tras `builder.Services.AddClientesInfraestructura(builder.Configuration);`:

```csharp
builder.Services.AddGestionAvicolaInfraestructura(builder.Configuration);
```

d) Tras `app.MapClientes();`:

```csharp
app.MapGestionAvicola();
```

e) Dentro del bloque `if (app.Environment.IsDevelopment() || ...)`, tras la siembra de Clientes:

```csharp
    var avicolaDb = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
    await avicolaDb.Database.MigrateAsync();
    await SemillaGestionAvicola.SembrarAsync(
        alcance.ServiceProvider, SemillaIdentidad.ClienteDemoId);
```

- [x] **Step 4: Verificar build**

Run: `dotnet build Icarus/Icarus.sln --nologo`
Expected: BUILD succeeded, 0 warnings.

- [x] **Step 5: Commit**

```bash
git add Icarus/src/Host Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/SemillaGestionAvicola.cs
git commit -m "feat(avicola): endpoints de granjas y galpones con entitlement y semilla demo"
```

---

### Task 8: Tests de arquitectura

**Files:**
- Modify: `Icarus/tests/Icarus.ArchitectureTests/ReglasDeCapasTests.cs`
- Modify: `Icarus/tests/Icarus.ArchitectureTests/ReglasDeModulosTests.cs`

**Interfaces:**
- Consumes: los tres ensamblados nuevos (referencias añadidas en Task 1).

- [x] **Step 1: Extender `ReglasDeCapasTests`**

- En `DominioNoDependeDeLibrerias`, añadir al array de ensamblados:
  `typeof(GestionAvicola.Domain.Granja).Assembly,`
- En `InfraestructuraNoDependeDelHost`, añadir:
  `typeof(GestionAvicola.Infrastructure.Persistencia.GestionAvicolaDbContext).Assembly,`
- En `AplicacionNoDependeDeInfraestructura`, añadir al array de ensamblados:
  `typeof(GestionAvicola.Application.Granjas.CrearGranjaCommand).Assembly,`
  y añadir `"Icarus.GestionAvicola.Infrastructure"` a la lista de `HaveDependencyOnAny`.

- [x] **Step 2: Extender `ReglasDeModulosTests`**

Añadir un segundo `Fact` (el módulo nuevo no conoce a los otros, y los otros no lo conocen: la composición es solo del Host):

```csharp
    [Fact]
    public void GestionAvicolaNoSeReferenciaConOtrosModulos()
    {
        var avicolaHaciaOtros = Types
            .InAssemblies(new[]
            {
                typeof(GestionAvicola.Domain.Granja).Assembly,
                typeof(GestionAvicola.Application.Granjas.CrearGranjaCommand).Assembly,
                typeof(GestionAvicola.Infrastructure.Persistencia.GestionAvicolaDbContext).Assembly,
            })
            .ShouldNot().HaveDependencyOnAny("Icarus.Clientes", "Icarus.Identity").GetResult();
        var otrosHaciaAvicola = Types
            .InAssemblies(new[]
            {
                typeof(Clientes.Domain.Cliente).Assembly,
                typeof(Clientes.Application.Clientes.CrearClienteCommand).Assembly,
                typeof(Clientes.Infrastructure.Persistencia.ClientesDbContext).Assembly,
                typeof(Identity.Domain.Rol).Assembly,
                typeof(Identity.Application.Sesiones.IniciarSesionCommand).Assembly,
                typeof(Identity.Infrastructure.Persistencia.IdentityDbContext).Assembly,
            })
            .ShouldNot().HaveDependencyOn("Icarus.GestionAvicola").GetResult();

        Assert.True(avicolaHaciaOtros.IsSuccessful,
            string.Join(", ", avicolaHaciaOtros.FailingTypeNames ?? []));
        Assert.True(otrosHaciaAvicola.IsSuccessful,
            string.Join(", ", otrosHaciaAvicola.FailingTypeNames ?? []));
    }
```

- [x] **Step 3: Ejecutar**

Run: `dotnet test Icarus/tests/Icarus.ArchitectureTests`
Expected: PASS (los 4 facts). Si falla, la causa más probable es un `using` de Clientes colado en GestionAvicola: quitarlo, no relajar el test.

- [x] **Step 4: Commit**

```bash
git add Icarus/tests/Icarus.ArchitectureTests
git commit -m "test(avicola): reglas de arquitectura para el modulo GestionAvicola"
```

---

### Task 9: Tests de integración de los endpoints

**Files:**
- Test: `Icarus/tests/Icarus.IntegrationTests/GestionAvicolaEndpointsTests.cs`

**Interfaces:**
- Consumes: `IdentityFactory` (Testcontainers.MsSql, entorno `Testing`, migración y semilla automáticas en `Program`), `SemillaIdentidad.EmailAdmin/EmailCliente/EmailTrabajador`, `IdentityFactory.ContrasenaDePrueba`. Requiere **Docker corriendo**.

**Contexto para el implementador:** el cliente semilla tiene el módulo `GestionAvicola` habilitado y, tras la Task 7, una granja demo con dos galpones. El trabajador semilla tiene asignada la funcionalidad `Granjas` (y NO `Galpones`). Las cuentas nuevas se crean con la alta embebida del Host (`POST /clientes` como admin + `PUT /clientes/{id}/modulos`). Los JSON van en camelCase por defecto (`nombre`, `numero`, `capacidadMaxima`, `gallinasActuales`, `fechaNacimientoLote` como `"2025-09-01"`, `descripcion`).

- [x] **Step 1: Escribir el test que falla**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.Identity.Infrastructure;
using Xunit;

namespace Icarus.IntegrationTests;

// Endpoints del módulo Gestión avícola (spec SP5): CRUD de granjas y
// galpones, aislamiento de tenant, entitlement y cascada de desactivación.
public class GestionAvicolaEndpointsTests : IClassFixture<IdentityFactory>
{
    private readonly IdentityFactory _factory;

    public GestionAvicolaEndpointsTests(IdentityFactory factory) => _factory = factory;

    private async Task<string> LoginComo(string email)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/identidad/sesion",
            new { email, contrasena = IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage PedidoAutenticado(HttpMethod metodo, string url, string token) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };

    private static HttpRequestMessage ConCuerpo(
        HttpMethod metodo, string url, string token, object cuerpo)
    {
        var pedido = PedidoAutenticado(metodo, url, token);
        pedido.Content = JsonContent.Create(cuerpo);
        return pedido;
    }

    // Alta embebida de un cliente con el módulo GestionAvicola y su cuenta de
    // acceso. Devuelve el token de la cuenta de rol Cliente.
    private async Task<string> CrearClienteAvicola()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"avicola-{Guid.NewGuid():N}@icarus.test";

        var alta = ConCuerpo(HttpMethod.Post, "/clientes", admin, new
        {
            razonSocial = "Avícola de Prueba S.A.C.",
            identificadorFiscal = $"3{Random.Shared.Next(100000000, 999999999)}",
            email,
            contrasena = IdentityFactory.ContrasenaDePrueba,
        });
        var respuestaAlta = await clienteHttp.SendAsync(alta);
        Assert.Equal(HttpStatusCode.Created, respuestaAlta.StatusCode);
        var clienteId = (await respuestaAlta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var modulos = ConCuerpo(HttpMethod.Put, $"/clientes/{clienteId}/modulos", admin,
            new { modulos = new[] { "GestionAvicola" } });
        Assert.Equal(HttpStatusCode.NoContent, (await clienteHttp.SendAsync(modulos)).StatusCode);

        return await LoginComo(email);
    }

    private async Task<Guid> CrearGranja(string token, string nombre)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(
            ConCuerpo(HttpMethod.Post, "/granjas", token, new { nombre }));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
    }

    private async Task<Guid> GranjaDemoIdDelClienteSemilla(string token)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/granjas", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var granjas = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, granjas.GetArrayLength());
        return granjas[0].GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task ClienteSemillaListaSuGranjaSembrada()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/granjas", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var granjas = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, granjas.GetArrayLength());
        Assert.Equal("Granja Demo", granjas[0].GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task ClienteNuevoCreaSuUnicaGranjaYLaSegundaDa409()
    {
        var token = await CrearClienteAvicola();
        var cliente = _factory.CreateClient();

        await CrearGranja(token, "Granja Nueva");

        var segunda = await cliente.SendAsync(
            ConCuerpo(HttpMethod.Post, "/granjas", token, new { nombre = "Otra Granja" }));
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task NombreUsadoNoSeLiberaConLaDesactivacion()
    {
        var token = await CrearClienteAvicola();
        var cliente = _factory.CreateClient();
        var granjaId = await CrearGranja(token, "Granja Repetida");

        var desactivar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Delete, $"/granjas/{granjaId}", token));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var otroNombre = await cliente.SendAsync(
            ConCuerpo(HttpMethod.Post, "/granjas", token, new { nombre = "Granja Distinta" }));
        Assert.Equal(HttpStatusCode.Created, otroNombre.StatusCode);

        // Tras desactivar la segunda, el primer nombre sigue reservado.
        var segundaId = (await otroNombre.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        await cliente.SendAsync(PedidoAutenticado(HttpMethod.Delete, $"/granjas/{segundaId}", token));
        var repetida = await cliente.SendAsync(
            ConCuerpo(HttpMethod.Post, "/granjas", token, new { nombre = "Granja Repetida" }));
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Fact]
    public async Task GranjaDeOtroTenantDevuelve404()
    {
        var tokenA = await CrearClienteAvicola();
        var granjaA = await CrearGranja(tokenA, "Granja del A");
        var tokenB = await CrearClienteAvicola();
        var cliente = _factory.CreateClient();

        var obtener = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/granjas/{granjaA}", tokenB));
        var eliminar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Delete, $"/granjas/{granjaA}", tokenB));

        Assert.Equal(HttpStatusCode.NotFound, obtener.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, eliminar.StatusCode);
    }

    [Fact]
    public async Task TrabajadorSemillaSinFuncionalidadGalponesRecibe403()
    {
        // El trabajador semilla tiene Granjas, no Galpones.
        var tokenCliente = await LoginComo(SemillaIdentidad.EmailCliente);
        var granjaId = await GranjaDemoIdDelClienteSemilla(tokenCliente);
        var tokenTrabajador = await LoginComo(SemillaIdentidad.EmailTrabajador);
        var cliente = _factory.CreateClient();

        var granjas = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, "/granjas", tokenTrabajador));
        var galpones = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/granjas/{granjaId}/galpones", tokenTrabajador));

        Assert.Equal(HttpStatusCode.OK, granjas.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, galpones.StatusCode);
    }

    [Fact]
    public async Task SinTokenDevuelve401()
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/granjas");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task CrudCompletoDeGalpon()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var granjaId = await GranjaDemoIdDelClienteSemilla(token);
        var cliente = _factory.CreateClient();

        var crear = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/granjas/{granjaId}/galpones", token, new
            {
                numero = "3",
                capacidadMaxima = 4000,
                gallinasActuales = 3900,
                fechaNacimientoLote = "2026-01-10",
                descripcion = "Galpón este",
            }));
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var galponId = (await crear.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var obtener = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/galpones/{galponId}", token));
        Assert.Equal(HttpStatusCode.OK, obtener.StatusCode);
        var galpon = await obtener.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("3", galpon.GetProperty("numero").GetString());
        Assert.Equal(3900, galpon.GetProperty("gallinasActuales").GetInt32());

        var actualizar = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Put, $"/galpones/{galponId}", token,
            new { numero = "3", descripcion = "Galpón este (anexo)", capacidadMaxima = 4200 }));
        Assert.Equal(HttpStatusCode.NoContent, actualizar.StatusCode);

        var inventario = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Put, $"/galpones/{galponId}/inventario", token,
            new { gallinasActuales = 4100 }));
        Assert.Equal(HttpStatusCode.NoContent, inventario.StatusCode);

        var lista = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/granjas/{granjaId}/galpones", token));
        var galpones = await lista.Content.ReadFromJsonAsync<JsonElement>();
        // Los tests de la clase comparten contenedor: no se aserta el total,
        // solo que el galpón creado está.
        Assert.Contains(galpones.EnumerateArray(),
            g => g.GetProperty("id").GetGuid() == galponId);

        var desactivar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Delete, $"/galpones/{galponId}", token));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var obtenerInactivo = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/galpones/{galponId}", token));
        Assert.Equal(HttpStatusCode.NotFound, obtenerInactivo.StatusCode);
    }

    [Fact]
    public async Task GalponConFechaFuturaDevuelve400()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var granjaId = await GranjaDemoIdDelClienteSemilla(token);
        var cliente = _factory.CreateClient();
        var manana = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");

        var respuesta = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/granjas/{granjaId}/galpones", token, new
            {
                numero = "4",
                capacidadMaxima = 4000,
                gallinasActuales = 0,
                fechaNacimientoLote = manana,
                descripcion = (string?)null,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task GalponConInventarioMayorQueCapacidadDevuelve400()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var granjaId = await GranjaDemoIdDelClienteSemilla(token);
        var cliente = _factory.CreateClient();

        var crear = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/granjas/{granjaId}/galpones", token, new
            {
                numero = "5",
                capacidadMaxima = 100,
                gallinasActuales = 101,
                fechaNacimientoLote = "2026-01-10",
                descripcion = (string?)null,
            }));
        Assert.Equal(HttpStatusCode.BadRequest, crear.StatusCode);

        var valido = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/granjas/{granjaId}/galpones", token, new
            {
                numero = "5",
                capacidadMaxima = 100,
                gallinasActuales = 100,
                fechaNacimientoLote = "2026-01-10",
                descripcion = (string?)null,
            }));
        var galponId = (await valido.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var ajuste = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Put, $"/galpones/{galponId}/inventario", token,
            new { gallinasActuales = 101 }));
        Assert.Equal(HttpStatusCode.BadRequest, ajuste.StatusCode);
    }

    [Fact]
    public async Task NumeroDeGalponDuplicadoDevuelve409()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var granjaId = await GranjaDemoIdDelClienteSemilla(token);
        var cliente = _factory.CreateClient();

        // La semilla ya tiene los galpones "1" y "2".
        var respuesta = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/granjas/{granjaId}/galpones", token, new
            {
                numero = "1",
                capacidadMaxima = 4000,
                gallinasActuales = 0,
                fechaNacimientoLote = "2026-01-10",
                descripcion = (string?)null,
            }));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task GalponEnGranjaInexistenteDevuelve404()
    {
        var token = await LoginComo(SemillaIdentidad.EmailCliente);
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/granjas/{Guid.NewGuid()}/galpones", token, new
            {
                numero = "9",
                capacidadMaxima = 4000,
                gallinasActuales = 0,
                fechaNacimientoLote = "2026-01-10",
                descripcion = (string?)null,
            }));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task DesactivarGranjaDesactivaSusGalpones()
    {
        var token = await CrearClienteAvicola();
        var cliente = _factory.CreateClient();
        var granjaId = await CrearGranja(token, "Granja Con Galpon");

        var crear = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/granjas/{granjaId}/galpones", token, new
            {
                numero = "1",
                capacidadMaxima = 4000,
                gallinasActuales = 100,
                fechaNacimientoLote = "2026-01-10",
                descripcion = (string?)null,
            }));
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);

        var desactivar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Delete, $"/granjas/{granjaId}", token));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var granja = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/granjas/{granjaId}", token));
        Assert.Equal(HttpStatusCode.NotFound, granja.StatusCode);

        // Cascada: el galpón quedó inactivo y los filtros lo ocultan.
        var galpones = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/granjas/{granjaId}/galpones", token));
        Assert.Equal(HttpStatusCode.OK, galpones.StatusCode);
        Assert.Equal(0, (await galpones.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
    }
}
```

- [x] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~GestionAvicolaEndpointsTests"`
Expected: PASS a la primera si las Tasks 1-7 quedaron bien (los endpoints ya existen). Si algo falla, NO tocar el test para que pase: corregir la implementación. El rojo real de estos tests se vio de forma dirigida durante el desarrollo si se escribieron antes que los endpoints; si se escriben después (válido aquí porque el comportamiento ya quedó fijado por los unit tests), verificar al menos una vez que fallan por el motivo correcto desactivando temporalmente una pieza (por ejemplo comentar `app.MapGestionAvicola();` debe dar 404 en todos) y volviendo a activarla.

- [x] **Step 3: Suite de integración completa**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests`
Expected: PASS, incluidos los tests previos (Entitlement, Clientes, Identity): la semilla nueva no debe romper nada existente.

- [x] **Step 4: Commit**

```bash
git add Icarus/tests/Icarus.IntegrationTests/GestionAvicolaEndpointsTests.cs
git commit -m "test(avicola): integracion de endpoints de granjas y galpones con tenant y entitlement"
```

---

### Task 10: Cierre (puerta de calidad, documentación, push)

**Files:**
- Modify: `AGENTS.md` (sección Proyecto)
- Modify: `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md` (regenerados, NO a mano)

- [x] **Step 1: Actualizar `AGENTS.md`**

En la sección `## Proyecto`, punto del backend: añadir el módulo nuevo y corregir la línea obsoleta del frontend (la PWA ya existe desde el plan 4). El párrafo queda aproximadamente:

```
- Backend .NET bajo `Icarus/`: solución con building blocks (Domain, Application,
  Observability), módulo Identity completo (JWT, usuarios, roles), módulo
  Clientes completo (agregados Cliente/Trabajador, filtros de tenant, entitlement
  por módulo) y módulo GestionAvicola (agregados Granja/Galpón, una granja
  activa por cliente), con puerta de calidad con gates de backend. El frontend
  React (PWA) vive bajo `web/`.
```

- [x] **Step 2: Regenerar adaptadores**

Run: `node quality/generar-adaptadores.mjs`
Expected: regenera `CLAUDE.md`, `GEMINI.md` y `.github/copilot-instructions.md` desde `AGENTS.md`.

- [x] **Step 3: Puerta de calidad completa**

Run: `./verify.ps1` (PowerShell, desde la raíz; exige Docker corriendo por Testcontainers)
Expected: todos los gates en verde: tests de la puerta, adaptadores, mojibake, enlaces, frontend lint/build/test, `dotnet build`, `dotnet test` completos.

- [x] **Step 4: Releer el diff propio y push**

```bash
git status --short
git log --oneline origin/develop..HEAD
git add AGENTS.md CLAUDE.md GEMINI.md .github/copilot-instructions.md docs
git commit -m "docs(agentes): modulo GestionAvicola en la seccion Proyecto"
git push origin develop
```

La revisión humana no existe: la puerta + la lectura del propio diff son la revisión (WORKFLOW.md, paso 7).

- [x] **Step 5: Cerrar el ciclo**

- Marcar las tareas de este plan como hechas.
- Si el trabajo quedó completo: borrar `docs/ai/HANDOFF.md` (es efímero). Si quedó a medias: actualizarlo con lo pendiente.

---

## Notas para el implementador

- **Orden de las tareas importa**: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10. La 4 y la 5 comparten un detalle: `DesactivarGranjaHandlerTests` (Task 4) usa `IRepositorioGalpones` (Task 5); crear esa interfaz ya en la Task 4, como indica su Step 2.
- **La regla "un cliente, una granja activa"** se fuerza en tres capas: handler (`ObtenerActivaDelTenantAsync`), índice único filtrado en BD y filtros globales que ocultan la inactiva. No quitar ninguna.
- **`IUnitOfWork` NO se inyecta en handlers de este módulo**: siempre `IUnidadTrabajoGestionAvicola`. Inyectar la genérica guardaría en el DbContext de Clientes y los tests de integración fallarían de forma confusa.
- **Nada de `DateTime.Now`**: fechas de negocio con `DateOnly` y `DateTime.UtcNow` (patrón de `Trabajador`).
- **No portar del legacy**: contadores (`ContadorHuevos`, `TotalGallinas`, `BajasGallinas`), métodos de estadística de `GestorAvicola`, ni el nombre `GestorAvicola`. El legacy tiene errores conocidos; el spec manda.
- **Si un gate de la puerta falla, se arregla el contenido, no el gate** (AGENTS.md).
