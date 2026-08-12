# Subproyecto 2 — Plan 1: Andamiaje backend, observabilidad y puerta de calidad

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Crear la solución .NET bajo `Icarus/` con building blocks (Domain, Application, Observability), Host con health check, tests de arquitectura, y extender la puerta de calidad y el CI con los gates de backend.

**Architecture:** Monolito modular por capas (spec: `docs/superpowers/specs/2026-08-12-subproyecto-2-arquitectura-inicial-design.md`). Plan 1 de 4: levanta el esqueleto completo y la observabilidad transversal; los proyectos `Identity` y `Clientes` quedan creados pero vacíos (se llenan en los planes 2 y 3). Los planes 2 (Identity), 3 (módulo Clientes) y 4 (frontend PWA) se escriben al inicio de sus sesiones, contra el estado real del repo.

**Tech Stack:** .NET 10 (SDK pineado), CPM (`Directory.Packages.props`), Serilog, MediatR, FluentValidation, xUnit, NSubstitute, NetArchTest, Microsoft.AspNetCore.Mvc.Testing.

## Global Constraints

- Identificadores, comentarios y mensajes en español correcto, UTF-8 sin BOM. Nunca mojibake.
- Anti-PII: nunca nombres, documentos, credenciales ni datos biométricos en logs ni respuestas; mensajes de error genéricos.
- SDK .NET pineado en `global.json`: `10.0.100` con `rollForward: latestFeature`.
- Todas las versiones de paquetes en `Directory.Packages.props` (CPM); ningún `Version=` en los `.csproj`.
- `TreatWarningsAsErrors` en `Directory.Build.props`.
- Ejecutar `./verify.sh` antes de cada commit. Prohibido `--no-verify`.
- Un test que nunca se vio en rojo no prueba nada: cada test se corre primero en rojo.
- Commits en `develop`, directos, mensaje en español estilo conventional commits.

---

### Task 1: Solución, props y proyectos

**Files:**
- Create: `Icarus/global.json`
- Create: `Icarus/Directory.Build.props`
- Create: `Icarus/Directory.Packages.props`
- Create: `Icarus/Icarus.sln` y proyectos bajo `Icarus/src/` e `Icarus/tests/` (vía CLI)

**Interfaces:**
- Consumes: nada.
- Produces: estructura de proyectos con referencias correctas; `dotnet build Icarus/Icarus.sln` en verde. Los tasks siguientes asumen estos nombres de proyecto exactos.

- [x] **Step 1: Crear `Icarus/global.json`**

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

- [x] **Step 2: Crear `Icarus/Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

- [x] **Step 3: Crear `Icarus/Directory.Packages.props`**

Versiones verificadas el 2026-08-12: las de MediatR, FluentValidation, Serilog,
test y analizadores contra `repos/dev_Caserito/CaseritoApp/Directory.Packages.props`;
las de `Serilog.AspNetCore`, `Serilog.Sinks.Console` y `Serilog.Formatting.Compact`
contra la API de NuGet (Caserito no las pinea).

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <!-- Analizadores globales: se aplican a TODOS los proyectos automáticamente -->
  <ItemGroup>
    <GlobalPackageReference Include="Roslynator.Analyzers" Version="4.13.1" />
    <GlobalPackageReference Include="SonarAnalyzer.CSharp" Version="10.6.0.109712" />
  </ItemGroup>

  <ItemGroup>
    <PackageVersion Include="MediatR" Version="12.4.1" />
    <PackageVersion Include="FluentValidation" Version="11.10.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.10.0" />
    <PackageVersion Include="Serilog" Version="4.1.0" />
    <PackageVersion Include="Serilog.AspNetCore" Version="10.0.0" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.1.1" />
    <PackageVersion Include="Serilog.Formatting.Compact" Version="3.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>
</Project>
```

- [x] **Step 4: Crear la solución y los proyectos**

```bash
cd Icarus
dotnet new sln -n Icarus
dotnet new classlib -n Icarus.BuildingBlocks.Domain          -o src/BuildingBlocks/Icarus.BuildingBlocks.Domain
dotnet new classlib -n Icarus.BuildingBlocks.Application     -o src/BuildingBlocks/Icarus.BuildingBlocks.Application
dotnet new classlib -n Icarus.BuildingBlocks.Observability   -o src/BuildingBlocks/Icarus.BuildingBlocks.Observability
dotnet new classlib -n Icarus.Identity.Domain                -o src/Identity/Icarus.Identity.Domain
dotnet new classlib -n Icarus.Identity.Application           -o src/Identity/Icarus.Identity.Application
dotnet new classlib -n Icarus.Identity.Infrastructure        -o src/Identity/Icarus.Identity.Infrastructure
dotnet new classlib -n Icarus.Clientes.Domain                -o src/Clientes/Icarus.Clientes.Domain
dotnet new classlib -n Icarus.Clientes.Application           -o src/Clientes/Icarus.Clientes.Application
dotnet new classlib -n Icarus.Clientes.Infrastructure        -o src/Clientes/Icarus.Clientes.Infrastructure
dotnet new web     -n Icarus.Host                            -o src/Host/Icarus.Host
dotnet new xunit   -n Icarus.UnitTests                       -o tests/Icarus.UnitTests
dotnet new xunit   -n Icarus.IntegrationTests                -o tests/Icarus.IntegrationTests
dotnet new xunit   -n Icarus.ArchitectureTests               -o tests/Icarus.ArchitectureTests
dotnet sln add src/BuildingBlocks/Icarus.BuildingBlocks.Domain src/BuildingBlocks/Icarus.BuildingBlocks.Application src/BuildingBlocks/Icarus.BuildingBlocks.Observability src/Identity/Icarus.Identity.Domain src/Identity/Icarus.Identity.Application src/Identity/Icarus.Identity.Infrastructure src/Clientes/Icarus.Clientes.Domain src/Clientes/Icarus.Clientes.Application src/Clientes/Icarus.Clientes.Infrastructure src/Host/Icarus.Host tests/Icarus.UnitTests tests/Icarus.IntegrationTests tests/Icarus.ArchitectureTests
```

- [x] **Step 5: Eliminar los archivos generados de ejemplo**

```bash
cd Icarus && find src tests -name 'Class1.cs' -delete -o -name 'UnitTest1.cs' -delete
```

- [x] **Step 6: Cablear las referencias según las reglas del spec**

```bash
cd Icarus
dotnet add src/BuildingBlocks/Icarus.BuildingBlocks.Application reference src/BuildingBlocks/Icarus.BuildingBlocks.Domain
dotnet add src/BuildingBlocks/Icarus.BuildingBlocks.Observability reference src/BuildingBlocks/Icarus.BuildingBlocks.Domain
dotnet add src/Identity/Icarus.Identity.Domain reference src/BuildingBlocks/Icarus.BuildingBlocks.Domain
dotnet add src/Identity/Icarus.Identity.Application reference src/Identity/Icarus.Identity.Domain src/BuildingBlocks/Icarus.BuildingBlocks.Application
dotnet add src/Identity/Icarus.Identity.Infrastructure reference src/Identity/Icarus.Identity.Application
dotnet add src/Clientes/Icarus.Clientes.Domain reference src/BuildingBlocks/Icarus.BuildingBlocks.Domain
dotnet add src/Clientes/Icarus.Clientes.Application reference src/Clientes/Icarus.Clientes.Domain src/BuildingBlocks/Icarus.BuildingBlocks.Application
dotnet add src/Clientes/Icarus.Clientes.Infrastructure reference src/Clientes/Icarus.Clientes.Application
dotnet add src/Host/Icarus.Host reference src/BuildingBlocks/Icarus.BuildingBlocks.Application src/BuildingBlocks/Icarus.BuildingBlocks.Observability
dotnet add tests/Icarus.UnitTests reference src/BuildingBlocks/Icarus.BuildingBlocks.Domain src/BuildingBlocks/Icarus.BuildingBlocks.Application src/BuildingBlocks/Icarus.BuildingBlocks.Observability src/Host/Icarus.Host
dotnet add tests/Icarus.IntegrationTests reference src/Host/Icarus.Host
dotnet add tests/Icarus.ArchitectureTests reference src/BuildingBlocks/Icarus.BuildingBlocks.Domain src/BuildingBlocks/Icarus.BuildingBlocks.Application src/Identity/Icarus.Identity.Domain src/Identity/Icarus.Identity.Application src/Clientes/Icarus.Clientes.Domain src/Clientes/Icarus.Clientes.Application
```

- [x] **Step 7: FrameworkReference y paquetes donde corresponde**

`Icarus.BuildingBlocks.Observability` necesita ASP.NET Core (middlewares).
Agregar a `src/BuildingBlocks/Icarus.BuildingBlocks.Observability/Icarus.BuildingBlocks.Observability.csproj`:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```

```bash
cd Icarus
dotnet add src/BuildingBlocks/Icarus.BuildingBlocks.Application package MediatR
dotnet add src/BuildingBlocks/Icarus.BuildingBlocks.Application package FluentValidation
dotnet add src/BuildingBlocks/Icarus.BuildingBlocks.Observability package Serilog.AspNetCore
dotnet add src/BuildingBlocks/Icarus.BuildingBlocks.Observability package Serilog.Sinks.Console
dotnet add src/BuildingBlocks/Icarus.BuildingBlocks.Observability package Serilog.Formatting.Compact
dotnet add tests/Icarus.UnitTests package NSubstitute
dotnet add tests/Icarus.ArchitectureTests package NetArchTest.Rules
dotnet add tests/Icarus.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
```

CPM toma las versiones de `Directory.Packages.props`. Si el CLI escribió
`Version=` en algún `.csproj`, borrarlo a mano (regla global).

- [x] **Step 8: Verificar build**

Run: `dotnet build Icarus/Icarus.sln --nologo`
Expected: `Build succeeded`, 0 warnings (los warnings son errores).

- [x] **Step 9: Commit**

```bash
git add Icarus
./verify.sh
git commit -m "feat: solución Icarus con building blocks, módulos vacíos y tests"
```

---

### Task 2: BuildingBlocks.Domain — primitivas de dominio

**Files:**
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/Entity.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/AggregateRoot.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/IDomainEvent.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain/DomainException.cs`
- Test: `Icarus/tests/Icarus.UnitTests/BuildingBlocks/EntityTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/BuildingBlocks/AggregateRootTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces (lo usan todos los tasks y planes siguientes):

```csharp
namespace Icarus.BuildingBlocks.Domain;

public abstract class Entity
{
    public Guid Id { get; protected set; }
    public override bool Equals(object? obj);
    public override int GetHashCode();
}

public abstract class AggregateRoot : Entity
{
    public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    protected void AddDomainEvent(IDomainEvent evento);
    public void ClearDomainEvents();
}

public interface IDomainEvent { DateTime OcurridoEn { get; } }

public abstract class DomainException : Exception { }
public sealed class NotFoundException : DomainException { public NotFoundException(string entidad, Guid id); }
public sealed class ConflictException : DomainException { public ConflictException(string mensaje); }
```

- [x] **Step 1: Escribir los tests en rojo**

`EntityTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Xunit;

namespace Icarus.UnitTests.BuildingBlocks;

public class EntityTests
{
    private sealed class EntidadFalsa : Entity
    {
        public EntidadFalsa() { }
        public EntidadFalsa(Guid id) => Id = id;
    }

    private sealed class OtraEntidad : Entity
    {
        public OtraEntidad(Guid id) => Id = id;
    }

    [Fact]
    public void DosEntidadesConMismoIdYTipoSonIguales()
    {
        var id = Guid.NewGuid();
        Assert.Equal(new EntidadFalsa(id), new EntidadFalsa(id));
    }

    [Fact]
    public void EntidadesConDistintoIdNoSonIguales()
    {
        Assert.NotEqual(new EntidadFalsa(), new EntidadFalsa());
    }

    [Fact]
    public void EntidadesDeDistintoTipoNoSonIgualesAunqueCompartanId()
    {
        var id = Guid.NewGuid();
        Entity a = new EntidadFalsa(id);
        Entity b = new OtraEntidad(id);
        Assert.NotEqual(a, b);
    }
}
```

`AggregateRootTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Xunit;

namespace Icarus.UnitTests.BuildingBlocks;

public class AggregateRootTests
{
    private sealed record EventoFalso(DateTime OcurridoEn) : IDomainEvent;

    private sealed class AgregadoFalso : AggregateRoot
    {
        public void Disparar() => AddDomainEvent(new EventoFalso(DateTime.UtcNow));
    }

    [Fact]
    public void AgregarEventoLoExponeEnDomainEvents()
    {
        var agregado = new AgregadoFalso();
        agregado.Disparar();
        Assert.Single(agregado.DomainEvents);
    }

    [Fact]
    public void ClearDomainEventsVaciaLaColeccion()
    {
        var agregado = new AgregadoFalso();
        agregado.Disparar();
        agregado.ClearDomainEvents();
        Assert.Empty(agregado.DomainEvents);
    }
}
```

- [x] **Step 2: Correr y verificar rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: FALLA de compilación (`Entity` y `AggregateRoot` no existen).

- [x] **Step 3: Implementación mínima**

`Entity.cs`:

```csharp
namespace Icarus.BuildingBlocks.Domain;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj)
    {
        if (obj is not Entity otro || otro.GetType() != GetType())
            return false;
        return Id == otro.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();
}
```

`AggregateRoot.cs`:

```csharp
namespace Icarus.BuildingBlocks.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent evento) => _domainEvents.Add(evento);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

`IDomainEvent.cs`:

```csharp
namespace Icarus.BuildingBlocks.Domain;

public interface IDomainEvent
{
    DateTime OcurridoEn { get; }
}
```

`DomainException.cs`:

```csharp
namespace Icarus.BuildingBlocks.Domain;

// Mensajes genéricos por la regla anti-PII: nunca incluir datos del trabajador,
// documentos ni credenciales.
public abstract class DomainException : Exception
{
    protected DomainException(string mensaje) : base(mensaje) { }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entidad, Guid id)
        : base($"{entidad} no encontrado.")
    {
        Entidad = entidad;
        EntidadId = id;
    }

    public string Entidad { get; }
    public Guid EntidadId { get; }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string mensaje) : base(mensaje) { }
}
```

- [x] **Step 4: Correr y verificar verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: PASS (5 tests).

- [x] **Step 5: Commit**

```bash
git add Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Domain Icarus/tests/Icarus.UnitTests
./verify.sh
git commit -m "feat: primitivas de dominio (Entity, AggregateRoot, eventos, excepciones)"
```

---

### Task 3: BuildingBlocks.Application — ICurrentUser, IUnitOfWork, ValidationBehavior

**Files:**
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/ICurrentUser.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/IUnitOfWork.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/Behaviors/ValidationBehavior.cs`
- Test: `Icarus/tests/Icarus.UnitTests/BuildingBlocks/ValidationBehaviorTests.cs`

**Interfaces:**
- Consumes: nada de tasks anteriores (interfaces puras).
- Produces:

```csharp
namespace Icarus.BuildingBlocks.Application;

public interface ICurrentUser
{
    bool EstaAutenticado { get; }
    Guid? UsuarioId { get; }
    string? Rol { get; }
    Guid? ClienteId { get; }
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

namespace Icarus.BuildingBlocks.Application.Behaviors;

// Pipeline de MediatR: valida con FluentValidation antes de ejecutar el handler.
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull;
```

- [x] **Step 1: Escribir el test en rojo**

`ValidationBehaviorTests.cs`:

```csharp
using FluentValidation;
using Icarus.BuildingBlocks.Application.Behaviors;
using MediatR;
using Xunit;

namespace Icarus.UnitTests.BuildingBlocks;

public class ValidationBehaviorTests
{
    private sealed record SolicitudFalsa(string Nombre) : IRequest<string>;

    private sealed class ValidadorFalso : AbstractValidator<SolicitudFalsa>
    {
        public ValidadorFalso() => RuleFor(s => s.Nombre).NotEmpty();
    }

    [Fact]
    public async Task SolicitudValidaLlamaAlSiguiente()
    {
        var behavior = new ValidationBehavior<SolicitudFalsa, string>(new[] { new ValidadorFalso() });
        var resultado = await behavior.Handle(
            new SolicitudFalsa("ok"),
            () => Task.FromResult("respuesta"),
            CancellationToken.None);
        Assert.Equal("respuesta", resultado);
    }

    [Fact]
    public async Task SolicitudInvalidaLanzaValidationException()
    {
        var behavior = new ValidationBehavior<SolicitudFalsa, string>(new[] { new ValidadorFalso() });
        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new SolicitudFalsa(""),
            () => Task.FromResult("respuesta"),
            CancellationToken.None));
    }

    [Fact]
    public async Task SinValidadoresLlamaAlSiguiente()
    {
        var behavior = new ValidationBehavior<SolicitudFalsa, string>(
            Enumerable.Empty<IValidator<SolicitudFalsa>>());
        var resultado = await behavior.Handle(
            new SolicitudFalsa(""),
            () => Task.FromResult("respuesta"),
            CancellationToken.None);
        Assert.Equal("respuesta", resultado);
    }
}
```

- [x] **Step 2: Correr y verificar rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: FALLA de compilación (`ValidationBehavior` no existe).

- [x] **Step 3: Implementación**

`ICurrentUser.cs`:

```csharp
namespace Icarus.BuildingBlocks.Application;

public interface ICurrentUser
{
    bool EstaAutenticado { get; }
    Guid? UsuarioId { get; }
    string? Rol { get; }
    Guid? ClienteId { get; }
}
```

`IUnitOfWork.cs`:

```csharp
namespace Icarus.BuildingBlocks.Application;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

`Behaviors/ValidationBehavior.cs`:

```csharp
using FluentValidation;
using MediatR;

namespace Icarus.BuildingBlocks.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var contexto = new ValidationContext<TRequest>(request);
        var resultados = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(contexto, cancellationToken)));
        var errores = resultados.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (errores.Count != 0)
            throw new ValidationException(errores);

        return await next();
    }
}
```

- [x] **Step 4: Correr y verificar verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: PASS (8 tests acumulados).

- [x] **Step 5: Commit**

```bash
git add Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application Icarus/tests/Icarus.UnitTests
./verify.sh
git commit -m "feat: ICurrentUser, IUnitOfWork y ValidationBehavior"
```

---

### Task 4: Tests de arquitectura

**Files:**
- Create: marcadores `Marcador.cs` en los cuatro proyectos de módulo vacíos
- Test: `Icarus/tests/Icarus.ArchitectureTests/ReglasDeCapasTests.cs`
- Test: `Icarus/tests/Icarus.ArchitectureTests/ReglasDeModulosTests.cs`

**Interfaces:**
- Consumes: `Entity` (task 2), `ICurrentUser` (task 3).
- Produces: reglas NetArchTest que todo código futuro debe respetar:
  `DominioNoDependeDeLibrerias`, `AplicacionNoDependeDeInfraestructura`,
  `ModulosNoSeReferencianEntreSi`. Los planes 2 y 3 extienden estas reglas a
  los proyectos `Infrastructure` cuando tengan tipos.

- [x] **Step 1: Crear los marcadores de assembly**

Los proyectos de módulo están vacíos; estos archivos anclan los tests de
arquitectura y se borran en los planes 2 y 3 cuando existan tipos reales:

- `Icarus/src/Identity/Icarus.Identity.Domain/Marcador.cs`:
  `namespace Icarus.Identity.Domain; public static class Marcador { }`
- `Icarus/src/Identity/Icarus.Identity.Application/Marcador.cs`:
  `namespace Icarus.Identity.Application; public static class Marcador { }`
- `Icarus/src/Clientes/Icarus.Clientes.Domain/Marcador.cs`:
  `namespace Icarus.Clientes.Domain; public static class Marcador { }`
- `Icarus/src/Clientes/Icarus.Clientes.Application/Marcador.cs`:
  `namespace Icarus.Clientes.Application; public static class Marcador { }`

- [x] **Step 2: Escribir los tests**

`ReglasDeCapasTests.cs`:

```csharp
using NetArchTest.Rules;
using Xunit;

namespace Icarus.ArchitectureTests;

public class ReglasDeCapasTests
{
    [Fact]
    public void DominioNoDependeDeLibrerias()
    {
        var resultado = Types
            .InAssemblies(new[]
            {
                typeof(BuildingBlocks.Domain.Entity).Assembly,
                typeof(Identity.Domain.Marcador).Assembly,
                typeof(Clientes.Domain.Marcador).Assembly,
            })
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions",
                "Serilog",
                "MediatR",
                "FluentValidation")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void AplicacionNoDependeDeInfraestructura()
    {
        var resultado = Types
            .InAssemblies(new[]
            {
                typeof(BuildingBlocks.Application.ICurrentUser).Assembly,
                typeof(Identity.Application.Marcador).Assembly,
                typeof(Clientes.Application.Marcador).Assembly,
            })
            .ShouldNot()
            .HaveDependencyOnAny("Icarus.Identity.Infrastructure", "Icarus.Clientes.Infrastructure")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
```

`ReglasDeModulosTests.cs`:

```csharp
using NetArchTest.Rules;
using Xunit;

namespace Icarus.ArchitectureTests;

public class ReglasDeModulosTests
{
    [Fact]
    public void ModulosNoSeReferencianEntreSi()
    {
        var clientesHaciaIdentity = Types
            .InAssembly(typeof(Clientes.Domain.Marcador).Assembly)
            .ShouldNot().HaveDependencyOn("Icarus.Identity").GetResult();
        var identityHaciaClientes = Types
            .InAssembly(typeof(Identity.Domain.Marcador).Assembly)
            .ShouldNot().HaveDependencyOn("Icarus.Clientes").GetResult();

        Assert.True(clientesHaciaIdentity.IsSuccessful,
            string.Join(", ", clientesHaciaIdentity.FailingTypeNames ?? []));
        Assert.True(identityHaciaClientes.IsSuccessful,
            string.Join(", ", identityHaciaClientes.FailingTypeNames ?? []));
    }
}
```

- [x] **Step 3: Correr y verificar verde (riesgo de test vacuo)**

Run: `dotnet test Icarus/tests/Icarus.ArchitectureTests --nologo`
Expected: PASS. Como las reglas sobre proyectos casi vacíos podrían pasar sin
probar nada, el paso siguiente verifica que el arnés detecta violaciones.

- [x] **Step 4: Prueba de fuego (ver la regla en rojo)**

1. Agregar temporalmente `<PackageReference Include="MediatR" />` al `.csproj`
   de `Icarus.Clientes.Domain` y un archivo `Temp.cs` con
   `using MediatR; namespace Icarus.Clientes.Domain; public static class Temp { private readonly IMediator? _m; }`.
2. Run: `dotnet test Icarus/tests/Icarus.ArchitectureTests --nologo`
   Expected: `DominioNoDependeDeLibrerias` FALLA y nombra a `Temp`.
3. Revertir: borrar `Temp.cs` y el `PackageReference`. Correr de nuevo: PASS.

- [x] **Step 5: Commit**

```bash
git add Icarus/tests/Icarus.ArchitectureTests Icarus/src/Identity Icarus/src/Clientes
./verify.sh
git commit -m "test: reglas de arquitectura (capas y aislamiento de módulos)"
```

---

### Task 5: BuildingBlocks.Observability — correlation ID y exception middleware

**Files:**
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ObservabilityExtensions.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/CorrelationIdMiddleware.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Observability/CorrelationIdMiddlewareTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs`

**Interfaces:**
- Consumes: `DomainException`, `NotFoundException`, `ConflictException` (task 2).
- Produces:

```csharp
namespace Icarus.BuildingBlocks.Observability;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservabilidad(this WebApplicationBuilder builder);
}

public sealed class CorrelationIdMiddleware
{
    public const string Header = "X-Correlation-ID";
    public CorrelationIdMiddleware(RequestDelegate next);
    public Task Invoke(HttpContext context);
}

public sealed class ExceptionHandlingMiddleware
{
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger);
    public Task Invoke(HttpContext context);
}
```

- [x] **Step 1: Tests en rojo — CorrelationIdMiddleware**

```csharp
using Icarus.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Icarus.UnitTests.Observability;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task SinHeaderGeneraUnCorrelationIdYLoExponeEnLaRespuesta()
    {
        var contexto = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.Invoke(contexto);

        var id = contexto.Response.Headers[CorrelationIdMiddleware.Header].ToString();
        Assert.True(Guid.TryParse(id, out _), $"ID inesperado: {id}");
    }

    [Fact]
    public async Task ConHeaderEntranteLoPropagaSinCambiarlo()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers[CorrelationIdMiddleware.Header] = "abc123";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.Invoke(contexto);

        Assert.Equal("abc123", contexto.Response.Headers[CorrelationIdMiddleware.Header].ToString());
    }

    [Fact]
    public async Task HeaderEntranteDemasiadoLargoSeReemplaza()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers[CorrelationIdMiddleware.Header] = new string('x', 100);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.Invoke(contexto);

        var id = contexto.Response.Headers[CorrelationIdMiddleware.Header].ToString();
        Assert.True(Guid.TryParse(id, out _));
    }
}
```

- [x] **Step 2: Tests en rojo — ExceptionHandlingMiddleware**

```csharp
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Icarus.UnitTests.Observability;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, JsonElement Cuerpo)> Ejecutar(Exception ex)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw ex,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.Invoke(contexto);

        contexto.Response.Body.Seek(0, SeekOrigin.Begin);
        var cuerpo = await JsonDocument.ParseAsync(contexto.Response.Body);
        return (contexto.Response.StatusCode, cuerpo.RootElement.Clone());
    }

    [Fact]
    public async Task NotFoundExceptionDevuelve404()
    {
        var (status, _) = await Ejecutar(new NotFoundException("Cliente", Guid.NewGuid()));
        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task ConflictExceptionDevuelve409()
    {
        var (status, _) = await Ejecutar(new ConflictException("conflicto"));
        Assert.Equal(StatusCodes.Status409Conflict, status);
    }

    [Fact]
    public async Task ValidationExceptionDevuelve400ConErroresPorCampo()
    {
        var fallas = new[] { new ValidationFailure("Nombre", "obligatorio") };
        var (status, cuerpo) = await Ejecutar(new ValidationException(fallas));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.True(cuerpo.GetProperty("errors").TryGetProperty("Nombre", out _));
    }

    [Fact]
    public async Task ExcepcionNoControladaDevuelve500GenericoSinDetalleTecnico()
    {
        var (status, cuerpo) = await Ejecutar(new InvalidOperationException("detalle interno sensible"));
        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("detalle interno sensible", cuerpo.ToString());
    }

    [Fact]
    public async Task TodaRespuestaIncluyeCorrelationId()
    {
        var (_, cuerpo) = await Ejecutar(new ConflictException("conflicto"));
        Assert.True(cuerpo.TryGetProperty("correlationId", out _));
    }
}
```

- [x] **Step 3: Correr y verificar rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: FALLA de compilación (los middlewares no existen).

- [x] **Step 4: Implementación**

`CorrelationIdMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Icarus.BuildingBlocks.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string Header = "X-Correlation-ID";
    private const int LongitudMaxima = 64;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var entrante = context.Request.Headers[Header].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(entrante) || entrante.Length > LongitudMaxima
            ? Guid.NewGuid().ToString()
            : entrante;

        context.Items[Header] = correlationId;
        context.Response.Headers[Header] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

`ExceptionHandlingMiddleware.cs`:

```csharp
using FluentValidation;
using Icarus.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Icarus.BuildingBlocks.Observability;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await EscribirProblemDetails(context, ex);
        }
    }

    private async Task EscribirProblemDetails(HttpContext context, Exception ex)
    {
        var (status, titulo) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflicto con el estado actual"),
            ValidationException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            DomainException => (StatusCodes.Status400BadRequest, "Error de negocio"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno"),
        };

        if (status >= StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Error no controlado");
        else
            _logger.LogWarning(ex, "Error de negocio ({Tipo})", ex.GetType().Name);

        var correlationId = context.Items[CorrelationIdMiddleware.Header] as string
            ?? context.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Instance = context.Request.Path,
        };
        problem.Extensions["correlationId"] = correlationId;

        if (ex is ValidationException validacion)
        {
            problem.Extensions["errors"] = validacion.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
```

`ObservabilityExtensions.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;

namespace Icarus.BuildingBlocks.Observability;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservabilidad(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, config) => config
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Aplicacion", "Icarus")
            .Enrich.WithProperty("Entorno", context.HostingEnvironment.EnvironmentName)
            .WriteTo.Console(new CompactJsonFormatter()));

        return builder;
    }
}
```

- [x] **Step 5: Correr y verificar verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: PASS (16 tests acumulados).

- [x] **Step 6: Commit**

```bash
git add Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability Icarus/tests/Icarus.UnitTests
./verify.sh
git commit -m "feat: observabilidad transversal (Serilog, correlation ID, exception middleware)"
```

---

### Task 6: Host — composición, CurrentUserService y health check

**Files:**
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs` (reemplazo completo)
- Create: `Icarus/src/Host/Icarus.Host/Servicios/CurrentUserService.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Host/CurrentUserServiceTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/HealthEndpointTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/CorrelationIdIntegrationTests.cs`

**Interfaces:**
- Consumes: `AddObservabilidad`, `CorrelationIdMiddleware`,
  `ExceptionHandlingMiddleware` (task 5); `ICurrentUser` (task 3).
- Produces: `GET /health` → 200 `{ "estado": "ok" }`; respuesta con header
  `X-Correlation-ID`; `CurrentUserService` (implementación de `ICurrentUser`)
  registrada como scoped. Los planes 2 y 3 agregan endpoints al Host.

- [x] **Step 1: Test unitario en rojo — CurrentUserService**

```csharp
using System.Security.Claims;
using Icarus.Host.Servicios;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Host;

public class CurrentUserServiceTests
{
    private static CurrentUserService CrearServicio(params Claim[] claims)
    {
        var contexto = new DefaultHttpContext();
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba"));
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(contexto);
        return new CurrentUserService(accessor);
    }

    [Fact]
    public void UsuarioAutenticadoExponeSusClaims()
    {
        var usuarioId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var servicio = CrearServicio(
            new Claim("sub", usuarioId.ToString()),
            new Claim("rol", "Cliente"),
            new Claim("clienteId", clienteId.ToString()));

        Assert.True(servicio.EstaAutenticado);
        Assert.Equal(usuarioId, servicio.UsuarioId);
        Assert.Equal("Cliente", servicio.Rol);
        Assert.Equal(clienteId, servicio.ClienteId);
    }

    [Fact]
    public void SinClaimsDevuelveNulosYNoAutenticado()
    {
        var servicio = CrearServicio();
        Assert.False(servicio.EstaAutenticado);
        Assert.Null(servicio.UsuarioId);
        Assert.Null(servicio.ClienteId);
    }
}
```

- [x] **Step 2: Tests de integración en rojo — health y correlation ID**

`HealthEndpointTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Icarus.IntegrationTests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task HealthResponde200()
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);
    }
}
```

`CorrelationIdIntegrationTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Icarus.IntegrationTests;

public class CorrelationIdIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorrelationIdIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task TodaRespuestaLlevaCorrelationId()
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.True(respuesta.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task CorrelationIdEntranteSePropagaALaRespuesta()
    {
        var cliente = _factory.CreateClient();
        var pedido = new HttpRequestMessage(HttpMethod.Get, "/health");
        pedido.Headers.Add("X-Correlation-ID", "trace-prueba-1");
        var respuesta = await cliente.SendAsync(pedido);
        Assert.Equal("trace-prueba-1",
            respuesta.Headers.GetValues("X-Correlation-ID").Single());
    }
}
```

- [x] **Step 3: Correr y verificar rojo**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --nologo`
Expected: FALLA de compilación (`CurrentUserService` no existe y `Program` no
es accesible para `WebApplicationFactory<Program>`).

- [x] **Step 4: Implementación**

`Servicios/CurrentUserService.cs`:

```csharp
using System.Security.Claims;
using Icarus.BuildingBlocks.Application;

namespace Icarus.Host.Servicios;

public sealed class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Usuario => _accessor.HttpContext?.User;

    public bool EstaAutenticado => Usuario?.Identity?.IsAuthenticated ?? false;

    public Guid? UsuarioId =>
        Guid.TryParse(Usuario?.FindFirstValue("sub"), out var id) ? id : null;

    public string? Rol => Usuario?.FindFirstValue("rol");

    public Guid? ClienteId =>
        Guid.TryParse(Usuario?.FindFirstValue("clienteId"), out var id) ? id : null;
}
```

`Program.cs` (reemplazo completo):

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Observability;
using Icarus.Host.Servicios;

var builder = WebApplicationBuilder.CreateBuilder(args);
builder.AddObservabilidad();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

app.Run();

// Expone Program a WebApplicationFactory en los tests de integración.
public partial class Program { }
```

- [x] **Step 5: Correr y verificar verde**

Run: `dotnet test Icarus/Icarus.sln --nologo`
Expected: PASS toda la solución.

- [x] **Step 6: Commit**

```bash
git add Icarus/src/Host Icarus/tests
./verify.sh
git commit -m "feat: Host con health check, correlation ID y CurrentUserService"
```

---

### Task 7: Puerta de calidad, CI y Seq opcional

**Files:**
- Modify: `quality/verify.mjs`
- Modify: `docs/ai/PUERTA_CALIDAD.md`
- Modify: `.github/workflows/ci.yml`
- Create: `docker-compose.dev.yml`

**Interfaces:**
- Consumes: solución completa del task 6.
- Produces: `./verify.sh` ejecuta también `dotnet build` y `dotnet test`; CI con
  job `backend`. Los planes siguientes asumen que el gate corre los tests .NET.

- [x] **Step 1: Extender los gates en `quality/verify.mjs`**

Agregar a `GATES`, después del gate de Enlaces:

```javascript
  { nombre: 'Backend build', comando: 'dotnet', args: ['build', 'Icarus/Icarus.sln', '--nologo'] },
  { nombre: 'Backend tests', comando: 'dotnet', args: ['test', 'Icarus/Icarus.sln', '--nologo', '--no-build'] },
```

Y actualizar el comentario de cabecera: la línea «Ningún gate necesita Docker
ni el SDK de .NET» pasa a «Los gates de backend necesitan el SDK de .NET 10;
los tests de integración con Testcontainers (planes 2-3) necesitan Docker».

- [x] **Step 2: Actualizar `docs/ai/PUERTA_CALIDAD.md`**

Agregar los dos gates a la lista de vigentes, con su racional (el compilador y
los tests son la verificación mecánica de las reglas de arquitectura y del
dominio). Sin cifras que caduquen.

- [x] **Step 3: Correr la puerta completa**

Run: `./verify.sh`
Expected: verde, incluyendo `Backend build` y `Backend tests`.

- [x] **Step 4: Agregar el job backend a `.github/workflows/ci.yml`**

```yaml
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7
      - name: Setup .NET
        uses: actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1 # v5
        with:
          global-json-file: Icarus/global.json
      - name: Build
        run: dotnet build Icarus/Icarus.sln --nologo
      - name: Tests
        run: dotnet test Icarus/Icarus.sln --nologo --no-build
```

El SHA corresponde a la v5 vigente al 2026-08-12 (resuelto con
`gh api repos/actions/setup-dotnet/git/refs/tags/v5 --jq .object.sha`); las
actions se pinnean por SHA, no por tag.

- [x] **Step 5: Crear `docker-compose.dev.yml` (Seq opcional)**

```yaml
# Servicios opcionales de desarrollo. Uso: docker compose -f docker-compose.dev.yml up -d
# Seq queda en http://localhost:5341
services:
  seq:
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: "Y"
    ports:
      - "5341:80"
    volumes:
      - seq-data:/data

volumes:
  seq-data:
```

La conexión de Serilog a Seq queda para el plan 2, cuando el Host tenga
`appsettings.Development.json` con configuración real.

- [x] **Step 6: Commit, push y verificación del CI**

```bash
git add quality/verify.mjs docs/ai/PUERTA_CALIDAD.md .github/workflows/ci.yml docker-compose.dev.yml
./verify.sh
git commit -m "ci: gates de backend en la puerta y job backend en CI"
git push origin develop
gh run list --limit 2
```

Expected: el run del push muestra los jobs `calidad` y `backend` en verde.

---

## Desviaciones registradas al ejecutar (2026-08-12)

Todas verificadas en rojo/verde y detalladas en los mensajes de commit:

1. **Serilog pineado en 4.3.0** (no 4.1.0): `Serilog.AspNetCore` 10.0.0 exige
   Serilog >= 4.3.0; con CPM + transitive pinning el pin 4.1.0 da NU1109.
   Aprobada por el usuario antes de aplicarla.
2. **Solución en formato `.sln` clásico**: el SDK 10 genera `.slnx` por defecto;
   se recreó con `dotnet new sln --format sln` para respetar las rutas del plan.
3. **Constructores estándar de excepción** en `DomainException`,
   `NotFoundException` y `ConflictException` (Roslynator RCS1194 con
   `TreatWarningsAsErrors`).
4. **Pragma S2094** en los `Marcador.cs` temporales (SonarAnalyzer no admite
   clases vacías). Se borran con los marcadores en los planes 2 y 3.
5. **FluentValidation agregado a `BuildingBlocks.Observability`**: el plan lo usa
   en `ExceptionHandlingMiddleware` pero no lo listaba en el Task 1.
6. **Program.cs**: `await app.RunAsync()` (S6966) y constructor `protected` en
   la clase `Program` parcial (S1118).
7. **`EstaAutenticado` exige identidad autenticada y claim `sub` válido**: el
   test del plan crea la identidad con `authenticationType`, lo que da
   `IsAuthenticated = true` aun sin claims.
8. **Test de la puerta actualizado**: el invariante «todo gate corre con node»
   pasó a admitir los gates `dotnet` de backend.

Nota operativa: los templates xUnit del SDK escriben `Version=` en los
`.csproj`; hay que quitarlos a mano por CPM (previsto en el Task 1, Step 7).
