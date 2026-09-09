using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9C Task 4 (spec: "Confirmar recepción y crédito"): CAISY confirma la
// recepción de un despacho despachado; se fija la fecha de negocio del
// servidor, se notifica a la bandeja del tenant y los reintentos chocan con
// el estado (409) sin duplicar nada.
public class ConfirmarRecepcionDespachoHuevoHandlerTests
{
    private readonly IRepositorioDespachosHuevo _repositorio = Substitute.For<IRepositorioDespachosHuevo>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _registroVuelo =
        new RegistroVuelo(NullLogger<RegistroVuelo>.Instance);
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly INotificacionesInternasDespachoHuevo _notificaciones =
        Substitute.For<INotificacionesInternasDespachoHuevo>();

    public ConfirmarRecepcionDespachoHuevoHandlerTests()
    {
        _usuarioActual.UsuarioId.Returns(Guid.NewGuid());
    }

    private ConfirmarRecepcionDespachoHuevoHandler CrearHandler() =>
        new(_repositorio, _usuarioActual, _registroVuelo, _unidadTrabajo, _notificaciones);

    private static DespachoHuevo CrearDespachado()
    {
        var despacho = new DespachoHuevo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Primera, 2, 30)]);
        despacho.Despachar(FechasNegocio.Hoy(), Guid.NewGuid(),
            [new DatosPrecioDespachoHuevo(TamanoHuevo.Primera, 12.50m, Guid.NewGuid())],
            new DatosDocumentoNota(
                Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash-sha256", "nota.jpg"));
        return despacho;
    }

    [Fact]
    public async Task RecibirDespachadoConfirmaYNotificaAlTenant()
    {
        var despacho = CrearDespachado();
        _repositorio.ObtenerPorIdAsync(despacho.Id, Arg.Any<CancellationToken>()).Returns(despacho);

        await CrearHandler().Handle(
            new ConfirmarRecepcionDespachoHuevoCommand(despacho.Id), CancellationToken.None);

        Assert.Equal(EstadoDespachoHuevo.Recibido, despacho.Estado);
        Assert.Equal(FechasNegocio.Hoy(), despacho.FechaRecepcion);
        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInternaDespachoHuevo>(n =>
            n.Tipo == TipoNotificacionDespachoHuevo.DespachoRecibido &&
            n.DespachoHuevoId == despacho.Id &&
            n.ClienteId == despacho.ClienteId));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecibirUnDespachoNoDespachadoDevuelveConflictoSinGuardar()
    {
        var despacho = new DespachoHuevo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Primera, 2, 30)]);
        _repositorio.ObtenerPorIdAsync(despacho.Id, Arg.Any<CancellationToken>()).Returns(despacho);

        var excepcion = await Assert.ThrowsAsync<ConflictException>(() =>
            CrearHandler().Handle(
                new ConfirmarRecepcionDespachoHuevoCommand(despacho.Id), CancellationToken.None));

        Assert.Equal("Solo un despacho despachado se puede recibir.", excepcion.Message);
        Assert.Equal(EstadoDespachoHuevo.Borrador, despacho.Estado);
        Assert.Null(despacho.FechaRecepcion);
        _notificaciones.DidNotReceive().Agregar(Arg.Any<NotificacionInternaDespachoHuevo>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecibirUnIdAjenoDevuelveNoEncontrado()
    {
        _repositorio.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((DespachoHuevo?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearHandler().Handle(
                new ConfirmarRecepcionDespachoHuevoCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
