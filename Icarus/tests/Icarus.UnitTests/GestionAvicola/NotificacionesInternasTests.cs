using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Notificaciones;
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8B Tarea 3 (spec: "Notificaciones internas"): las notificaciones son
// entidades persistentes con tipo, pedido, destinatario técnico y metadatos
// técnicos; el texto lo compone la UI y los motivos no se duplican. Cada envío
// genera exactamente una notificación para CAISY y cada decisión de CAISY una
// para el tenant. Marcar como leída es propio del alcance y los cruces se
// impiden con un 404 genérico.
public class NotificacionesInternasTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly Guid GestorCaisyId = Guid.NewGuid();

    private readonly IRepositorioPedidosAlimento _repositorio =
        Substitute.For<IRepositorioPedidosAlimento>();
    private readonly IRepositorioNotificacionesPrecios _repositorioPrecios =
        Substitute.For<IRepositorioNotificacionesPrecios>();
    private readonly INotificacionesInternas _notificaciones =
        Substitute.For<INotificacionesInternas>();
    private readonly ICurrentUser _usuarioTenant = Substitute.For<ICurrentUser>();
    private readonly ICurrentUser _usuarioCaisy = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _registroVuelo =
        new RegistroVuelo(NullLogger<RegistroVuelo>.Instance);
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();

    public NotificacionesInternasTests()
    {
        _usuarioTenant.EstaAutenticado.Returns(true);
        _usuarioTenant.UsuarioId.Returns(UsuarioId);
        _usuarioTenant.ClienteId.Returns(ClienteId);
        _usuarioCaisy.EstaAutenticado.Returns(true);
        _usuarioCaisy.UsuarioId.Returns(GestorCaisyId);
        _usuarioCaisy.ClienteId.Returns((Guid?)null);
    }

    private EnviarPedidoAlimentoHandler CrearEnviador(ICurrentUser usuario) =>
        new(_repositorio, _repositorioPrecios, new OpcionesPedidosAlimento(),
            usuario, _registroVuelo, _unidadTrabajo, _notificaciones);

    private DevolverPedidoAlimentoHandler CrearDevolvedor() =>
        new(_repositorio, _usuarioCaisy, _registroVuelo, _unidadTrabajo, _notificaciones);

    private RechazarPedidoAlimentoHandler CrearRechazador() =>
        new(_repositorio, _usuarioCaisy, _registroVuelo, _unidadTrabajo, _notificaciones);

    private AceptarPedidoAlimentoHandler CrearAceptor() =>
        new(_repositorio, _usuarioCaisy, _registroVuelo, _unidadTrabajo, _notificaciones);

    private ActualizarEntregaEstimadaPedidoHandler CrearActualizador() =>
        new(_repositorio, _usuarioCaisy, _registroVuelo, _unidadTrabajo, _notificaciones);

    private static IReadOnlyList<DatosDetallePedido> LineasBolsa(int bolsas = 100) =>
        [new(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, bolsas)];

    private static NotificacionPreciosAlimentos PublicacionVigente() =>
        new(new(2025, 11, 2), new(2025, 11, 10), 1.20m, 0.60m, 0.75m,
            [new DatosDetallePrecio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, null, null)]);

    private PedidoAlimento PedidoEnBorrador()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        return pedido;
    }

    private void PrepararPublicacionVigente() =>
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(PublicacionVigente());

    private async Task<PedidoAlimento> PedidoSolicitado()
    {
        var pedido = PedidoEnBorrador();
        PrepararPublicacionVigente();
        await CrearEnviador(_usuarioTenant).Handle(
            new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None);
        return pedido;
    }

    [Fact]
    public async Task ElEnvioCreaUnaNotificacionCaisySolicitado()
    {
        var pedido = await PedidoSolicitado();

        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInterna>(n =>
            n.Tipo == TipoNotificacionPedido.PedidoSolicitado &&
            n.PedidoId == pedido.Id &&
            n.ClienteId == null &&
            !n.Leida));
    }

    [Fact]
    public async Task ElReenvioTrasDevolucionCreaNotificacionDeReenvio()
    {
        var pedido = await PedidoSolicitado();
        _notificaciones.ClearReceivedCalls();

        await CrearDevolvedor().Handle(
            new DevolverPedidoAlimentoCommand(pedido.Id, "Revise las cantidades"), CancellationToken.None);
        PrepararPublicacionVigente();
        await CrearEnviador(_usuarioTenant).Handle(
            new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None);

        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInterna>(n =>
            n.Tipo == TipoNotificacionPedido.PedidoReenviado &&
            n.PedidoId == pedido.Id &&
            n.ClienteId == null));
    }

    [Fact]
    public async Task LaDevolucionCreaUnaNotificacionTenantConElDestinatarioDelPedido()
    {
        var pedido = await PedidoSolicitado();
        _notificaciones.ClearReceivedCalls();

        await CrearDevolvedor().Handle(
            new DevolverPedidoAlimentoCommand(pedido.Id, "Revise las cantidades"), CancellationToken.None);

        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInterna>(n =>
            n.Tipo == TipoNotificacionPedido.PedidoDevuelto &&
            n.PedidoId == pedido.Id &&
            n.ClienteId == ClienteId &&
            n.Meta == null));
    }

    [Fact]
    public async Task ElRechazoCreaUnaNotificacionTenant()
    {
        var pedido = await PedidoSolicitado();
        _notificaciones.ClearReceivedCalls();

        await CrearRechazador().Handle(
            new RechazarPedidoAlimentoCommand(pedido.Id, "Sin stock para esa presentación"),
            CancellationToken.None);

        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInterna>(n =>
            n.Tipo == TipoNotificacionPedido.PedidoRechazado &&
            n.PedidoId == pedido.Id &&
            n.ClienteId == ClienteId));
        Assert.Equal(EstadoPedidoAlimento.Rechazado, pedido.Estado);
    }

    [Fact]
    public async Task LaAceptacionCreaUnaNotificacionTenant()
    {
        var pedido = await PedidoSolicitado();
        _notificaciones.ClearReceivedCalls();

        await CrearAceptor().Handle(
            new AceptarPedidoAlimentoCommand(pedido.Id, FechasNegocio.Hoy().AddDays(3)),
            CancellationToken.None);

        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInterna>(n =>
            n.Tipo == TipoNotificacionPedido.PedidoAceptado &&
            n.PedidoId == pedido.Id &&
            n.ClienteId == ClienteId));
        Assert.Equal(EstadoPedidoAlimento.Aceptado, pedido.Estado);
    }

    [Fact]
    public async Task ElCambioDeEntregaEstimadaCreaNotificacionConMetaTecnica()
    {
        var pedido = await PedidoSolicitado();
        await CrearAceptor().Handle(
            new AceptarPedidoAlimentoCommand(pedido.Id, FechasNegocio.Hoy().AddDays(3)),
            CancellationToken.None);
        _notificaciones.ClearReceivedCalls();
        var nuevaFecha = FechasNegocio.Hoy().AddDays(10);

        await CrearActualizador().Handle(
            new ActualizarEntregaEstimadaPedidoCommand(pedido.Id, nuevaFecha), CancellationToken.None);

        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInterna>(n =>
            n.Tipo == TipoNotificacionPedido.EntregaEstimadaActualizada &&
            n.PedidoId == pedido.Id &&
            n.ClienteId == ClienteId &&
            n.Meta != null && n.Meta.Contains("fechaEntregaEstimada", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task MarcarLeidaEsPropioDelAlcanceYLosCrucesSeImpiden()
    {
        var notificacion = NotificacionInterna.ParaTenant(
            TipoNotificacionPedido.PedidoAceptado, Guid.NewGuid(), ClienteId);
        var repositorioNotificaciones = Substitute.For<INotificacionesInternas>();
        repositorioNotificaciones.ObtenerPorIdAsync(notificacion.Id, Arg.Any<CancellationToken>())
            .Returns(notificacion);
        var manejador = new MarcarNotificacionLeidaHandler(
            repositorioNotificaciones, _usuarioTenant, _unidadTrabajo);

        await manejador.Handle(
            new MarcarNotificacionLeidaCommand(notificacion.Id), CancellationToken.None);

        Assert.True(notificacion.Leida);
        Assert.Equal(UsuarioId, notificacion.LeidaPor);
        await repositorioNotificaciones.DidNotReceive().ObtenerPorIdAsync(
            Arg.Is<Guid>(g => g != notificacion.Id), Arg.Any<CancellationToken>());

        // Un usuario de otro tenant recibe un 404 genérico: sin enumeración.
        var ajeno = Substitute.For<ICurrentUser>();
        ajeno.EstaAutenticado.Returns(true);
        ajeno.UsuarioId.Returns(Guid.NewGuid());
        ajeno.ClienteId.Returns(Guid.NewGuid());
        var manejadorAjeno = new MarcarNotificacionLeidaHandler(
            repositorioNotificaciones, ajeno, _unidadTrabajo);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            manejadorAjeno.Handle(
                new MarcarNotificacionLeidaCommand(notificacion.Id), CancellationToken.None));
    }

    [Fact]
    public async Task MarcarLeidaEsIdempotenteAnteReintentos()
    {
        var notificacion = NotificacionInterna.ParaCaisy(
            TipoNotificacionPedido.PedidoSolicitado, Guid.NewGuid());
        notificacion.MarcarLeida(UsuarioId);
        var primeraLectura = notificacion.FechaLeidaUtc;

        notificacion.MarcarLeida(Guid.NewGuid());

        Assert.True(notificacion.Leida);
        Assert.Equal(primeraLectura, notificacion.FechaLeidaUtc);
    }

    [Fact]
    public async Task ElListadoConsultaSoloElAlcaneDelUsuario()
    {
        var repositorioNotificaciones = Substitute.For<INotificacionesInternas>();
        await new ListarNotificacionesHandler(repositorioNotificaciones, _usuarioTenant).Handle(
            new ListarNotificacionesQuery(), CancellationToken.None);

        await repositorioNotificaciones.Received(1).ListarAsync(
            ClienteId, Arg.Any<CancellationToken>());

        await new ListarNotificacionesHandler(repositorioNotificaciones, _usuarioCaisy).Handle(
            new ListarNotificacionesQuery(), CancellationToken.None);

        await repositorioNotificaciones.Received(1).ListarAsync(
            null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElContadorDeNoLeidasConsultaElAlcanceDelUsuario()
    {
        var repositorioNotificaciones = Substitute.For<INotificacionesInternas>();
        await new ContarNotificacionesNoLeidasHandler(repositorioNotificaciones, _usuarioCaisy).Handle(
            new ContarNotificacionesNoLeidasQuery(), CancellationToken.None);

        await repositorioNotificaciones.Received(1).ContarNoLeidasAsync(
            null, Arg.Any<CancellationToken>());
    }
}
