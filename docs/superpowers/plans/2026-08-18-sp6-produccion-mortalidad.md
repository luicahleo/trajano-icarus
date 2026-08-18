# SP6 — Producción diaria y Mortalidad — Plan de implementación

> Estado de cierre: Tasks 1–10 ejecutadas. La puerta completa (`verify.ps1`) y
> el push quedan a cargo del usuario; la suite completa de integración quedó
> bloqueada por la salida del contenedor Testcontainers.MsSql (código 17).

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Añadir al módulo `GestionAvicola` (ya existente tras SP5) los agregados `RegistroProduccion` (recogidas de huevos) y `RegistroMortalidad`, el ajuste de inventario del galpón por mortalidad y la consulta de eficiencia diaria con umbral del 70 %.

**Architecture:** Mismo bounded context y schema (`gestion_avicola`). Dos agregados raíz propios con `ClienteId` desnormalizado y filtros globales de tenant + soft delete. La mortalidad descuenta el inventario del galpón en el handler (con `Decidir` en el registro de vuelo). La eficiencia es siempre derivada: se calcula al consultar con snapshots de gallinas vivas por evento. La fecha la fija el servidor; edición solo el mismo día (sellado de dominio).

**Tech Stack:** .NET 10, EF Core 10 (SqlServer), MediatR, FluentValidation, xUnit + NSubstitute, Testcontainers.MsSql (Docker corriendo).

**Spec:** `docs/superpowers/specs/2026-08-18-sp6-produccion-mortalidad-design.md` (leerlo primero; es la fuente de las reglas).

## Global Constraints

- Idioma: identificadores, mensajes y tests en español correcto; UTF-8 sin BOM; nunca mojibake.
- Anti-PII: errores genéricos; nunca nombres en logs. Anti-enumeración: id de otro tenant = 404 (`NotFoundException`).
- TDD: cada test se ve en rojo antes de implementar (para tipos nuevos, el rojo es el error de compilación). Tests en español estilo frase.
- `TreatWarningsAsErrors=true` con Roslynator y SonarAnalyzer: build sin warnings.
- `sealed` en todo; `sealed record` para commands/queries/DTOs; `sealed class` para entidades, handlers, validators y repositorios.
- La constante 30 vive UNA vez en `Maple.HuevosPorMaple`; el umbral 70 en `EficienciaPostura.UmbralDescarte`. Nunca como números sueltos (salvo en tests, al verificar la constante).
- `IUnitOfWork` genérica NO se usa en este módulo: siempre `IUnidadTrabajoGestionAvicola`.
- Filtros globales EF **sin `.Value`** sobre el `Guid?` del tenant.
- Fechas de negocio con `DateOnly`/`TimeOnly` y `DateTime.UtcNow`. Nada de `DateTime.Now`.
- La `Fecha` de un registro la fija el handler (servidor), nunca viene del cliente. El dominio solo rechaza fechas futuras; el sellado (edición solo el mismo día) sí es invariante de dominio.
- Soft delete en todo: `DELETE` = `Desactivar()`, nunca borrado físico.
- No modificar `Program.cs` (ensamblados ya registrados en SP5) ni crear políticas (ya se generan para todos los valores de `Funcionalidades`).
- Rutas relativas a la raíz del repo (`Trajano-Icarus/`). Docker corriendo para integración.
- Commits por tarea con el test dirigido en verde; puerta completa (`./verify.ps1`) antes del push final. Prohibido `--no-verify`; si un gate falla, se arregla el contenido.

---

### Task 1: Constantes de dominio (`Maple`, `EficienciaPostura`)

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/EficienciaPosturaTests.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/Maple.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EficienciaPostura.cs`

**Interfaces:**
- Produces: `Maple.HuevosPorMaple` (const int = 30); `EficienciaPostura.UmbralDescarte` (const decimal = 70), `EficienciaPostura.Calcular(int totalVendible, int gallinasVivas) → decimal`, `EficienciaPostura.EstaBajoUmbral(decimal eficiencia) → bool`. Las usan las Tasks 2, 4 y 6.

- [ ] **Step 1: Escribir el test que falla**

```csharp
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class EficienciaPosturaTests
{
    [Fact]
    public void MapleSonTreintaHuevos() => Assert.Equal(30, Maple.HuevosPorMaple);

    [Fact]
    public void UmbralDeDescarteEsSetenta() => Assert.Equal(70m, EficienciaPostura.UmbralDescarte);

    [Fact]
    public void CalcularDevuelvePorcentajeConDosDecimales() =>
        Assert.Equal(80.81m, EficienciaPostura.Calcular(2400, 2970));

    [Fact]
    public void CalcularSinGallinasDevuelveCero() =>
        Assert.Equal(0m, EficienciaPostura.Calcular(2400, 0));

    [Fact]
    public void EstaBajoUmbralComparaContraElSetenta()
    {
        Assert.True(EficienciaPostura.EstaBajoUmbral(69.99m));
        Assert.False(EficienciaPostura.EstaBajoUmbral(70m));
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EficienciaPosturaTests"`
Expected: FALLA la compilación (los tipos no existen).

- [ ] **Step 3: Implementación mínima**

`Maple.cs`:

```csharp
namespace Icarus.GestionAvicola.Domain;

// Unidad estándar de empaque (glosario): un maple son 30 huevos. La constante
// se declara una sola vez aquí; nunca repetir el 30 como número suelto.
public static class Maple
{
    public const int HuevosPorMaple = 30;
}
```

`EficienciaPostura.cs`:

```csharp
namespace Icarus.GestionAvicola.Domain;

// Métrica central del negocio (glosario): huevos vendibles del día ÷ gallinas
// vivas. Siempre derivada, nunca persistida. Si cae bajo el umbral, el lote
// se considera para descarte (venta como carne).
public static class EficienciaPostura
{
    public const decimal UmbralDescarte = 70m;

    public static decimal Calcular(int totalVendible, int gallinasVivas) =>
        gallinasVivas <= 0
            ? 0m
            : Math.Round(totalVendible * 100m / gallinasVivas, 2);

    public static bool EstaBajoUmbral(decimal eficiencia) => eficiencia < UmbralDescarte;
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EficienciaPosturaTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/Maple.cs Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EficienciaPostura.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/EficienciaPosturaTests.cs
git commit -m "feat(avicola): constantes de dominio maple y eficiencia de postura"
```

---

### Task 2: Agregado `RegistroProduccion` (dominio, TDD)

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/RegistroProduccionTests.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/RegistroProduccion.cs`

**Interfaces:**
- Consumes: `AggregateRoot`, `ReglaNegocioException`, `Maple` (Task 1).
- Produces: `RegistroProduccion(Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora, int cantidadMaples, int unidadesIncompletas, int maplesDescarte, int unidadesDescarte, int gallinasVivas, Guid? idempotencyKey)` (+ sobrecarga con `Guid id` primero para tests), propiedades `GalponId`, `ClienteId`, `Fecha`, `Hora`, `CantidadMaples`, `UnidadesIncompletas`, `MaplesDescarte`, `UnidadesDescarte`, `GallinasVivas`, `IdempotencyKey`, `EstaActivo`; métodos `TotalHuevosVendibles() → int`, `TotalHuevosDescarte() → int`, `Editar(int cantidadMaples, int unidadesIncompletas, int maplesDescarte, int unidadesDescarte, TimeOnly hora)`, `Desactivar()`.

- [ ] **Step 1: Escribir el test que falla**

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class RegistroProduccionTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Ayer = Hoy.AddDays(-1);
    private static readonly TimeOnly DiezAm = new(10, 0, 0);

    private static RegistroProduccion RecogidaDeHoy() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Hoy, DiezAm, 10, 5, 1, 2, 4800, null);

    [Fact]
    public void CtorValidoAsignaYNaceActivo()
    {
        var recogida = RecogidaDeHoy();
        Assert.Equal(10, recogida.CantidadMaples);
        Assert.Equal(5, recogida.UnidadesIncompletas);
        Assert.Equal(1, recogida.MaplesDescarte);
        Assert.Equal(2, recogida.UnidadesDescarte);
        Assert.Equal(4800, recogida.GallinasVivas);
        Assert.True(recogida.EstaActivo);
    }

    [Fact]
    public void TotalesUsanLaConstanteDelMaple()
    {
        var recogida = RecogidaDeHoy();
        Assert.Equal(305, recogida.TotalHuevosVendibles());
        Assert.Equal(32, recogida.TotalHuevosDescarte());
    }

    [Fact]
    public void CtorFechaFuturaLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new RegistroProduccion(Guid.NewGuid(), Guid.NewGuid(), Hoy.AddDays(1), DiezAm,
                1, 0, 0, 0, 100, null));
        Assert.Equal("La fecha de la recogida no puede ser futura.", ex.Message);
    }

    [Fact]
    public void CtorSueltosInvalidosLanzaReglaNegocio()
    {
        Assert.Throws<ReglaNegocioException>(() =>
            new RegistroProduccion(Guid.NewGuid(), Guid.NewGuid(), Hoy, DiezAm,
                1, 30, 0, 0, 100, null));
        Assert.Throws<ReglaNegocioException>(() =>
            new RegistroProduccion(Guid.NewGuid(), Guid.NewGuid(), Hoy, DiezAm,
                1, 0, 0, 30, 100, null));
    }

    [Fact]
    public void EditarDeHoyActualizaCantidadesYHora()
    {
        var recogida = RecogidaDeHoy();
        recogida.Editar(12, 0, 2, 0, new TimeOnly(14, 30, 0));
        Assert.Equal(12, recogida.CantidadMaples);
        Assert.Equal(0, recogida.UnidadesIncompletas);
        Assert.Equal(2, recogida.MaplesDescarte);
        Assert.Equal(new TimeOnly(14, 30, 0), recogida.Hora);
    }

    [Fact]
    public void EditarDeAyerLanzaSellado()
    {
        var recogida = new RegistroProduccion(Guid.NewGuid(), Guid.NewGuid(), Ayer, DiezAm,
            10, 5, 0, 0, 4800, null);
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            recogida.Editar(12, 0, 0, 0, DiezAm));
        Assert.Equal("El registro está sellado: solo se puede corregir el mismo día.", ex.Message);
    }

    [Fact]
    public void DesactivarDeAyerLanzaSellado()
    {
        var recogida = new RegistroProduccion(Guid.NewGuid(), Guid.NewGuid(), Ayer, DiezAm,
            10, 5, 0, 0, 4800, null);
        Assert.Throws<ReglaNegocioException>(() => recogida.Desactivar());
        Assert.True(recogida.EstaActivo);
    }

    [Fact]
    public void DesactivarDeHoyMarcaInactivoSinBorrar()
    {
        var recogida = RecogidaDeHoy();
        recogida.Desactivar();
        Assert.False(recogida.EstaActivo);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~RegistroProduccionTests"`
Expected: FALLA la compilación (el tipo no existe).

- [ ] **Step 3: Implementación mínima**

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Recogida de huevos (spec SP6): el trabajador recoge cuando puede a lo largo
// del día (no hay turnos); cada recogida es un registro propio y el total del
// día es la suma. La Fecha la fija el servidor y es inmutable; la corrección
// solo es posible el mismo día (sellado de dominio). GallinasVivas congela la
// población del momento: la eficiencia histórica nunca cambia. El descarte se
// cuenta como el huevo bueno (maples + sueltos) pero no entra al vendible.
public sealed class RegistroProduccion : AggregateRoot
{
    private RegistroProduccion()
    {
    }

    public RegistroProduccion(
        Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora,
        int cantidadMaples, int unidadesIncompletas, int maplesDescarte, int unidadesDescarte,
        int gallinasVivas, Guid? idempotencyKey)
    {
        if (galponId == Guid.Empty)
            throw new ReglaNegocioException("La recogida debe pertenecer a un galpón.");
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La recogida debe pertenecer a un cliente.");
        if (fecha > Hoy())
            throw new ReglaNegocioException("La fecha de la recogida no puede ser futura.");
        ValidarCantidades(cantidadMaples, unidadesIncompletas, maplesDescarte, unidadesDescarte);
        if (gallinasVivas < 0)
            throw new ReglaNegocioException("Las gallinas vivas no pueden ser negativas.");

        GalponId = galponId;
        ClienteId = clienteId;
        Fecha = fecha;
        Hora = hora;
        CantidadMaples = cantidadMaples;
        UnidadesIncompletas = unidadesIncompletas;
        MaplesDescarte = maplesDescarte;
        UnidadesDescarte = unidadesDescarte;
        GallinasVivas = gallinasVivas;
        IdempotencyKey = idempotencyKey;
        EstaActivo = true;
    }

    // Para tests que necesitan ids fijos.
    public RegistroProduccion(
        Guid id, Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora,
        int cantidadMaples, int unidadesIncompletas, int maplesDescarte, int unidadesDescarte,
        int gallinasVivas, Guid? idempotencyKey)
        : this(galponId, clienteId, fecha, hora, cantidadMaples, unidadesIncompletas,
            maplesDescarte, unidadesDescarte, gallinasVivas, idempotencyKey) => Id = id;

    public Guid GalponId { get; private set; }

    public Guid ClienteId { get; private set; }

    public DateOnly Fecha { get; private set; }

    public TimeOnly Hora { get; private set; }

    public int CantidadMaples { get; private set; }

    public int UnidadesIncompletas { get; private set; }

    public int MaplesDescarte { get; private set; }

    public int UnidadesDescarte { get; private set; }

    public int GallinasVivas { get; private set; }

    public Guid? IdempotencyKey { get; private set; }

    public bool EstaActivo { get; private set; }

    public int TotalHuevosVendibles() =>
        CantidadMaples * Maple.HuevosPorMaple + UnidadesIncompletas;

    public int TotalHuevosDescarte() =>
        MaplesDescarte * Maple.HuevosPorMaple + UnidadesDescarte;

    // Corrección del mismo día (spec SP6): pasada la medianoche, sellado.
    public void Editar(
        int cantidadMaples, int unidadesIncompletas, int maplesDescarte, int unidadesDescarte,
        TimeOnly hora)
    {
        ExigirDiaAbierto();
        ValidarCantidades(cantidadMaples, unidadesIncompletas, maplesDescarte, unidadesDescarte);

        CantidadMaples = cantidadMaples;
        UnidadesIncompletas = unidadesIncompletas;
        MaplesDescarte = maplesDescarte;
        UnidadesDescarte = unidadesDescarte;
        Hora = hora;
    }

    // Soft delete (glosario): nunca borrado físico; también solo el mismo día.
    public void Desactivar()
    {
        ExigirDiaAbierto();
        EstaActivo = false;
    }

    private void ExigirDiaAbierto()
    {
        if (Fecha < Hoy())
            throw new ReglaNegocioException(
                "El registro está sellado: solo se puede corregir el mismo día.");
    }

    private static void ValidarCantidades(
        int cantidadMaples, int unidadesIncompletas, int maplesDescarte, int unidadesDescarte)
    {
        if (cantidadMaples < 0 || maplesDescarte < 0)
            throw new ReglaNegocioException("Los maples no pueden ser negativos.");
        if (unidadesIncompletas < 0 || unidadesIncompletas >= Maple.HuevosPorMaple
            || unidadesDescarte < 0 || unidadesDescarte >= Maple.HuevosPorMaple)
            throw new ReglaNegocioException("Las unidades sueltas deben estar entre 0 y 29.");
    }

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~RegistroProduccionTests"`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/RegistroProduccion.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/RegistroProduccionTests.cs
git commit -m "feat(avicola): agregado RegistroProduccion con descarte y sellado del dia"
```

---

### Task 3: Agregado `RegistroMortalidad` (dominio, TDD)

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/RegistroMortalidadTests.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/RegistroMortalidad.cs`

**Interfaces:**
- Produces: `RegistroMortalidad(Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora, int cantidadMuertas, int gallinasVivas, Guid? idempotencyKey)` (+ sobrecarga con `Guid id` primero), propiedades `GalponId`, `ClienteId`, `Fecha`, `Hora`, `CantidadMuertas`, `GallinasVivas`, `IdempotencyKey`, `EstaActivo`; métodos `Editar(int cantidadMuertas, TimeOnly hora, int gallinasVivas)`, `Desactivar()`. Ojo: en mortalidad `GallinasVivas` SÍ se actualiza al editar (el snapshot refleja el inventario tras el ajuste); en producción es inmutable.

- [ ] **Step 1: Escribir el test que falla**

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class RegistroMortalidadTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Ayer = Hoy.AddDays(-1);
    private static readonly TimeOnly SeisAm = new(6, 0, 0);

    [Fact]
    public void CtorValidoAsignaYNaceActivo()
    {
        var registro = new RegistroMortalidad(Guid.NewGuid(), Guid.NewGuid(), Hoy, SeisAm,
            15, 4785, null);
        Assert.Equal(15, registro.CantidadMuertas);
        Assert.Equal(4785, registro.GallinasVivas);
        Assert.True(registro.EstaActivo);
    }

    [Fact]
    public void CtorSinMuertasLanzaReglaNegocio()
    {
        var ex = Assert.Throws<ReglaNegocioException>(() =>
            new RegistroMortalidad(Guid.NewGuid(), Guid.NewGuid(), Hoy, SeisAm, 0, 4800, null));
        Assert.Equal("La cantidad de muertas debe ser mayor que cero.", ex.Message);
    }

    [Fact]
    public void CtorFechaFuturaLanzaReglaNegocio()
    {
        Assert.Throws<ReglaNegocioException>(() =>
            new RegistroMortalidad(Guid.NewGuid(), Guid.NewGuid(), Hoy.AddDays(1), SeisAm,
                5, 4800, null));
    }

    [Fact]
    public void EditarDeHoyActualizaCantidadHoraYSnapshot()
    {
        var registro = new RegistroMortalidad(Guid.NewGuid(), Guid.NewGuid(), Hoy, SeisAm,
            15, 4785, null);
        registro.Editar(20, new TimeOnly(7, 0, 0), 4780);
        Assert.Equal(20, registro.CantidadMuertas);
        Assert.Equal(new TimeOnly(7, 0, 0), registro.Hora);
        Assert.Equal(4780, registro.GallinasVivas);
    }

    [Fact]
    public void EditarDeAyerLanzaSellado()
    {
        var registro = new RegistroMortalidad(Guid.NewGuid(), Guid.NewGuid(), Ayer, SeisAm,
            15, 4785, null);
        var ex = Assert.Throws<ReglaNegocioException>(() => registro.Editar(20, SeisAm, 4780));
        Assert.Equal("El registro está sellado: solo se puede corregir el mismo día.", ex.Message);
    }

    [Fact]
    public void DesactivarDeAyerLanzaSellado()
    {
        var registro = new RegistroMortalidad(Guid.NewGuid(), Guid.NewGuid(), Ayer, SeisAm,
            15, 4785, null);
        Assert.Throws<ReglaNegocioException>(() => registro.Desactivar());
        Assert.True(registro.EstaActivo);
    }

    [Fact]
    public void DesactivarDeHoyMarcaInactivoSinBorrar()
    {
        var registro = new RegistroMortalidad(Guid.NewGuid(), Guid.NewGuid(), Hoy, SeisAm,
            15, 4785, null);
        registro.Desactivar();
        Assert.False(registro.EstaActivo);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~RegistroMortalidadTests"`
Expected: FALLA la compilación (el tipo no existe).

- [ ] **Step 3: Implementación mínima**

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Evento de bajas (spec SP6): fecha, hora y cuántas gallinas murieron. Sin
// causa ni observaciones: no hacen falta. GallinasVivas es el snapshot del
// inventario del galpón DESPUÉS de descontar; se actualiza al editar la
// cantidad (mismo día), porque el ajuste de inventario también cambia. El
// sellado es el mismo que en producción: corrección solo el mismo día.
public sealed class RegistroMortalidad : AggregateRoot
{
    private RegistroMortalidad()
    {
    }

    public RegistroMortalidad(
        Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora,
        int cantidadMuertas, int gallinasVivas, Guid? idempotencyKey)
    {
        if (galponId == Guid.Empty)
            throw new ReglaNegocioException("La mortalidad debe pertenecer a un galpón.");
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La mortalidad debe pertenecer a un cliente.");
        if (fecha > Hoy())
            throw new ReglaNegocioException("La fecha de la mortalidad no puede ser futura.");
        if (cantidadMuertas <= 0)
            throw new ReglaNegocioException("La cantidad de muertas debe ser mayor que cero.");
        if (gallinasVivas < 0)
            throw new ReglaNegocioException("Las gallinas vivas no pueden ser negativas.");

        GalponId = galponId;
        ClienteId = clienteId;
        Fecha = fecha;
        Hora = hora;
        CantidadMuertas = cantidadMuertas;
        GallinasVivas = gallinasVivas;
        IdempotencyKey = idempotencyKey;
        EstaActivo = true;
    }

    // Para tests que necesitan ids fijos.
    public RegistroMortalidad(
        Guid id, Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora,
        int cantidadMuertas, int gallinasVivas, Guid? idempotencyKey)
        : this(galponId, clienteId, fecha, hora, cantidadMuertas, gallinasVivas, idempotencyKey)
        => Id = id;

    public Guid GalponId { get; private set; }

    public Guid ClienteId { get; private set; }

    public DateOnly Fecha { get; private set; }

    public TimeOnly Hora { get; private set; }

    public int CantidadMuertas { get; private set; }

    public int GallinasVivas { get; private set; }

    public Guid? IdempotencyKey { get; private set; }

    public bool EstaActivo { get; private set; }

    // Corrección del mismo día (spec SP6). El handler repone la cantidad
    // anterior, descuenta la nueva y pasa el snapshot resultante.
    public void Editar(int cantidadMuertas, TimeOnly hora, int gallinasVivas)
    {
        ExigirDiaAbierto();
        if (cantidadMuertas <= 0)
            throw new ReglaNegocioException("La cantidad de muertas debe ser mayor que cero.");
        if (gallinasVivas < 0)
            throw new ReglaNegocioException("Las gallinas vivas no pueden ser negativas.");

        CantidadMuertas = cantidadMuertas;
        Hora = hora;
        GallinasVivas = gallinasVivas;
    }

    // Soft delete (glosario): nunca borrado físico; también solo el mismo día.
    // El handler repone las muertas al inventario del galpón.
    public void Desactivar()
    {
        ExigirDiaAbierto();
        EstaActivo = false;
    }

    private void ExigirDiaAbierto()
    {
        if (Fecha < Hoy())
            throw new ReglaNegocioException(
                "El registro está sellado: solo se puede corregir el mismo día.");
    }

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~RegistroMortalidadTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/RegistroMortalidad.cs Icarus/tests/Icarus.UnitTests/GestionAvicola/RegistroMortalidadTests.cs
git commit -m "feat(avicola): agregado RegistroMortalidad con sellado del dia"
```

---

### Task 4: Application de Producción (handlers, TDD)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Produccion/IRepositorioProduccion.cs`
- Create: `.../Produccion/RegistrarProduccionCommand.cs`, `RegistrarProduccionHandler.cs`, `RegistrarProduccionValidator.cs`
- Create: `.../Produccion/EditarProduccionCommand.cs`, `EditarProduccionHandler.cs`, `EditarProduccionValidator.cs`
- Create: `.../Produccion/DesactivarProduccionCommand.cs`, `DesactivarProduccionHandler.cs`
- Create: `.../Produccion/ListarProduccionPorDiaQuery.cs`, `ListarProduccionPorDiaHandler.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/RegistrarProduccionHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/EditarYDesactivarProduccionHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ListarProduccionPorDiaHandlerTests.cs`

**Interfaces:**
- Consumes: `RegistroProduccion`, `Galpon` (Domain); `IRepositorioGalpones` (SP5, tiene `ObtenerPorIdAsync`); `IUnidadTrabajoGestionAvicola` (SP5); `NotFoundException`.
- Produces:
  - `IRepositorioProduccion`: `void Agregar(RegistroProduccion)`, `Task<RegistroProduccion?> ObtenerPorIdAsync(Guid, CancellationToken)`, `Task<IReadOnlyList<RegistroProduccion>> ListarPorDiaAsync(Guid galponId, DateOnly fecha, CancellationToken)`, `Task<IReadOnlyList<RegistroProduccion>> ListarPorRangoAsync(Guid galponId, DateOnly desde, DateOnly hasta, CancellationToken)`, `Task<RegistroProduccion?> ObtenerPorIdempotencyKeyAsync(Guid galponId, Guid idempotencyKey, CancellationToken)`.
  - `RecogidaResumen(Guid Id, DateOnly Fecha, TimeOnly Hora, int CantidadMaples, int UnidadesIncompletas, int MaplesDescarte, int UnidadesDescarte, int GallinasVivas, int TotalVendible, int TotalDescarte)`.
  - `ProduccionDiaResumen(Guid GalponId, DateOnly Fecha, IReadOnlyList<RecogidaResumen> Recogidas, int TotalMaples, int TotalUnidadesIncompletas, int TotalVendible, int TotalMaplesDescarte, int TotalUnidadesDescarte, int TotalDescarte)`.
  - Commands: `RegistrarProduccionCommand(Guid GalponId, TimeOnly? Hora, int CantidadMaples, int UnidadesIncompletas, int MaplesDescarte, int UnidadesDescarte, Guid? IdempotencyKey) : IRequest<Guid>`, `EditarProduccionCommand(Guid ProduccionId, TimeOnly Hora, int CantidadMaples, int UnidadesIncompletas, int MaplesDescarte, int UnidadesDescarte) : IRequest`, `DesactivarProduccionCommand(Guid ProduccionId) : IRequest`; query: `ListarProduccionPorDiaQuery(Guid GalponId, DateOnly? Fecha) : IRequest<ProduccionDiaResumen>`.

- [ ] **Step 1: Escribir los tests que fallan**

`RegistrarProduccionHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class RegistrarProduccionHandlerTests
{
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRepositorioProduccion _produccion = Substitute.For<IRepositorioProduccion>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly RegistrarProduccionHandler _handler;
    private readonly Galpon _galpon;

    public RegistrarProduccionHandlerTests()
    {
        _galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30), null);
        _handler = new RegistrarProduccionHandler(_galpones, _produccion, _unidadTrabajo);
    }

    private RegistrarProduccionCommand ComandoValido(Guid? idempotencyKey = null) =>
        new(_galpon.Id, new TimeOnly(10, 0, 0), 10, 5, 1, 2, idempotencyKey);

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Galpon?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(ComandoValido(), CancellationToken.None));

        Assert.Equal("Galpon no encontrado.", ex.Message);
        _produccion.DidNotReceive().Agregar(Arg.Any<RegistroProduccion>());
    }

    [Fact]
    public async Task RegistraConFechaDelServidorSnapshotYHoraDelCliente()
    {
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);

        var id = await _handler.Handle(ComandoValido(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _produccion.Received(1).Agregar(Arg.Is<RegistroProduccion>(r =>
            r.GalponId == _galpon.Id && r.ClienteId == _galpon.ClienteId
            && r.Fecha == DateOnly.FromDateTime(DateTime.UtcNow)
            && r.Hora == new TimeOnly(10, 0, 0)
            && r.CantidadMaples == 10 && r.UnidadesIncompletas == 5
            && r.MaplesDescarte == 1 && r.UnidadesDescarte == 2
            && r.GallinasVivas == 4800));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HoraNulaUsaLaDelServidor()
    {
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);

        await _handler.Handle(
            new RegistrarProduccionCommand(_galpon.Id, null, 10, 5, 0, 0, null),
            CancellationToken.None);

        _produccion.Received(1).Agregar(Arg.Is<RegistroProduccion>(r =>
            r.Hora != default));
    }

    [Fact]
    public async Task IdempotencyKeyRepetidaDevuelveLaExistenteSinDuplicar()
    {
        var key = Guid.NewGuid();
        var existente = new RegistroProduccion(_galpon.Id, _galpon.ClienteId,
            DateOnly.FromDateTime(DateTime.UtcNow), new TimeOnly(9, 0, 0), 10, 5, 0, 0, 4800, key);
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);
        _produccion.ObtenerPorIdempotencyKeyAsync(_galpon.Id, key, Arg.Any<CancellationToken>())
            .Returns(existente);

        var id = await _handler.Handle(ComandoValido(key), CancellationToken.None);

        Assert.Equal(existente.Id, id);
        _produccion.DidNotReceive().Agregar(Arg.Any<RegistroProduccion>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

`EditarYDesactivarProduccionHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class EditarYDesactivarProduccionHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioProduccion _produccion = Substitute.For<IRepositorioProduccion>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();

    [Fact]
    public async Task EditarInexistenteLanzaNotFound()
    {
        _produccion.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((RegistroProduccion?)null);
        var handler = new EditarProduccionHandler(_produccion, _unidadTrabajo);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new EditarProduccionCommand(
                Guid.NewGuid(), new TimeOnly(11, 0, 0), 12, 0, 0, 0), CancellationToken.None));

        Assert.Equal("Registro de producción no encontrado.", ex.Message);
    }

    [Fact]
    public async Task EditarDeHoyEditaYGuarda()
    {
        var recogida = new RegistroProduccion(Guid.NewGuid(), Guid.NewGuid(), Hoy,
            new TimeOnly(10, 0, 0), 10, 5, 1, 2, 4800, null);
        _produccion.ObtenerPorIdAsync(recogida.Id, Arg.Any<CancellationToken>()).Returns(recogida);
        var handler = new EditarProduccionHandler(_produccion, _unidadTrabajo);

        await handler.Handle(new EditarProduccionCommand(
            recogida.Id, new TimeOnly(11, 0, 0), 12, 0, 0, 0), CancellationToken.None);

        Assert.Equal(12, recogida.CantidadMaples);
        Assert.Equal(0, recogida.UnidadesIncompletas);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesactivarInexistenteLanzaNotFound()
    {
        _produccion.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((RegistroProduccion?)null);
        var handler = new DesactivarProduccionHandler(_produccion, _unidadTrabajo);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new DesactivarProduccionCommand(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("Registro de producción no encontrado.", ex.Message);
    }

    [Fact]
    public async Task DesactivarDeHoyDesactivaYGuarda()
    {
        var recogida = new RegistroProduccion(Guid.NewGuid(), Guid.NewGuid(), Hoy,
            new TimeOnly(10, 0, 0), 10, 5, 0, 0, 4800, null);
        _produccion.ObtenerPorIdAsync(recogida.Id, Arg.Any<CancellationToken>()).Returns(recogida);
        var handler = new DesactivarProduccionHandler(_produccion, _unidadTrabajo);

        await handler.Handle(new DesactivarProduccionCommand(recogida.Id), CancellationToken.None);

        Assert.False(recogida.EstaActivo);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

`ListarProduccionPorDiaHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ListarProduccionPorDiaHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRepositorioProduccion _produccion = Substitute.For<IRepositorioProduccion>();
    private readonly ListarProduccionPorDiaHandler _handler;
    private readonly Galpon _galpon;

    public ListarProduccionPorDiaHandlerTests()
    {
        _galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, Hoy.AddDays(-30), null);
        _handler = new ListarProduccionPorDiaHandler(_galpones, _produccion);
    }

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Galpon?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new ListarProduccionPorDiaQuery(Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task AgregaLosTotalesDelDiaIncluidoElDescarte()
    {
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);
        _produccion.ListarPorDiaAsync(_galpon.Id, Hoy, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroProduccion>
            {
                new(_galpon.Id, _galpon.ClienteId, Hoy, new TimeOnly(10, 0, 0), 10, 5, 1, 0, 4800, null),
                new(_galpon.Id, _galpon.ClienteId, Hoy, new TimeOnly(14, 0, 0), 20, 10, 0, 3, 4795, null),
            });

        var resumen = await _handler.Handle(
            new ListarProduccionPorDiaQuery(_galpon.Id, Hoy), CancellationToken.None);

        Assert.Equal(2, resumen.Recogidas.Count);
        Assert.Equal(30, resumen.TotalMaples);
        Assert.Equal(15, resumen.TotalUnidadesIncompletas);
        Assert.Equal(915, resumen.TotalVendible);
        Assert.Equal(1, resumen.TotalMaplesDescarte);
        Assert.Equal(3, resumen.TotalUnidadesDescarte);
        Assert.Equal(33, resumen.TotalDescarte);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~Produccion"`
Expected: FALLA la compilación (no existen los tipos de Application).

- [ ] **Step 3: Implementación mínima**

`Produccion/IRepositorioProduccion.cs`:

```csharp
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Produccion;

public interface IRepositorioProduccion
{
    void Agregar(RegistroProduccion registro);

    // Respeta los filtros globales (tenant + activos): id ajeno o inactivo
    // devuelve null, igual que uno inexistente (anti-enumeración).
    Task<RegistroProduccion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Respeta los filtros globales. Recogidas activas del galpón en un día,
    // ordenadas por hora.
    Task<IReadOnlyList<RegistroProduccion>> ListarPorDiaAsync(
        Guid galponId, DateOnly fecha, CancellationToken cancellationToken = default);

    // Respeta los filtros globales. Recogidas activas del galpón en un rango
    // (consulta de eficiencia).
    Task<IReadOnlyList<RegistroProduccion>> ListarPorRangoAsync(
        Guid galponId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default);

    // Respeta los filtros globales: idempotencia de los reintentos de la PWA
    // offline (spec SP6).
    Task<RegistroProduccion?> ObtenerPorIdempotencyKeyAsync(
        Guid galponId, Guid idempotencyKey, CancellationToken cancellationToken = default);
}

public sealed record RecogidaResumen(
    Guid Id, DateOnly Fecha, TimeOnly Hora, int CantidadMaples, int UnidadesIncompletas,
    int MaplesDescarte, int UnidadesDescarte, int GallinasVivas, int TotalVendible, int TotalDescarte);

public sealed record ProduccionDiaResumen(
    Guid GalponId, DateOnly Fecha, IReadOnlyList<RecogidaResumen> Recogidas,
    int TotalMaples, int TotalUnidadesIncompletas, int TotalVendible,
    int TotalMaplesDescarte, int TotalUnidadesDescarte, int TotalDescarte);
```

`Produccion/RegistrarProduccionCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Produccion;

// Registro de vuelo (spec SP6): las cantidades no son PII. La Fecha la fija
// el servidor; la Hora real de la recogida la manda el cliente (opcional).
public sealed record RegistrarProduccionCommand(
    Guid GalponId, TimeOnly? Hora, int CantidadMaples, int UnidadesIncompletas,
    int MaplesDescarte, int UnidadesDescarte, Guid? IdempotencyKey)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.produccion.registrar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["CantidadMaples"] = DatoRegistroVuelo.Entero,
            ["UnidadesIncompletas"] = DatoRegistroVuelo.Entero,
            ["MaplesDescarte"] = DatoRegistroVuelo.Entero,
            ["UnidadesDescarte"] = DatoRegistroVuelo.Entero,
        });
}
```

`Produccion/RegistrarProduccionHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Produccion;

public sealed class RegistrarProduccionHandler : IRequestHandler<RegistrarProduccionCommand, Guid>
{
    private readonly IRepositorioGalpones _galpones;
    private readonly IRepositorioProduccion _produccion;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public RegistrarProduccionHandler(
        IRepositorioGalpones galpones, IRepositorioProduccion produccion,
        IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _galpones = galpones;
        _produccion = produccion;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task<Guid> Handle(
        RegistrarProduccionCommand request, CancellationToken cancellationToken)
    {
        // El galpón debe existir, estar activo y ser del tenant actual; el
        // filtro global lo garantiza y un id ajeno da 404 (anti-enumeración).
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);

        // Idempotencia (spec SP6): un reintento de la PWA offline devuelve la
        // recogida ya registrada en vez de duplicarla.
        if (request.IdempotencyKey is Guid key)
        {
            var existente = await _produccion.ObtenerPorIdempotencyKeyAsync(
                galpon.Id, key, cancellationToken);
            if (existente is not null)
                return existente.Id;
        }

        // La Fecha la fija el servidor (ventana "solo hoy", spec SP6); la Hora
        // real de la recogida la manda el cliente. GallinasVivas congela la
        // población del momento para la eficiencia histórica.
        var registro = new RegistroProduccion(
            galpon.Id, galpon.ClienteId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            request.Hora ?? TimeOnly.FromDateTime(DateTime.UtcNow),
            request.CantidadMaples, request.UnidadesIncompletas,
            request.MaplesDescarte, request.UnidadesDescarte,
            galpon.GallinasActuales, request.IdempotencyKey);
        _produccion.Agregar(registro);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
        return registro.Id;
    }
}
```

`Produccion/RegistrarProduccionValidator.cs`:

```csharp
using FluentValidation;
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Produccion;

public sealed class RegistrarProduccionValidator : AbstractValidator<RegistrarProduccionCommand>
{
    public RegistrarProduccionValidator()
    {
        RuleFor(c => c.CantidadMaples).GreaterThanOrEqualTo(0);
        RuleFor(c => c.MaplesDescarte).GreaterThanOrEqualTo(0);
        RuleFor(c => c.UnidadesIncompletas).InclusiveBetween(0, Maple.HuevosPorMaple - 1);
        RuleFor(c => c.UnidadesDescarte).InclusiveBetween(0, Maple.HuevosPorMaple - 1);
    }
}
```

`Produccion/EditarProduccionCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Produccion;

public sealed record EditarProduccionCommand(
    Guid ProduccionId, TimeOnly Hora, int CantidadMaples, int UnidadesIncompletas,
    int MaplesDescarte, int UnidadesDescarte) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.produccion.editar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["CantidadMaples"] = DatoRegistroVuelo.Entero,
            ["UnidadesIncompletas"] = DatoRegistroVuelo.Entero,
            ["MaplesDescarte"] = DatoRegistroVuelo.Entero,
            ["UnidadesDescarte"] = DatoRegistroVuelo.Entero,
        });
}
```

`Produccion/EditarProduccionHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Produccion;

public sealed class EditarProduccionHandler : IRequestHandler<EditarProduccionCommand>
{
    private readonly IRepositorioProduccion _produccion;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public EditarProduccionHandler(
        IRepositorioProduccion produccion, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _produccion = produccion;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(EditarProduccionCommand request, CancellationToken cancellationToken)
    {
        var recogida = await _produccion.ObtenerPorIdAsync(request.ProduccionId, cancellationToken)
            ?? throw new NotFoundException("Registro de producción", request.ProduccionId);

        // El sellado (solo el mismo día) es invariante de dominio.
        recogida.Editar(
            request.CantidadMaples, request.UnidadesIncompletas,
            request.MaplesDescarte, request.UnidadesDescarte, request.Hora);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

`Produccion/EditarProduccionValidator.cs`:

```csharp
using FluentValidation;
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Produccion;

public sealed class EditarProduccionValidator : AbstractValidator<EditarProduccionCommand>
{
    public EditarProduccionValidator()
    {
        RuleFor(c => c.CantidadMaples).GreaterThanOrEqualTo(0);
        RuleFor(c => c.MaplesDescarte).GreaterThanOrEqualTo(0);
        RuleFor(c => c.UnidadesIncompletas).InclusiveBetween(0, Maple.HuevosPorMaple - 1);
        RuleFor(c => c.UnidadesDescarte).InclusiveBetween(0, Maple.HuevosPorMaple - 1);
    }
}
```

`Produccion/DesactivarProduccionCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Produccion;

public sealed record DesactivarProduccionCommand(Guid ProduccionId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.produccion.desactivar", new Dictionary<string, DatoRegistroVuelo>());
}
```

`Produccion/DesactivarProduccionHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Produccion;

public sealed class DesactivarProduccionHandler : IRequestHandler<DesactivarProduccionCommand>
{
    private readonly IRepositorioProduccion _produccion;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public DesactivarProduccionHandler(
        IRepositorioProduccion produccion, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _produccion = produccion;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(DesactivarProduccionCommand request, CancellationToken cancellationToken)
    {
        var recogida = await _produccion.ObtenerPorIdAsync(request.ProduccionId, cancellationToken)
            ?? throw new NotFoundException("Registro de producción", request.ProduccionId);

        recogida.Desactivar();
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

`Produccion/ListarProduccionPorDiaQuery.cs`:

```csharp
using MediatR;

namespace Icarus.GestionAvicola.Application.Produccion;

// Fecha nula = hoy. Leer días pasados está permitido; lo sellado es editar.
public sealed record ListarProduccionPorDiaQuery(Guid GalponId, DateOnly? Fecha)
    : IRequest<ProduccionDiaResumen>;
```

`Produccion/ListarProduccionPorDiaHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using MediatR;

namespace Icarus.GestionAvicola.Application.Produccion;

public sealed class ListarProduccionPorDiaHandler
    : IRequestHandler<ListarProduccionPorDiaQuery, ProduccionDiaResumen>
{
    private readonly IRepositorioGalpones _galpones;
    private readonly IRepositorioProduccion _produccion;

    public ListarProduccionPorDiaHandler(
        IRepositorioGalpones galpones, IRepositorioProduccion produccion)
    {
        _galpones = galpones;
        _produccion = produccion;
    }

    public async Task<ProduccionDiaResumen> Handle(
        ListarProduccionPorDiaQuery request, CancellationToken cancellationToken)
    {
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);

        var fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var recogidas = await _produccion.ListarPorDiaAsync(galpon.Id, fecha, cancellationToken);

        return new ProduccionDiaResumen(
            galpon.Id, fecha,
            recogidas.Select(r => new RecogidaResumen(
                r.Id, r.Fecha, r.Hora, r.CantidadMaples, r.UnidadesIncompletas,
                r.MaplesDescarte, r.UnidadesDescarte, r.GallinasVivas,
                r.TotalHuevosVendibles(), r.TotalHuevosDescarte())).ToList(),
            recogidas.Sum(r => r.CantidadMaples),
            recogidas.Sum(r => r.UnidadesIncompletas),
            recogidas.Sum(r => r.TotalHuevosVendibles()),
            recogidas.Sum(r => r.MaplesDescarte),
            recogidas.Sum(r => r.UnidadesDescarte),
            recogidas.Sum(r => r.TotalHuevosDescarte()));
    }
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~Produccion"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Produccion Icarus/tests/Icarus.UnitTests/GestionAvicola
git commit -m "feat(avicola): handlers de produccion con idempotencia y totales del dia"
```

---

### Task 5: Application de Mortalidad (handlers, TDD)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Mortalidad/IRepositorioMortalidad.cs`
- Create: `.../Mortalidad/RegistrarMortalidadCommand.cs`, `RegistrarMortalidadHandler.cs`, `RegistrarMortalidadValidator.cs`
- Create: `.../Mortalidad/EditarMortalidadCommand.cs`, `EditarMortalidadHandler.cs`, `EditarMortalidadValidator.cs`
- Create: `.../Mortalidad/DesactivarMortalidadCommand.cs`, `DesactivarMortalidadHandler.cs`
- Create: `.../Mortalidad/ListarMortalidadPorDiaQuery.cs`, `ListarMortalidadPorDiaHandler.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/RegistrarMortalidadHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/EditarYDesactivarMortalidadHandlerTests.cs`

**Interfaces:**
- Consumes: `RegistroMortalidad`, `Galpon.AjustarInventarioGallinas(int)` (SP5), `IRepositorioGalpones`, `IUnidadTrabajoGestionAvicola`, `IRegistroVuelo.Decidir(string operacion, string codigo, string resultado, IReadOnlyDictionary<string, object?>? campos)`.
- Produces:
  - `IRepositorioMortalidad`: `void Agregar(RegistroMortalidad)`, `Task<RegistroMortalidad?> ObtenerPorIdAsync(Guid, CancellationToken)`, `Task<IReadOnlyList<RegistroMortalidad>> ListarPorDiaAsync(Guid galponId, DateOnly fecha, CancellationToken)`, `Task<IReadOnlyList<RegistroMortalidad>> ListarPorRangoAsync(Guid galponId, DateOnly desde, DateOnly hasta, CancellationToken)`, `Task<RegistroMortalidad?> ObtenerPorIdempotencyKeyAsync(Guid galponId, Guid idempotencyKey, CancellationToken)`.
  - `MortalidadResumen(Guid Id, DateOnly Fecha, TimeOnly Hora, int CantidadMuertas, int GallinasVivas)`.
  - `MortalidadDiaResumen(Guid GalponId, DateOnly Fecha, IReadOnlyList<MortalidadResumen> Registros, int TotalMuertas)`.
  - Commands: `RegistrarMortalidadCommand(Guid GalponId, TimeOnly? Hora, int CantidadMuertas, Guid? IdempotencyKey) : IRequest<Guid>`, `EditarMortalidadCommand(Guid MortalidadId, TimeOnly Hora, int CantidadMuertas) : IRequest`, `DesactivarMortalidadCommand(Guid MortalidadId) : IRequest`; query: `ListarMortalidadPorDiaQuery(Guid GalponId, DateOnly? Fecha) : IRequest<MortalidadDiaResumen>`.

- [ ] **Step 1: Escribir los tests que fallan**

`RegistrarMortalidadHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class RegistrarMortalidadHandlerTests
{
    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRepositorioMortalidad _mortalidad = Substitute.For<IRepositorioMortalidad>();
    private readonly IRegistroVuelo _registroVuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly RegistrarMortalidadHandler _handler;
    private readonly Galpon _galpon;

    public RegistrarMortalidadHandlerTests()
    {
        _galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30), null);
        _handler = new RegistrarMortalidadHandler(
            _galpones, _mortalidad, _registroVuelo, _unidadTrabajo);
    }

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Galpon?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new RegistrarMortalidadCommand(
                Guid.NewGuid(), null, 10, null), CancellationToken.None));
        _mortalidad.DidNotReceive().Agregar(Arg.Any<RegistroMortalidad>());
    }

    [Fact]
    public async Task DescuentaElInventarioRegistraConSnapshotYNarra()
    {
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);

        var id = await _handler.Handle(new RegistrarMortalidadCommand(
            _galpon.Id, new TimeOnly(6, 0, 0), 15, null), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(4785, _galpon.GallinasActuales);
        _mortalidad.Received(1).Agregar(Arg.Is<RegistroMortalidad>(r =>
            r.CantidadMuertas == 15 && r.GallinasVivas == 4785
            && r.Fecha == DateOnly.FromDateTime(DateTime.UtcNow)));
        _registroVuelo.Received(1).Decidir(
            "avicola.mortalidad.registrar", "ajuste_inventario", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(d => (int)d["GallinasVivas"] == 4785));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MuertasMayorQueInventarioLanzaReglaNegocio()
    {
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);

        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            _handler.Handle(new RegistrarMortalidadCommand(
                _galpon.Id, null, 4801, null), CancellationToken.None));
        _mortalidad.DidNotReceive().Agregar(Arg.Any<RegistroMortalidad>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IdempotencyKeyRepetidaNoDuplicaNiDescuentaDosVeces()
    {
        var key = Guid.NewGuid();
        var existente = new RegistroMortalidad(_galpon.Id, _galpon.ClienteId,
            DateOnly.FromDateTime(DateTime.UtcNow), new TimeOnly(6, 0, 0), 15, 4785, key);
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);
        _mortalidad.ObtenerPorIdempotencyKeyAsync(_galpon.Id, key, Arg.Any<CancellationToken>())
            .Returns(existente);

        var id = await _handler.Handle(new RegistrarMortalidadCommand(
            _galpon.Id, null, 15, key), CancellationToken.None);

        Assert.Equal(existente.Id, id);
        Assert.Equal(4800, _galpon.GallinasActuales);
        _mortalidad.DidNotReceive().Agregar(Arg.Any<RegistroMortalidad>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

`EditarYDesactivarMortalidadHandlerTests.cs`:

```csharp
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class EditarYDesactivarMortalidadHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRepositorioMortalidad _mortalidad = Substitute.For<IRepositorioMortalidad>();
    private readonly IRegistroVuelo _registroVuelo = Substitute.For<IRegistroVuelo>();
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();

    private Galpon GalponCon(int gallinas) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, gallinas, Hoy.AddDays(-30), null);

    [Fact]
    public async Task EditarReponeLaAnteriorYDescuentaLaNueva()
    {
        // El galpón quedó en 4785 tras registrar 15 muertas; se corrige a 20.
        var galpon = GalponCon(4785);
        var registro = new RegistroMortalidad(galpon.Id, galpon.ClienteId, Hoy,
            new TimeOnly(6, 0, 0), 15, 4785, null);
        _mortalidad.ObtenerPorIdAsync(registro.Id, Arg.Any<CancellationToken>()).Returns(registro);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        var handler = new EditarMortalidadHandler(
            _mortalidad, _galpones, _registroVuelo, _unidadTrabajo);

        await handler.Handle(new EditarMortalidadCommand(
            registro.Id, new TimeOnly(6, 30, 0), 20), CancellationToken.None);

        Assert.Equal(4780, galpon.GallinasActuales);
        Assert.Equal(20, registro.CantidadMuertas);
        Assert.Equal(4780, registro.GallinasVivas);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditarInexistenteLanzaNotFound()
    {
        _mortalidad.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((RegistroMortalidad?)null);
        var handler = new EditarMortalidadHandler(
            _mortalidad, _galpones, _registroVuelo, _unidadTrabajo);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new EditarMortalidadCommand(
                Guid.NewGuid(), new TimeOnly(6, 0, 0), 10), CancellationToken.None));

        Assert.Equal("Registro de mortalidad no encontrado.", ex.Message);
    }

    [Fact]
    public async Task DesactivarReponeLasMuertasAlInventario()
    {
        var galpon = GalponCon(4785);
        var registro = new RegistroMortalidad(galpon.Id, galpon.ClienteId, Hoy,
            new TimeOnly(6, 0, 0), 15, 4785, null);
        _mortalidad.ObtenerPorIdAsync(registro.Id, Arg.Any<CancellationToken>()).Returns(registro);
        _galpones.ObtenerPorIdAsync(galpon.Id, Arg.Any<CancellationToken>()).Returns(galpon);
        var handler = new DesactivarMortalidadHandler(
            _mortalidad, _galpones, _registroVuelo, _unidadTrabajo);

        await handler.Handle(new DesactivarMortalidadCommand(registro.Id), CancellationToken.None);

        Assert.False(registro.EstaActivo);
        Assert.Equal(4800, galpon.GallinasActuales);
        _registroVuelo.Received(1).Decidir(
            "avicola.mortalidad.desactivar", "ajuste_inventario", "aplicada",
            Arg.Is<IReadOnlyDictionary<string, object?>>(d => (int)d["GallinasVivas"] == 4800));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesactivarSelladaNoTocaElInventario()
    {
        var galpon = GalponCon(4785);
        var registro = new RegistroMortalidad(galpon.Id, galpon.ClienteId, Hoy.AddDays(-1),
            new TimeOnly(6, 0, 0), 15, 4785, null);
        _mortalidad.ObtenerPorIdAsync(registro.Id, Arg.Any<CancellationToken>()).Returns(registro);
        var handler = new DesactivarMortalidadHandler(
            _mortalidad, _galpones, _registroVuelo, _unidadTrabajo);

        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            handler.Handle(new DesactivarMortalidadCommand(registro.Id), CancellationToken.None));

        Assert.Equal(4785, galpon.GallinasActuales);
        await _galpones.DidNotReceive().ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~Mortalidad"`
Expected: FALLA la compilación.

- [ ] **Step 3: Implementación mínima**

`Mortalidad/IRepositorioMortalidad.cs`:

```csharp
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public interface IRepositorioMortalidad
{
    void Agregar(RegistroMortalidad registro);

    // Respeta los filtros globales (tenant + activos): id ajeno o inactivo
    // devuelve null, igual que uno inexistente (anti-enumeración).
    Task<RegistroMortalidad?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Respeta los filtros globales. Eventos activos del galpón en un día,
    // ordenados por hora.
    Task<IReadOnlyList<RegistroMortalidad>> ListarPorDiaAsync(
        Guid galponId, DateOnly fecha, CancellationToken cancellationToken = default);

    // Respeta los filtros globales. Eventos activos del galpón en un rango
    // (consulta de eficiencia).
    Task<IReadOnlyList<RegistroMortalidad>> ListarPorRangoAsync(
        Guid galponId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default);

    // Respeta los filtros globales: idempotencia de los reintentos de la PWA
    // offline (spec SP6).
    Task<RegistroMortalidad?> ObtenerPorIdempotencyKeyAsync(
        Guid galponId, Guid idempotencyKey, CancellationToken cancellationToken = default);
}

public sealed record MortalidadResumen(
    Guid Id, DateOnly Fecha, TimeOnly Hora, int CantidadMuertas, int GallinasVivas);

public sealed record MortalidadDiaResumen(
    Guid GalponId, DateOnly Fecha, IReadOnlyList<MortalidadResumen> Registros, int TotalMuertas);
```

`Mortalidad/RegistrarMortalidadCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public sealed record RegistrarMortalidadCommand(
    Guid GalponId, TimeOnly? Hora, int CantidadMuertas, Guid? IdempotencyKey)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.mortalidad.registrar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["CantidadMuertas"] = DatoRegistroVuelo.Entero,
            ["GallinasVivas"] = DatoRegistroVuelo.Entero,
        });
}
```

`Mortalidad/RegistrarMortalidadHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public sealed class RegistrarMortalidadHandler : IRequestHandler<RegistrarMortalidadCommand, Guid>
{
    private readonly IRepositorioGalpones _galpones;
    private readonly IRepositorioMortalidad _mortalidad;
    private readonly IRegistroVuelo _registroVuelo;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public RegistrarMortalidadHandler(
        IRepositorioGalpones galpones, IRepositorioMortalidad mortalidad,
        IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _galpones = galpones;
        _mortalidad = mortalidad;
        _registroVuelo = registroVuelo;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task<Guid> Handle(
        RegistrarMortalidadCommand request, CancellationToken cancellationToken)
    {
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);

        // Idempotencia (spec SP6): un reintento no descuenta dos veces.
        if (request.IdempotencyKey is Guid key)
        {
            var existente = await _mortalidad.ObtenerPorIdempotencyKeyAsync(
                galpon.Id, key, cancellationToken);
            if (existente is not null)
                return existente.Id;
        }

        // Descuenta el inventario del galpón: la invariante de SP5 (0 <=
        // actuales <= capacidad) rechaza muertas > actuales. El snapshot del
        // registro es el inventario resultante.
        galpon.AjustarInventarioGallinas(galpon.GallinasActuales - request.CantidadMuertas);
        _registroVuelo.Decidir(
            "avicola.mortalidad.registrar", "ajuste_inventario", "aplicada",
            new Dictionary<string, object?> { ["GallinasVivas"] = galpon.GallinasActuales });

        var registro = new RegistroMortalidad(
            galpon.Id, galpon.ClienteId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            request.Hora ?? TimeOnly.FromDateTime(DateTime.UtcNow),
            request.CantidadMuertas, galpon.GallinasActuales, request.IdempotencyKey);
        _mortalidad.Agregar(registro);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
        return registro.Id;
    }
}
```

`Mortalidad/RegistrarMortalidadValidator.cs`:

```csharp
using FluentValidation;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public sealed class RegistrarMortalidadValidator : AbstractValidator<RegistrarMortalidadCommand>
{
    public RegistrarMortalidadValidator() => RuleFor(c => c.CantidadMuertas).GreaterThan(0);
}
```

`Mortalidad/EditarMortalidadCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public sealed record EditarMortalidadCommand(Guid MortalidadId, TimeOnly Hora, int CantidadMuertas)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.mortalidad.editar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["CantidadMuertas"] = DatoRegistroVuelo.Entero,
            ["GallinasVivas"] = DatoRegistroVuelo.Entero,
        });
}
```

`Mortalidad/EditarMortalidadHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using MediatR;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public sealed class EditarMortalidadHandler : IRequestHandler<EditarMortalidadCommand>
{
    private readonly IRepositorioMortalidad _mortalidad;
    private readonly IRepositorioGalpones _galpones;
    private readonly IRegistroVuelo _registroVuelo;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public EditarMortalidadHandler(
        IRepositorioMortalidad mortalidad, IRepositorioGalpones galpones,
        IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _mortalidad = mortalidad;
        _galpones = galpones;
        _registroVuelo = registroVuelo;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(EditarMortalidadCommand request, CancellationToken cancellationToken)
    {
        var registro = await _mortalidad.ObtenerPorIdAsync(request.MortalidadId, cancellationToken)
            ?? throw new NotFoundException("Registro de mortalidad", request.MortalidadId);
        var galpon = await _galpones.ObtenerPorIdAsync(registro.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", registro.GalponId);

        // Mismo día (el sellado lo fuerza el dominio en Editar): repone la
        // cantidad anterior y descuenta la nueva. Si Editar lanza por sellado,
        // no se guarda nada.
        galpon.AjustarInventarioGallinas(
            galpon.GallinasActuales + registro.CantidadMuertas - request.CantidadMuertas);
        _registroVuelo.Decidir(
            "avicola.mortalidad.editar", "ajuste_inventario", "aplicada",
            new Dictionary<string, object?> { ["GallinasVivas"] = galpon.GallinasActuales });

        registro.Editar(request.CantidadMuertas, request.Hora, galpon.GallinasActuales);
        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

`Mortalidad/EditarMortalidadValidator.cs`:

```csharp
using FluentValidation;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public sealed class EditarMortalidadValidator : AbstractValidator<EditarMortalidadCommand>
{
    public EditarMortalidadValidator() => RuleFor(c => c.CantidadMuertas).GreaterThan(0);
}
```

`Mortalidad/DesactivarMortalidadCommand.cs`:

```csharp
using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public sealed record DesactivarMortalidadCommand(Guid MortalidadId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.mortalidad.desactivar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["CantidadMuertas"] = DatoRegistroVuelo.Entero,
            ["GallinasVivas"] = DatoRegistroVuelo.Entero,
        });
}
```

`Mortalidad/DesactivarMortalidadHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using MediatR;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public sealed class DesactivarMortalidadHandler : IRequestHandler<DesactivarMortalidadCommand>
{
    private readonly IRepositorioMortalidad _mortalidad;
    private readonly IRepositorioGalpones _galpones;
    private readonly IRegistroVuelo _registroVuelo;
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo;

    public DesactivarMortalidadHandler(
        IRepositorioMortalidad mortalidad, IRepositorioGalpones galpones,
        IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    {
        _mortalidad = mortalidad;
        _galpones = galpones;
        _registroVuelo = registroVuelo;
        _unidadTrabajo = unidadTrabajo;
    }

    public async Task Handle(DesactivarMortalidadCommand request, CancellationToken cancellationToken)
    {
        var registro = await _mortalidad.ObtenerPorIdAsync(request.MortalidadId, cancellationToken)
            ?? throw new NotFoundException("Registro de mortalidad", request.MortalidadId);

        // Primero el sellado: si el día está cerrado, no se toca el inventario.
        registro.Desactivar();

        var galpon = await _galpones.ObtenerPorIdAsync(registro.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", registro.GalponId);

        // Se reponen las muertas al inventario del galpón (spec SP6).
        galpon.AjustarInventarioGallinas(galpon.GallinasActuales + registro.CantidadMuertas);
        _registroVuelo.Decidir(
            "avicola.mortalidad.desactivar", "ajuste_inventario", "aplicada",
            new Dictionary<string, object?> { ["GallinasVivas"] = galpon.GallinasActuales });

        await _unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
```

`Mortalidad/ListarMortalidadPorDiaQuery.cs`:

```csharp
using MediatR;

namespace Icarus.GestionAvicola.Application.Mortalidad;

// Fecha nula = hoy. Leer días pasados está permitido; lo sellado es editar.
public sealed record ListarMortalidadPorDiaQuery(Guid GalponId, DateOnly? Fecha)
    : IRequest<MortalidadDiaResumen>;
```

`Mortalidad/ListarMortalidadPorDiaHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using MediatR;

namespace Icarus.GestionAvicola.Application.Mortalidad;

public sealed class ListarMortalidadPorDiaHandler
    : IRequestHandler<ListarMortalidadPorDiaQuery, MortalidadDiaResumen>
{
    private readonly IRepositorioGalpones _galpones;
    private readonly IRepositorioMortalidad _mortalidad;

    public ListarMortalidadPorDiaHandler(
        IRepositorioGalpones galpones, IRepositorioMortalidad mortalidad)
    {
        _galpones = galpones;
        _mortalidad = mortalidad;
    }

    public async Task<MortalidadDiaResumen> Handle(
        ListarMortalidadPorDiaQuery request, CancellationToken cancellationToken)
    {
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);

        var fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var registros = await _mortalidad.ListarPorDiaAsync(galpon.Id, fecha, cancellationToken);

        return new MortalidadDiaResumen(
            galpon.Id, fecha,
            registros.Select(r => new MortalidadResumen(
                r.Id, r.Fecha, r.Hora, r.CantidadMuertas, r.GallinasVivas)).ToList(),
            registros.Sum(r => r.CantidadMuertas));
    }
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~Mortalidad"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Mortalidad Icarus/tests/Icarus.UnitTests/GestionAvicola
git commit -m "feat(avicola): handlers de mortalidad con ajuste de inventario e idempotencia"
```

---

### Task 6: Consulta de eficiencia por galpón (TDD)

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Eficiencia/ObtenerEficienciaGalponQuery.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Eficiencia/ObtenerEficienciaGalponHandler.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Eficiencia/ObtenerEficienciaGalponValidator.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerEficienciaGalponHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioProduccion.ListarPorRangoAsync`, `IRepositorioMortalidad.ListarPorRangoAsync`, `IRepositorioGalpones.ObtenerPorIdAsync`, `EficienciaPostura` (Task 1), `TotalHuevosVendibles()`/`TotalHuevosDescarte()` (Task 2).
- Produces: `EficienciaDiaResumen(DateOnly Fecha, int TotalMaples, int TotalUnidadesIncompletas, int TotalVendible, int TotalMaplesDescarte, int TotalUnidadesDescarte, int TotalDescarte, int GallinasVivas, decimal Eficiencia, bool BajoUmbral)`, `EficienciaGalponResumen(Guid GalponId, DateOnly Desde, DateOnly Hasta, IReadOnlyList<EficienciaDiaResumen> Dias)`, `ObtenerEficienciaGalponQuery(Guid GalponId, DateOnly? Desde, DateOnly? Hasta) : IRequest<EficienciaGalponResumen>`.

- [ ] **Step 1: Escribir el test que falla**

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Eficiencia;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ObtenerEficienciaGalponHandlerTests
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly IRepositorioGalpones _galpones = Substitute.For<IRepositorioGalpones>();
    private readonly IRepositorioProduccion _produccion = Substitute.For<IRepositorioProduccion>();
    private readonly IRepositorioMortalidad _mortalidad = Substitute.For<IRepositorioMortalidad>();
    private readonly ObtenerEficienciaGalponHandler _handler;
    private readonly Galpon _galpon;

    public ObtenerEficienciaGalponHandlerTests()
    {
        _galpon = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 2970, Hoy.AddDays(-30), null);
        _handler = new ObtenerEficienciaGalponHandler(_galpones, _produccion, _mortalidad);
        _galpones.ObtenerPorIdAsync(_galpon.Id, Arg.Any<CancellationToken>()).Returns(_galpon);
    }

    [Fact]
    public async Task GalponInexistenteLanzaNotFound()
    {
        _galpones.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Galpon?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(new ObtenerEficienciaGalponQuery(
                Guid.NewGuid(), null, null), CancellationToken.None));
    }

    [Fact]
    public async Task EficienciaDelDiaUsaElSnapshotDelUltimoEvento()
    {
        var dia = Hoy.AddDays(-2);
        _produccion.ListarPorRangoAsync(_galpon.Id, dia, Hoy, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroProduccion>
            {
                new(_galpon.Id, _galpon.ClienteId, dia, new TimeOnly(10, 0, 0), 50, 0, 0, 0, 3000, null),
                new(_galpon.Id, _galpon.ClienteId, dia, new TimeOnly(14, 0, 0), 30, 0, 2, 5, 3000, null),
            });
        // La mortalidad de las 18:00 es el último evento del día: su snapshot
        // (2970) es la población del día.
        _mortalidad.ListarPorRangoAsync(_galpon.Id, dia, Hoy, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroMortalidad>
            {
                new(_galpon.Id, _galpon.ClienteId, dia, new TimeOnly(18, 0, 0), 30, 2970, null),
            });

        var resumen = await _handler.Handle(
            new ObtenerEficienciaGalponQuery(_galpon.Id, dia, Hoy), CancellationToken.None);

        var eficienciaDia = Assert.Single(resumen.Dias, d => d.Fecha == dia);
        Assert.Equal(2400, eficienciaDia.TotalVendible);
        Assert.Equal(65, eficienciaDia.TotalDescarte);
        Assert.Equal(2970, eficienciaDia.GallinasVivas);
        Assert.Equal(80.81m, eficienciaDia.Eficiencia);
        Assert.False(eficienciaDia.BajoUmbral);
    }

    [Fact]
    public async Task BajoElSetentaMarcaBajoUmbral()
    {
        var dia = Hoy.AddDays(-1);
        _produccion.ListarPorRangoAsync(_galpon.Id, dia, Hoy, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroProduccion>
            {
                new(_galpon.Id, _galpon.ClienteId, dia, new TimeOnly(10, 0, 0), 60, 0, 0, 0, 3000, null),
            });
        _mortalidad.ListarPorRangoAsync(_galpon.Id, dia, Hoy, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroMortalidad>());

        var resumen = await _handler.Handle(
            new ObtenerEficienciaGalponQuery(_galpon.Id, dia, Hoy), CancellationToken.None);

        var eficienciaDia = Assert.Single(resumen.Dias, d => d.Fecha == dia);
        Assert.Equal(60m, eficienciaDia.Eficiencia);
        Assert.True(eficienciaDia.BajoUmbral);
    }

    [Fact]
    public async Task ElDescarteNoInflaLaEficiencia()
    {
        _produccion.ListarPorRangoAsync(_galpon.Id, Hoy.AddDays(-6), Hoy, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroProduccion>
            {
                new(_galpon.Id, _galpon.ClienteId, Hoy, new TimeOnly(10, 0, 0), 0, 0, 5, 0, 2970, null),
            });
        _mortalidad.ListarPorRangoAsync(_galpon.Id, Hoy.AddDays(-6), Hoy, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroMortalidad>());

        var resumen = await _handler.Handle(
            new ObtenerEficienciaGalponQuery(_galpon.Id, null, null), CancellationToken.None);

        var eficienciaDia = Assert.Single(resumen.Dias, d => d.Fecha == Hoy);
        Assert.Equal(0, eficienciaDia.TotalVendible);
        Assert.Equal(150, eficienciaDia.TotalDescarte);
        Assert.Equal(0m, eficienciaDia.Eficiencia);
    }

    [Fact]
    public async Task HoySinEventosDevuelveLaPoblacionActual()
    {
        _produccion.ListarPorRangoAsync(_galpon.Id, Hoy.AddDays(-6), Hoy, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroProduccion>());
        _mortalidad.ListarPorRangoAsync(_galpon.Id, Hoy.AddDays(-6), Hoy, Arg.Any<CancellationToken>())
            .Returns(new List<RegistroMortalidad>());

        var resumen = await _handler.Handle(
            new ObtenerEficienciaGalponQuery(_galpon.Id, null, null), CancellationToken.None);

        var hoy = Assert.Single(resumen.Dias);
        Assert.Equal(Hoy, hoy.Fecha);
        Assert.Equal(2970, hoy.GallinasVivas);
        Assert.Equal(0m, hoy.Eficiencia);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EficienciaGalpon"`
Expected: FALLA la compilación.

- [ ] **Step 3: Implementación mínima**

`Eficiencia/ObtenerEficienciaGalponQuery.cs`:

```csharp
using MediatR;

namespace Icarus.GestionAvicola.Application.Eficiencia;

// Consulta derivada (spec SP6): nada de eficiencia persistida. Desde/Hasta
// nulos = últimos 7 días hasta hoy.
public sealed record ObtenerEficienciaGalponQuery(Guid GalponId, DateOnly? Desde, DateOnly? Hasta)
    : IRequest<EficienciaGalponResumen>;

public sealed record EficienciaDiaResumen(
    DateOnly Fecha, int TotalMaples, int TotalUnidadesIncompletas, int TotalVendible,
    int TotalMaplesDescarte, int TotalUnidadesDescarte, int TotalDescarte,
    int GallinasVivas, decimal Eficiencia, bool BajoUmbral);

public sealed record EficienciaGalponResumen(
    Guid GalponId, DateOnly Desde, DateOnly Hasta, IReadOnlyList<EficienciaDiaResumen> Dias);
```

`Eficiencia/ObtenerEficienciaGalponHandler.cs`:

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Eficiencia;

public sealed class ObtenerEficienciaGalponHandler
    : IRequestHandler<ObtenerEficienciaGalponQuery, EficienciaGalponResumen>
{
    private readonly IRepositorioGalpones _galpones;
    private readonly IRepositorioProduccion _produccion;
    private readonly IRepositorioMortalidad _mortalidad;

    public ObtenerEficienciaGalponHandler(
        IRepositorioGalpones galpones, IRepositorioProduccion produccion,
        IRepositorioMortalidad mortalidad)
    {
        _galpones = galpones;
        _produccion = produccion;
        _mortalidad = mortalidad;
    }

    public async Task<EficienciaGalponResumen> Handle(
        ObtenerEficienciaGalponQuery request, CancellationToken cancellationToken)
    {
        var galpon = await _galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);

        var hasta = request.Hasta ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var desde = request.Desde ?? hasta.AddDays(-6);

        var recogidas = await _produccion.ListarPorRangoAsync(galpon.Id, desde, hasta, cancellationToken);
        var bajas = await _mortalidad.ListarPorRangoAsync(galpon.Id, desde, hasta, cancellationToken);

        var fechas = recogidas.Select(r => r.Fecha)
            .Union(bajas.Select(m => m.Fecha))
            .OrderBy(f => f);

        var dias = fechas.Select(fecha =>
        {
            var recogidasDia = recogidas.Where(r => r.Fecha == fecha).ToList();
            var vendible = recogidasDia.Sum(r => r.TotalHuevosVendibles());

            // Población del día: snapshot del último evento activo del día
            // (recogida o mortalidad, la más reciente por hora). Así la
            // eficiencia histórica queda congelada (spec SP6).
            var gallinasVivas = recogidasDia
                .Select(r => (r.Hora, r.GallinasVivas))
                .Concat(bajas.Where(m => m.Fecha == fecha).Select(m => (m.Hora, m.GallinasVivas)))
                .OrderByDescending(e => e.Hora)
                .First().GallinasVivas;

            var eficiencia = EficienciaPostura.Calcular(vendible, gallinasVivas);
            return new EficienciaDiaResumen(
                fecha,
                recogidasDia.Sum(r => r.CantidadMaples),
                recogidasDia.Sum(r => r.UnidadesIncompletas),
                vendible,
                recogidasDia.Sum(r => r.MaplesDescarte),
                recogidasDia.Sum(r => r.UnidadesDescarte),
                recogidasDia.Sum(r => r.TotalHuevosDescarte()),
                gallinasVivas, eficiencia, EficienciaPostura.EstaBajoUmbral(eficiencia));
        }).ToList();

        // Hoy sin eventos todavía: la población actual con eficiencia cero.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        if (hasta == hoy && dias.All(d => d.Fecha != hoy))
        {
            dias.Add(new EficienciaDiaResumen(
                hoy, 0, 0, 0, 0, 0, 0, galpon.GallinasActuales, 0m,
                EficienciaPostura.EstaBajoUmbral(0m)));
        }

        return new EficienciaGalponResumen(galpon.Id, desde, hasta, dias);
    }
}
```

`Eficiencia/ObtenerEficienciaGalponValidator.cs`:

```csharp
using FluentValidation;

namespace Icarus.GestionAvicola.Application.Eficiencia;

public sealed class ObtenerEficienciaGalponValidator : AbstractValidator<ObtenerEficienciaGalponQuery>
{
    public ObtenerEficienciaGalponValidator()
    {
        RuleFor(q => q.Desde)
            .LessThanOrEqualTo(q => q.Hasta)
            .When(q => q.Desde is not null && q.Hasta is not null)
            .WithMessage("El rango de fechas es inválido.");
    }
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~EficienciaGalpon"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Eficiencia Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerEficienciaGalponHandlerTests.cs
git commit -m "feat(avicola): consulta de eficiencia por galpon con snapshot por dia"
```

---

### Task 7: Infraestructura (DbSets, configuraciones, repositorios, migración)

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs`
- Create: `.../Persistencia/ConfiguracionRegistroProduccion.cs`
- Create: `.../Persistencia/ConfiguracionRegistroMortalidad.cs`
- Create: `.../Repositorios/RepositorioProduccion.cs`
- Create: `.../Repositorios/RepositorioMortalidad.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- Create: `.../Migrations/` (generada por `dotnet ef`)

**Interfaces:**
- Consumes: todo lo de las Tasks 2-5.
- Produces: migración `ProduccionYMortalidad`; repositorios registrados en DI.

- [ ] **Step 1: DbSets y filtros en `GestionAvicolaDbContext`**

En `GestionAvicolaDbContext.cs`, añadir los DbSet junto a los existentes:

```csharp
    public DbSet<RegistroProduccion> RegistrosProduccion => Set<RegistroProduccion>();

    public DbSet<RegistroMortalidad> RegistrosMortalidad => Set<RegistroMortalidad>();
```

Y en `OnModelCreating`, junto a los filtros existentes (misma regla: sin `.Value` sobre el nullable):

```csharp
        modelBuilder.Entity<RegistroProduccion>().HasQueryFilter(r =>
            r.EstaActivo && (_clienteIdActual == null || r.ClienteId == _clienteIdActual));
        modelBuilder.Entity<RegistroMortalidad>().HasQueryFilter(r =>
            r.EstaActivo && (_clienteIdActual == null || r.ClienteId == _clienteIdActual));
```

- [ ] **Step 2: Configuraciones EF**

`ConfiguracionRegistroProduccion.cs`:

```csharp
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionRegistroProduccion : IEntityTypeConfiguration<RegistroProduccion>
{
    public void Configure(EntityTypeBuilder<RegistroProduccion> builder)
    {
        builder.ToTable("registros_produccion", t =>
        {
            // Las invariantes del agregado, como última línea de defensa en BD.
            t.HasCheckConstraint("CK_registros_produccion_maples",
                "[CantidadMaples] >= 0 AND [MaplesDescarte] >= 0");
            t.HasCheckConstraint("CK_registros_produccion_sueltos",
                "[UnidadesIncompletas] >= 0 AND [UnidadesIncompletas] < 30"
                + " AND [UnidadesDescarte] >= 0 AND [UnidadesDescarte] < 30");
        });
        builder.Property(r => r.Fecha).HasColumnType("date");
        builder.Property(r => r.Hora).HasColumnType("time");
        builder.HasIndex(r => new { r.GalponId, r.Fecha });
        builder.HasIndex(r => r.ClienteId);

        // Idempotencia de la PWA offline (spec SP6): una key, un registro.
        builder.HasIndex(r => r.IdempotencyKey).IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}
```

`ConfiguracionRegistroMortalidad.cs`:

```csharp
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class ConfiguracionRegistroMortalidad : IEntityTypeConfiguration<RegistroMortalidad>
{
    public void Configure(EntityTypeBuilder<RegistroMortalidad> builder)
    {
        builder.ToTable("registros_mortalidad", t =>
            t.HasCheckConstraint("CK_registros_mortalidad_cantidad", "[CantidadMuertas] > 0"));
        builder.Property(r => r.Fecha).HasColumnType("date");
        builder.Property(r => r.Hora).HasColumnType("time");
        builder.HasIndex(r => new { r.GalponId, r.Fecha });
        builder.HasIndex(r => r.ClienteId);
        builder.HasIndex(r => r.IdempotencyKey).IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}
```

- [ ] **Step 3: Repositorios**

`RepositorioProduccion.cs`:

```csharp
using Icarus.GestionAvicola.Application.Produccion;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioProduccion : IRepositorioProduccion
{
    private readonly GestionAvicolaDbContext _db;

    public RepositorioProduccion(GestionAvicolaDbContext db) => _db = db;

    public void Agregar(RegistroProduccion registro) => _db.RegistrosProduccion.Add(registro);

    public async Task<RegistroProduccion?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await _db.RegistrosProduccion.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RegistroProduccion>> ListarPorDiaAsync(
        Guid galponId, DateOnly fecha, CancellationToken cancellationToken = default) =>
        await _db.RegistrosProduccion
            .Where(r => r.GalponId == galponId && r.Fecha == fecha)
            .OrderBy(r => r.Hora)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RegistroProduccion>> ListarPorRangoAsync(
        Guid galponId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default) =>
        await _db.RegistrosProduccion
            .Where(r => r.GalponId == galponId && r.Fecha >= desde && r.Fecha <= hasta)
            .OrderBy(r => r.Fecha).ThenBy(r => r.Hora)
            .ToListAsync(cancellationToken);

    public async Task<RegistroProduccion?> ObtenerPorIdempotencyKeyAsync(
        Guid galponId, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        await _db.RegistrosProduccion.SingleOrDefaultAsync(
            r => r.GalponId == galponId && r.IdempotencyKey == idempotencyKey, cancellationToken);
}
```

`RepositorioMortalidad.cs`:

```csharp
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

public sealed class RepositorioMortalidad : IRepositorioMortalidad
{
    private readonly GestionAvicolaDbContext _db;

    public RepositorioMortalidad(GestionAvicolaDbContext db) => _db = db;

    public void Agregar(RegistroMortalidad registro) => _db.RegistrosMortalidad.Add(registro);

    public async Task<RegistroMortalidad?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await _db.RegistrosMortalidad.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RegistroMortalidad>> ListarPorDiaAsync(
        Guid galponId, DateOnly fecha, CancellationToken cancellationToken = default) =>
        await _db.RegistrosMortalidad
            .Where(r => r.GalponId == galponId && r.Fecha == fecha)
            .OrderBy(r => r.Hora)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RegistroMortalidad>> ListarPorRangoAsync(
        Guid galponId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default) =>
        await _db.RegistrosMortalidad
            .Where(r => r.GalponId == galponId && r.Fecha >= desde && r.Fecha <= hasta)
            .OrderBy(r => r.Fecha).ThenBy(r => r.Hora)
            .ToListAsync(cancellationToken);

    public async Task<RegistroMortalidad?> ObtenerPorIdempotencyKeyAsync(
        Guid galponId, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        await _db.RegistrosMortalidad.SingleOrDefaultAsync(
            r => r.GalponId == galponId && r.IdempotencyKey == idempotencyKey, cancellationToken);
}
```

- [ ] **Step 4: Registrar repositorios en DI**

En `DependencyInjection.cs` (`AddGestionAvicolaInfraestructura`), añadir los usings de `Icarus.GestionAvicola.Application.Mortalidad` y `Icarus.GestionAvicola.Application.Produccion`, y junto a los registros existentes:

```csharp
        servicios.AddScoped<IRepositorioProduccion, RepositorioProduccion>();
        servicios.AddScoped<IRepositorioMortalidad, RepositorioMortalidad>();
```

- [ ] **Step 5: Generar la migración**

```bash
cd Icarus && dotnet tool restore
dotnet ef migrations add ProduccionYMortalidad \
  --project src/GestionAvicola/Icarus.GestionAvicola.Infrastructure \
  --startup-project src/GestionAvicola/Icarus.GestionAvicola.Infrastructure \
  --context GestionAvicolaDbContext
```

Expected: migración generada con las tablas `gestion_avicola.registros_produccion` y `gestion_avicola.registros_mortalidad`, columnas `Fecha` (`date`) y `Hora` (`time`), índices `(GalponId, Fecha)` y únicos filtrados de `IdempotencyKey`, y los check constraints. Revisar el archivo generado antes de seguir.

- [ ] **Step 6: Verificar build y tests**

Run: `dotnet build Icarus/Icarus.sln --nologo` y `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~GestionAvicola"`
Expected: BUILD succeeded, 0 warnings; tests PASS.

- [ ] **Step 7: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure
git commit -m "feat(avicola): persistencia de produccion y mortalidad con migracion"
```

---

### Task 8: Endpoints en el Host

**Files:**
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs`

**Interfaces:**
- Consumes: commands/queries de las Tasks 4-6; `PoliticasClientes.Para(Funcionalidades.ProduccionHuevos/Mortalidad)` (las políticas ya existen: se generan para todos los valores del enum en `AddClientesInfraestructura`).
- Produces: endpoints `/galpones/{id}/produccion`, `/galpones/{id}/mortalidad`, `/galpones/{id}/eficiencia`, `/produccion/{id}`, `/mortalidad/{id}`.

- [ ] **Step 1: Añadir los endpoints**

En `GestionAvicolaEndpoints.cs`:

a) Usings nuevos:

```csharp
using Icarus.GestionAvicola.Application.Eficiencia;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Application.Produccion;
```

b) Políticas, junto a las existentes:

```csharp
        var politicaProduccion = PoliticasClientes.Para(Funcionalidades.ProduccionHuevos);
        var politicaMortalidad = PoliticasClientes.Para(Funcionalidades.Mortalidad);
```

c) Dentro de `MapGestionAvicola`, tras el grupo `galpones` existente (antes del `return app;`):

```csharp
        // Producción y mortalidad (spec SP6): pensados para el rol Trabajador
        // (el recolector) con la funcionalidad asignada; el Cliente también
        // puede. La Fecha la fija el servidor; DELETE = desactivar (soft
        // delete), nunca borrado físico.
        galpones.MapPost("/{galponId:guid}/produccion",
            async (Guid galponId, RegistrarProduccionRequest c, ISender mediator) =>
            {
                var id = await mediator.Send(new RegistrarProduccionCommand(
                    galponId, c.Hora, c.CantidadMaples, c.UnidadesIncompletas,
                    c.MaplesDescarte, c.UnidadesDescarte, c.IdempotencyKey));
                return Results.Created($"/produccion/{id}", new { id });
            }).RequireAuthorization(politicaProduccion);

        galpones.MapGet("/{galponId:guid}/produccion",
            async (Guid galponId, DateOnly? fecha, ISender mediator) =>
                Results.Ok(await mediator.Send(new ListarProduccionPorDiaQuery(galponId, fecha))))
            .RequireAuthorization(politicaProduccion);

        galpones.MapGet("/{galponId:guid}/eficiencia",
            async (Guid galponId, DateOnly? desde, DateOnly? hasta, ISender mediator) =>
                Results.Ok(await mediator.Send(
                    new ObtenerEficienciaGalponQuery(galponId, desde, hasta))))
            .RequireAuthorization(politicaProduccion);

        galpones.MapPost("/{galponId:guid}/mortalidad",
            async (Guid galponId, RegistrarMortalidadRequest c, ISender mediator) =>
            {
                var id = await mediator.Send(new RegistrarMortalidadCommand(
                    galponId, c.Hora, c.CantidadMuertas, c.IdempotencyKey));
                return Results.Created($"/mortalidad/{id}", new { id });
            }).RequireAuthorization(politicaMortalidad);

        galpones.MapGet("/{galponId:guid}/mortalidad",
            async (Guid galponId, DateOnly? fecha, ISender mediator) =>
                Results.Ok(await mediator.Send(new ListarMortalidadPorDiaQuery(galponId, fecha))))
            .RequireAuthorization(politicaMortalidad);

        var produccion = app.MapGroup("/produccion");
        produccion.MapPut("/{id:guid}", async (Guid id, EditarProduccionRequest c, ISender mediator) =>
        {
            await mediator.Send(new EditarProduccionCommand(
                id, c.Hora, c.CantidadMaples, c.UnidadesIncompletas,
                c.MaplesDescarte, c.UnidadesDescarte));
            return Results.NoContent();
        }).RequireAuthorization(politicaProduccion);
        produccion.MapDelete("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new DesactivarProduccionCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(politicaProduccion);

        var mortalidad = app.MapGroup("/mortalidad");
        mortalidad.MapPut("/{id:guid}", async (Guid id, EditarMortalidadRequest c, ISender mediator) =>
        {
            await mediator.Send(new EditarMortalidadCommand(id, c.Hora, c.CantidadMuertas));
            return Results.NoContent();
        }).RequireAuthorization(politicaMortalidad);
        mortalidad.MapDelete("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            await mediator.Send(new DesactivarMortalidadCommand(id));
            return Results.NoContent();
        }).RequireAuthorization(politicaMortalidad);
```

d) Records de request, junto a los existentes al final de la clase:

```csharp
    private sealed record RegistrarProduccionRequest(
        TimeOnly? Hora, int CantidadMaples, int UnidadesIncompletas,
        int MaplesDescarte, int UnidadesDescarte, Guid? IdempotencyKey);

    private sealed record EditarProduccionRequest(
        TimeOnly Hora, int CantidadMaples, int UnidadesIncompletas,
        int MaplesDescarte, int UnidadesDescarte);

    private sealed record RegistrarMortalidadRequest(
        TimeOnly? Hora, int CantidadMuertas, Guid? IdempotencyKey);

    private sealed record EditarMortalidadRequest(TimeOnly Hora, int CantidadMuertas);
```

- [ ] **Step 2: Verificar build**

Run: `dotnet build Icarus/Icarus.sln --nologo`
Expected: BUILD succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add Icarus/src/Host/Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs
git commit -m "feat(avicola): endpoints de produccion mortalidad y eficiencia"
```

---

### Task 9: Tests de integración de los endpoints

**Files:**
- Test: `Icarus/tests/Icarus.IntegrationTests/ProduccionMortalidadEndpointsTests.cs`

**Interfaces:**
- Consumes: `IdentityFactory` (Testcontainers.MsSql; migra y siembra en `Testing`), `SemillaIdentidad.EmailAdmin/EmailCliente`, `IdentityFactory.ContrasenaDePrueba`, `GestionAvicolaDbContext` (accesible vía la referencia al Host) para sembrar registros de ayer. Requiere **Docker corriendo**.

**Contexto para el implementador:** los tests de la clase comparten contenedor: **cada test crea su propio cliente + granja + galpón** (helpers abajo) para no depender del orden. Las funcionalidades se asignan por nombre de enum en minúsculas (`"produccionHuevos"`, `"mortalidad"`), igual que `"granjas"` en los tests de entitlement. Los JSON van en camelCase; `TimeOnly` como `"10:30:00"`, `DateOnly` como `"2026-08-18"`. El `GestionAvicolaDbContext` resuelto desde `_factory.Services` fuera de request tiene `ClienteId` nulo (rol plataforma): los filtros quedan abiertos y permite sembrar registros con fecha de ayer directamente.

- [ ] **Step 1: Escribir el test que falla**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Icarus.Identity.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Icarus.IntegrationTests;

// Endpoints de producción, mortalidad y eficiencia (spec SP6): flujo completo,
// aislamiento de tenant, entitlement del trabajador, ventana del día e
// idempotencia. Cada test crea su propio cliente/granja/galpón (la clase
// comparte contenedor).
public class ProduccionMortalidadEndpointsTests : IClassFixture<IdentityFactory>
{
    private readonly IdentityFactory _factory;

    public ProduccionMortalidadEndpointsTests(IdentityFactory factory) => _factory = factory;

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

    private async Task<(Guid ClienteId, string Token)> CrearClienteAvicola()
    {
        var admin = await LoginComo(SemillaIdentidad.EmailAdmin);
        var clienteHttp = _factory.CreateClient();
        var email = $"avicola-{Guid.NewGuid():N}@icarus.test";

        var alta = ConCuerpo(HttpMethod.Post, "/clientes", admin, new
        {
            razonSocial = "Avícola de Prueba S.A.C.",
            identificadorFiscal = $"4{Random.Shared.Next(100000000, 999999999)}",
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

        return (clienteId, await LoginComo(email));
    }

    // Cliente + granja + galpón. Devuelve (token, clienteId, galponId).
    private async Task<(string Token, Guid ClienteId, Guid GalponId)> CrearGalponNuevo(
        int gallinas = 3000)
    {
        var (clienteId, token) = await CrearClienteAvicola();
        var cliente = _factory.CreateClient();

        var granja = await cliente.SendAsync(
            ConCuerpo(HttpMethod.Post, "/granjas", token, new { nombre = "Granja SP6" }));
        Assert.Equal(HttpStatusCode.Created, granja.StatusCode);
        var granjaId = (await granja.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var galpon = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/granjas/{granjaId}/galpones", token, new
            {
                numero = "1",
                capacidadMaxima = 5000,
                gallinasActuales = gallinas,
                fechaNacimientoLote = "2026-01-10",
                descripcion = (string?)null,
            }));
        Assert.Equal(HttpStatusCode.Created, galpon.StatusCode);
        var galponId = (await galpon.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        return (token, clienteId, galponId);
    }

    private async Task<Guid> RegistrarMortalidad(string token, Guid galponId, int muertas)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/galpones/{galponId}/mortalidad", token,
            new { hora = (string?)null, cantidadMuertas = muertas, idempotencyKey = (Guid?)null }));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
    }

    private async Task<int> GallinasActuales(string token, Guid galponId)
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/galpones/{galponId}", token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("gallinasActuales").GetInt32();
    }

    private async Task<Guid> SembrarRecogidaDeAyer(Guid galponId, Guid clienteId)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
        var recogida = new RegistroProduccion(
            galponId, clienteId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            new TimeOnly(10, 0, 0), 10, 0, 0, 0, 3000, null);
        db.RegistrosProduccion.Add(recogida);
        await db.SaveChangesAsync();
        return recogida.Id;
    }

    [Fact]
    public async Task FlujoCompletoRecogidaMortalidadEficiencia()
    {
        var (token, _, galponId) = await CrearGalponNuevo();
        var cliente = _factory.CreateClient();

        var recogida = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/galpones/{galponId}/produccion", token, new
            {
                hora = "10:30:00",
                cantidadMaples = 80,
                unidadesIncompletas = 0,
                maplesDescarte = 2,
                unidadesDescarte = 5,
                idempotencyKey = (Guid?)null,
            }));
        Assert.Equal(HttpStatusCode.Created, recogida.StatusCode);

        await RegistrarMortalidad(token, galponId, 30);
        Assert.Equal(2970, await GallinasActuales(token, galponId));

        var eficiencia = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/galpones/{galponId}/eficiencia", token));
        Assert.Equal(HttpStatusCode.OK, eficiencia.StatusCode);
        var cuerpo = await eficiencia.Content.ReadFromJsonAsync<JsonElement>();
        var dia = Assert.Single(cuerpo.GetProperty("dias").EnumerateArray());
        Assert.Equal(2400, dia.GetProperty("totalVendible").GetInt32());
        Assert.Equal(65, dia.GetProperty("totalDescarte").GetInt32());
        Assert.Equal(2970, dia.GetProperty("gallinasVivas").GetInt32());
        Assert.Equal(80.81m, dia.GetProperty("eficiencia").GetDecimal());
        Assert.False(dia.GetProperty("bajoUmbral").GetBoolean());
    }

    [Fact]
    public async Task EficienciaBajoElSetentaMarcaBajoUmbral()
    {
        var (token, _, galponId) = await CrearGalponNuevo();
        var cliente = _factory.CreateClient();

        // 60 maples = 1800 huevos sobre 3000 gallinas = 60 %.
        await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/galpones/{galponId}/produccion", token, new
            {
                hora = (string?)null, cantidadMaples = 60, unidadesIncompletas = 0,
                maplesDescarte = 0, unidadesDescarte = 0, idempotencyKey = (Guid?)null,
            }));

        var eficiencia = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/galpones/{galponId}/eficiencia", token));
        var dia = Assert.Single(
            (await eficiencia.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("dias").EnumerateArray());
        Assert.Equal(60m, dia.GetProperty("eficiencia").GetDecimal());
        Assert.True(dia.GetProperty("bajoUmbral").GetBoolean());
    }

    [Fact]
    public async Task GalponDeOtroTenantDevuelve404()
    {
        var (_, _, galponIdA) = await CrearGalponNuevo();
        var tokenB = await CrearClienteAvicola();
        var cliente = _factory.CreateClient();

        var crear = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/galpones/{galponIdA}/produccion", tokenB, new
            {
                hora = (string?)null, cantidadMaples = 1, unidadesIncompletas = 0,
                maplesDescarte = 0, unidadesDescarte = 0, idempotencyKey = (Guid?)null,
            }));
        var listar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/galpones/{galponIdA}/produccion", tokenB));

        Assert.Equal(HttpStatusCode.NotFound, crear.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, listar.StatusCode);
    }

    [Fact]
    public async Task TrabajadorConSoloProduccionRegistraRecogidaPeroNoMortalidad()
    {
        var (tokenCliente, clienteId, galponId) = await CrearGalponNuevo();
        var cliente = _factory.CreateClient();

        // Alta de trabajador con solo la funcionalidad ProduccionHuevos.
        var emailTrabajador = $"recolector-{Guid.NewGuid():N}@icarus.test";
        var alta = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/clientes/{clienteId}/trabajadores", tokenCliente, new
            {
                nombre = "Recolector Ficticio",
                documentoIdentidad = $"8{Random.Shared.Next(10000000, 99999999)}",
                cargo = "Recolector",
                fechaIngreso = "2026-01-15",
                email = emailTrabajador,
                contrasena = IdentityFactory.ContrasenaDePrueba,
            }));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var trabajadorId = (await alta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var funcionalidades = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Put, $"/clientes/{clienteId}/trabajadores/{trabajadorId}/funcionalidades",
            tokenCliente, new { funcionalidades = new[] { "produccionHuevos" } }));
        Assert.Equal(HttpStatusCode.NoContent, funcionalidades.StatusCode);
        var tokenTrabajador = await LoginComo(emailTrabajador);

        var recogida = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/galpones/{galponId}/produccion", tokenTrabajador, new
            {
                hora = (string?)null, cantidadMaples = 10, unidadesIncompletas = 0,
                maplesDescarte = 0, unidadesDescarte = 0, idempotencyKey = (Guid?)null,
            }));
        var mortalidad = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/galpones/{galponId}/mortalidad", tokenTrabajador,
            new { hora = (string?)null, cantidadMuertas = 5, idempotencyKey = (Guid?)null }));

        Assert.Equal(HttpStatusCode.Created, recogida.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, mortalidad.StatusCode);
    }

    [Fact]
    public async Task EditarYDesactivarRecogidaDeHoyFunciona()
    {
        var (token, _, galponId) = await CrearGalponNuevo();
        var cliente = _factory.CreateClient();

        var crear = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Post, $"/galpones/{galponId}/produccion", token, new
            {
                hora = "10:00:00", cantidadMaples = 10, unidadesIncompletas = 5,
                maplesDescarte = 0, unidadesDescarte = 0, idempotencyKey = (Guid?)null,
            }));
        var recogidaId = (await crear.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var editar = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Put, $"/produccion/{recogidaId}", token, new
            {
                hora = "10:05:00", cantidadMaples = 12, unidadesIncompletas = 0,
                maplesDescarte = 1, unidadesDescarte = 0,
            }));
        Assert.Equal(HttpStatusCode.NoContent, editar.StatusCode);

        var dia = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/galpones/{galponId}/produccion", token));
        var resumen = await dia.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(12, resumen.GetProperty("totalMaples").GetInt32());
        Assert.Equal(360, resumen.GetProperty("totalVendible").GetInt32());

        var desactivar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Delete, $"/produccion/{recogidaId}", token));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);

        var diaTras = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/galpones/{galponId}/produccion", token));
        Assert.Equal(0, (await diaTras.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("recogidas").GetArrayLength());
    }

    [Fact]
    public async Task RecogidaDeAyerEstaSellada()
    {
        var (token, clienteId, galponId) = await CrearGalponNuevo();
        var cliente = _factory.CreateClient();

        // Sembrada directo en BD con fecha de ayer (filtros abiertos fuera de
        // request, ClienteId nulo de plataforma).
        var recogidaId = await SembrarRecogidaDeAyer(galponId, clienteId);

        var editar = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Put, $"/produccion/{recogidaId}", token, new
            {
                hora = "10:05:00", cantidadMaples = 99, unidadesIncompletas = 0,
                maplesDescarte = 0, unidadesDescarte = 0,
            }));
        var desactivar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Delete, $"/produccion/{recogidaId}", token));

        Assert.Equal(HttpStatusCode.BadRequest, editar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, desactivar.StatusCode);
    }

    [Fact]
    public async Task IdempotencyKeyRepetidaNoDuplicaLaRecogida()
    {
        var (token, _, galponId) = await CrearGalponNuevo();
        var cliente = _factory.CreateClient();
        var key = Guid.NewGuid();
        var cuerpo = new
        {
            hora = "10:00:00", cantidadMaples = 10, unidadesIncompletas = 0,
            maplesDescarte = 0, unidadesDescarte = 0, idempotencyKey = key,
        };

        var primero = await cliente.SendAsync(
            ConCuerpo(HttpMethod.Post, $"/galpones/{galponId}/produccion", token, cuerpo));
        var segundo = await cliente.SendAsync(
            ConCuerpo(HttpMethod.Post, $"/galpones/{galponId}/produccion", token, cuerpo));

        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);
        var id1 = (await primero.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var id2 = (await segundo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(id1, id2);

        var dia = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Get, $"/galpones/{galponId}/produccion", token));
        Assert.Equal(10, (await dia.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("totalMaples").GetInt32());
    }

    [Fact]
    public async Task EditarYDesactivarMortalidadAjustaElInventario()
    {
        var (token, _, galponId) = await CrearGalponNuevo();
        var cliente = _factory.CreateClient();

        var mortalidadId = await RegistrarMortalidad(token, galponId, 30);
        Assert.Equal(2970, await GallinasActuales(token, galponId));

        var editar = await cliente.SendAsync(ConCuerpo(
            HttpMethod.Put, $"/mortalidad/{mortalidadId}", token,
            new { hora = "06:30:00", cantidadMuertas = 50 }));
        Assert.Equal(HttpStatusCode.NoContent, editar.StatusCode);
        Assert.Equal(2950, await GallinasActuales(token, galponId));

        var desactivar = await cliente.SendAsync(
            PedidoAutenticado(HttpMethod.Delete, $"/mortalidad/{mortalidadId}", token));
        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);
        Assert.Equal(3000, await GallinasActuales(token, galponId));
    }

    [Fact]
    public async Task SinTokenDevuelve401()
    {
        var cliente = _factory.CreateClient();
        var respuesta = await cliente.GetAsync($"/galpones/{Guid.NewGuid()}/produccion");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~ProduccionMortalidadEndpointsTests"`
Expected: PASS si las Tasks 1-8 quedaron bien. Si algo falla, corregir la implementación, no el test. Para ver el rojo al menos una vez: comentar temporalmente los `MapPost("/{galponId:guid}/produccion"...)` y comprobar que los tests fallan con 404/405; después restaurar.

- [ ] **Step 3: Suite de integración completa**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests`
Expected: PASS, incluidos los tests de SP5 (la semilla no cambió; nada nuevo debe romperlos).

- [ ] **Step 4: Commit**

```bash
git add Icarus/tests/Icarus.IntegrationTests/ProduccionMortalidadEndpointsTests.cs
git commit -m "test(avicola): integracion de produccion mortalidad y eficiencia"
```

---

### Task 10: Cierre (puerta de calidad, documentación, push)

**Files:**
- Modify: `AGENTS.md` (sección Proyecto)
- Modify: `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md` (regenerados, NO a mano)

- [ ] **Step 1: Actualizar `AGENTS.md`**

En la sección `## Proyecto`, punto del backend: donde dice "módulo GestionAvicola (agregados Granja/Galpón, una granja activa por cliente)" dejarlo como:

```
y módulo GestionAvicola (agregados Granja/Galpón —una granja activa por
cliente—, recogidas de producción con huevos de descarte, mortalidad con
ajuste de inventario y eficiencia diaria con umbral del 70 %)
```

- [ ] **Step 2: Regenerar adaptadores**

Run: `node quality/generar-adaptadores.mjs`
Expected: regenera `CLAUDE.md`, `GEMINI.md` y `.github/copilot-instructions.md`.

- [ ] **Step 3: Puerta de calidad completa**

Run: `./verify.ps1` (PowerShell, desde la raíz; exige Docker corriendo)
Expected: todos los gates en verde (tests de la puerta, adaptadores, mojibake, enlaces, frontend, `dotnet build`, `dotnet test` completos).

- [ ] **Step 4: Releer el diff propio y push**

```bash
git status --short
git log --oneline origin/develop..HEAD
git add AGENTS.md CLAUDE.md GEMINI.md .github/copilot-instructions.md
git commit -m "docs(agentes): produccion y mortalidad en la seccion Proyecto"
git push origin develop
```

- [ ] **Step 5: Cerrar el ciclo**

- Marcar las tareas de este plan como hechas.
- Si el trabajo quedó completo: borrar `docs/ai/HANDOFF.md`. Si quedó a medias: actualizarlo con lo pendiente.

---

## Notas para el implementador

- **Orden de las tareas**: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10. Las Tasks 4-6 solo crean Application; el build completo vuelve a compilar bien en cada una (todo es código nuevo).
- **La `Fecha` la fija el servidor** en los handlers de registro; el dominio solo rechaza futuras. Así la ventana "solo hoy" se cumple por construcción, y los tests pueden construir histórico con el ctor.
- **El sellado es de dominio** (`ExigirDiaAbierto`): un registro de ayer no se edita ni se desactiva, aunque el handler lo intente.
- **Mortalidad e inventario**: registrar descuenta, editar repone-y-descuenta, desactivar repone. Siempre a través de `Galpon.AjustarInventarioGallinas` (total absoluto), nunca manipulando `GallinasActuales` a mano, y siempre narrado con `Decidir`.
- **En producción `GallinasVivas` es inmutable; en mortalidad se actualiza al editar.** Es deliberado: en producción es la foto del momento de la recogida; en mortalidad debe reflejar el inventario tras el ajuste corregido.
- **Idempotencia**: la comprobación va ANTES de cualquier efecto (descuento de inventario), y devuelve el id existente sin guardar.
- **La eficiencia nunca se persiste**: se calcula en el handler con `EficienciaPostura.Calcular` sobre los snapshots.
- **No portar del legacy**: `NumeroRegistro`, `PorcentajeMortalidad`, `EficienciaProduccion` persistida, causa probable, acciones tomadas, ni la creación automática de mortalidad desde producción.
- **Si un gate de la puerta falla, se arregla el contenido, no el gate** (AGENTS.md).
