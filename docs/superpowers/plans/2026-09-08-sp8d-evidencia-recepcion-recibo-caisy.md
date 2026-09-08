# SP8D — Evidencia fotográfica del receptor y recibo PDF de CAISY — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar la carga de respaldo fotográfico de CAISY (SP8C) por una
foto obligatoria del receptor adjuntada en el mismo paso que la confirmación
de recepción, y agregar un recibo PDF imprimible en Trajano.GestorCaisy con
los datos ya guardados del despacho.

**Architecture:** `DocumentoNotaEntrega` deja de admitir sustitución y pasa a
representar solo el respaldo del receptor; `ConfirmarRecepcionPedidoCommand`
se extiende para recibir el archivo junto con las cantidades y lo persiste en
la misma transacción que la transición de estado. Una nueva query de solo
lectura (`ObtenerReciboPedidoPdfQuery`) renderiza un PDF con QuestPDF a partir
de datos ya persistidos, sin tocar el agregado.

**Tech Stack:** .NET 10 / ASP.NET Core Minimal APIs, EF Core (SQL Server),
MediatR, FluentValidation, QuestPDF (nuevo), React + TanStack Query (PWA),
ASP.NET Core MVC (Trajano.GestorCaisy), xUnit + NSubstitute, Testcontainers.MsSql.

## Global Constraints

- Español correcto, UTF-8 sin BOM, sin mojibake, en toda cadena visible y
  comentario.
- Anti-PII: nunca registrar en el registro de vuelo/Seq nombre de archivo,
  número de nota, motivo ni contenido de imagen — solo ids técnicos, estados
  y conteos.
- TDD obligatorio: cada tarea empieza por un test en rojo.
- `./verify.ps1` (o `./verify.sh`) antes de cada commit; prohibido
  `--no-verify`.
- La feature de pedidos es siempre online: no agregar cola offline, IndexedDB
  ni sincronización diferida.
- Ningún comando mutable admite reintentos duplicados: los handlers existentes
  ya devuelven 409 al reintentar sobre un estado que cambió; conservar ese
  comportamiento.
- Licencia QuestPDF Community: gratuita mientras los ingresos anuales de la
  organización sean menores a USD 1M. Si eso deja de aplicar, hay que migrar
  a una licencia paga o a `PdfSharpCore` — dejarlo anotado, no bloquea esta
  implementación.

---

### Task 1: Simplificar `DocumentoNotaEntrega` (quitar sustitución)

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DocumentoNotaEntrega.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/EntregaPedidoAlimentoTests.cs`

**Interfaces:**
- Produces: `DocumentoNotaEntrega` sin `Activo`, `ReemplazadoPorId`,
  `FechaDesactivacionUtc` ni `Desactivar(...)`. El constructor interno
  conserva la misma firma: `(Guid claveOriginal, Guid claveVista, string mime, long tamanoBytes, long tamanoVistaBytes, string hashSha256, string nombreSeguro)`.

- [ ] **Step 1: Leer el test existente que cubre sustitución/activo**

Abrí `EntregaPedidoAlimentoTests.cs` y localizá los tests que verifican
`Activo`, `ReemplazadoPorId` o `Desactivar`. Anotá sus nombres; se van a
reemplazar en el Step 3 por un test que confirma que el documento ya no tiene
esos miembros (el compilador es la prueba: si el proyecto compila sin ellos,
quedaron eliminados).

- [ ] **Step 2: Escribir el test en rojo que fija el nuevo contrato**

```csharp
[Fact]
public void AgregarDocumentoNoExponeSustitucion()
{
    var entrega = new EntregaPedidoAlimento(
        "N-1", new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 8), null,
        [new DetalleEntregaPedidoAlimento(TipoAlimento.PosturaUno, 100)]);

    var documento = entrega.AgregarDocumento(new DocumentoNotaEntrega(
        Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512,
        new string('a', 64), "recepcion.jpg"));

    Assert.Single(entrega.Documentos);
    Assert.Same(documento, entrega.Documentos[0]);
}
```

Este test ya pasa hoy (no depende de lo que se va a borrar), así que en
rigor no está en rojo por sí solo — lo que fija el contrato es que el
proyecto deje de compilar en cuanto se borre `ReemplazarDocumento` de
`EntregaPedidoAlimento` mientras algún test viejo todavía lo invoque. Por
eso el orden real es: primero borrás los tests viejos de sustitución (Step
3), después el código de producción (Step 4); el "rojo" de esta tarea es la
falta de compilación, no un assert fallido.

- [ ] **Step 3: Borrar los tests que cubrían `Activo`/`Desactivar`/sustitución**

En `EntregaPedidoAlimentoTests.cs`, eliminá cualquier test que llame a
`ReemplazarDocumento`, lea `.Activo` o `.ReemplazadoPorId`. Agregá el test
del Step 2 en su lugar.

- [ ] **Step 4: Simplificar `DocumentoNotaEntrega.cs`**

```csharp
using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Respaldo probatorio de la nota en papel que recibe el tenant al recibir la
// mercadería (spec SP8D): una imagen guardada en un volumen privado. SQL
// conserva solo la clave lógica opaca de original y vista, el MIME, los
// tamaños, el hash SHA-256 y un nombre seguro para mostrar; nunca la ruta
// física, Base64 ni una URL pública. El contenido lo custodia
// IAlmacenDocumentosPedido. Se crea una sola vez, junto con la confirmación
// de recepción: no admite sustitución posterior porque la recepción es
// terminal.
public sealed class DocumentoNotaEntrega : Entity
{
    private DocumentoNotaEntrega()
    {
    }

    internal DocumentoNotaEntrega(
        Guid claveOriginal, Guid claveVista, string mime, long tamanoBytes,
        long tamanoVistaBytes, string hashSha256, string nombreSeguro)
    {
        ClaveOriginal = claveOriginal;
        ClaveVista = claveVista;
        Mime = mime;
        TamanoBytes = tamanoBytes;
        TamanoVistaBytes = tamanoVistaBytes;
        HashSha256 = hashSha256;
        NombreSeguro = nombreSeguro;
        FechaUtc = DateTime.UtcNow;
    }

    public Guid ClaveOriginal { get; private set; }

    public Guid ClaveVista { get; private set; }

    public string Mime { get; private set; } = string.Empty;

    public long TamanoBytes { get; private set; }

    public long TamanoVistaBytes { get; private set; }

    public string HashSha256 { get; private set; } = string.Empty;

    public string NombreSeguro { get; private set; } = string.Empty;

    public DateTime FechaUtc { get; private set; }
}

// Datos que el almacenamiento privado entrega al dominio tras validar y
// guardar el archivo; la creación de claves y hash es responsabilidad del
// almacén, nunca del agregado.
public sealed record DatosDocumentoNota(
    Guid ClaveOriginal,
    Guid ClaveVista,
    string Mime,
    long TamanoBytes,
    long TamanoVistaBytes,
    string HashSha256,
    string NombreSeguro);
```

- [ ] **Step 5: Compilar y correr los tests del proyecto de dominio**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~EntregaPedidoAlimentoTests`
Expected: el build falla si algo todavía referencia `Activo`/`Desactivar` —
seguí el error hasta el archivo que lo usa (`EntregaPedidoAlimento.cs`, se
arregla en la Tarea 2). Una vez arregladas las tareas 1-3 juntas, este
comando debe pasar en verde.

- [ ] **Step 6: Commit (junto con la Tarea 2 y 3, ver su Step final)**

No hagas commit todavía: las tareas 1-3 tocan el mismo ciclo de compilación
del proyecto de dominio. Commiteá recién al final de la Tarea 3.

---

### Task 2: Quitar `ReemplazarDocumento` de `EntregaPedidoAlimento`

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EntregaPedidoAlimento.cs`

**Interfaces:**
- Consumes: `DocumentoNotaEntrega` sin sustitución (Tarea 1).
- Produces: `EntregaPedidoAlimento.AgregarDocumento(DocumentoNotaEntrega) : DocumentoNotaEntrega` — sin cambios de firma. `ReemplazarDocumento` ya no existe.

- [ ] **Step 1: Borrar el método `ReemplazarDocumento`**

En `EntregaPedidoAlimento.cs`, eliminá el bloque completo:

```csharp
    // Sustitución con auditoría: el previo queda desactivado con la referencia
    // al nuevo; el contenido ya guardado no se toca (documentos inmutables).
    public DocumentoNotaEntrega ReemplazarDocumento(Guid documentoId, DocumentoNotaEntrega nuevo)
    {
        var previo = _documentos.SingleOrDefault(d => d.Id == documentoId && d.Activo)
            ?? throw new ReglaNegocioException("El documento a reemplazar no existe o ya fue reemplazado.");
        previo.Desactivar(nuevo.Id);
        _documentos.Add(nuevo);
        return nuevo;
    }
```

`AgregarDocumento` queda igual: sigue siendo el único punto de alta.

- [ ] **Step 2: Compilar**

Run: `dotnet build Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain`
Expected: sigue en rojo hasta la Tarea 3, porque `PedidoAlimento.cs` todavía
invoca `ReemplazarDocumento`.

---

### Task 3: `PedidoAlimento` — quitar `AgregarDocumentoNota`/`ReemplazarDocumentoNota`, extender `ConfirmarRecepcion`

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PedidoAlimento.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/RecepcionPedidoAlimentoTests.cs`

**Interfaces:**
- Consumes: `DatosDocumentoNota` (Tarea 1), `EntregaPedidoAlimento.AgregarDocumento` (Tarea 2).
- Produces: `PedidoAlimento.ConfirmarRecepcion(IReadOnlyList<DatosLineaRecepcion> lineasRecibidas, DatosDocumentoNota documentoReceptor, Guid actorId) : void`. Ya no existen `AgregarDocumentoNota`, `ReemplazarDocumentoNota` ni `AplicarSobreDespachado`.

- [ ] **Step 1: Escribir el test en rojo — recepción sin documento falla**

En `RecepcionPedidoAlimentoTests.cs`, agregá (usando el helper existente que
arma un pedido en `Despachado`; revisá el archivo para el nombre exacto del
builder, p. ej. `PedidoDespachado()` o similar ya usado por los tests de esa
clase):

```csharp
[Fact]
public void ConfirmarRecepcionAdjuntaElDocumentoDelReceptor()
{
    var pedido = PedidoDespachado(); // helper ya existente en el archivo
    var datosDocumento = new DatosDocumentoNota(
        Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 2048, 1024,
        new string('a', 64), "recepcion.jpg");

    pedido.ConfirmarRecepcion(
        [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 100)],
        datosDocumento, Guid.NewGuid());

    var documento = Assert.Single(pedido.Entrega!.Documentos);
    Assert.Equal(datosDocumento.HashSha256, documento.HashSha256);
    Assert.Equal(datosDocumento.NombreSeguro, documento.NombreSeguro);
}
```

- [ ] **Step 2: Correr el test y verificar que falla por firma**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~ConfirmarRecepcionAdjuntaElDocumentoDelReceptor`
Expected: FAIL en compilación — `ConfirmarRecepcion` todavía no acepta
`DatosDocumentoNota`.

- [ ] **Step 3: Editar `PedidoAlimento.cs`**

Borrá estos tres miembros completos:

```csharp
    public DocumentoNotaEntrega AgregarDocumentoNota(DatosDocumentoNota datos) =>
        AplicarSobreDespachado(e => e.AgregarDocumento(new DocumentoNotaEntrega(
            datos.ClaveOriginal, datos.ClaveVista, datos.Mime, datos.TamanoBytes,
            datos.TamanoVistaBytes, datos.HashSha256, datos.NombreSeguro)));

    public DocumentoNotaEntrega ReemplazarDocumentoNota(Guid documentoId, DatosDocumentoNota datos) =>
        AplicarSobreDespachado(e => e.ReemplazarDocumento(documentoId, new DocumentoNotaEntrega(
            datos.ClaveOriginal, datos.ClaveVista, datos.Mime, datos.TamanoBytes,
            datos.TamanoVistaBytes, datos.HashSha256, datos.NombreSeguro)));

    private DocumentoNotaEntrega AplicarSobreDespachado(
        Func<EntregaPedidoAlimento, DocumentoNotaEntrega> operacion)
    {
        AsegurarEstado(
            EstadoPedidoAlimento.Despachado,
            "Los respaldos de la nota se registran sobre un pedido despachado.");
        if (_entrega is null)
            throw new ReglaNegocioException("El pedido no tiene una nota registrada.");
        return operacion(_entrega);
    }
```

Cambiá la firma de `ConfirmarRecepcion` para que reciba también los datos del
documento y lo agregue a la entrega antes de calcular el resultado (el orden
no afecta el cálculo de diferencias, que sigue igual):

```csharp
    public void ConfirmarRecepcion(
        IReadOnlyList<DatosLineaRecepcion> lineasRecibidas,
        DatosDocumentoNota documentoReceptor, Guid actorId)
    {
        AsegurarEstado(EstadoPedidoAlimento.Despachado, "Solo un pedido despachado se puede recibir.");
        _entrega!.AgregarDocumento(new DocumentoNotaEntrega(
            documentoReceptor.ClaveOriginal, documentoReceptor.ClaveVista,
            documentoReceptor.Mime, documentoReceptor.TamanoBytes,
            documentoReceptor.TamanoVistaBytes, documentoReceptor.HashSha256,
            documentoReceptor.NombreSeguro));
        var recibidas = lineasRecibidas
            .GroupBy(l => l.Tipo)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.CantidadRecibida));
        // ... el resto del método sigue exactamente igual que hoy ...
    }
```

No toques nada después de esa primera parte: la comparación de líneas, el
cálculo de `DiferenciaRecepcion` y `RegistrarTransicion` quedan idénticos a
la implementación actual (líneas 242-271 del archivo original).

- [ ] **Step 4: Correr los tests de dominio completos**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PedidoAlimento|FullyQualifiedName~Recepcion|FullyQualifiedName~Entrega`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain Icarus/tests/Icarus.UnitTests/GestionAvicola
git commit -m "refactor(pedidos): mover el respaldo de la nota a la confirmacion de recepcion del receptor"
```

---

### Task 4: Aplicación — extender `ConfirmarRecepcionPedidoCommand`, quitar `AgregarDocumentoNota*`

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Documentos/AlmacenDocumentosPedido.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidosAlimentoHandlerTests.cs`

**Interfaces:**
- Consumes: `IAlmacenDocumentosPedido.GuardarAsync(Stream, CancellationToken) : Task<DocumentoAlmacenado>` (sin cambios), `PedidoAlimento.ConfirmarRecepcion(..., DatosDocumentoNota, Guid)` (Tarea 3).
- Produces: `ConfirmarRecepcionPedidoCommand(Guid PedidoId, IReadOnlyList<DatosLineaRecepcion> LineasRecibidas, Stream Contenido, string? NombreArchivo) : IRequest`. `AgregarDocumentoNotaCommand` y su handler/validator ya no existen.

- [ ] **Step 1: Escribir el test en rojo — falla sin `Contenido`**

En `PedidosAlimentoHandlerTests.cs` (o el archivo de tests de recepción que
corresponda según cómo esté organizada la suite — revisá si hay un
`ConfirmarRecepcionPedidoHandlerTests` separado; si no existe, agregalo a
`PedidosAlimentoHandlerTests.cs`):

```csharp
[Fact]
public async Task ConfirmarRecepcionSinContenidoFallaValidacion()
{
    var validador = new ConfirmarRecepcionPedidoValidator();
    var resultado = await validador.ValidateAsync(
        new ConfirmarRecepcionPedidoCommand(
            Guid.NewGuid(), [new DatosLineaRecepcion(TipoAlimento.PosturaUno, 100)],
            Contenido: null!, NombreArchivo: "foto.jpg"));

    Assert.False(resultado.IsValid);
    Assert.Contains(resultado.Errors, e => e.PropertyName == nameof(ConfirmarRecepcionPedidoCommand.Contenido));
}
```

- [ ] **Step 2: Correr y verificar que falla en compilación**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~ConfirmarRecepcionSinContenidoFallaValidacion`
Expected: FAIL — el comando todavía no tiene `Contenido` ni `NombreArchivo`.

- [ ] **Step 3: Editar `ComandosPedidosAlimento.cs`**

Borrá el bloque completo de `AgregarDocumentoNotaCommand` (record +
`IOperacionRegistrable`), su `AgregarDocumentoNotaValidator` y su
`AgregarDocumentoNotaHandler` (líneas equivalentes a las actuales 522-529,
544-552 y 567-630 del archivo original).

Cambiá `ConfirmarRecepcionPedidoCommand`:

```csharp
// Recepción (spec SP8D): el tenant confirma desde Despachado la cantidad
// realmente recibida por línea y adjunta, en el mismo envío, una foto de su
// copia de la nota — obligatoria, sin excepción. El resultado (conforme o
// con diferencias) se notifica a la bandeja de CAISY en la misma
// transacción. Los reintentos chocan con el estado y responden 409 sin
// duplicar nada.
public sealed record ConfirmarRecepcionPedidoCommand(
    Guid PedidoId, IReadOnlyList<DatosLineaRecepcion> LineasRecibidas,
    Stream Contenido, string? NombreArchivo)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.pedidos.recibir", new Dictionary<string, DatoRegistroVuelo>
        { ["Lineas"] = DatoRegistroVuelo.Entero });
}
```

Actualizá `ConfirmarRecepcionPedidoValidator` sumando la regla de
`Contenido`:

```csharp
public sealed class ConfirmarRecepcionPedidoValidator
    : AbstractValidator<ConfirmarRecepcionPedidoCommand>
{
    public ConfirmarRecepcionPedidoValidator()
    {
        RuleFor(c => c.PedidoId).NotEmpty();
        RuleFor(c => c.LineasRecibidas).NotNull().NotEmpty();
        RuleForEach(c => c.LineasRecibidas)
            .Must(l => l.CantidadRecibida >= 0)
            .WithMessage("La cantidad recibida no puede ser negativa.");
        RuleFor(c => c.Contenido).NotNull()
            .WithMessage("La foto de la nota recibida es obligatoria.");
        RuleFor(c => c.NombreArchivo).MaximumLength(260);
    }
}
```

Reescribí `ConfirmarRecepcionPedidoHandler` para guardar el archivo antes de
llamar al agregado, reutilizando el mismo saneo de nombre que tenía
`AgregarDocumentoNotaHandler` (movelo a este handler o a un método estático
compartido en el mismo archivo):

```csharp
public sealed class ConfirmarRecepcionPedidoHandler(
    IRepositorioPedidosAlimento repositorio,
    IAlmacenDocumentosPedido almacen,
    ICurrentUser usuarioActual,
    IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo,
    INotificacionesInternas notificaciones)
    : IRequestHandler<ConfirmarRecepcionPedidoCommand>
{
    public async Task Handle(ConfirmarRecepcionPedidoCommand request, CancellationToken cancellationToken)
    {
        var actorId = usuarioActual.UsuarioId
            ?? throw new UnauthorizedAccessException("La sesión no es válida.");
        var pedido = await repositorio.ObtenerPorIdAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Estado != EstadoPedidoAlimento.Despachado)
            throw new ConflictException("Solo un pedido despachado se puede recibir.");

        var guardado = await almacen.GuardarAsync(request.Contenido, cancellationToken);
        var datosDocumento = new DatosDocumentoNota(
            guardado.ClaveOriginal, guardado.ClaveVista, guardado.Mime,
            guardado.TamanoOriginalBytes, guardado.TamanoVistaBytes,
            guardado.HashSha256, SanearNombre(request.NombreArchivo));

        pedido.ConfirmarRecepcion(request.LineasRecibidas, datosDocumento, actorId);
        notificaciones.Agregar(NotificacionInterna.ParaCaisy(
            pedido.Estado == EstadoPedidoAlimento.RecibidoConforme
                ? TipoNotificacionPedido.RecepcionConforme
                : TipoNotificacionPedido.RecepcionConDiferencias,
            pedido.Id));
        registroVuelo.Decidir("avicola.pedidos.recibir", "recepcion", "aplicada",
            new Dictionary<string, object?>
            {
                ["Lineas"] = request.LineasRecibidas.Count,
                ["Diferencias"] = pedido.Recepcion!.Diferencias.Count,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }

    // Nombre seguro (spec SP8C/SP8D): sin rutas ni caracteres problemáticos;
    // conjunto explícito e independiente de la plataforma. No se guarda el
    // nombre original completo del cliente.
    private static string SanearNombre(string? nombreArchivo)
    {
        var nombre = Path.GetFileName(nombreArchivo?.Trim() ?? string.Empty);
        var sano = new string(nombre
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ' ? c : '-')
            .ToArray())
            .Replace("..", "-", StringComparison.Ordinal)
            .Trim('.', ' ');
        if (sano.Length > 200)
            sano = sano[^200..];
        return sano.Length == 0 ? "recepcion.jpg" : sano;
    }
}
```

Si un pedido queda a medio camino (falla el guardado del archivo), la
excepción propaga antes de tocar el agregado y `SaveChangesAsync` nunca se
llama: no queda transición ni notificación a medias. Si el guardado tiene
éxito pero `SaveChangesAsync` falla después, EF revierte los cambios
trackeados del agregado en memoria, pero el archivo físico ya quedó escrito
en el volumen — es un huérfano físico aceptable (mismo comportamiento que ya
tenía `AgregarDocumentoNotaHandler`; no se agrega compensación porque el spec
SP8C ya asumía este trade-off para el volumen privado).

- [ ] **Step 4: Quitar `MaxDocumentosPorNota`**

En `AlmacenDocumentosPedido.cs`, borrá la propiedad
`MaxDocumentosPorNota` de `OpcionesAlmacenDocumentosPedido` (ya no hay
comando que la necesite: la recepción crea como máximo un documento, una
sola vez).

```csharp
public sealed class OpcionesAlmacenDocumentosPedido
{
    public const string Seccion = "AlmacenDocumentosPedido";

    public string Ruta { get; set; } = string.Empty;

    public long MaxTamanoBytes { get; set; } = 5 * 1024 * 1024;

    public int MaxDimensionesPixeles { get; set; } = 8000;
}
```

Buscá otras referencias a `MaxDocumentosPorNota` en el repo (appsettings.json
de `Icarus.Host`, `docs/operacion/respaldos-notas.md`) y quitalas o
actualizalas — ver Tarea 9.

- [ ] **Step 5: Correr los tests**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PedidosAlimentoHandlerTests|FullyQualifiedName~ConfirmarRecepcion`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application Icarus/tests/Icarus.UnitTests
git commit -m "feat(pedidos): exigir foto del receptor al confirmar la recepcion y quitar la carga de CAISY"
```

---

### Task 5: Migración EF — quitar columnas de sustitución

**Files:**
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDocumentoNotaEntrega.cs`
- Create: migración nueva vía `dotnet ef migrations add` (ver Step 2)

**Interfaces:**
- Consumes: `DocumentoNotaEntrega` sin `Activo`/`ReemplazadoPorId`/`FechaDesactivacionUtc` (Tarea 1).

- [ ] **Step 1: Actualizar el mapping EF**

```csharp
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

// Respaldo privado de la nota que aporta el receptor al confirmar la
// recepción (spec SP8D): SQL guarda clave lógica, MIME, tamaños, hash y
// nombre seguro; nunca ruta física, Base64 ni URL pública. El contenido vive
// en el volumen privado.
public sealed class ConfiguracionDocumentoNotaEntrega
    : IEntityTypeConfiguration<DocumentoNotaEntrega>
{
    public void Configure(EntityTypeBuilder<DocumentoNotaEntrega> builder)
    {
        builder.ToTable("documentos_nota_entrega");
        builder.Property(d => d.Mime).HasMaxLength(50).IsRequired();
        builder.Property(d => d.HashSha256).HasMaxLength(64).IsRequired();
        builder.Property(d => d.NombreSeguro).HasMaxLength(200).IsRequired();

        builder.HasOne<EntregaPedidoAlimento>().WithMany(e => e.Documentos)
            .HasForeignKey("EntregaPedidoAlimentoId").IsRequired();

        builder.HasIndex("EntregaPedidoAlimentoId");
    }
}
```

- [ ] **Step 2: Generar la migración**

Run (desde `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure`,
con el proyecto de startup que uses habitualmente para migraciones —
revisá el resto de migraciones en la carpeta para copiar el mismo comando
`dotnet ef` con `--startup-project`):

```bash
dotnet ef migrations add RespaldoNotaSoloReceptor \
  --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure \
  --startup-project Icarus/src/Host/Icarus.Host
```

Expected: se genera `Migrations/<timestamp>_RespaldoNotaSoloReceptor.cs` con
`DropColumn` para `Activo`, `ReemplazadoPorId` y `FechaDesactivacionUtc`, y
`DropForeignKey`/`DropIndex` para la auto-referencia de sustitución que ya no
existe. Abrí el archivo generado y confirmá que el `Down()` recrea esas
columnas (rollback seguro).

- [ ] **Step 3: Aplicar la migración contra la base local (Docker corriendo)**

Run: `dotnet ef database update --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure --startup-project Icarus/src/Host/Icarus.Host`
Expected: aplica sin error.

- [ ] **Step 4: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure
git commit -m "chore(pedidos): migracion que quita las columnas de sustitucion del respaldo de nota"
```

---

### Task 6: Endpoint API — quitar carga de CAISY, multipart en `/recibir`

**Files:**
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`

**Interfaces:**
- Consumes: `ConfirmarRecepcionPedidoCommand(Guid, IReadOnlyList<DatosLineaRecepcion>, Stream, string?)` (Tarea 4).
- Produces: `POST /pedidos-alimento/{id}/recibir` ahora es `multipart/form-data` con campos `archivo` (IFormFile) y `lineas` (string JSON). `POST /pedidos-alimento-caisy/{id}/nota/documentos` ya no existe.

- [ ] **Step 1: Escribir el test de integración en rojo**

En `PedidosAlimentoEndpointsTests.cs`, agregá (seguí el patrón de
autenticación/fixture que ya usa el archivo para llegar a un pedido en
`Despachado`; el nombre exacto del helper de fixture está en ese mismo
archivo, revisalo antes de escribir el test):

```csharp
[Fact]
public async Task RecibirSinArchivoDevuelveBadRequest()
{
    var cliente = await ClienteAutenticadoAsync(); // helper ya existente
    var pedidoId = await PedidoDespachadoAsync(cliente); // helper ya existente

    using var contenido = new MultipartFormDataContent
    {
        { new StringContent("""[{"tipoAlimento":"PosturaUno","cantidadRecibida":100}]"""), "lineas" },
    };
    var respuesta = await cliente.PostAsync($"/pedidos-alimento/{pedidoId}/recibir", contenido);

    Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
}

[Fact]
public async Task CargaDeCaisyYaNoExiste()
{
    var caisy = await CaisyAutenticadoAsync(); // helper ya existente
    var respuesta = await caisy.PostAsync(
        $"/pedidos-alimento-caisy/{Guid.NewGuid()}/nota/documentos", new MultipartFormDataContent());

    Assert.True(
        respuesta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
}
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter FullyQualifiedName~RecibirSinArchivoDevuelveBadRequest|FullyQualifiedName~CargaDeCaisyYaNoExiste`
Expected: FAIL — el endpoint `/recibir` todavía espera JSON, y
`/nota/documentos` todavía existe.

- [ ] **Step 3: Editar `PedidosAlimentoEndpoints.cs`**

Reemplazá el endpoint `tenant.MapPost("/{id:guid}/recibir", ...)`:

```csharp
        // Recepción por línea con foto obligatoria (spec SP8D): el tenant
        // confirma desde Despachado la cantidad realmente recibida y adjunta
        // en el mismo envío una foto de su copia de la nota. El resultado se
        // notifica a CAISY en la misma transacción. Un reintento responde 409.
        tenant.MapPost("/{id:guid}/recibir", async Task<IResult> (
            Guid id, IFormFile? archivo, [FromForm] string lineas,
            ISender mediator, CancellationToken cancellationToken) =>
        {
            if (archivo is null || archivo.Length == 0)
                return Results.BadRequest(new { error = "Falta la foto de la nota recibida." });
            if (archivo.Length > TamanoMaximoImagen)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            List<LineaRecepcionRequest> lineasParseadas;
            try
            {
                lineasParseadas = System.Text.Json.JsonSerializer.Deserialize<List<LineaRecepcionRequest>>(
                    lineas, JsonSerializerOptions.Web) ?? [];
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.BadRequest(new { error = "Las líneas recibidas no son válidas." });
            }
            var datosLineas = lineasParseadas.Select(linea =>
            {
                if (!Enum.TryParse<TipoAlimento>(linea.TipoAlimento, true, out var tipo))
                    throw new ValidationException("El tipo de alimento indicado no existe.");
                return new DatosLineaRecepcion(tipo, linea.CantidadRecibida);
            }).ToList();
            await using var contenido = archivo.OpenReadStream();
            await mediator.Send(
                new ConfirmarRecepcionPedidoCommand(id, datosLineas, contenido, archivo.FileName),
                cancellationToken);
            return Results.NoContent();
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoImagen));
```

Agregá `using System.Text.Json;` si no está. Borrá por completo el bloque
`caisy.MapPost("/{id:guid}/nota/documentos", ...)` (comando y su
`.DisableAntiforgery().WithMetadata(...)`).

- [ ] **Step 4: Correr los tests de integración de nuevo**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter FullyQualifiedName~PedidosAlimentoEndpointsTests`
Expected: PASS (requiere Docker corriendo para Testcontainers.MsSql).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs Icarus/tests/Icarus.IntegrationTests
git commit -m "feat(pedidos): recibir pasa a multipart con foto obligatoria y se retira la carga de CAISY"
```

---

### Task 7: Recibo PDF — interfaz y query en Application

**Files:**
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ReciboPedidoAlimento.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ReciboPedidoAlimentoHandlerTests.cs` (nuevo)

**Interfaces:**
- Produces: `IReciboPedidoRenderer.RenderizarAsync(PedidoAlimento pedido, CancellationToken) : Task<byte[]>` (interfaz, implementación en Infraestructura — Tarea 8). `ObtenerReciboPedidoPdfQuery(Guid PedidoId) : IRequest<byte[]>` + su handler.

- [ ] **Step 1: Escribir el test en rojo**

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ReciboPedidoAlimentoHandlerTests
{
    [Fact]
    public async Task DevuelveNotFoundSiElPedidoNoExiste()
    {
        var repositorio = Substitute.For<IRepositorioPedidosAlimento>();
        repositorio.ObtenerConHistorialAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PedidoAlimento?)null);
        var renderer = Substitute.For<IReciboPedidoRenderer>();
        var handler = new ObtenerReciboPedidoPdfHandler(repositorio, renderer);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ObtenerReciboPedidoPdfQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
```

- [ ] **Step 2: Correr y confirmar que falla en compilación**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~ReciboPedidoAlimentoHandlerTests`
Expected: FAIL — no existen los tipos todavía.

- [ ] **Step 3: Crear `ReciboPedidoAlimento.cs`**

```csharp
using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.PedidosAlimento;

// Recibo imprimible (spec SP8D "Recibo PDF en Trajano.GestorCaisy"): consulta
// de solo lectura sobre datos ya persistidos del despacho — número y fecha de
// nota, líneas con precio y subtotal congelados, totales. No toca el
// agregado ni la máquina de estados; se puede pedir cualquier cantidad de
// veces sobre un pedido con entrega registrada.
public sealed record ObtenerReciboPedidoPdfQuery(Guid PedidoId) : IRequest<byte[]>;

// Contrato técnico del renderizado: la implementación (QuestPDF u otra) vive
// en Infraestructura, igual que IAlmacenDocumentosPedido.
public interface IReciboPedidoRenderer
{
    Task<byte[]> RenderizarAsync(PedidoAlimento pedido, CancellationToken cancellationToken = default);
}

public sealed class ObtenerReciboPedidoPdfHandler(
    IRepositorioPedidosAlimento repositorio, IReciboPedidoRenderer renderer)
    : IRequestHandler<ObtenerReciboPedidoPdfQuery, byte[]>
{
    public async Task<byte[]> Handle(ObtenerReciboPedidoPdfQuery request, CancellationToken cancellationToken)
    {
        var pedido = await repositorio.ObtenerConHistorialAsync(request.PedidoId, cancellationToken)
            ?? throw new NotFoundException("Pedido de alimento", request.PedidoId);
        if (pedido.Entrega is null)
            throw new NotFoundException("Entrega de pedido", request.PedidoId);
        return await renderer.RenderizarAsync(pedido, cancellationToken);
    }
}
```

Si `IRepositorioPedidosAlimento.ObtenerConHistorialAsync` no incluye
`Entrega`/`Detalles` en su `Include`, revisá
`RepositorioPedidosAlimento.cs` (Infraestructura) y sumá los `Include`
necesarios — el recibo necesita `pedido.Entrega.Lineas`,
`pedido.Entrega.NumeroNota/FechaNota/FechaDespacho/TotalNetoInformado` y
`pedido.Detalles` (para precio y subtotal congelados por tipo).

- [ ] **Step 4: Correr el test**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~ReciboPedidoAlimentoHandlerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application Icarus/tests/Icarus.UnitTests
git commit -m "feat(pedidos): agregar la consulta del recibo pdf del despacho"
```

---

### Task 8: Recibo PDF — renderer QuestPDF en Infraestructura

**Files:**
- Modify: `Icarus/Directory.Packages.props`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Icarus.GestionAvicola.Infrastructure.csproj`
- Create: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Documentos/ReciboPedidoRendererQuestPdf.cs`
- Modify: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- Test: `Icarus/tests/Icarus.UnitTests/GestionAvicola/ReciboPedidoRendererQuestPdfTests.cs` (nuevo)

**Interfaces:**
- Consumes: `IReciboPedidoRenderer` (Tarea 7).
- Produces: `ReciboPedidoRendererQuestPdf : IReciboPedidoRenderer`, registrado en DI.

- [ ] **Step 1: Agregar el paquete QuestPDF**

Run (desde la raíz del repo, para que resuelva la versión estable más
reciente y la fije en `Directory.Packages.props`):

```bash
dotnet add Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure package QuestPDF
```

Expected: agrega `<PackageVersion Include="QuestPDF" Version="X.Y.Z" />` a
`Icarus/Directory.Packages.props` y `<PackageReference Include="QuestPDF" />`
al `.csproj` de Infraestructura. Confirmá manualmente en ambos archivos que
quedó así (central package management exige que el `.csproj` no lleve
versión).

- [ ] **Step 2: Escribir el test en rojo**

```csharp
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Documentos;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

public class ReciboPedidoRendererQuestPdfTests
{
    [Fact]
    public async Task GeneraUnPdfNoVacio()
    {
        var pedido = PedidoAlimentoTestBuilder.Despachado(); // helper a crear/reutilizar si ya existe uno similar
        var renderer = new ReciboPedidoRendererQuestPdf();

        var bytes = await renderer.RenderizarAsync(pedido, CancellationToken.None);

        Assert.NotEmpty(bytes);
        // Firma %PDF- al inicio del archivo: confirma que es un PDF real.
        Assert.Equal("%PDF-"u8.ToArray(), bytes[..5]);
    }
}
```

Si no existe un builder de test que arme un `PedidoAlimento` en estado
`Despachado` con `Entrega` y `Detalles` cargados, revisá cómo lo arman
`RecepcionPedidoAlimentoTests.cs`/`EntregaPedidoAlimentoTests.cs` y reusá el
mismo patrón en vez de crear uno nuevo.

- [ ] **Step 3: Correr y confirmar que falla (no existe el tipo)**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~ReciboPedidoRendererQuestPdfTests`
Expected: FAIL en compilación.

- [ ] **Step 4: Implementar el renderer**

```csharp
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Icarus.GestionAvicola.Infrastructure.Documentos;

// Recibo imprimible (spec SP8D): un PDF de una página con los datos ya
// guardados del despacho, para que CAISY lo firme/selle en papel. No
// registra nada en el registro de vuelo por sí mismo — el handler que lo
// invoca es quien decide qué se audita.
public sealed class ReciboPedidoRendererQuestPdf : IReciboPedidoRenderer
{
    static ReciboPedidoRendererQuestPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> RenderizarAsync(PedidoAlimento pedido, CancellationToken cancellationToken = default)
    {
        var entrega = pedido.Entrega
            ?? throw new InvalidOperationException("El pedido no tiene entrega registrada.");
        var precios = pedido.Detalles.ToDictionary(d => d.TipoAlimento);

        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(30);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(11));

                pagina.Header().Text("Recibo de despacho de alimento")
                    .SemiBold().FontSize(18);

                pagina.Content().Column(columna =>
                {
                    columna.Spacing(8);
                    columna.Item().Text($"Número de nota: {entrega.NumeroNota}");
                    columna.Item().Text($"Fecha de nota: {entrega.FechaNota:dd/MM/yyyy}");
                    columna.Item().Text($"Fecha de despacho: {entrega.FechaDespacho:dd/MM/yyyy}");

                    columna.Item().PaddingTop(10).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(columnas =>
                        {
                            columnas.RelativeColumn(2);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                            columnas.RelativeColumn(1);
                        });
                        tabla.Header(encabezado =>
                        {
                            encabezado.Cell().Text("Tipo").SemiBold();
                            encabezado.Cell().Text("Entregado").SemiBold();
                            encabezado.Cell().Text("Precio / 40 kg").SemiBold();
                            encabezado.Cell().Text("Subtotal").SemiBold();
                        });
                        foreach (var linea in entrega.Lineas)
                        {
                            var detalle = precios.GetValueOrDefault(linea.TipoAlimento);
                            var subtotal = detalle?.PrecioFinalPor40Kg is { } precio
                                ? precio * linea.Equivalentes40Kg
                                : (decimal?)null;
                            tabla.Cell().Text(linea.TipoAlimento.ToString());
                            tabla.Cell().Text(linea.CantidadEntregada.ToString());
                            tabla.Cell().Text(detalle?.PrecioFinalPor40Kg?.ToString("0.00") ?? "—");
                            tabla.Cell().Text(subtotal?.ToString("0.00") ?? "—");
                        }
                    });

                    columna.Item().PaddingTop(10)
                        .Text($"Total informado (nota): {entrega.TotalNetoInformado?.ToString("0.00") ?? "—"}");
                    columna.Item()
                        .Text($"Total despachado (canónico): {pedido.Detalles.Sum(d => d.SubtotalSolicitado ?? 0):0.00}");

                    columna.Item().PaddingTop(30).Row(fila =>
                    {
                        fila.RelativeItem().Column(firma =>
                        {
                            firma.Item().PaddingTop(30).LineHorizontal(1);
                            firma.Item().Text("Firma y sello de CAISY");
                        });
                    });
                });
            });
        });

        return Task.FromResult(documento.GeneratePdf());
    }
}
```

Ajustá los nombres de propiedades (`LineaEntregaPedidoAlimento`,
`DetallePedidoAlimento`, etc.) a los reales del dominio si difieren — revisá
`EntregaPedidoAlimento.cs`, `PedidoAlimento.cs` y el tipo de
`pedido.Detalles` antes de compilar.

- [ ] **Step 5: Registrar en DI**

En `DependencyInjection.cs` de Infraestructura, cerca de donde se registra
`IAlmacenDocumentosPedido`, agregá:

```csharp
services.AddSingleton<IReciboPedidoRenderer, ReciboPedidoRendererQuestPdf>();
```

(Singleton porque el renderer no tiene estado propio; si `DependencyInjection.cs`
usa otro ciclo de vida para clases similares sin estado, seguí ese patrón en
su lugar.)

- [ ] **Step 6: Correr el test**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~ReciboPedidoRendererQuestPdfTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Icarus/Directory.Packages.props Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure Icarus/tests/Icarus.UnitTests
git commit -m "feat(pedidos): generar el recibo pdf del despacho con questpdf"
```

---

### Task 9: Endpoint del recibo PDF

**Files:**
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`

**Interfaces:**
- Consumes: `ObtenerReciboPedidoPdfQuery` (Tarea 7).
- Produces: `GET /pedidos-alimento-caisy/{id}/recibo.pdf`.

- [ ] **Step 1: Test en rojo**

```csharp
[Fact]
public async Task ReciboPdfDevuelveContenidoPdf()
{
    var caisy = await CaisyAutenticadoAsync();
    var pedidoId = await PedidoDespachadoAsync(caisy);

    var respuesta = await caisy.GetAsync($"/pedidos-alimento-caisy/{pedidoId}/recibo.pdf");

    Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
}
```

- [ ] **Step 2: Correr y confirmar que falla (404, ruta inexistente)**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter FullyQualifiedName~ReciboPdfDevuelveContenidoPdf`
Expected: FAIL.

- [ ] **Step 3: Agregar el endpoint**

En el grupo `caisy` de `PedidosAlimentoEndpoints.cs`, junto al resto de
acciones de despacho:

```csharp
        // Recibo imprimible (spec SP8D): PDF con los datos ya guardados del
        // despacho, reimprimible sin límite sobre cualquier pedido con
        // entrega registrada.
        caisy.MapGet("/{id:guid}/recibo.pdf", async (Guid id, ISender mediator,
            CancellationToken cancellationToken) =>
        {
            var bytes = await mediator.Send(new ObtenerReciboPedidoPdfQuery(id), cancellationToken);
            return Results.File(bytes, "application/pdf", "recibo.pdf");
        });
```

- [ ] **Step 4: Correr el test**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter FullyQualifiedName~ReciboPdfDevuelveContenidoPdf`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs Icarus/tests/Icarus.IntegrationTests
git commit -m "feat(pedidos): exponer el endpoint del recibo pdf para caisy"
```

---

### Task 10: Trajano.GestorCaisy — quitar carga de respaldo, agregar "Imprimir recibo"

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/IApiIcarusClient.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PedidosController.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Models/PedidosVistas.cs`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Despachar.cshtml`
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Pedidos/Detalles.cshtml`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/PedidosControllerTests.cs` (ajustar el archivo existente que cubra este controller — revisar su nombre exacto en la carpeta de tests antes de editar)

**Interfaces:**
- Consumes: nada nuevo del backend además de lo ya existente.
- Produces: `IApiIcarusClient.ObtenerReciboPdfAsync(Guid id, CancellationToken) : Task<Stream>`. `SubirDocumentoNotaAsync` ya no existe.

- [ ] **Step 1: Test en rojo del controller**

Ubicá el archivo de tests existente para `PedidosController` (buscá
`Trajano.GestorCaisy.Tests` por `PedidosController`). Agregá:

```csharp
[Fact]
public async Task Recibo_DevuelveArchivoPdf()
{
    var api = Substitute.For<IApiIcarusClient>();
    var pedidoId = Guid.NewGuid();
    api.ObtenerReciboPdfAsync(pedidoId, Arg.Any<CancellationToken>())
        .Returns(new MemoryStream("%PDF-1.4"u8.ToArray()));
    var controller = new PedidosController(api);

    var resultado = await controller.Recibo(pedidoId, CancellationToken.None);

    var archivo = Assert.IsType<FileStreamResult>(resultado);
    Assert.Equal("application/pdf", archivo.ContentType);
}
```

Seguí el patrón exacto de construcción de `PedidosController` (constructor,
mocks de autenticación si el archivo de tests los usa) que ya tenga el
archivo existente en vez del snippet mínimo de arriba.

- [ ] **Step 2: Correr y confirmar que falla en compilación**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~Recibo_DevuelveArchivoPdf`
Expected: FAIL.

- [ ] **Step 3: `IApiIcarusClient` — quitar `SubirDocumentoNotaAsync`, agregar `ObtenerReciboPdfAsync`**

En `IApiIcarusClient.cs`, borrá:

```csharp
    Task<Guid> SubirDocumentoNotaAsync(
        Guid id, Stream contenido, string nombreArchivo,
        Guid? reemplazaDocumentoId, CancellationToken token = default);
```

y agregá:

```csharp
    // Recibo imprimible (spec SP8D): PDF con los datos ya guardados del
    // despacho, para que CAISY lo firme/selle en papel.
    Task<Stream> ObtenerReciboPdfAsync(Guid id, CancellationToken token = default);
```

- [ ] **Step 4: `ApiIcarusClient` — implementación**

En `ApiIcarusClient.cs`, borrá `SubirDocumentoNotaAsync` y
`PeticionMultipartDocumento` (ya no tienen consumidores). Agregá, cerca de
`DescargarDocumentoNotaAsync`:

```csharp
    public async Task<Stream> ObtenerReciboPdfAsync(Guid id, CancellationToken token = default)
    {
        using var respuesta = await EnviarConSesionAsync(
            accessToken => PeticionJson(HttpMethod.Get,
                $"pedidos-alimento-caisy/{id}/recibo.pdf", accessToken), token);
        await AsegurarExitoAsync(respuesta, token);
        var memoria = new MemoryStream();
        await respuesta.Content.CopyToAsync(memoria, token);
        memoria.Position = 0;
        return memoria;
    }
```

- [ ] **Step 5: `PedidosController` — quitar carga, agregar `Recibo`**

En `Despachar` (POST), borrá el bloque que sube `formulario.Archivos` (desde
`var respaldos = 0;` hasta el `TempData["Exito"] = (respaldos, fallidos) switch { ... }`)
y reemplazá el mensaje de éxito por uno fijo:

```csharp
        TempData["Exito"] = "El pedido quedó despachado con su nota registrada.";
        return RedirectToAction(nameof(Detalles), new { id });
```

Borrá por completo la acción `AgregarNotaDocumento` (el bloque
`[HttpPost("{id:guid}/Nota/Documentos")]` ... hasta su cierre).

Agregá una acción nueva:

```csharp
    // Recibo imprimible (spec SP8D): PDF con los datos del despacho, para
    // firmar/sellar en papel antes de entregarlo al transportista.
    [HttpGet("{id:guid}/Recibo")]
    public async Task<IActionResult> Recibo(Guid id, CancellationToken token)
    {
        var contenido = await api.ObtenerReciboPdfAsync(id, token);
        return File(contenido, "application/pdf", "recibo.pdf");
    }
```

- [ ] **Step 6: `PedidosVistas.cs` — quitar `Archivos` del formulario**

En `FormularioDespachoVista`, borrá:

```csharp
    public List<IFormFile> Archivos { get; set; } = [];
```

- [ ] **Step 7: `Despachar.cshtml` — quitar el campo de archivos**

Borrá el bloque:

```html
        <div class="campo">
            <label for="Archivos">Imágenes de respaldo (páginas o reverso)</label>
            <input id="Archivos" name="Archivos" type="file" accept="image/jpeg,image/png,image/webp" multiple />
            <span class="campo__ayuda">
                Hasta 8 imágenes de 5 MB por nota. Se guardan privadas: la
                interfaz muestra una copia segura sin metadatos.
            </span>
        </div>
```

Actualizá también el párrafo de la cabecera (quitar la mención a "Las
imágenes son respaldos privados...", ya no aplica a esta pantalla).

- [ ] **Step 8: `Detalles.cshtml` — quitar el formulario de carga, agregar "Imprimir recibo"**

Reemplazá la sección "Respaldos de la nota" completa (desde
`<section class="tarjeta tarjeta--tabla">\n    <h2>Respaldos de la nota</h2>`
hasta su `</section>` de cierre, incluido el `<form asp-action="AgregarNotaDocumento" ...>`)
por una versión de solo lectura, ya que ahora esos documentos son de la
recepción del tenant y CAISY solo los consulta:

```html
@if (pedido.Recepcion is { } recepcionConDocumentos && entrega.Documentos.Count > 0)
{
    <section class="tarjeta tarjeta--tabla">
        <h2>Respaldo fotográfico del receptor</h2>
        <div class="respaldos">
            @foreach (var documento in entrega.Documentos)
            {
                <figure class="respaldos__item">
                    <img src="@Url.Action("NotaDocumento", "Pedidos", new { id = pedido.Id, documentoId = documento.Id })"
                         alt="Respaldo de la nota recibida" loading="lazy" />
                    <figcaption>@(documento.TamanoBytes / 1024) KB</figcaption>
                </figure>
            }
        </div>
    </section>
}
```

Ajustá el nombre de la variable `documento.Activo`/`.NombreSeguro` si el DTO
`DocumentoNotaApi` (revisá su definición en `Servicios/` — probablemente
junto a `PedidoDetalleApi`) todavía tiene `Activo`; si es así, quitalo ahí
también en este mismo step (ya no hay sustitución, el campo no tiene sentido).

En la sección "Resumen" (donde están los botones de acción), agregá el botón
de impresión junto al resto, visible siempre que haya entrega:

```html
        @if (pedido.Entrega is not null)
        {
            <a class="boton" asp-action="Recibo" asp-route-id="@pedido.Id" target="_blank">Imprimir recibo</a>
        }
```

(Insertalo dentro del `<div class="acciones">` existente, antes del enlace
"Volver a la bandeja".)

- [ ] **Step 9: Correr los tests del proyecto GestorCaisy**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`
Expected: PASS. Si hay tests viejos que cubrían `AgregarNotaDocumento` o
`SubirDocumentoNotaAsync`, borralos (ya no hay nada que probar ahí).

- [ ] **Step 10: Compilar toda la solución**

Run: `dotnet build Icarus/Icarus.sln`
Expected: sin errores. Prestá atención a cualquier otro sitio que referencie
`documento.Activo` en las vistas de GestorCaisy — buscalo con grep antes de
dar la tarea por terminada.

- [ ] **Step 11: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy Icarus/tests/Trajano.GestorCaisy.Tests
git commit -m "feat(gestorcaisy): quitar la carga de respaldo y agregar el recibo imprimible"
```

---

### Task 11: PWA — foto obligatoria en la confirmación de recepción

**Files:**
- Modify: `web/src/features/pedidos-alimento/api.ts`
- Modify: `web/src/features/pedidos-alimento/PedidoAlimentoDetallePage.tsx`
- Test: `web/src/features/pedidos-alimento/__tests__/PedidoAlimentoDetallePage.test.tsx` (ajustar/crear si no existe uno para esta pantalla — buscar con glob `web/src/features/pedidos-alimento/**/*.test.tsx` antes de escribir)

**Interfaces:**
- Produces: `recibirPedido(id: string, lineas: LineaRecepcionDatos[], archivo: File) : Promise<void>`. `DocumentoNota` pierde el campo `activo`.

- [ ] **Step 1: Test en rojo**

Buscá el archivo de test existente de esta pantalla (si no existe, creá uno
nuevo mínimo centrado en el botón de confirmar). El caso a cubrir:

```tsx
it('deshabilita confirmar recepción hasta elegir una foto', async () => {
  // renderizar la página con un pedido en estado Despachado (usar el mismo
  // mock/fixture que ya use el resto de tests de esta pantalla)
  render(<PedidoAlimentoDetallePage />, { wrapper: crearWrapperDeTest() });

  const boton = await screen.findByRole('button', { name: /confirmar recepción/i });
  expect(boton).toBeDisabled();
});
```

Ajustá el nombre del wrapper/fixture al que ya use el resto de la suite de
esta feature — revisá otros `*.test.tsx` de `pedidos-alimento` antes de
escribir este.

- [ ] **Step 2: Correr y confirmar que falla**

Run: `npm --prefix web test -- PedidoAlimentoDetallePage`
Expected: FAIL — hoy el botón no depende de ninguna foto.

- [ ] **Step 3: `api.ts` — cambiar `recibirPedido` y quitar `activo`**

```typescript
export interface DocumentoNota {
  id: string;
  nombreSeguro: string;
  mime: string;
  tamanoBytes: number;
}
```

```typescript
// Recepción con foto obligatoria (spec SP8D): el tenant confirma desde
// Despachado la cantidad realmente recibida y adjunta, en el mismo envío,
// una foto de su copia de la nota. El estado final lo decide el backend.
export const recibirPedido = (id: string, lineas: LineaRecepcionDatos[], archivo: File) => {
  const formData = new FormData();
  formData.append('lineas', JSON.stringify(lineas));
  formData.append('archivo', archivo, archivo.name);
  return peticion<void>({ ruta: `/pedidos-alimento/${id}/recibir`, metodo: 'POST', cuerpo: formData });
};
```

`peticion()` en `lib/http.ts` ya detecta `FormData` y no le fuerza
`Content-Type` ni lo serializa como JSON (línea 102 de ese archivo) — no
hace falta tocar `http.ts`.

- [ ] **Step 4: `PedidoAlimentoDetallePage.tsx` — input de foto, compresión y envío**

Agregá estado y una función de compresión client-side arriba del componente:

```typescript
// Compresión client-side (spec SP8D): pensada para conectividad rural, no
// reemplaza el reprocesamiento del servidor (que igual corrige orientación y
// quita metadatos). Si el navegador no soporta canvas, se sube el archivo
// original sin tocar y el servidor lo reprocesa igual.
async function comprimirImagen(archivo: File): Promise<File> {
  if (typeof document === 'undefined' || typeof createImageBitmap !== 'function') return archivo;
  try {
    const bitmap = await createImageBitmap(archivo);
    const ladoMaximo = 1600;
    const escala = Math.min(1, ladoMaximo / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bitmap.width * escala);
    canvas.height = Math.round(bitmap.height * escala);
    const contexto = canvas.getContext('2d');
    if (!contexto) return archivo;
    contexto.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, 'image/jpeg', 0.7),
    );
    if (!blob) return archivo;
    return new File([blob], archivo.name, { type: 'image/jpeg' });
  } catch {
    return archivo;
  }
}
```

En el componente, sumá el estado de la foto:

```typescript
  const [fotoRecepcion, setFotoRecepcion] = useState<File | null>(null);
```

Cambiá la mutation `recibir`:

```typescript
  const recibir = useMutation({
    mutationFn: () => {
      if (!fotoRecepcion) throw new Error('Falta la foto de la nota recibida.');
      return recibirPedido(
        id!,
        pedido!.entrega!.lineas.map((l) => ({
          tipoAlimento: l.tipoAlimento,
          cantidadRecibida: Number(recibidas[l.tipoAlimento] ?? l.cantidadEntregada),
        })),
        fotoRecepcion,
      );
    },
    onSuccess: () => {
      setConfirmarRecepcion(false);
      setFotoRecepcion(null);
      setError(null);
      refrescar();
    },
    onError: (e) => setError(e instanceof Error ? e.message : 'No se pudo confirmar la recepción.'),
  });
```

En el bloque `pedido.estado === 'Despachado'` (el `<Paper>` de "Confirmar
recepción"), agregá el input de archivo antes del botón:

```tsx
            <Stack spacing={1} sx={{ mt: 2 }}>
              <Button variant="outlined" component="label">
                {fotoRecepcion ? `Foto elegida: ${fotoRecepcion.name}` : 'Elegir foto de la nota recibida'}
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  hidden
                  onChange={async (e) => {
                    const archivo = e.target.files?.[0];
                    if (archivo) setFotoRecepcion(await comprimirImagen(archivo));
                  }}
                />
              </Button>
            </Stack>
          </Stack>
          <Button
            variant="contained"
            onClick={() => setConfirmarRecepcion(true)}
            sx={{ mt: 2 }}
            disabled={!fotoRecepcion}
          >
            Confirmar recepción
          </Button>
```

(El primer `</Stack>` de arriba cierra el `<Stack spacing={1}>` que ya
envuelve las líneas de cantidad — revisá la indentación real del archivo al
insertar, el snippet asume que el nuevo bloque de foto va inmediatamente
después del `.map` de líneas y antes del botón "Confirmar recepción" que ya
existe.)

En `RespaldoNota`, quitá toda referencia a `documento.activo` (la prop
`opacity: documento.activo ? 1 : 0.4` y el texto `(reemplazado)`), ya que el
documento nunca se reemplaza:

```tsx
    <Paper variant="outlined" sx={{ p: 1, width: 170 }}>
      {error ? (
        <Typography variant="caption" color="text.secondary">
          Sin vista previa
        </Typography>
      ) : url ? (
        <Box
          component="img"
          src={url}
          alt={`Respaldo de la nota: ${documento.nombreSeguro}`}
          sx={{ width: '100%', height: 120, objectFit: 'cover', borderRadius: 1 }}
        />
      ) : (
        <Box sx={{ width: '100%', height: 120, bgcolor: 'grey.100', borderRadius: 1 }} />
      )}
      <Typography variant="caption" sx={{ wordBreak: 'break-all', display: 'block' }}>
        {documento.nombreSeguro}
      </Typography>
      <Button size="small" onClick={() => void descargarOriginal()}>
        Descargar original
      </Button>
    </Paper>
```

- [ ] **Step 5: Correr los tests de la feature**

Run: `npm --prefix web test -- pedidos-alimento`
Expected: PASS. Revisá también `npm --prefix web run typecheck` (o el script
equivalente definido en `web/package.json`) porque `DocumentoNota` perdió un
campo.

- [ ] **Step 6: Commit**

```bash
git add web/src/features/pedidos-alimento
git commit -m "feat(pedidos): exigir foto en la confirmacion de recepcion con compresion client-side"
```

---

### Task 12: Documentación y limpieza final

**Files:**
- Modify: `docs/operacion/respaldos-notas.md`
- Modify: `Icarus/src/Host/Icarus.Host/appsettings.json` (si define `MaxDocumentosPorNota`)

**Interfaces:** ninguna — solo texto y configuración.

- [ ] **Step 1: Buscar referencias residuales**

Run: `grep -rn "MaxDocumentosPorNota\|ReemplazarDocumentoNota\|AgregarDocumentoNota\|SubirDocumentoNotaAsync" Icarus web docs --include="*.cs" --include="*.ts" --include="*.tsx" --include="*.cshtml" --include="*.json" --include="*.md"`

(en PowerShell: `Select-String -Path Icarus,web,docs -Pattern "MaxDocumentosPorNota|ReemplazarDocumentoNota|AgregarDocumentoNota|SubirDocumentoNotaAsync" -Recurse`)

Expected: solo coincidencias dentro de migraciones EF ya generadas (las
columnas viejas en el `Down()` de la migración de la Tarea 5 son correctas y
se dejan) y, si aplica, `appsettings.json`.

- [ ] **Step 2: Quitar `MaxDocumentosPorNota` de `appsettings.json`**

Si `Icarus/src/Host/Icarus.Host/appsettings.json` tiene una sección
`AlmacenDocumentosPedido` con `MaxDocumentosPorNota`, borrá esa línea (las
demás — `Ruta`, `MaxTamanoBytes`, `MaxDimensionesPixeles` — quedan igual).

- [ ] **Step 3: Actualizar `docs/operacion/respaldos-notas.md`**

Reemplazá cualquier mención a "CAISY" como quien sube el respaldo por "el
receptor". Puntos concretos a revisar (el archivo completo ya se leyó
durante el brainstorming; localizá estas frases y ajustalas):

- La tabla "Qué respaldar": la fila `documentos-pedidos` sigue igual en
  contenido técnico, pero la descripción pasa de "notas de alimento" en
  general a "respaldo fotográfico del receptor al confirmar recepción".
- La sección "Cuotas y límites": quitar la fila `MaxDocumentosPorNota` de la
  tabla (ya no existe esa opción) y la frase final sobre "subir respaldos
  solo es posible sobre pedidos despachados" — reemplazarla por: "la foto se
  sube en el mismo paso que la confirmación de recepción, nunca por
  separado".
- La sección "Monitorización": el punto de Seq que menciona "la bandeja
  (CAISY)" pasa a "el evento de recepción del tenant".

- [ ] **Step 4: Verificar mojibake y enlaces del cambio documental**

Run: `./quality/verificar-mojibake.ps1 docs/operacion/respaldos-notas.md`
(o el script equivalente de `quality/` que ya use el proyecto para este gate
— revisar `docs/ai/PUERTA_CALIDAD.md` si el nombre exacto difiere).

- [ ] **Step 5: Commit**

```bash
git add docs/operacion/respaldos-notas.md Icarus/src/Host/Icarus.Host/appsettings.json
git commit -m "docs(operacion): actualizar respaldos-notas.md al respaldo del receptor"
```

---

### Task 13: Puerta de calidad completa y cierre

**Files:** ninguno nuevo — solo verificación.

- [ ] **Step 1: Correr la puerta de calidad completa**

Run: `./verify.ps1` (o `./verify.sh` según el entorno)
Expected: todos los gates en verde, incluidos los tests de integración con
Docker corriendo (Testcontainers.MsSql).

- [ ] **Step 2: Si algo falla, arreglar el contenido — nunca relajar el gate**

Según AGENTS.md: "Si el gate falla, se arregla el contenido, no el gate."
No usar `--no-verify` en ningún commit ni push.

- [ ] **Step 3: Informar pruebas no ejecutadas, si las hay**

Si algún paso de este plan no pudo correrse (por ejemplo, Docker no estaba
disponible para los tests de integración), dejarlo explícito en el mensaje
final al usuario: qué no se ejecutó y por qué, en vez de asumir que pasó.

- [ ] **Step 4: Push (solo si el usuario lo pide explícitamente)**

`develop` es la rama de trabajo por defecto: commit y push directos tras
verificar, sin PR — pero el push a remoto se confirma con el usuario antes
de ejecutarlo, según las reglas generales de la sesión.
