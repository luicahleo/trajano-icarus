using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.PedidosAlimento;
using Icarus.GestionAvicola.Application.PreciosAlimentos;
using Icarus.GestionAvicola.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8B Tarea 2 (spec: "Pedido y cantidades", "Límite semanal" y
// "Persistencia y consistencia"): el borrador es compartido del tenant, el
// envío congela los precios vigentes y la fecha de negocio de Bolivia en una
// transacción atómica, y el límite semanal configurable se comprueba dentro de
// la misma transacción con una consulta bloqueable.
public class PedidosAlimentoHandlerTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private readonly IRepositorioPedidosAlimento _repositorio =
        Substitute.For<IRepositorioPedidosAlimento>();
    private readonly IRepositorioNotificacionesPrecios _repositorioPrecios =
        Substitute.For<IRepositorioNotificacionesPrecios>();
    private readonly ICurrentUser _usuarioActual = Substitute.For<ICurrentUser>();
    private readonly IRegistroVuelo _registroVuelo =
        new RegistroVuelo(NullLogger<RegistroVuelo>.Instance);
    private readonly IUnidadTrabajoGestionAvicola _unidadTrabajo =
        Substitute.For<IUnidadTrabajoGestionAvicola>();
    private readonly ITransaccionPedidos _transaccion = Substitute.For<ITransaccionPedidos>();

    private readonly OpcionesPedidosAlimento _opciones = new() { MaximoPorSemana = 3 };

    private CrearPedidoAlimentoHandler CrearCreador() =>
        new(_repositorio, _usuarioActual, _registroVuelo, _unidadTrabajo);

    private EditarPedidoAlimentoHandler CrearEditor() =>
        new(_repositorio, _registroVuelo, _unidadTrabajo);

    private DesactivarPedidoAlimentoHandler CrearDesactivador() =>
        new(_repositorio, _registroVuelo, _unidadTrabajo);

    private EnviarPedidoAlimentoHandler CrearEnviador() =>
        new(_repositorio, _repositorioPrecios, _opciones, _usuarioActual, _registroVuelo, _unidadTrabajo);

    public PedidosAlimentoHandlerTests()
    {
        _usuarioActual.EstaAutenticado.Returns(true);
        _usuarioActual.UsuarioId.Returns(UsuarioId);
        _usuarioActual.ClienteId.Returns(ClienteId);
        _repositorio.IniciarTransaccionAsync(Arg.Any<CancellationToken>())
            .Returns(_transaccion);
    }

    private static IReadOnlyList<DatosDetallePedido> LineasBolsa(int bolsas = 100) =>
        [new(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, bolsas)];

    private static NotificacionPreciosAlimentos PublicacionVigente() =>
        new(new(2025, 11, 2), new(2025, 11, 10), 1.20m, 0.60m, 0.75m,
            [new DatosDetallePrecio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, null, null)]);

    [Fact]
    public async Task CrearAsignaElTenantYConservaElCreadorComoAuditoria()
    {
        var id = await CrearCreador().Handle(
            new CrearPedidoAlimentoCommand(LineasBolsa()), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        _repositorio.Received(1).Agregar(Arg.Is<PedidoAlimento>(p =>
            p.ClienteId == ClienteId &&
            p.CreadoPor == UsuarioId &&
            p.Estado == EstadoPedidoAlimento.Borrador &&
            p.EstaActivo));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnTrabajadorAutorizadoCreaBorradoresDelTenant()
    {
        _usuarioActual.TrabajadorId.Returns(Guid.NewGuid());

        await CrearCreador().Handle(new CrearPedidoAlimentoCommand(LineasBolsa()), CancellationToken.None);

        _repositorio.Received(1).Agregar(Arg.Is<PedidoAlimento>(p => p.ClienteId == ClienteId));
    }

    [Fact]
    public async Task CrearSinCuentaDeTenantFalla()
    {
        _usuarioActual.ClienteId.Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearCreador().Handle(new CrearPedidoAlimentoCommand(LineasBolsa()), CancellationToken.None));

        _repositorio.DidNotReceive().Agregar(Arg.Any<PedidoAlimento>());
    }

    [Fact]
    public async Task EditarSoloBorradorYRegistraLineasRecreadas()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

        await CrearEditor().Handle(
            new EditarPedidoAlimentoCommand(pedido.Id, LineasBolsa(150)), CancellationToken.None);

        Assert.Equal(150, pedido.Detalles.Single().CantidadSolicitada);
        _repositorio.Received(1).AgregarDetalle(Arg.Is<DetallePedidoAlimento>(
            d => d.CantidadSolicitada == 150));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditarUnPedidoYaEnviadoDevuelveConflicto()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        pedido.EnviarACaisy(FechasNegocio.Hoy(), UsuarioId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CrearEditor().Handle(
                new EditarPedidoAlimentoCommand(pedido.Id, LineasBolsa(150)), CancellationToken.None));

        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditarUnIdAjenoDevuelveNoEncontrado()
    {
        _repositorio.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PedidoAlimento?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearEditor().Handle(
                new EditarPedidoAlimentoCommand(Guid.NewGuid(), LineasBolsa()), CancellationToken.None));
    }

    [Fact]
    public async Task DesactivarSoloBorrador()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

        await CrearDesactivador().Handle(
            new DesactivarPedidoAlimentoCommand(pedido.Id), CancellationToken.None);

        Assert.False(pedido.EstaActivo);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        var enviado = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        enviado.EnviarACaisy(FechasNegocio.Hoy(), UsuarioId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        _repositorio.ObtenerPorIdAsync(enviado.Id, Arg.Any<CancellationToken>()).Returns(enviado);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CrearDesactivador().Handle(
                new DesactivarPedidoAlimentoCommand(enviado.Id), CancellationToken.None));
        Assert.True(enviado.EstaActivo);
    }

    [Fact]
    public async Task EnviarCongelaPreciosVigentesConFechaDeNegocio()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        var publicacion = PublicacionVigente();
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(publicacion);

        await CrearEnviador().Handle(
            new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None);

        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);
        Assert.Equal(FechasNegocio.Hoy(), pedido.FechaPedido);
        var linea = pedido.Detalles.Single();
        Assert.Equal(180m, linea.PrecioFinalPor40Kg);
        Assert.Equal(18000m, linea.SubtotalSolicitado);
        Assert.Equal(publicacion.Id, linea.NotificacionPreciosAlimentosId);
        await _transaccion.Received(1).ConfirmarAsync(Arg.Any<CancellationToken>());
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElCupoSeConsultaEnLaSemanaIsoActualDelTenant()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(PublicacionVigente());
        var hoy = FechasNegocio.Hoy();
        var inicioSemana = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));

        await CrearEnviador().Handle(
            new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None);

        await _repositorio.Received(1).ContarEnviadosEnSemanaBloqueandoAsync(
            ClienteId, inicioSemana, inicioSemana.AddDays(6), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnviarSinPublicacionVigenteDejaElBorradorIntacto()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((NotificacionPreciosAlimentos?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CrearEnviador().Handle(
                new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None));

        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
        Assert.Null(pedido.FechaPedido);
        Assert.All(pedido.Detalles, d => Assert.Null(d.PrecioFinalPor40Kg));
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _transaccion.DidNotReceive().ConfirmarAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnviarFallaCompletoSiFaltaPrecioDeUnaLinea()
    {
        var lineas = new List<DatosDetallePedido>
        {
            new(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 100),
            new(TipoAlimento.PosturaDos, PresentacionAlimento.Bolsa, 50),
        };
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, lineas);
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        // La publicación vigente no trae precio para PosturaDos.
        _repositorioPrecios.ObtenerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(PublicacionVigente());

        await Assert.ThrowsAsync<ReglaNegocioException>(() =>
            CrearEnviador().Handle(
                new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None));

        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
        Assert.Null(pedido.FechaPedido);
        Assert.All(pedido.Detalles, d => Assert.Null(d.SubtotalSolicitado));
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnviarConElCupoAgotadoDevuelveConflicto()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _repositorio.ContarEnviadosEnSemanaBloqueandoAsync(
            ClienteId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(3);

        var excepcion = await Assert.ThrowsAsync<ConflictException>(() =>
            CrearEnviador().Handle(
                new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None));

        Assert.Contains("límite semanal", excepcion.Message, StringComparison.Ordinal);
        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _transaccion.DidNotReceive().ConfirmarAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnviarUnPedidoYaEnviadoDevuelveConflictoSinGastarCupo()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        pedido.EnviarACaisy(FechasNegocio.Hoy(), UsuarioId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CrearEnviador().Handle(
                new EnviarPedidoAlimentoCommand(pedido.Id), CancellationToken.None));

        // El reintento no consulta cupo ni guarda nada: la transición no se repite.
        await _repositorio.DidNotReceive().ContarEnviadosEnSemanaBloqueandoAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarDevuelveResumenesDelTenant()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        _repositorio.ListarAsync(Arg.Any<CancellationToken>()).Returns([pedido]);

        var resumenes = await new ListarPedidosAlimentoHandler(_repositorio).Handle(
            new ListarPedidosAlimentoQuery(), CancellationToken.None);

        var resumen = Assert.Single(resumenes);
        Assert.Equal(pedido.Id, resumen.Id);
        Assert.Equal("Borrador", resumen.Estado);
        Assert.Equal(1, resumen.CantidadLineas);
        Assert.Null(resumen.TotalSolicitado);
    }

    [Fact]
    public async Task ObtenerDevuelveElDetalleConHistorial()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        pedido.EnviarACaisy(FechasNegocio.Hoy(), UsuarioId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        pedido.DevolverParaCorreccion("Revise la cantidad", Guid.NewGuid());
        _repositorio.ObtenerConHistorialAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

        var detalle = await new ObtenerPedidoAlimentoHandler(_repositorio).Handle(
            new ObtenerPedidoAlimentoQuery(pedido.Id), CancellationToken.None);

        Assert.Equal(2, detalle.Historial.Count);
        Assert.Equal("Revise la cantidad", detalle.Historial[1].Motivo);
        Assert.Equal(180m, detalle.Lineas.Single().PrecioFinalPor40Kg);
    }
}
