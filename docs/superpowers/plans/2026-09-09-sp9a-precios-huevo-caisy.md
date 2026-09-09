# SP9A — GestorRecepcionHuevos y publicación de precios de huevo — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introducir la funcionalidad CAISY `GestorRecepcionHuevos` y el catálogo
global de publicaciones de precio de huevo (por tamaño, con importación desde
Excel), gestionable desde Trajano.GestorCaisy. Es el bloque base de SP9: SP9B
(despacho desde la PWA) y SP9C (recepción, recibo y crédito) no empiezan hasta
que este quede integrado.

**Architecture:** Mismo patrón ya construido para `NotificacionPreciosAlimentos`
en SP8A: agregado global sin tenant (`PublicacionPrecioHuevo`) con estados
`Borrador → Publicada` (o `Anulada` si nunca llegó a entrar en vigor),
publicación versionada e inmutable, catálogo accesible solo por la política
`FuncionalidadCaisy:GestorRecepcionHuevos`. La captura es exclusivamente por
Excel (a diferencia de alimento, que también acepta PDF) porque el documento
real de CAISY para huevo ya llega en ese formato.

**Tech Stack:** .NET 10 / ASP.NET Core Minimal APIs, EF Core (SQL Server),
MediatR, FluentValidation, ClosedXML 0.105.0 (ya referenciado en
`Icarus/Directory.Packages.props`), ASP.NET Core MVC (Trajano.GestorCaisy),
xUnit + NSubstitute, Testcontainers.MsSql.

## Global Constraints

- Español correcto, UTF-8 sin BOM, sin mojibake, en toda cadena visible,
  comentario y mensaje de error.
- Anti-PII: el registro de vuelo (Seq) nunca lleva contenido del documento,
  solo ids técnicos, estados y conteos (mismo patrón que
  `avicola.precios.importar-excel` en SP8A).
- TDD estricto: cada tarea empieza con una prueba en rojo por el motivo
  correcto antes de implementar.
- `./verify.ps1` (o `./verify.sh`) antes de cada commit; prohibido
  `--no-verify`. Docker debe estar activo para integración
  (Testcontainers.MsSql).
- Valores numéricos de enums persistidos como entero (`FuncionalidadesCaisy`,
  `EstadoPublicacionPrecioHuevo`, `TamanoHuevo`) son estables: agregar al
  final, nunca renumerar.
- Un commit por tarea, con `git diff --check` limpio antes de cada uno.

---

### Task 1: Funcionalidad CAISY `GestorRecepcionHuevos`

**Files:**
- Modify: `Icarus/src/Identity/Icarus.Identity.Domain/FuncionalidadesCaisy.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Autenticacion/ConstantesAutorizacion.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Autenticacion/ReclamosCaisy.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Program.cs`
- Modify: `Icarus/tests/Icarus.UnitTests/Identity/FuncionalidadesCaisyTests.cs`

**Interfaces:**
- Produces: `FuncionalidadesCaisy.GestorRecepcionHuevos = 2` (backend);
  `ConstantesAutorizacion.BitGestorRecepcionHuevos = 2`,
  `ConstantesAutorizacion.PoliticaGestorRecepcionHuevos = "GestorRecepcionHuevos"`;
  `ReclamosCaisy.TieneGestorRecepcionHuevos(ClaimsPrincipal usuario)`.

El backend registra la política `FuncionalidadCaisy:GestorRecepcionHuevos`
automáticamente (itera el enum en
`Icarus.Identity.Infrastructure/DependencyInjection.cs`, sin tocar ese
archivo). Solo `Trajano.GestorCaisy` necesita el registro manual, porque su
`ReclamosCaisy`/política es independiente del backend.

- [ ] **Step 1: Actualizar el test que ya anticipa el nombre**

`FuncionalidadesCaisyTests.cs` ya tiene, desde SP8, un caso que espera que
`"GestorRecepcionHuevos"` sea **rechazado** por no estar definido
(`UnaFuncionalidadNoDefinidaEsRechazada`). Moverlo a la lista de válidos y
agregar cobertura de bitmask/token, dejando el archivo así:

```csharp
    [Theory]
    [InlineData("GestorPedidoAlimento")]
    [InlineData("GestorRecepcionHuevos")]
    public void SoloSeAdmitenFuncionalidadesDefinidas(string nombre)
    {
        Assert.True(ReglasFuncionalidadesCaisy.EsValida(nombre));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ninguno")]
    [InlineData("GestorInexistente")]
    public void UnaFuncionalidadNoDefinidaEsRechazada(string nombre)
    {
        Assert.False(ReglasFuncionalidadesCaisy.EsValida(nombre));
    }

    [Fact]
    public void LosNombresValidosSeCombinanEnUnBitmaskConVariasFunciones()
    {
        var combinadas = ReglasFuncionalidadesCaisy.Combinar(
            ["GestorPedidoAlimento", "GestorRecepcionHuevos"]);

        Assert.Equal(
            FuncionalidadesCaisy.GestorPedidoAlimento | FuncionalidadesCaisy.GestorRecepcionHuevos,
            combinadas);
    }

    [Fact]
    public void ElTokenDeGestorCaisyConGestorRecepcionHuevosIncluyeElClaimDeBitmask()
    {
        var token = CrearEmisor().Emitir(
            Guid.NewGuid(), nameof(Rol.GestorCaisy), null, null,
            FuncionalidadesCaisy.GestorRecepcionHuevos, out _);
        var leido = Leer(token);

        Assert.Equal(
            ((int)FuncionalidadesCaisy.GestorRecepcionHuevos).ToString(),
            leido.Claims.Single(c => c.Type == ClaimsIdentidad.FuncionalidadesCaisy).Value);
    }
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FuncionalidadesCaisy`
Expected: FAIL — `SoloSeAdmitenFuncionalidadesDefinidas("GestorRecepcionHuevos")`
falla porque el enum todavía no define ese valor
(`ReglasFuncionalidadesCaisy.EsValida` devuelve `false`).

- [ ] **Step 2: Agregar el bit al enum del backend**

En `FuncionalidadesCaisy.cs`, agregar sin renumerar:

```csharp
public enum FuncionalidadesCaisy
{
    Ninguno = 0,
    GestorPedidoAlimento = 1,
    GestorRecepcionHuevos = 2,
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FuncionalidadesCaisy`
Expected: PASS (todas las pruebas del archivo, incluidas las nuevas).

- [ ] **Step 3: Réplica manual en Trajano.GestorCaisy**

En `ConstantesAutorizacion.cs`, agregar junto a las constantes existentes:

```csharp
    // Bitmask FuncionalidadesCaisy del backend: GestorRecepcionHuevos = 2.
    public const int BitGestorRecepcionHuevos = 2;

    public const string PoliticaGestorRecepcionHuevos = "GestorRecepcionHuevos";
```

En `ReclamosCaisy.cs`, agregar el método análogo:

```csharp
    public static bool TieneGestorRecepcionHuevos(ClaimsPrincipal usuario) =>
        usuario.HasClaim(c => c.Type == ConstantesAutorizacion.ClaimRol
                && c.Value == ConstantesAutorizacion.RolGestorCaisy)
            && int.TryParse(
                usuario.FindFirst(ConstantesAutorizacion.ClaimFuncionalidadesCaisy)?.Value,
                out var mascara)
            && (mascara & ConstantesAutorizacion.BitGestorRecepcionHuevos)
                == ConstantesAutorizacion.BitGestorRecepcionHuevos;
```

En `Program.cs`, junto al registro de política existente (línea ~78-83),
agregar una segunda política:

```csharp
builder.Services.AddAuthorization(opciones =>
{
    opciones.AddPolicy(
        ConstantesAutorizacion.PoliticaGestorPedidoAlimento, politica =>
            politica.AddRequirements(new RequerimientoRolYFuncionalidad(
                ConstantesAutorizacion.RolGestorCaisy,
                ConstantesAutorizacion.BitGestorPedidoAlimento)));
    opciones.AddPolicy(
        ConstantesAutorizacion.PoliticaGestorRecepcionHuevos, politica =>
            politica.AddRequirements(new RequerimientoRolYFuncionalidad(
                ConstantesAutorizacion.RolGestorCaisy,
                ConstantesAutorizacion.BitGestorRecepcionHuevos)));
});
```

- [ ] **Step 4: Correr toda la suite de Identity y GestorCaisy**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter Caisy`
Expected: PASS.
Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS (el proyecto sigue compilando y las pruebas existentes de
`GestorPedidoAlimento` no se rompen).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Identity/Icarus.Identity.Domain/FuncionalidadesCaisy.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Autenticacion/ConstantesAutorizacion.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Autenticacion/ReclamosCaisy.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Program.cs \
  Icarus/tests/Icarus.UnitTests/Identity/FuncionalidadesCaisyTests.cs
git commit -m "feat(identity): agregar funcionalidad caisy GestorRecepcionHuevos"
```

---

### Task 2: Dominio — `PublicacionPrecioHuevo`

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PublicacionPrecioHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DetallePrecioHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TamanoHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoPublicacionPrecioHuevo.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PublicacionPrecioHuevoTests.cs`

**Interfaces:**
- Produces: `PublicacionPrecioHuevo` (ctor `(DateOnly fechaNotificacion,
  DateOnly fechaVigencia, decimal servicio)`, ctor con
  `IReadOnlyList<DatosDetallePrecioHuevo> detalles`, ctor con `Guid id` para
  tests), propiedades `FechaNotificacion`, `FechaVigencia`,
  `Estado` (`EstadoPublicacionPrecioHuevo`), `EstaActivo`, `Version` (rowversion),
  `DocumentoOriginalId`, `Servicio`, `Detalles`
  (`IReadOnlyCollection<DetallePrecioHuevo>`); métodos
  `AsignarDocumentoOriginal(Guid)`, `ActualizarBorrador(DateOnly, DateOnly,
  decimal, IReadOnlyList<DatosDetallePrecioHuevo>)`,
  `ActualizarBorrador(IReadOnlyList<DatosDetallePrecioHuevo>)`, `Publicar()`,
  `DescartarBorrador()`, `AnularFutura(DateOnly hoy)`.
- Produces: `DetallePrecioHuevo` (propiedades `Tamano` (`TamanoHuevo`),
  `PrecioAlProductor` (decimal), `PrecioActualDocumento` (decimal?)).
- Produces: `DatosDetallePrecioHuevo(TamanoHuevo Tamano, decimal
  PrecioAlProductor, decimal? PrecioActualDocumento = null)` — record.
- Produces: `TamanoHuevo { Extra = 0, Primera = 1, Segunda = 2, Tercera = 3,
  Cuarta = 4, Quinta = 5 }` (mismo orden que el glosario de dominio y el
  legacy).
- Produces: `EstadoPublicacionPrecioHuevo { Borrador = 0, Publicada = 1,
  Anulada = 2 }`.

Este es el mismo patrón que `NotificacionPreciosAlimentos`
(`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionPreciosAlimentos.cs`),
con dos diferencias de negocio:
1. Un solo campo `Servicio` en la cabecera (no tres aportes), y un único
   detalle por tamaño en vez de por (tipo, presentación).
2. Sin franjas de edad (`EdadDesdeDias`/`EdadHastaDias` no aplican a huevo).

- [ ] **Step 1: Escribir el test de dominio en rojo**

```csharp
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9A (spec: "Precios: captura y congelamiento"): cabecera global
// versionada, inmutable tras publicarse. Servicio es un único valor por
// publicación (no por tamaño); PrecioAlProductor ya es el monto que se
// congela en los despachos.
public class PublicacionPrecioHuevoTests
{
    private static readonly DateOnly FechaNotificacion = new(2026, 10, 30);
    private static readonly DateOnly FechaVigencia = new(2026, 11, 1);

    private static DatosDetallePrecioHuevo Datos(
        TamanoHuevo tamano, decimal precio = 0.70m, decimal? precioActualDocumento = null) =>
        new(tamano, precio, precioActualDocumento);

    private static PublicacionPrecioHuevo BorradorConSeisDetalles()
    {
        var tamanos = new[]
        {
            TamanoHuevo.Extra, TamanoHuevo.Primera, TamanoHuevo.Segunda,
            TamanoHuevo.Tercera, TamanoHuevo.Cuarta, TamanoHuevo.Quinta,
        };
        var detalles = tamanos.Select(t => Datos(t, 0.70m + (int)t * 0.01m)).ToList();
        return new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m, detalles);
    }

    [Fact]
    public void ElBorradorSeCreaVacioYPuedeActualizarse()
    {
        var publicacion = new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m);

        Assert.Equal(EstadoPublicacionPrecioHuevo.Borrador, publicacion.Estado);
        Assert.Empty(publicacion.Detalles);

        publicacion.ActualizarBorrador([Datos(TamanoHuevo.Extra, 0.80m)]);

        Assert.Single(publicacion.Detalles);
        Assert.Equal(0.057m, publicacion.Servicio);
    }

    [Fact]
    public void AdmiteSeisTamanosUnicos()
    {
        var publicacion = BorradorConSeisDetalles();

        Assert.Equal(6, publicacion.Detalles.Count);
    }

    [Fact]
    public void NoAdmiteDosDetallesConElMismoTamano()
    {
        var detalles = new List<DatosDetallePrecioHuevo>
        {
            Datos(TamanoHuevo.Extra, 0.80m),
            Datos(TamanoHuevo.Primera, 0.70m),
            Datos(TamanoHuevo.Extra, 0.81m),
        };

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m, detalles));

        Assert.Equal("Cada tamaño solo puede tener un precio en la publicación.", excepcion.Message);
    }

    [Fact]
    public void ElServicioYLosPreciosDebenSerPositivos()
    {
        var excepcionServicio = Assert.Throws<ReglaNegocioException>(() =>
            new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0m));
        var excepcionPrecio = Assert.Throws<ReglaNegocioException>(() =>
            new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m,
                [Datos(TamanoHuevo.Extra, 0m)]));

        Assert.Equal("El servicio debe ser mayor que cero.", excepcionServicio.Message);
        Assert.Equal("El precio al productor debe ser mayor que cero.", excepcionPrecio.Message);
    }

    [Fact]
    public void PublicarSellaElBorrador()
    {
        var publicacion = BorradorConSeisDetalles();

        publicacion.Publicar();

        Assert.Equal(EstadoPublicacionPrecioHuevo.Publicada, publicacion.Estado);
        Assert.Throws<ReglaNegocioException>(() =>
            publicacion.ActualizarBorrador([Datos(TamanoHuevo.Extra, 0.99m)]));
    }

    [Fact]
    public void NoSePublicaUnBorradorSinDetalles()
    {
        var publicacion = new PublicacionPrecioHuevo(FechaNotificacion, FechaVigencia, 0.057m);

        var excepcion = Assert.Throws<ReglaNegocioException>(() => publicacion.Publicar());

        Assert.Equal("La publicación debe tener al menos un detalle de precio.", excepcion.Message);
        Assert.Equal(EstadoPublicacionPrecioHuevo.Borrador, publicacion.Estado);
    }

    [Fact]
    public void UnaPublicacionFuturaSePuedeAnularYUnaEfectivaNo()
    {
        var futura = BorradorConSeisDetalles();
        futura.Publicar();
        var hoy = futura.FechaVigencia.AddDays(-1);
        var efectiva = BorradorConSeisDetalles();
        efectiva.Publicar();
        var hoyDeLaEfectiva = efectiva.FechaVigencia;

        futura.AnularFutura(hoy);

        Assert.Equal(EstadoPublicacionPrecioHuevo.Anulada, futura.Estado);
        Assert.Throws<ReglaNegocioException>(() => efectiva.AnularFutura(hoyDeLaEfectiva));
        Assert.Equal(EstadoPublicacionPrecioHuevo.Publicada, efectiva.Estado);
    }

    [Fact]
    public void ElPrecioActualDelDocumentoSeConservaComoControl()
    {
        var publicacion = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.057m,
            [Datos(TamanoHuevo.Extra, 0.7954m, 0.7957m)]);

        var detalle = publicacion.Detalles.Single();

        Assert.Equal(0.7957m, detalle.PrecioActualDocumento);
        Assert.Equal(0.7954m, detalle.PrecioAlProductor);
    }

    [Fact]
    public void DescartarBorradorLoDesactivaSinBorrarlo()
    {
        var publicacion = BorradorConSeisDetalles();

        publicacion.DescartarBorrador();

        Assert.False(publicacion.EstaActivo);
        Assert.Equal(EstadoPublicacionPrecioHuevo.Borrador, publicacion.Estado);
        Assert.Equal(6, publicacion.Detalles.Count);
    }

    [Fact]
    public void DescartarNoAlcanzaAUnaPublicacionNiAUnaAnulada()
    {
        var publicada = BorradorConSeisDetalles();
        publicada.Publicar();

        var excepcion = Assert.Throws<ReglaNegocioException>(() => publicada.DescartarBorrador());

        Assert.Equal("Solo un borrador se puede descartar.", excepcion.Message);
        Assert.True(publicada.EstaActivo);
    }
}
```

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter PublicacionPrecioHuevo`
Expected: FAIL con `CS0246` (los tipos del dominio todavía no existen).

- [ ] **Step 2: Implementar `TamanoHuevo` y `EstadoPublicacionPrecioHuevo`**

```csharp
// TamanoHuevo.cs
namespace Icarus.GestionAvicola.Domain;

// Tamaños de huevo del catálogo de CAISY (glosario de dominio, spec SP9).
// Valores estables porque se persisten como entero.
public enum TamanoHuevo
{
    Extra = 0,
    Primera = 1,
    Segunda = 2,
    Tercera = 3,
    Cuarta = 4,
    Quinta = 5,
}
```

```csharp
// EstadoPublicacionPrecioHuevo.cs
namespace Icarus.GestionAvicola.Domain;

// Estados de una publicación de precio de huevo (spec SP9), mismo ciclo que
// EstadoNotificacionPreciosAlimentos: Borrador editable, Publicada inmutable
// y vigente hasta que otra publicación posterior entra en vigor, Anulada solo
// alcanza a publicaciones futuras. Valores estables.
public enum EstadoPublicacionPrecioHuevo
{
    Borrador = 0,
    Publicada = 1,
    Anulada = 2,
}
```

- [ ] **Step 3: Implementar `DetallePrecioHuevo`**

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Una fila de precio dentro de una publicación (spec SP9). PrecioAlProductor
// es el monto que se congela al despachar; el Servicio vive en la cabecera
// (Publicacion.Servicio), no por fila.
public sealed class DetallePrecioHuevo : Entity
{
    private DetallePrecioHuevo()
    {
    }

    public DetallePrecioHuevo(
        TamanoHuevo tamano, decimal precioAlProductor, decimal? precioActualDocumento = null)
    {
        if (precioAlProductor <= 0)
            throw new ReglaNegocioException("El precio al productor debe ser mayor que cero.");

        Tamano = tamano;
        PrecioAlProductor = precioAlProductor;
        PrecioActualDocumento = precioActualDocumento;
    }

    public TamanoHuevo Tamano { get; private set; }

    public decimal PrecioAlProductor { get; private set; }

    // Columna «Precio Actual» del documento (spec SP9): control de
    // publicación contra la vigente; nunca sustituye a PrecioAlProductor.
    public decimal? PrecioActualDocumento { get; private set; }
}
```

- [ ] **Step 4: Implementar `PublicacionPrecioHuevo`**

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Cabecera global de una publicación de precio de huevo (spec SP9): sin
// tenant, versionada e inmutable tras publicarse. Rige desde FechaVigencia
// hasta que otra publicación posterior entra en vigor. Servicio es un único
// valor por publicación (spec SP9, confirmado contra el documento real de
// CAISY: la boleta de recepción usa el mismo valor de servicio para las seis
// filas).
public sealed class PublicacionPrecioHuevo : AggregateRoot
{
    private readonly List<DetallePrecioHuevo> _detalles = [];

    private PublicacionPrecioHuevo()
    {
    }

    public PublicacionPrecioHuevo(DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio)
        : this(fechaNotificacion, fechaVigencia, servicio, [])
    {
    }

    public PublicacionPrecioHuevo(
        DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio,
        IReadOnlyList<DatosDetallePrecioHuevo> detalles)
    {
        AsignarDatos(fechaNotificacion, fechaVigencia, servicio);
        ReemplazarDetalles(detalles);
    }

    // Para tests que necesitan ids fijos.
    public PublicacionPrecioHuevo(Guid id, DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio)
        : this(fechaNotificacion, fechaVigencia, servicio) => Id = id;

    public DateOnly FechaNotificacion { get; private set; }

    public DateOnly FechaVigencia { get; private set; }

    public EstadoPublicacionPrecioHuevo Estado { get; private set; } = EstadoPublicacionPrecioHuevo.Borrador;

    public bool EstaActivo { get; private set; } = true;

#pragma warning disable S1144 // Setter técnico para EF rowversion
    public byte[]? Version { get; private set; }
#pragma warning restore S1144

    public Guid? DocumentoOriginalId { get; private set; }

    public decimal Servicio { get; private set; }

    public IReadOnlyCollection<DetallePrecioHuevo> Detalles => _detalles.AsReadOnly();

    public void AsignarDocumentoOriginal(Guid documentoOriginalId)
    {
        AsegurarEditable("Solo un borrador acepta un documento original.");
        DocumentoOriginalId = documentoOriginalId;
    }

    public void ActualizarBorrador(
        DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio,
        IReadOnlyList<DatosDetallePrecioHuevo> detalles)
    {
        AsegurarEditable("Una publicación ya no es editable.");
        AsignarDatos(fechaNotificacion, fechaVigencia, servicio);
        ReemplazarDetalles(detalles);
    }

    public void ActualizarBorrador(IReadOnlyList<DatosDetallePrecioHuevo> detalles) =>
        ActualizarBorrador(FechaNotificacion, FechaVigencia, Servicio, detalles);

    public void Publicar()
    {
        AsegurarEditable("La publicación ya está publicada o anulada.");
        if (_detalles.Count == 0)
            throw new ReglaNegocioException("La publicación debe tener al menos un detalle de precio.");
        Estado = EstadoPublicacionPrecioHuevo.Publicada;
    }

    public void DescartarBorrador()
    {
        AsegurarEditable("Solo un borrador se puede descartar.");
        EstaActivo = false;
    }

    public void AnularFutura(DateOnly hoy)
    {
        if (Estado != EstadoPublicacionPrecioHuevo.Publicada)
            throw new ReglaNegocioException("Solo una publicación vigente o futura se puede anular.");
        if (FechaVigencia <= hoy)
            throw new ReglaNegocioException("Una publicación ya efectiva no se puede anular.");
        Estado = EstadoPublicacionPrecioHuevo.Anulada;
    }

    private void AsegurarEditable(string mensaje)
    {
        if (Estado != EstadoPublicacionPrecioHuevo.Borrador)
            throw new ReglaNegocioException(mensaje);
    }

    private void AsignarDatos(DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio)
    {
        if (servicio <= 0)
            throw new ReglaNegocioException("El servicio debe ser mayor que cero.");

        FechaNotificacion = fechaNotificacion;
        FechaVigencia = fechaVigencia;
        Servicio = servicio;
    }

    private void ReemplazarDetalles(IReadOnlyList<DatosDetallePrecioHuevo> detalles)
    {
        var repetidos = detalles.GroupBy(d => d.Tamano).FirstOrDefault(g => g.Count() > 1);
        if (repetidos is not null)
            throw new ReglaNegocioException("Cada tamaño solo puede tener un precio en la publicación.");

        _detalles.Clear();
        foreach (var datos in detalles)
            _detalles.Add(new DetallePrecioHuevo(datos.Tamano, datos.PrecioAlProductor, datos.PrecioActualDocumento));
    }
}

public sealed record DatosDetallePrecioHuevo(
    TamanoHuevo Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento = null);
```

- [ ] **Step 5: Correr las pruebas y confirmar verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter PublicacionPrecioHuevo`
Expected: PASS (12/12).

- [ ] **Step 6: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PublicacionPrecioHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DetallePrecioHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TamanoHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoPublicacionPrecioHuevo.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/PublicacionPrecioHuevoTests.cs
git commit -m "feat(avicola): modelar publicaciones de precio de huevo"
```

---

### Task 3: Persistencia, puertos e importador Excel

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/PuertosPreciosHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionPublicacionPrecioHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDetallePrecioHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioPublicacionesPreciosHuevo.cs`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Importacion/ImportadorPublicacionPrecioHuevoExcel.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ImportadorPublicacionPrecioHuevoExcelTests.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/Fixtures/PublicacionPrecioHuevoMuestra.xlsx`
- Modify: `Icarus/tests/Icarus.IntegrationTests/` (nueva migración generada por
  `dotnet ef migrations add`)

**Interfaces:**
- Consumes: `PublicacionPrecioHuevo`, `DetallePrecioHuevo`, `TamanoHuevo`,
  `EstadoPublicacionPrecioHuevo`, `DatosDetallePrecioHuevo` (Task 2).
  `IAlmacenDocumentosPrecios` (ya existente en
  `Icarus.GestionAvicola.Application.PreciosAlimentos`, reutilizado tal cual:
  el contrato ya es genérico — `GuardarAsync(Stream)` / `AbrirAsync(Guid)` —
  no hace falta un almacén nuevo).
- Produces: `IRepositorioPublicacionesPreciosHuevo`,
  `IImportadorPublicacionPrecioHuevoExcel`, `DatosPublicacionPrecioHuevo`,
  `ErrorImportacionPrecioHuevo`, `ResultadoImportacionPrecioHuevo` — para que
  Task 4 los consuma.

**`PuertosPreciosHuevo.cs`** — mismo patrón que
`PuertosPreciosAlimentos.cs`
(`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/PuertosPreciosAlimentos.cs`),
sin puerto de PDF ni almacén propio (se reutiliza `IAlmacenDocumentosPrecios`):

```csharp
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.PreciosHuevo;

public interface IRepositorioPublicacionesPreciosHuevo
{
    void Agregar(PublicacionPrecioHuevo publicacion);

    // Los detalles recreados por ActualizarBorrador llevan clave Guid
    // generada en el dominio: se registran como Added explícitamente (mismo
    // motivo que IRepositorioNotificacionesPrecios.AgregarDetalle).
    void AgregarDetalle(DetallePrecioHuevo detalle);

    Task<PublicacionPrecioHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<PublicacionPrecioHuevo?> ObtenerVigenteAsync(
        DateOnly fecha, CancellationToken cancellationToken = default);

    Task<bool> ExistePublicadaConVigenciaIgualAsync(
        DateOnly fechaVigencia, Guid? excluyendoId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicacionPrecioHuevo>> ListarHistorialAsync(
        CancellationToken cancellationToken = default);
}

public interface IImportadorPublicacionPrecioHuevoExcel
{
    ResultadoImportacionPrecioHuevo Importar(Stream contenido);
}

public sealed record DatosPublicacionPrecioHuevo(
    DateOnly FechaNotificacion, DateOnly FechaVigencia, decimal Servicio,
    IReadOnlyList<DatosDetallePrecioHuevo> Detalles);

public sealed record ErrorImportacionPrecioHuevo(int? Fila, string Mensaje);

public sealed record ResultadoImportacionPrecioHuevo(
    DatosPublicacionPrecioHuevo? Propuesta, IReadOnlyList<ErrorImportacionPrecioHuevo> Errores);
```

**EF config** — mirroring
`ConfiguracionNotificacionPreciosAlimentos.cs`/`ConfiguracionDetallePrecioAlimento.cs`:

```csharp
// ConfiguracionPublicacionPrecioHuevo.cs
public sealed class ConfiguracionPublicacionPrecioHuevo : IEntityTypeConfiguration<PublicacionPrecioHuevo>
{
    public void Configure(EntityTypeBuilder<PublicacionPrecioHuevo> builder)
    {
        builder.ToTable("publicaciones_precios_huevo", t =>
            t.HasCheckConstraint("CK_publicaciones_precios_huevo_servicio", "[Servicio] > 0"));
        builder.Property(p => p.FechaNotificacion).HasColumnType("date");
        builder.Property(p => p.FechaVigencia).HasColumnType("date");
        builder.Property(p => p.Servicio).HasColumnType("decimal(10,4)");
        builder.Property(p => p.Estado).HasConversion<int>();
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasIndex(p => p.FechaVigencia).IsUnique()
            .HasFilter("[Estado] = 1 AND [EstaActivo] = 1");

        builder.HasMany(p => p.Detalles).WithOne()
            .HasForeignKey("PublicacionPrecioHuevoId").IsRequired();
        builder.Navigation(p => p.Detalles).HasField("_detalles");
    }
}
```

```csharp
// ConfiguracionDetallePrecioHuevo.cs
public sealed class ConfiguracionDetallePrecioHuevo : IEntityTypeConfiguration<DetallePrecioHuevo>
{
    public void Configure(EntityTypeBuilder<DetallePrecioHuevo> builder)
    {
        builder.ToTable("detalles_precio_huevo", t =>
            t.HasCheckConstraint("CK_detalles_precio_huevo_productor", "[PrecioAlProductor] > 0"));
        builder.Property(d => d.Tamano).HasConversion<int>();
        builder.Property(d => d.PrecioAlProductor).HasColumnType("decimal(10,4)");
        builder.Property(d => d.PrecioActualDocumento).HasColumnType("decimal(10,4)");

        builder.HasIndex("PublicacionPrecioHuevoId", nameof(DetallePrecioHuevo.Tamano)).IsUnique();
    }
}
```

Nota: el precio real de CAISY llega con 4 decimales (`0.7957`), por eso
`decimal(10,4)` en vez de `decimal(10,2)` como en alimento.

**Repositorio** — copiar `RepositorioNotificacionesPrecios.cs`
(`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioNotificacionesPrecios.cs`)
renombrando tipos: `db.PublicacionesPreciosHuevo` en vez de
`db.NotificacionesPreciosAlimentos`, `EstadoPublicacionPrecioHuevo.Publicada`
en vez de `EstadoNotificacionPreciosAlimentos.Publicada`, comparando
`FechaVigencia` en vez de `VigenteDesde`.

**`GestionAvicolaDbContext.cs`**: agregar

```csharp
    public DbSet<PublicacionPrecioHuevo> PublicacionesPreciosHuevo => Set<PublicacionPrecioHuevo>();
```

junto al `DbSet` existente de `NotificacionesPreciosAlimentos`, y en
`OnModelCreating`:

```csharp
        modelBuilder.Entity<PublicacionPrecioHuevo>().HasQueryFilter(p => p.EstaActivo);
```

**`DependencyInjection.cs`**: agregar junto a los registros de precios de
alimento existentes:

```csharp
        servicios.AddScoped<IRepositorioPublicacionesPreciosHuevo, RepositorioPublicacionesPreciosHuevo>();
        servicios.AddScoped<IImportadorPublicacionPrecioHuevoExcel, ImportadorPublicacionPrecioHuevoExcel>();
```

(no hace falta registrar un almacén nuevo: `IAlmacenDocumentosPrecios` ya
está registrado y se reutiliza).

**Importador Excel** — formato real confirmado con el documento
`Cambio_Precio_Huevo_2026-11-01.xlsx`: fila con etiqueta `Fecha de
Notificación:` (columna B) y fila `Fecha de Entrada en Vigencia:` (columna
B); tabla con cabecera `Tamaño / Precio Actual [Bs.] / Nuevo Precio Al
Productor [Bs.] / Servicios [Bs.] / Precio Unitario [Bs.] / Diferencia Al
Productor [Bs.]`; una fila por tamaño (`EXTRA`, `PRIMERA`, `SEGUNDA`,
`TERCERA`, `CUARTA`, `QUINTA`). **El importador solo lee `Tamaño`, `Precio
Actual`, `Nuevo Precio Al Productor` y `Servicios`** — deliberadamente
ignora `Precio Unitario` y `Diferencia Al Productor` porque en el archivo
real esas dos columnas son fórmulas sin valor calculado en caché
(`<f>C8+D8</f><v></v>`); depender de ellas sería frágil. Como el dominio
modela `Servicio` como un único valor por publicación, el importador valida
que las seis filas compartan el mismo valor de `Servicios` y, si difieren,
agrega un error de fila en vez de adivinar cuál usar.

```csharp
using System.Globalization;
using ClosedXML.Excel;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Infrastructure.Importacion;

// Importa el formato real de CAISY para el cambio de precio de huevo (spec
// SP9). Ignora deliberadamente "Precio Unitario" y "Diferencia Al Productor":
// son fórmulas sin valor en caché en el documento real.
public sealed class ImportadorPublicacionPrecioHuevoExcel : IImportadorPublicacionPrecioHuevoExcel
{
    private static readonly IReadOnlyDictionary<string, TamanoHuevo> Tamanos = new Dictionary<string, TamanoHuevo>
    {
        ["EXTRA"] = TamanoHuevo.Extra,
        ["PRIMERA"] = TamanoHuevo.Primera,
        ["SEGUNDA"] = TamanoHuevo.Segunda,
        ["TERCERA"] = TamanoHuevo.Tercera,
        ["CUARTA"] = TamanoHuevo.Cuarta,
        ["QUINTA"] = TamanoHuevo.Quinta,
    };

    public ResultadoImportacionPrecioHuevo Importar(Stream contenido)
    {
        try
        {
            using var libro = new XLWorkbook(contenido);
            var hoja = libro.Worksheets.FirstOrDefault();
            var usados = hoja?.RangeUsed();
            if (usados is null) return Error("El archivo no contiene datos.");

            DateOnly? fechaNotificacion = null;
            DateOnly? fechaVigencia = null;
            foreach (var fila in usados.Rows())
            {
                var primera = Normalizar(fila.Cell(1).GetString());
                if (primera.StartsWith("FECHA DE NOTIFICACION", StringComparison.Ordinal))
                    fechaNotificacion = LeerFecha(fila.Cell(2));
                else if (primera.StartsWith("FECHA DE ENTRADA EN VIGENCIA", StringComparison.Ordinal))
                    fechaVigencia = LeerFecha(fila.Cell(2));
            }

            var errores = new List<ErrorImportacionPrecioHuevo>();
            var encabezado = usados.Rows().FirstOrDefault(r =>
                r.CellsUsed().Any(c => Normalizar(c.GetString()) == "TAMANO"));
            if (encabezado is null)
                errores.Add(new ErrorImportacionPrecioHuevo(null, "No se encontró la cabecera de la tabla de precios."));

            var detalles = new List<DatosDetallePrecioHuevo>();
            decimal? servicioComun = null;
            if (encabezado is not null)
            {
                var columnas = Columnas(encabezado);
                foreach (var fila in usados.Rows().Where(r => r.RowNumber() > encabezado.RowNumber()))
                {
                    var tamanoTexto = Normalizar(Texto(fila, columnas.Tamano));
                    if (string.IsNullOrWhiteSpace(tamanoTexto)) continue;
                    if (!Tamanos.TryGetValue(tamanoTexto, out var tamano))
                    { errores.Add(new ErrorImportacionPrecioHuevo(fila.RowNumber(), $"El tamaño '{tamanoTexto}' no es reconocido.")); continue; }

                    var precioProductor = DecimalCelda(fila, columnas.PrecioProductor);
                    if (precioProductor is null or <= 0)
                    { errores.Add(new ErrorImportacionPrecioHuevo(fila.RowNumber(), "El precio al productor debe ser mayor que cero.")); continue; }

                    var servicio = DecimalCelda(fila, columnas.Servicio);
                    if (servicio is null or <= 0)
                    { errores.Add(new ErrorImportacionPrecioHuevo(fila.RowNumber(), "El servicio debe ser mayor que cero.")); continue; }
                    if (servicioComun is null) servicioComun = servicio;
                    else if (servicioComun != servicio)
                    { errores.Add(new ErrorImportacionPrecioHuevo(fila.RowNumber(), "El servicio debe ser el mismo para todos los tamaños.")); continue; }

                    detalles.Add(new DatosDetallePrecioHuevo(
                        tamano, precioProductor.Value, DecimalCelda(fila, columnas.PrecioActual)));
                }
            }
            if (fechaNotificacion is null) errores.Add(new ErrorImportacionPrecioHuevo(null, "No se encontró la fecha de notificación."));
            if (fechaVigencia is null) errores.Add(new ErrorImportacionPrecioHuevo(null, "No se encontró la fecha de entrada en vigencia."));
            if (detalles.Count == 0) errores.Add(new ErrorImportacionPrecioHuevo(null, "No se encontraron filas de precio."));
            if (errores.Count > 0) return new ResultadoImportacionPrecioHuevo(null, errores);

            var propuesta = new DatosPublicacionPrecioHuevo(
                fechaNotificacion!.Value, fechaVigencia!.Value, servicioComun!.Value, detalles);
            return new ResultadoImportacionPrecioHuevo(propuesta, []);
        }
        catch (Exception)
        {
            return Error("El archivo no se pudo leer como libro de Excel.");
        }
    }

    private static (int? Tamano, int? PrecioActual, int? PrecioProductor, int? Servicio) Columnas(IXLRangeRow fila)
    {
        int? tamano = null, actual = null, productor = null, servicio = null;
        foreach (var celda in fila.CellsUsed())
        {
            var nombre = Normalizar(celda.GetString());
            if (nombre == "TAMANO") tamano = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("PRECIO ACTUAL")) actual = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("NUEVO PRECIO AL PRODUCTOR")) productor = celda.Address.ColumnNumber;
            else if (nombre.StartsWith("SERVICIOS")) servicio = celda.Address.ColumnNumber;
        }
        return (tamano, actual, productor, servicio);
    }

    private static string Texto(IXLRangeRow fila, int? columna) => columna is null ? "" : fila.Cell(columna.Value).GetString().Trim();
    private static decimal? DecimalCelda(IXLRangeRow fila, int? columna)
    {
        if (columna is null) return null;
        var celda = fila.Cell(columna.Value);
        if (celda.TryGetValue<decimal>(out var valor)) return valor;
        return decimal.TryParse(celda.GetString().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out valor) ? valor : null;
    }
    private static DateOnly? LeerFecha(IXLCell celda)
    {
        if (celda.TryGetValue<DateTime>(out var fecha)) return DateOnly.FromDateTime(fecha);
        return DateTime.TryParseExact(celda.GetString().Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha) ? DateOnly.FromDateTime(fecha) : null;
    }
    private static ResultadoImportacionPrecioHuevo Error(string mensaje) => new(null, [new ErrorImportacionPrecioHuevo(null, mensaje)]);
    private static string Normalizar(string texto)
    {
        var sb = new System.Text.StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(System.Text.NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToUpperInvariant(c));
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
```

- [ ] **Step 1: Crear un fixture Excel determinístico**

Con ClosedXML (script de un solo uso, no forma parte del proyecto), generar
`Icarus/tests/Icarus.UnitTests/GestionAvicola/Fixtures/PublicacionPrecioHuevoMuestra.xlsx`
replicando la estructura real: fila 4 `Fecha de Notificación:` / `30/10/2026`,
fila 5 `Fecha de Entrada en Vigencia:` / `01/11/2026`, fila 7 cabecera
(`Tamaño`, `Precio Actual [Bs.]`, `Nuevo Precio Al Productor [Bs.]`,
`Servicios [Bs.]`, `Precio Unitario [Bs.]`, `Diferencia Al Productor [Bs.]`),
filas 8-13 con `EXTRA` .. `QUINTA`, precios y `Servicios = 0.057` en las seis
filas (valores anonimizados, no los reales del documento del usuario).
Commitear el `.xlsx` generado como fixture binario, igual que
`Fixtures/NotificacionPreciosMuestra.pdf` en SP8A.

- [ ] **Step 2: Escribir el test del importador en rojo**

```csharp
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Importacion;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ImportadorPublicacionPrecioHuevoExcelTests
{
    private static Stream AbrirFixture() =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory,
            "GestionAvicola", "Fixtures", "PublicacionPrecioHuevoMuestra.xlsx"));

    [Fact]
    public void ImportaLasSeisFilasConFechasYServicioComun()
    {
        var resultado = new ImportadorPublicacionPrecioHuevoExcel().Importar(AbrirFixture());

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Propuesta);
        Assert.Equal(new DateOnly(2026, 10, 30), resultado.Propuesta!.FechaNotificacion);
        Assert.Equal(new DateOnly(2026, 11, 1), resultado.Propuesta.FechaVigencia);
        Assert.Equal(0.057m, resultado.Propuesta.Servicio);
        Assert.Equal(6, resultado.Propuesta.Detalles.Count);
        Assert.Contains(resultado.Propuesta.Detalles, d => d.Tamano == TamanoHuevo.Extra);
    }

    [Fact]
    public void UnArchivoSinCabeceraDeTablaProduceError()
    {
        using var vacio = new MemoryStream();
        var resultado = new ImportadorPublicacionPrecioHuevoExcel().Importar(vacio);

        Assert.NotEmpty(resultado.Errores);
        Assert.Null(resultado.Propuesta);
    }
}
```

Asegurarse de que el `.csproj` de test copia la carpeta `Fixtures/` al
directorio de salida (mismo mecanismo que usa
`NotificacionPreciosMuestra.pdf`; revisar
`Icarus.UnitTests.csproj` si hace falta agregar el ítem).

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter ImportadorPublicacionPrecioHuevoExcel`
Expected: FAIL — `CS0246` (el importador todavía no existe).

- [ ] **Step 3: Implementar el importador, la persistencia y el DI**

Crear los archivos listados arriba con el contenido dado. Ejecutar

```bash
dotnet ef migrations add PreciosHuevo --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure --startup-project Icarus/src/Host/Icarus.Host
```

Verificar que la migración crea `publicaciones_precios_huevo` y
`detalles_precio_huevo` con los índices y check constraints esperados.

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter ImportadorPublicacionPrecioHuevoExcel`
Expected: PASS (2/2).

- [ ] **Step 4: Correr toda la suite de integración (Docker activo)**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests`
Expected: PASS — confirma que la migración aplica limpia contra
Testcontainers.MsSql y no rompe pruebas existentes de GestionAvicola.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/PuertosPreciosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionPublicacionPrecioHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDetallePrecioHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioPublicacionesPreciosHuevo.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Importacion/ImportadorPublicacionPrecioHuevoExcel.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs \
  Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/ \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/ImportadorPublicacionPrecioHuevoExcelTests.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/Fixtures/PublicacionPrecioHuevoMuestra.xlsx
git commit -m "feat(avicola): importar y persistir publicaciones de precio de huevo"
```

---

### Task 4: Application — comandos y consultas

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/ComandosPreciosHuevo.cs`
- Create: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosHuevoHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioPublicacionesPreciosHuevo`,
  `IImportadorPublicacionPrecioHuevoExcel`, `IAlmacenDocumentosPrecios`
  (Task 3); `IUnidadTrabajoGestionAvicola`, `IRegistroVuelo`,
  `IOperacionRegistrable`, `DescriptorOperacionRegistroVuelo`,
  `DatoRegistroVuelo`, `NotFoundException`, `ConflictException` (ya
  existentes en `Icarus.BuildingBlocks.*`, reutilizados tal cual).
- Produces: `ImportarPublicacionPrecioHuevoExcelCommand`,
  `ActualizarBorradorPrecioHuevoCommand`, `PublicarPublicacionPrecioHuevoCommand`,
  `AnularPublicacionPrecioHuevoFuturaCommand`, `DescartarBorradorPrecioHuevoCommand`,
  `ListarPublicacionesPrecioHuevoQuery`, `ObtenerPublicacionPrecioHuevoQuery`,
  `ObtenerPrecioHuevoVigenteQuery`, `DescargarDocumentoOriginalPrecioHuevoQuery`
  — para que Task 5 (API) los invoque con `ISender`.

Este archivo es un mirror casi 1:1 de
`ComandosPreciosAlimentos.cs`
(`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs`),
sin el comando de importación PDF y sin la comprobación de discrepancia por
línea al publicar (huevo no tiene el control «Precio actual» bloqueante que
tiene alimento — ver más abajo, sí se conserva como dato informativo pero no
bloquea `Publicar`). Registrar cada comando con
`IOperacionRegistrable` usando el prefijo `avicola.precios-huevo.*`:

```csharp
using FluentValidation;
using FluentValidation.Results;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.PreciosHuevo;

// Fecha de negocio del sistema: Bolivia (America/La_Paz), misma regla que
// FechasNegocio en Icarus.GestionAvicola.Application.PreciosAlimentos. Se
// duplica en este archivo (en vez de referenciar la otra carpeta de feature)
// para mantener PreciosHuevo autocontenido, siguiendo el mismo aislamiento
// por carpeta que ya separa PreciosAlimentos, PedidosAlimento y Vacunacion.
public static class FechasNegocio
{
    public static DateOnly Hoy() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz")));
}

public sealed record ImportarPublicacionPrecioHuevoExcelCommand(Stream Contenido)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.importar-excel",
        new Dictionary<string, DatoRegistroVuelo> { ["DetallesImportados"] = DatoRegistroVuelo.Entero });
}

public sealed record ActualizarBorradorPrecioHuevoCommand(
    Guid PublicacionId, DateOnly FechaNotificacion, DateOnly FechaVigencia,
    decimal Servicio, IReadOnlyList<DatosDetallePrecioHuevo> Detalles)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.actualizar-borrador",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadDetalles"] = DatoRegistroVuelo.Entero });
}

public sealed record PublicarPublicacionPrecioHuevoCommand(Guid PublicacionId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.publicar",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadDetalles"] = DatoRegistroVuelo.Entero });
}

public sealed record AnularPublicacionPrecioHuevoFuturaCommand(Guid PublicacionId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.anular-futura", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record DescartarBorradorPrecioHuevoCommand(Guid PublicacionId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.precios-huevo.descartar-borrador", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record ListarPublicacionesPrecioHuevoQuery
    : IRequest<IReadOnlyList<PublicacionPrecioHuevoResumen>>;

public sealed record PublicacionPrecioHuevoResumen(
    Guid Id, DateOnly FechaNotificacion, DateOnly FechaVigencia, string Estado,
    int CantidadDetalles, bool TieneDocumentoOriginal);

public sealed record ObtenerPublicacionPrecioHuevoQuery(Guid PublicacionId)
    : IRequest<PublicacionPrecioHuevoDetalle>;

public sealed record ObtenerPrecioHuevoVigenteQuery(DateOnly? Fecha)
    : IRequest<PublicacionPrecioHuevoDetalle?>;

public sealed record DetallePrecioHuevoResumen(
    Guid Id, string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento,
    decimal PrecioUnitario);

public sealed record PublicacionPrecioHuevoDetalle(
    Guid Id, DateOnly FechaNotificacion, DateOnly FechaVigencia, string Estado,
    decimal Servicio, Guid? DocumentoOriginalId, IReadOnlyList<DetallePrecioHuevoResumen> Detalles);

public sealed record DescargarDocumentoOriginalPrecioHuevoQuery(Guid PublicacionId)
    : IRequest<Stream>;

public sealed class ActualizarBorradorPrecioHuevoValidator : AbstractValidator<ActualizarBorradorPrecioHuevoCommand>
{
    public ActualizarBorradorPrecioHuevoValidator()
    {
        RuleFor(c => c.Servicio).GreaterThan(0);
        RuleFor(c => c.Detalles).NotNull().NotEmpty();
        RuleForEach(c => c.Detalles).ChildRules(detalle =>
            detalle.RuleFor(d => d.PrecioAlProductor).GreaterThan(0));
    }
}

public sealed class PublicarPublicacionPrecioHuevoValidator : AbstractValidator<PublicarPublicacionPrecioHuevoCommand>
{
    public PublicarPublicacionPrecioHuevoValidator() => RuleFor(c => c.PublicacionId).NotEmpty();
}

public sealed class ImportarPublicacionPrecioHuevoExcelHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IImportadorPublicacionPrecioHuevoExcel importador,
    IAlmacenDocumentosPrecios almacen,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ImportarPublicacionPrecioHuevoExcelCommand, Guid>
{
    public async Task<Guid> Handle(ImportarPublicacionPrecioHuevoExcelCommand request, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream();
        await request.Contenido.CopyToAsync(memoria, cancellationToken);
        var bytes = memoria.ToArray();
        var resultado = importador.Importar(new MemoryStream(bytes));
        if (resultado.Errores.Count > 0 || resultado.Propuesta is null)
            throw new ValidationException(resultado.Errores.Select(e => new ValidationFailure(
                "Documento", e.Fila is { } fila ? $"Fila {fila}: {e.Mensaje}" : e.Mensaje)));
        Guid documentoOriginalId;
        await using (var original = new MemoryStream(bytes))
            documentoOriginalId = await almacen.GuardarAsync(original, cancellationToken);
        var propuesta = resultado.Propuesta;
        var publicacion = new PublicacionPrecioHuevo(
            propuesta.FechaNotificacion, propuesta.FechaVigencia, propuesta.Servicio, propuesta.Detalles);
        publicacion.AsignarDocumentoOriginal(documentoOriginalId);
        repositorio.Agregar(publicacion);
        registroVuelo.Decidir("avicola.precios-huevo.importar-excel", "importacion", "aplicada",
            new Dictionary<string, object?> { ["DetallesImportados"] = propuesta.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return publicacion.Id;
    }
}

public sealed class ActualizarBorradorPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ActualizarBorradorPrecioHuevoCommand>
{
    public async Task Handle(ActualizarBorradorPrecioHuevoCommand request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        publicacion.ActualizarBorrador(
            request.FechaNotificacion, request.FechaVigencia, request.Servicio, request.Detalles);
        foreach (var detalle in publicacion.Detalles)
            repositorio.AgregarDetalle(detalle);
        registroVuelo.Decidir("avicola.precios-huevo.actualizar-borrador", "edicion", "aplicada",
            new Dictionary<string, object?> { ["CantidadDetalles"] = publicacion.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// A diferencia de alimento, publicar precio de huevo no bloquea por
// discrepancia con la vigente: GestorRecepcionHuevos es la única fuente y el
// control «Precio Actual» queda solo como referencia visual (spec SP9).
public sealed class PublicarPublicacionPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<PublicarPublicacionPrecioHuevoCommand>
{
    public async Task Handle(PublicarPublicacionPrecioHuevoCommand request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        if (await repositorio.ExistePublicadaConVigenciaIgualAsync(
                publicacion.FechaVigencia, publicacion.Id, cancellationToken))
            throw new ConflictException("Ya existe una publicación activa con esa vigencia.");
        publicacion.Publicar();
        registroVuelo.Decidir("avicola.precios-huevo.publicar", "publicacion", "aplicada",
            new Dictionary<string, object?> { ["CantidadDetalles"] = publicacion.Detalles.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AnularPublicacionPrecioHuevoFuturaHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<AnularPublicacionPrecioHuevoFuturaCommand>
{
    public async Task Handle(AnularPublicacionPrecioHuevoFuturaCommand request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        publicacion.AnularFutura(FechasNegocio.Hoy());
        registroVuelo.Decidir("avicola.precios-huevo.anular-futura", "anulacion", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DescartarBorradorPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DescartarBorradorPrecioHuevoCommand>
{
    public async Task Handle(DescartarBorradorPrecioHuevoCommand request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        publicacion.DescartarBorrador();
        registroVuelo.Decidir("avicola.precios-huevo.descartar-borrador", "borrado", "aplicada",
            new Dictionary<string, object?>());
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ListarPublicacionesPrecioHuevoHandler(IRepositorioPublicacionesPreciosHuevo repositorio)
    : IRequestHandler<ListarPublicacionesPrecioHuevoQuery, IReadOnlyList<PublicacionPrecioHuevoResumen>>
{
    public async Task<IReadOnlyList<PublicacionPrecioHuevoResumen>> Handle(
        ListarPublicacionesPrecioHuevoQuery request, CancellationToken cancellationToken) =>
        (await repositorio.ListarHistorialAsync(cancellationToken))
            .Select(p => new PublicacionPrecioHuevoResumen(
                p.Id, p.FechaNotificacion, p.FechaVigencia, p.Estado.ToString(),
                p.Detalles.Count, p.DocumentoOriginalId is not null))
            .ToList();
}

public sealed class ObtenerPublicacionPrecioHuevoHandler(IRepositorioPublicacionesPreciosHuevo repositorio)
    : IRequestHandler<ObtenerPublicacionPrecioHuevoQuery, PublicacionPrecioHuevoDetalle>
{
    public async Task<PublicacionPrecioHuevoDetalle> Handle(
        ObtenerPublicacionPrecioHuevoQuery request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        return MapeadorPreciosHuevo.Mapear(publicacion);
    }
}

public sealed class ObtenerPrecioHuevoVigenteHandler(IRepositorioPublicacionesPreciosHuevo repositorio)
    : IRequestHandler<ObtenerPrecioHuevoVigenteQuery, PublicacionPrecioHuevoDetalle?>
{
    public async Task<PublicacionPrecioHuevoDetalle?> Handle(
        ObtenerPrecioHuevoVigenteQuery request, CancellationToken cancellationToken)
    {
        var vigente = await repositorio.ObtenerVigenteAsync(
            request.Fecha ?? FechasNegocio.Hoy(), cancellationToken);
        return vigente is null ? null : MapeadorPreciosHuevo.Mapear(vigente);
    }
}

public sealed class DescargarDocumentoOriginalPrecioHuevoHandler(
    IRepositorioPublicacionesPreciosHuevo repositorio,
    IAlmacenDocumentosPrecios almacen)
    : IRequestHandler<DescargarDocumentoOriginalPrecioHuevoQuery, Stream>
{
    public async Task<Stream> Handle(
        DescargarDocumentoOriginalPrecioHuevoQuery request, CancellationToken cancellationToken)
    {
        var publicacion = await repositorio.ObtenerPorIdAsync(request.PublicacionId, cancellationToken)
            ?? throw new NotFoundException("Publicación de precio de huevo", request.PublicacionId);
        if (publicacion.DocumentoOriginalId is not { } clave)
            throw new NotFoundException("Documento original", request.PublicacionId);
        return await almacen.AbrirAsync(clave, cancellationToken)
            ?? throw new NotFoundException("Documento original", request.PublicacionId);
    }
}

internal static class MapeadorPreciosHuevo
{
    public static PublicacionPrecioHuevoDetalle Mapear(PublicacionPrecioHuevo publicacion) =>
        new(publicacion.Id, publicacion.FechaNotificacion, publicacion.FechaVigencia,
            publicacion.Estado.ToString(), publicacion.Servicio, publicacion.DocumentoOriginalId,
            publicacion.Detalles
                .OrderBy(d => d.Tamano)
                .Select(d => new DetallePrecioHuevoResumen(
                    d.Id, d.Tamano.ToString(), d.PrecioAlProductor, d.PrecioActualDocumento,
                    d.PrecioAlProductor + publicacion.Servicio))
                .ToList());
}
```

- [ ] **Step 1: Escribir los tests de handlers en rojo**

Mirror de `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs`
(si existe con ese nombre; si no, buscar el archivo de tests de handlers de
`ComandosPreciosAlimentos.cs` y usarlo como referencia exacta de cómo se
mockean `IRepositorioNotificacionesPrecios`, `IRegistroVuelo` e
`IUnidadTrabajoGestionAvicola` con NSubstitute). Cubrir como mínimo:
- `ImportarPublicacionPrecioHuevoExcelHandler` guarda el original y crea el
  borrador cuando el importador no devuelve errores.
- `ImportarPublicacionPrecioHuevoExcelHandler` lanza `ValidationException` y
  no guarda nada cuando el importador devuelve errores.
- `ActualizarBorradorPrecioHuevoHandler` lanza `NotFoundException` con un id
  inexistente.
- `PublicarPublicacionPrecioHuevoHandler` lanza `ConflictException` cuando ya
  existe una publicación activa con la misma `FechaVigencia`.
- `ObtenerPrecioHuevoVigenteHandler` devuelve `null` cuando no hay ninguna
  publicación vigente en la fecha pedida.

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter PreciosHuevoHandler`
Expected: FAIL (los tipos de `ComandosPreciosHuevo.cs` no existen todavía).

- [ ] **Step 2: Implementar `ComandosPreciosHuevo.cs`** con el contenido dado
  arriba.

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter PreciosHuevoHandler`
Expected: PASS.

- [ ] **Step 3: Registrar MediatR**

Confirmar que el registro de MediatR en
`Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs` (o donde se
registre `services.AddMediatR(...)` a nivel de ensamblado) ya escanea todo
`Icarus.GestionAvicola.Application` por convención de ensamblado — no
debería requerir un registro manual adicional por carpeta de feature.
Confirmarlo corriendo la suite completa:

Run: `dotnet test Icarus/tests/Icarus.UnitTests`
Expected: PASS (todo el proyecto, no solo el filtro de esta tarea).

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosHuevo/ComandosPreciosHuevo.cs \
  Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosHuevoHandlerTests.cs
git commit -m "feat(avicola): comandos y consultas de precios de huevo"
```

---

### Task 5: API — `/precios-huevo-caisy`

**Files:**
- Create: `Icarus/src/Host/Icarus.Host/Endpoints/PreciosHuevoEndpoints.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs`
- Create: `Icarus/tests/Icarus.IntegrationTests/PreciosHuevoEndpointsTests.cs`

**Interfaces:**
- Consumes: comandos/queries de Task 4, `PoliticasAutorizacion.FuncionalidadCaisy`,
  `FuncionalidadesCaisy.GestorRecepcionHuevos` (Task 1).
- Produces: rutas HTTP bajo `/precios-huevo-caisy` para que Task 6
  (Trajano.GestorCaisy) las consuma.

Mirror de `PreciosAlimentosEndpoints.cs`
(`Icarus/src/Host/Icarus.Host/Endpoints/PreciosAlimentosEndpoints.cs`), sin la
rama PDF (solo `.xlsx`) y con la política de `GestorRecepcionHuevos`:

```csharp
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.Identity.Domain;
using Icarus.Identity.Infrastructure.Autenticacion;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Icarus.Host.Endpoints;

// Catálogo global de publicaciones de precio de huevo (spec SP9): reservado a
// las cuentas CAISY con GestorRecepcionHuevos. Sin contenido del documento en
// los logs (anti-PII): solo ids técnicos y conteos.
public static class PreciosHuevoEndpoints
{
    private const long TamanoMaximoExcel = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapPreciosHuevo(this IEndpointRouteBuilder app)
    {
        var politica = PoliticasAutorizacion.FuncionalidadCaisy(FuncionalidadesCaisy.GestorRecepcionHuevos);
        var grupo = app.MapGroup("/precios-huevo-caisy").RequireAuthorization(politica);

        grupo.MapPost("/importar", async Task<IResult> (
            IFormFile? archivo, ISender mediator, CancellationToken cancellationToken) =>
        {
            if (archivo is null || archivo.Length == 0)
                return Results.BadRequest(new { error = "Falta el archivo Excel." });
            if (archivo.Length > TamanoMaximoExcel)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            if (Path.GetExtension(archivo.FileName).ToLowerInvariant() != ".xlsx")
                return Results.BadRequest(new { error = "Solo se acepta un archivo XLSX." });
            await using var contenido = archivo.OpenReadStream();
            var id = await mediator.Send(new ImportarPublicacionPrecioHuevoExcelCommand(contenido), cancellationToken);
            return Results.Created($"/precios-huevo-caisy/{id}", new { id });
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoExcel));

        grupo.MapGet("/", async (ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ListarPublicacionesPrecioHuevoQuery(), cancellationToken)));

        grupo.MapGet("/vigente", async Task<IResult> (
            DateOnly? fecha, ISender mediator, CancellationToken cancellationToken) =>
        {
            var vigente = await mediator.Send(new ObtenerPrecioHuevoVigenteQuery(fecha), cancellationToken);
            return vigente is null ? Results.NotFound() : Results.Ok(vigente);
        });

        grupo.MapGet("/{id:guid}", async Task<IResult> (
            Guid id, ISender mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ObtenerPublicacionPrecioHuevoQuery(id), cancellationToken)));

        grupo.MapPut("/{id:guid}", async (Guid id, ActualizarBorradorPrecioHuevoCommand comando,
            ISender mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(comando with { PublicacionId = id }, cancellationToken);
            return Results.NoContent();
        });

        grupo.MapPost("/{id:guid}/publicar", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new PublicarPublicacionPrecioHuevoCommand(id), cancellationToken);
            return Results.NoContent();
        });

        grupo.MapPost("/{id:guid}/anular", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new AnularPublicacionPrecioHuevoFuturaCommand(id), cancellationToken);
            return Results.NoContent();
        });

        grupo.MapDelete("/{id:guid}", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            await mediator.Send(new DescartarBorradorPrecioHuevoCommand(id), cancellationToken);
            return Results.NoContent();
        });

        grupo.MapGet("/{id:guid}/documento-original", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
            Results.Stream(await mediator.Send(new DescargarDocumentoOriginalPrecioHuevoQuery(id), cancellationToken),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

        return app;
    }
}
```

En `Program.cs`, junto a `api.MapPedidosAlimento();` (línea ~107):

```csharp
api.MapPreciosHuevo();
```

- [ ] **Step 1: Escribir las pruebas de integración en rojo**

Mirror de `PreciosAlimentosEndpointsTests.cs`
(`Icarus/tests/Icarus.IntegrationTests/PreciosAlimentosEndpointsTests.cs`),
adaptando el payload a XLSX y a los campos de huevo. Cubrir como mínimo:
- importar un XLSX válido crea un borrador (201 + id);
- importar sin archivo da 400; importar un `.pdf` da 400 (solo se acepta
  xlsx); un archivo mayor al límite da 413;
- editar, publicar, listar, obtener vigente y descargar el original con la
  política correcta;
- 401 sin token; 403 con una cuenta CAISY que solo tiene
  `GestorPedidoAlimento` (sin `GestorRecepcionHuevos`);
- 409 al publicar dos publicaciones con la misma `FechaVigencia`.

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter PreciosHuevoEndpointsTests`
Expected: FAIL — 404 en todas las rutas (`MapPreciosHuevo` todavía no está
registrado).

- [ ] **Step 2: Implementar el endpoint y registrar la ruta**

Crear `PreciosHuevoEndpoints.cs` con el contenido dado y agregar la línea en
`Program.cs`.

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter PreciosHuevoEndpointsTests`
Expected: PASS.

- [ ] **Step 3: Correr toda la suite de integración**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests`
Expected: PASS (Docker activo).

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/Host/Icarus.Host/Endpoints/PreciosHuevoEndpoints.cs \
  Icarus/src/Host/Icarus.Host/Program.cs \
  Icarus/tests/Icarus.IntegrationTests/PreciosHuevoEndpointsTests.cs
git commit -m "feat(api): exponer publicaciones de precio de huevo"
```

---

### Task 6: Trajano.GestorCaisy — bandeja de precios de huevo

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs`
  (y su interfaz `IApiIcarusClient` si está en archivo separado — revisar)
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosHuevoController.cs`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Index.cshtml`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Detalles.cshtml`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Importar.cshtml`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Editar.cshtml`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/ConfirmarPublicacion.cshtml`
- Create: `Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/Descartar.cshtml`
- Modify: menú/layout compartido donde ya se linkea `PreciosController`
  (buscar `ReclamosCaisy.TieneGestorPedidoAlimento` en `Views/Shared/` para
  ubicar el archivo exacto)
- Create: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosHuevoControllerTests.cs`
- Create: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPreciosHuevoTests.cs`

**Interfaces:**
- Consumes: endpoints de Task 5 vía `IApiIcarusClient`;
  `ConstantesAutorizacion.PoliticaGestorRecepcionHuevos`,
  `ReclamosCaisy.TieneGestorRecepcionHuevos` (Task 1).

**`ApiIcarusClient`** — agregar los DTOs y métodos análogos a los de precios
de alimento (`ListarNotificacionesAsync`, `ObtenerNotificacionAsync`,
`ImportarPdfAsync`, `ActualizarBorradorAsync`, `PublicarAsync`,
`AnularFuturaAsync`, `DescartarBorradorAsync`,
`DescargarDocumentoOriginalAsync`, ver
`Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs:53-135`),
apuntando a `precios-huevo-caisy` en vez de `precios-alimentos`:

```csharp
public sealed record PublicacionPrecioHuevoResumenApi(
    Guid Id, DateOnly FechaNotificacion, DateOnly FechaVigencia, string Estado,
    int CantidadDetalles, bool TieneDocumentoOriginal);

public sealed record DetallePrecioHuevoApi(
    Guid Id, string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento, decimal PrecioUnitario);

public sealed record PublicacionPrecioHuevoDetalleApi(
    Guid Id, DateOnly FechaNotificacion, DateOnly FechaVigencia, string Estado,
    decimal Servicio, Guid? DocumentoOriginalId, IReadOnlyList<DetallePrecioHuevoApi> Detalles);

public sealed record DatosDetalleHuevoApi(string Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento);

public sealed record ComandoActualizarBorradorHuevoApi(
    Guid PublicacionId, DateOnly FechaNotificacion, DateOnly FechaVigencia,
    decimal Servicio, IReadOnlyList<DatosDetalleHuevoApi> Detalles);
```

con métodos `ListarPublicacionesHuevoAsync`, `ObtenerPublicacionHuevoAsync`,
`ImportarExcelHuevoAsync(Stream contenido, string nombreArchivo, ...)`,
`ActualizarBorradorHuevoAsync`, `PublicarHuevoAsync`, `AnularFuturaHuevoAsync`,
`DescartarBorradorHuevoAsync`, `DescargarDocumentoOriginalHuevoAsync` — mismo
cuerpo que sus pares de alimento, cambiando la ruta base a
`precios-huevo-caisy` y el tipo de multipart a `.xlsx`
(`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`).

**`PreciosHuevoController.cs`** — mirror de `PreciosController.cs`
(`Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosController.cs`),
con `[Route("PreciosHuevo")]`, `[Authorize(Policy =
ConstantesAutorizacion.PoliticaGestorRecepcionHuevos)]`, sin la ruta raíz
`~/` (esa la sigue teniendo `PreciosController` para alimento), y sin acción
`Anular` opcional si se prefiere simplificar — pero por paridad con alimento,
mantenerla. Reemplazar todo campo `TipoAlimento`/`Presentacion`/`EdadDesdeDias`
por `Tamano`; `AporteCaisy`/`Fondo`/`Servicios` por el único `Servicio`.

**Vistas**: mismas seis vistas que `Views/Precios/*.cshtml`, adaptando la
tabla de detalle a una fila por tamaño (`Tamaño`, `Precio Actual`, `Precio Al
Productor`, `Precio Unitario` calculado) en vez de tipo/presentación/edad.

**Menú**: agregar un ítem "Precios de huevo" visible solo si
`ReclamosCaisy.TieneGestorRecepcionHuevos(User)` es verdadero, junto al ítem
existente de "Precios de alimento" (gateado por
`TieneGestorPedidoAlimento`).

- [ ] **Step 1: Escribir las pruebas del controller y del flujo en rojo**

Mirror de los tests existentes de `PreciosController`
(`Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/`) y del flujo
end-to-end con `WebApplicationFactory` propio de GestorCaisy
(`Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/`), cubriendo como
mínimo: acceso denegado sin la funcionalidad `GestorRecepcionHuevos`; listar,
importar, revisar borrador, publicar con confirmación explícita, descartar
con confirmación explícita, descargar original.

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter PreciosHuevo`
Expected: FAIL — el controller y las vistas no existen (`CS0246` o vista no
encontrada).

- [ ] **Step 2: Implementar cliente, controller, vistas y menú**

Crear/modificar los archivos listados arriba.

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter PreciosHuevo`
Expected: PASS.

- [ ] **Step 3: Correr toda la suite del proyecto MVC**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS — confirma que `GestorPedidoAlimento` sigue intacto y que la
prueba de arquitectura "Trajano.GestorCaisy no depende de Icarus ni de EF"
sigue verde.

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosHuevoController.cs \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/PreciosHuevo/ \
  Icarus/src/Apps/Trajano.GestorCaisy/Views/Shared/ \
  Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosHuevoControllerTests.cs \
  Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPreciosHuevoTests.cs
git commit -m "feat(gestor-caisy): publicar precios de huevo"
```

---

## Cierre SP9A

- [ ] Ejecutar toda la suite: `dotnet test Icarus/tests/Icarus.UnitTests`,
  `dotnet test Icarus/tests/Icarus.IntegrationTests` (Docker activo),
  `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`,
  `dotnet test Icarus/tests/Icarus.ArchitectureTests`.
- [ ] Ejecutar `./verify.ps1`, revisar `git diff --check` y el diff completo
  del bloque.
- [ ] Actualizar el glosario de dominio
  (`docs/dominio/glosario-avicola.md`) con "Publicación de precio de huevo" y
  "GestorRecepcionHuevos" si no quedaron ya cubiertos por el spec.
- [ ] Push a `develop` solo cuando las seis tareas estén verdes y el bloque
  esté completo (no hacer push parcial).

**No empezar SP9B (despacho desde la PWA) hasta que este plan esté cerrado e
integrado en `develop`.**
