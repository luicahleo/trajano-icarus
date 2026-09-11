using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.NotificacionesDespachoHuevo;
using Icarus.GestionAvicola.Domain;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP9F: los tres handlers de la bandeja del despacho de huevo derivan del rol
// el conjunto de tipos visibles y lo bajan al repositorio, para que el
// listado y el contador no puedan desincronizarse. Hasta esta feature no
// había ninguna prueba de estos handlers.
public class NotificacionesDespachoHuevoHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();

    private readonly INotificacionesInternasDespachoHuevo _repositorio =
        Substitute.For<INotificacionesInternasDespachoHuevo>();

    private static ICurrentUser Usuario(string? rol, Guid? clienteId)
    {
        var usuario = Substitute.For<ICurrentUser>();
        usuario.Rol.Returns(rol);
        usuario.ClienteId.Returns(clienteId);
        usuario.UsuarioId.Returns(Guid.NewGuid());
        return usuario;
    }

    [Fact]
    public async Task ElListadoDelTrabajadorPideSoloLosTiposOperativos()
    {
        _repositorio.ListarAsync(
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await new ListarNotificacionesDespachoHuevoHandler(
                _repositorio, Usuario("Trabajador", ClienteId))
            .Handle(new ListarNotificacionesDespachoHuevoQuery(), CancellationToken.None);

        await _repositorio.Received(1).ListarAsync(
            ClienteId,
            Arg.Is<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(tipos =>
                tipos.Contains(TipoNotificacionDespachoHuevo.DespachoRecibido)
                && !tipos.Contains(TipoNotificacionDespachoHuevo.AjusteCredito)
                && !tipos.Contains(TipoNotificacionDespachoHuevo.CreditoInsuficiente)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElListadoDelClientePideTodosLosTipos()
    {
        _repositorio.ListarAsync(
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await new ListarNotificacionesDespachoHuevoHandler(
                _repositorio, Usuario("Cliente", ClienteId))
            .Handle(new ListarNotificacionesDespachoHuevoQuery(), CancellationToken.None);

        await _repositorio.Received(1).ListarAsync(
            ClienteId,
            Arg.Is<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(tipos =>
                tipos.Count == Enum.GetValues<TipoNotificacionDespachoHuevo>().Length),
            Arg.Any<CancellationToken>());
    }

    // Regresión de la bandeja global ya desplegada: una cuenta de CAISY lleva
    // ClienteId nulo y tiene que seguir viendo CreditoInsuficiente.
    [Fact]
    public async Task ElListadoDeCaisyConservaElAlcanceGlobalYLosTiposFinancieros()
    {
        _repositorio.ListarAsync(
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        await new ListarNotificacionesDespachoHuevoHandler(
                _repositorio, Usuario("GestorCaisy", null))
            .Handle(new ListarNotificacionesDespachoHuevoQuery(), CancellationToken.None);

        await _repositorio.Received(1).ListarAsync(
            null,
            Arg.Is<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(tipos =>
                tipos.Contains(TipoNotificacionDespachoHuevo.CreditoInsuficiente)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElListadoOrdenaDeLoMasNuevoALoMasViejo()
    {
        var vieja = NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(Guid.NewGuid(), ClienteId);
        var nueva = NotificacionInternaDespachoHuevo.ParaAjusteCredito(
            Guid.NewGuid(), ClienteId, "27.0000 Bs — Precio mal digitado.");
        _repositorio.ListarAsync(
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(),
                Arg.Any<CancellationToken>())
            .Returns([vieja, nueva]);

        var resultado = await new ListarNotificacionesDespachoHuevoHandler(
                _repositorio, Usuario("Cliente", ClienteId))
            .Handle(new ListarNotificacionesDespachoHuevoQuery(), CancellationToken.None);

        Assert.Equal(2, resultado.Count);
        Assert.Equal("AjusteCredito", resultado[0].Tipo);
        Assert.Equal("27.0000 Bs — Precio mal digitado.", resultado[0].Meta);
    }

    [Fact]
    public async Task ElContadorDelTrabajadorPideSoloLosTiposOperativos()
    {
        await new ContarNotificacionesDespachoHuevoNoLeidasHandler(
                _repositorio, Usuario("Trabajador", ClienteId))
            .Handle(new ContarNotificacionesDespachoHuevoNoLeidasQuery(), CancellationToken.None);

        await _repositorio.Received(1).ContarNoLeidasAsync(
            ClienteId,
            Arg.Is<IReadOnlyCollection<TipoNotificacionDespachoHuevo>>(tipos =>
                !tipos.Contains(TipoNotificacionDespachoHuevo.AjusteCredito)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElTrabajadorNoPuedeMarcarLeidaUnaNotificacionFinanciera()
    {
        var notificacion = NotificacionInternaDespachoHuevo.ParaAjusteCredito(
            Guid.NewGuid(), ClienteId, "27.0000 Bs — Precio mal digitado.");
        _repositorio.ObtenerPorIdAsync(notificacion.Id, Arg.Any<CancellationToken>())
            .Returns(notificacion);
        var unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new MarcarNotificacionDespachoHuevoLeidaHandler(
                    _repositorio, Usuario("Trabajador", ClienteId), unidadTrabajo)
                .Handle(
                    new MarcarNotificacionDespachoHuevoLeidaCommand(notificacion.Id),
                    CancellationToken.None));

        Assert.False(notificacion.Leida);
        await unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElTrabajadorSiPuedeMarcarLeidaUnaNotificacionOperativa()
    {
        var notificacion = NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(
            Guid.NewGuid(), ClienteId);
        _repositorio.ObtenerPorIdAsync(notificacion.Id, Arg.Any<CancellationToken>())
            .Returns(notificacion);
        var unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();

        await new MarcarNotificacionDespachoHuevoLeidaHandler(
                _repositorio, Usuario("Trabajador", ClienteId), unidadTrabajo)
            .Handle(
                new MarcarNotificacionDespachoHuevoLeidaCommand(notificacion.Id),
                CancellationToken.None);

        Assert.True(notificacion.Leida);
        await unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElClienteSiPuedeMarcarLeidaUnaNotificacionFinanciera()
    {
        var notificacion = NotificacionInternaDespachoHuevo.ParaAjusteCredito(
            Guid.NewGuid(), ClienteId, "27.0000 Bs — Precio mal digitado.");
        _repositorio.ObtenerPorIdAsync(notificacion.Id, Arg.Any<CancellationToken>())
            .Returns(notificacion);
        var unidadTrabajo = Substitute.For<IUnidadTrabajoGestionAvicola>();

        await new MarcarNotificacionDespachoHuevoLeidaHandler(
                _repositorio, Usuario("Cliente", ClienteId), unidadTrabajo)
            .Handle(
                new MarcarNotificacionDespachoHuevoLeidaCommand(notificacion.Id),
                CancellationToken.None);

        Assert.True(notificacion.Leida);
        await unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
