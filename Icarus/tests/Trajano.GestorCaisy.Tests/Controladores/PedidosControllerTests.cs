using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Trajano.GestorCaisy.Controllers;
using Trajano.GestorCaisy.Models;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Controladores;

// SP8B: la bandeja global lista con filtros y paginación, el detalle habilita
// las decisiones según el estado y cada decisión exige su confirmación y
// motivo cuando corresponde.
public class PedidosControllerTests
{
    private readonly ApiIcarusFalsa _api = new();
    private readonly PedidosController _controlador;

    public PedidosControllerTests()
    {
        _controlador = new PedidosController(_api)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>()),
        };
        _controlador.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task IndexListaConLosFiltrosYLasNotificaciones()
    {
        _api.PaginaDePedidos = new PaginaPedidosApi(
            [new PedidoResumenApi(
                Guid.NewGuid(), Guid.NewGuid(), "Solicitado", "Bolsa",
                new(2025, 11, 2), null, 14120m, 1)],
            1, 1, 20);
        _api.NotificacionesDePedidos = new BandejaNotificacionesApi(
            [new NotificacionPedidoApi(
                Guid.NewGuid(), "PedidoSolicitado", Guid.NewGuid(),
                new(2025, 11, 2, 15, 0, 0, DateTimeKind.Utc), false, null)],
            1);

        var vista = await _controlador.Index(new FiltrosPedidosVista(), default);

        var modelo = Assert.IsType<BandejaPedidosVista>(((ViewResult)vista).Model);
        Assert.Equal(1, modelo.Pagina.Total);
        Assert.Equal(1, modelo.Notificaciones.Contador);
        Assert.NotNull(_api.UltimosFiltros);
        Assert.Equal(1, _api.UltimosFiltros!.Pagina);
        Assert.Equal(1, _api.VecesListarNotificaciones);
    }

    [Fact]
    public async Task IndexPropagaLaPaginaPedida()
    {
        await _controlador.Index(new FiltrosPedidosVista { Pagina = 3, TamanoPagina = 50 }, default);

        Assert.Equal(3, _api.UltimosFiltros!.Pagina);
        Assert.Equal(50, _api.UltimosFiltros.TamanoPagina);
    }

    [Fact]
    public async Task DetallesHabilitaDecisionesSoloParaSolicitado()
    {
        var id = Guid.NewGuid();
        _api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaPedidoDetalle>(((ViewResult)vista).Model);
        Assert.True(modelo.PuedeProcesarse);
        Assert.False(modelo.PuedeActualizarEntrega);
    }

    [Fact]
    public async Task DetallesDeUnAceptadoSoloPermiteCambiarLaEntrega()
    {
        var id = Guid.NewGuid();
        _api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Aceptado", new(2025, 12, 1));

        var vista = await _controlador.Detalles(id, default);

        var modelo = Assert.IsType<VistaPedidoDetalle>(((ViewResult)vista).Model);
        Assert.False(modelo.PuedeProcesarse);
        Assert.True(modelo.PuedeActualizarEntrega);
    }

    [Fact]
    public async Task DetallesInexistenteDevuelve404()
    {
        _api.ErrorDeObtenerPedido = new ErrorApiException(404, "Recurso no encontrado");

        var resultado = await _controlador.Detalles(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task DevolverValidaElMotivoObligatorio()
    {
        var id = Guid.NewGuid();
        var formulario = new FormularioMotivoVista { Id = id, Motivo = "   " };
        _controlador.ModelState.AddModelError(nameof(formulario.Motivo), "El motivo es obligatorio.");

        var resultado = await _controlador.Devolver(id, formulario, default);

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.Equal("DecisionMotivo", vista.ViewName);
        Assert.Equal(0, _api.VecesDevolver);
    }

    [Fact]
    public async Task DevolverEnviadoConMotivoRedirigeAlDetalle()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.Devolver(
            id, new FormularioMotivoVista { Id = id, Motivo = "Revise las cantidades" }, default);

        var redireccion = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PedidosController.Detalles), redireccion.ActionName);
        Assert.Equal(1, _api.VecesDevolver);
        Assert.Equal((id, "Revise las cantidades"), _api.UltimaDecisionConMotivo);
    }

    [Fact]
    public async Task RechazarConflictoMuestraElMensajeEnElFormulario()
    {
        var id = Guid.NewGuid();
        _api.ErrorDeDecision = new ErrorApiException(409, "Conflicto con el estado actual");

        var resultado = await _controlador.Rechazar(
            id, new FormularioMotivoVista { Id = id, Motivo = "Sin stock" }, default);

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.Equal("DecisionMotivo", vista.ViewName);
        Assert.True(_controlador.ModelState.ContainsKey(string.Empty));
    }

    [Fact]
    public async Task AceptarConFechaPasadaRechazaSinLlamarALaApi()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.Aceptar(
            id, new FormularioEntregaVista { Id = id, FechaEntregaEstimada = FechasDeOficina.Hoy().AddDays(-1) },
            default);

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.Equal("Aceptar", vista.ViewName);
        Assert.Equal(0, _api.VecesAceptar);
        Assert.True(_controlador.ModelState.ContainsKey(nameof(FormularioEntregaVista.FechaEntregaEstimada)));
    }

    [Fact]
    public async Task AceptarConFechaValidaRedirige()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.Aceptar(
            id, new FormularioEntregaVista { Id = id, FechaEntregaEstimada = FechasDeOficina.Hoy().AddDays(3) },
            default);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(1, _api.VecesAceptar);
        Assert.Equal((id, FechasDeOficina.Hoy().AddDays(3)), _api.UltimaFechaEntrega);
    }

    [Fact]
    public async Task ActualizarEntregaEstimadaRedirigeYNotifica()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.EntregaEstimada(
            id, new FormularioEntregaVista { Id = id, FechaEntregaEstimada = FechasDeOficina.Hoy().AddDays(10) },
            default);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(1, _api.VecesActualizarEntrega);
    }

    [Fact]
    public async Task MarcarLeidaMarcaYVuelveALaBandeja()
    {
        var id = Guid.NewGuid();

        var resultado = await _controlador.MarcarLeida(id, default);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(1, _api.VecesMarcarLeida);
        Assert.Equal(id, _api.UltimaNotificacionMarcada);
    }
}
