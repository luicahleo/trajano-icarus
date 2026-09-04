using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Application.Notificaciones;
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
    private readonly INotificacionesInternas _notificaciones =
        Substitute.For<INotificacionesInternas>();

    private readonly OpcionesPedidosAlimento _opciones = new() { MaximoPorSemana = 3 };

    private CrearPedidoAlimentoHandler CrearCreador() =>
        new(_repositorio, _usuarioActual, _registroVuelo, _unidadTrabajo);

    private EditarPedidoAlimentoHandler CrearEditor() =>
        new(_repositorio, _registroVuelo, _unidadTrabajo);

    private DesactivarPedidoAlimentoHandler CrearDesactivador() =>
        new(_repositorio, _registroVuelo, _unidadTrabajo);

    private EnviarPedidoAlimentoHandler CrearEnviador() =>
        new(_repositorio, _repositorioPrecios, _opciones, _usuarioActual, _registroVuelo,
            _unidadTrabajo, _notificaciones);

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
    public async Task ElCupoVisibleConsultaLaSemanaIsoActual()
    {
        var hoy = FechasNegocio.Hoy();
        var inicioSemana = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));

        var cupo = await new ObtenerCupoPedidosHandler(
            _repositorio, _opciones, _usuarioActual).Handle(
            new ObtenerCupoPedidosQuery(), CancellationToken.None);

        Assert.Equal(3, cupo.Maximo);
        Assert.Equal(inicioSemana, cupo.Desde);
        Assert.Equal(inicioSemana.AddDays(6), cupo.Hasta);
        await _repositorio.Received(1).ContarEnviadosEnSemanaAsync(
            ClienteId, inicioSemana, inicioSemana.AddDays(6), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ElCupoSinCuentaDeTenantFalla()
    {
        _usuarioActual.ClienteId.Returns((Guid?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new ObtenerCupoPedidosHandler(_repositorio, _opciones, _usuarioActual).Handle(
                new ObtenerCupoPedidosQuery(), CancellationToken.None));
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

    // SP8C Tarea 1 (spec: "Despacho, nota y recepción"): CAISY registra el
    // despacho con su nota; la notificación va a la bandeja del tenant y los
    // datos de la nota no salen hacia el registro de vuelo (anti-PII).
    private static PedidoAlimento PedidoAceptado() =>
        Aceptado(new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa()));

    private static PedidoAlimento Aceptado(PedidoAlimento pedido)
    {
        pedido.EnviarACaisy(FechasNegocio.Hoy(), UsuarioId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        pedido.Aceptar(FechasNegocio.Hoy().AddDays(3), FechasNegocio.Hoy(), UsuarioId);
        return pedido;
    }

    private static DatosLineaEntrega LineaEntregada(int cantidad = 100) =>
        new(TipoAlimento.PosturaUno, cantidad);

    [Fact]
    public async Task DespacharDesdeAceptadoRegistraLaEntregaYNotificaAlTenant()
    {
        var pedido = PedidoAceptado();
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

        await CrearDespachador().Handle(
            new RegistrarDespachoPedidoCommand(
                pedido.Id, "NOTA-77", FechasNegocio.Hoy().AddDays(-1), 18000m,
                [LineaEntregada()]), CancellationToken.None);

        Assert.Equal(EstadoPedidoAlimento.Despachado, pedido.Estado);
        Assert.Equal("NOTA-77", pedido.Entrega!.NumeroNota);
        Assert.Equal(FechasNegocio.Hoy(), pedido.Entrega.FechaDespacho);
        _notificaciones.Received(1).Agregar(Arg.Is<NotificacionInterna>(n =>
            n.Tipo == TipoNotificacionPedido.PedidoDespachado &&
            n.PedidoId == pedido.Id &&
            n.ClienteId == ClienteId));
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DespacharUnPedidoNoAceptadoDevuelveConflicto()
    {
        var pedido = new PedidoAlimento(Guid.NewGuid(), ClienteId, UsuarioId, LineasBolsa());
        pedido.EnviarACaisy(FechasNegocio.Hoy(), UsuarioId,
            [new DatosPrecioEnvio(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 180m, Guid.NewGuid())]);
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CrearDespachador().Handle(
                new RegistrarDespachoPedidoCommand(
                    pedido.Id, "NOTA-77", FechasNegocio.Hoy(), null,
                    [LineaEntregada()]), CancellationToken.None));

        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);
        Assert.Null(pedido.Entrega);
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DespacharUnIdAjenoDevuelveNoEncontrado()
    {
        _repositorio.ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PedidoAlimento?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearDespachador().Handle(
                new RegistrarDespachoPedidoCommand(
                    Guid.NewGuid(), "NOTA-77", FechasNegocio.Hoy(), null,
                    [LineaEntregada()]), CancellationToken.None));
    }

    private RegistrarDespachoPedidoHandler CrearDespachador() =>
        new(_repositorio, _usuarioActual, _registroVuelo, _unidadTrabajo, _notificaciones);

    // SP8C Tarea 2 (spec: "Documentos privados"): los respaldos de la nota los
    // registra CAISY sobre el pedido despachado; el contenido pasa por el
    // almacén privado (firma, MIME, tamaño, hash) y a SQL solo llegan clave
    // lógica y metadatos. Nunca se registran nombres de archivo (anti-PII).
    private readonly IAlmacenDocumentosPedido _almacen = Substitute.For<IAlmacenDocumentosPedido>();
    private readonly OpcionesAlmacenDocumentosPedido _opcionesDocumentos = new() { MaxDocumentosPorNota = 2 };

    private AgregarDocumentoNotaHandler CrearRegistradorDocumentos() =>
        new(_repositorio, _almacen, _opcionesDocumentos, _usuarioActual, _registroVuelo, _unidadTrabajo);

    private static DocumentoAlmacenado DocumentoAlmacenadoFalso() =>
        new(Guid.NewGuid(), Guid.NewGuid(),
            "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90",
            "image/jpeg", 1200, 900);

    [Fact]
    public async Task AgregarDocumentoNotaGuardaEnElAlmacenYRegistraEnElPedido()
    {
        var pedido = PedidoAceptado();
        pedido.RegistrarDespacho("NOTA-1", FechasNegocio.Hoy(), null,
            [LineaEntregada()], FechasNegocio.Hoy(), UsuarioId);
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _almacen.GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(DocumentoAlmacenadoFalso());

        var id = await CrearRegistradorDocumentos().Handle(
            new AgregarDocumentoNotaCommand(
                pedido.Id, new MemoryStream([1, 2, 3]), "nota frente.jpg", null),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        var documento = pedido.Entrega!.Documentos.Single();
        Assert.Equal(id, documento.Id);
        Assert.Equal("nota frente.jpg", documento.NombreSeguro);
        Assert.Equal("image/jpeg", documento.Mime);
        Assert.True(documento.Activo);
        _repositorio.Received(1).AgregarDocumentoNota(documento);
        await _unidadTrabajo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReemplazarDocumentoNotaDesactivaElPrevioConTrazabilidad()
    {
        var pedido = PedidoAceptado();
        pedido.RegistrarDespacho("NOTA-1", FechasNegocio.Hoy(), null,
            [LineaEntregada()], FechasNegocio.Hoy(), UsuarioId);
        var previo = pedido.AgregarDocumentoNota(new DatosDocumentoNota(
            Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 100, 80,
            "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90", "borrosa.jpg"));
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _almacen.GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(DocumentoAlmacenadoFalso());

        var idNuevo = await CrearRegistradorDocumentos().Handle(
            new AgregarDocumentoNotaCommand(
                pedido.Id, new MemoryStream([1]), "neta.jpg", previo.Id),
            CancellationToken.None);

        Assert.False(previo.Activo);
        Assert.Equal(idNuevo, previo.ReemplazadoPorId);
        Assert.Equal(2, pedido.Entrega!.Documentos.Count);
    }

    [Fact]
    public async Task AgregarDocumentoNotaFueraDeDespachadoDevuelveConflictoSinGuardar()
    {
        var pedido = PedidoAceptado();
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CrearRegistradorDocumentos().Handle(
                new AgregarDocumentoNotaCommand(
                    pedido.Id, new MemoryStream([1]), "nota.jpg", null),
                CancellationToken.None));

        await _almacen.DidNotReceiveWithAnyArgs().GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _unidadTrabajo.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarDocumentoNotaSuperaElMaximoDevuelveConflicto()
    {
        var pedido = PedidoAceptado();
        pedido.RegistrarDespacho("NOTA-1", FechasNegocio.Hoy(), null,
            [LineaEntregada()], FechasNegocio.Hoy(), UsuarioId);
        pedido.AgregarDocumentoNota(new DatosDocumentoNota(
            Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 100, 80,
            "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90", "a.jpg"));
        pedido.AgregarDocumentoNota(new DatosDocumentoNota(
            Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 100, 80,
            "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90", "b.jpg"));
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);

        var excepcion = await Assert.ThrowsAsync<ConflictException>(() =>
            CrearRegistradorDocumentos().Handle(
                new AgregarDocumentoNotaCommand(
                    pedido.Id, new MemoryStream([1]), "c.jpg", null),
                CancellationToken.None));

        Assert.Contains("La nota admite hasta 2 imágenes", excepcion.Message, StringComparison.Ordinal);
        await _almacen.DidNotReceiveWithAnyArgs().GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgregarDocumentoNotaSanitizaElNombreSeguro()
    {
        var pedido = PedidoAceptado();
        pedido.RegistrarDespacho("NOTA-1", FechasNegocio.Hoy(), null,
            [LineaEntregada()], FechasNegocio.Hoy(), UsuarioId);
        _repositorio.ObtenerPorIdAsync(pedido.Id, Arg.Any<CancellationToken>()).Returns(pedido);
        _almacen.GuardarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(DocumentoAlmacenadoFalso());

        await CrearRegistradorDocumentos().Handle(
            new AgregarDocumentoNotaCommand(
                pedido.Id, new MemoryStream([1]), "../../nota<1>.jpg", null),
            CancellationToken.None);

        var nombre = pedido.Entrega!.Documentos.Single().NombreSeguro;
        Assert.DoesNotContain("..", nombre, StringComparison.Ordinal);
        Assert.DoesNotContain("/", nombre, StringComparison.Ordinal);
        Assert.DoesNotContain("<", nombre, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DespacharSinNumeroDeNotaFallaLaValidacion()
    {
        var pedido = PedidoAceptado();
        var validator = new RegistrarDespachoPedidoValidator();

        var resultado = await validator.ValidateAsync(
            new RegistrarDespachoPedidoCommand(
                pedido.Id, " ", FechasNegocio.Hoy(), null,
                [LineaEntregada()]), CancellationToken.None);

        Assert.False(resultado.IsValid);
        Assert.Equal(EstadoPedidoAlimento.Aceptado, pedido.Estado);
    }
}
