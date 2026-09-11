using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Application.PreciosHuevo;
using Icarus.GestionAvicola.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using FechasNegocio = Icarus.GestionAvicola.Application.PreciosHuevo.FechasNegocio;

namespace Icarus.UnitTests.GestionAvicola;

// SP9D (spec: "Corrección de una publicación vigente"): la vista previa no
// persiste nada; confirmar publica la correctiva, corrige la errónea y
// genera un ajuste + notificación por cada despacho Recibido con diferencia
// distinta de cero. Los despachos Despachado no se tocan acá — su
// reconciliación es perezosa, en ConfirmarRecepcion.
public class CorregirPublicacionPrecioHuevoHandlerTests
{
    private static readonly DateOnly FechaNotificacion = FechasNegocio.Hoy().AddDays(-15);
    private static readonly DateOnly FechaVigencia = FechasNegocio.Hoy().AddDays(-10);

    private readonly IRepositorioPublicacionesPreciosHuevo _repositorioPrecios =
        Substitute.For<IRepositorioPublicacionesPreciosHuevo>();
    private readonly IRepositorioDespachosHuevo _repositorioDespachos =
        Substitute.For<IRepositorioDespachosHuevo>();
    private readonly IRepositorioAjustesCreditoHuevo _repositorioAjustes =
        Substitute.For<IRepositorioAjustesCreditoHuevo>();
    private readonly INotificacionesInternasDespachoHuevo _notificaciones =
        Substitute.For<INotificacionesInternasDespachoHuevo>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _registroVuelo =
        new RegistroVuelo(NullLogger<RegistroVuelo>.Instance);
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();

    public CorregirPublicacionPrecioHuevoHandlerTests()
    {
        _usuarioActual.UsuarioId.Returns(Guid.NewGuid());
        _repositorioPrecios.IniciarTransaccionAsync(Arg.Any<CancellationToken>())
            .Returns(Substitute.For<ITransaccionPreciosHuevo>());
    }

    private CorregirPublicacionPrecioHuevoVigenteHandler CrearHandler() => new(
        _repositorioPrecios, _repositorioDespachos, _repositorioAjustes, _notificaciones,
        _usuarioActual, _registroVuelo, _unidadTrabajo);

    private static PublicacionPrecioHuevo PublicacionVigente(decimal precioExtra, decimal servicio = 0.05m)
    {
        var publicacion = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, servicio, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, precioExtra)]);
        publicacion.Publicar();
        return publicacion;
    }

    private static DespachoHuevo DespachoRecibidoQueUso(Guid publicacionId, Guid clienteId)
    {
        var despacho = new DespachoHuevo(clienteId, Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0)]);
        despacho.Despachar(FechaVigencia, Guid.NewGuid(),
            [new DatosPrecioDespachoHuevo(TamanoHuevo.Extra, 0.75m, publicacionId)],
            new DatosDocumentoNota(Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash", "nota.jpg"));
        despacho.ConfirmarRecepcion(FechaVigencia.AddDays(1), Guid.NewGuid());
        return despacho;
    }

    [Fact]
    public async Task ConfirmarCorrigeLaErroneaPublicaLaCorrectivaYAjustaLosRecibidos()
    {
        var erronea = PublicacionVigente(0.75m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.05m, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.85m)]);
        var clienteId = Guid.NewGuid();
        var despacho = DespachoRecibidoQueUso(erronea.Id, clienteId);
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, Arg.Any<CancellationToken>())
            .Returns([despacho]);

        await CrearHandler().Handle(
            new CorregirPublicacionPrecioHuevoVigenteCommand(erronea.Id, correctiva.Id, "Precio mal digitado."),
            CancellationToken.None);

        Assert.Equal(EstadoPublicacionPrecioHuevo.Corregida, erronea.Estado);
        Assert.Equal(correctiva.Id, erronea.PublicacionCorrectivaId);
        Assert.Equal(EstadoPublicacionPrecioHuevo.Publicada, correctiva.Estado);
        // Diferencia: (0.85 + 0.05) - 0.75 congelado = 0.15 por huevo, y la
        // línea es 1 amarra = 180 huevos (DetalleDespachoHuevo.HuevosPorAmarra):
        // 0.15 * 180 = 27.
        _repositorioAjustes.Received(1).Agregar(Arg.Is<AjusteCreditoHuevo>(a =>
            a.ClienteId == clienteId && a.DespachoHuevoId == despacho.Id && a.Monto == 27m));
        // El Meta lleva el monto con CUATRO decimales (ítem 4 del backlog: el
        // crédito de huevo se muestra con cuatro en toda la aplicación, y
        // AjustesCreditoHuevo.tsx usa formatoMonedaExacta para el mismo
        // monto). La PWA muestra este texto crudo, así que el formato es
        // contrato de presentación.
        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInternaDespachoHuevo>(n =>
            n.Tipo == TipoNotificacionDespachoHuevo.AjusteCredito &&
            n.DespachoHuevoId == despacho.Id && n.ClienteId == clienteId &&
            n.Meta == "27.0000 Bs — Precio mal digitado."));
        // La corrección se persiste en dos SaveChanges ordenados dentro de la
        // misma transacción explícita (spec SP9D addendum): primero la
        // errónea sale de Publicada, después entra la correctiva.
        await _unidadTrabajo.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnDespachoSinDiferenciaNoGeneraAjuste()
    {
        var erronea = PublicacionVigente(0.75m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.05m, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.70m)]);
        var despacho = DespachoRecibidoQueUso(erronea.Id, Guid.NewGuid());
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, Arg.Any<CancellationToken>())
            .Returns([despacho]);

        await CrearHandler().Handle(
            new CorregirPublicacionPrecioHuevoVigenteCommand(erronea.Id, correctiva.Id, "Revisión sin cambios."),
            CancellationToken.None);

        _repositorioAjustes.DidNotReceive().Agregar(Arg.Any<AjusteCreditoHuevo>());
        _notificaciones.DidNotReceive().Agregar(Arg.Any<NotificacionInternaDespachoHuevo>());
    }

    [Fact]
    public async Task UnMotivoLargoNoDesbordaElMetaDeLaNotificacion()
    {
        var erronea = PublicacionVigente(0.75m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.05m, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.85m)]);
        var despacho = DespachoRecibidoQueUso(erronea.Id, Guid.NewGuid());
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioDespachos.ListarRecibidosPorPublicacionAsync(erronea.Id, Arg.Any<CancellationToken>())
            .Returns([despacho]);
        // El validator admite hasta 500 caracteres de motivo, pero
        // NotificacionInternaDespachoHuevo.Meta es nvarchar(500): el texto
        // compuesto «monto — motivo» debe truncar el motivo para no
        // desbordar la columna en el SaveChanges.
        var motivo = new string('x', 500);
        var capturadas = new List<NotificacionInternaDespachoHuevo>();
        _notificaciones.When(n => n.Agregar(Arg.Any<NotificacionInternaDespachoHuevo>()))
            .Do(llamada => capturadas.Add(llamada.Arg<NotificacionInternaDespachoHuevo>()));

        await CrearHandler().Handle(
            new CorregirPublicacionPrecioHuevoVigenteCommand(erronea.Id, correctiva.Id, motivo),
            CancellationToken.None);

        var meta = Assert.Single(capturadas).Meta;
        Assert.True(meta!.Length <= 500);
        Assert.Contains(motivo[..100], meta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaCorrectivaConVigenciaFuturaSeRechaza()
    {
        var erronea = PublicacionVigente(0.75m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechasNegocio.Hoy().AddDays(5), 0.05m,
            [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.85m)]);
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(erronea);

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            CrearHandler().Handle(
                new CorregirPublicacionPrecioHuevoVigenteCommand(erronea.Id, correctiva.Id, "motivo"),
                CancellationToken.None));

        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SoloSePuedeCorregirLaPublicacionRealmenteVigente()
    {
        var erronea = PublicacionVigente(0.75m);
        var otraVigente = PublicacionVigente(0.60m);
        var correctiva = new PublicacionPrecioHuevo(
            FechaNotificacion, FechaVigencia, 0.05m, [new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 0.85m)]);
        _repositorioPrecios.ObtenerPorIdAsync(erronea.Id, Arg.Any<CancellationToken>()).Returns(erronea);
        _repositorioPrecios.ObtenerPorIdAsync(correctiva.Id, Arg.Any<CancellationToken>()).Returns(correctiva);
        // La vigente real es otra distinta de "erronea" (por ejemplo, una
        // publicación histórica ya superada, no la efectiva actual).
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(otraVigente);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CrearHandler().Handle(
                new CorregirPublicacionPrecioHuevoVigenteCommand(erronea.Id, correctiva.Id, "motivo"),
                CancellationToken.None));

        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
