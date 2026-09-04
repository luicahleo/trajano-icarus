using System.Net;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Integracion;

// Flujo HTML completo de la bandeja de pedidos (SP8B) contra la API falsa:
// listado con filtros y paginación, detalle congelado e historial, y las
// cuatro decisiones con confirmación, validación y protección antiforgery.
public class FlujoPedidosTests
{
    [Fact]
    public async Task BandejaVaciaMuestraElEstadoVacio()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();

        var html = await cliente.GetStringAsync("/Pedidos");

        Assert.Contains("No hay pedidos con ese filtro.", html);
        Assert.Contains("Novedades para CAISY", html);
    }

    [Fact]
    public async Task BandejaMuestraFilasFiltrosYPaginacion()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        aplicacion.Api.PaginaDePedidos = new PaginaPedidosApi(
            Enumerable.Range(0, 3).Select(_ => new PedidoResumenApi(
                Guid.NewGuid(), Guid.NewGuid(), "Solicitado", "Bolsa",
                new(2025, 11, 2), null, 14120m, 1)).ToList(),
            3, 1, 20);

        var html = await cliente.GetStringAsync("/Pedidos?estado=Solicitado");

        Assert.Contains("Solicitado", html);
        Assert.Contains("02/11/2025", html);
        Assert.Contains("Página 1 de 1 con 3 pedidos.", html);
        Assert.Equal("Solicitado", aplicacion.Api.UltimosFiltros!.Estado);
    }

    [Fact]
    public async Task DetallesMuestraLineasCongeladasHistorialYAcciones()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");

        var html = await cliente.GetStringAsync($"/Pedidos/{id}");

        Assert.Contains("Líneas congeladas al envío", html);
        Assert.Contains("PosturaUno", html);
        Assert.Contains("Borrador → Solicitado", html);
        Assert.Contains($"/Pedidos/{id}/Devolver", html);
        Assert.Contains($"/Pedidos/{id}/Rechazar", html);
        Assert.Contains($"/Pedidos/{id}/Aceptar", html);
    }

    [Fact]
    public async Task DetallesInexistenteDevuelve404()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        aplicacion.Api.ErrorDeObtenerPedido = new ErrorApiException(
            404, "Recurso no encontrado");

        var respuesta = await cliente.GetAsync($"/Pedidos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task DevolverPideMotivoYAlConfirmarRedirige()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, $"/Pedidos/{id}/Devolver");

        var respuesta = await cliente.PostAsync($"/Pedidos/{id}/Devolver",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Motivo"] = "Revise las cantidades",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/Pedidos/{id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(1, aplicacion.Api.VecesDevolver);
        Assert.Equal((id, "Revise las cantidades"), aplicacion.Api.UltimaDecisionConMotivo);
    }

    [Fact]
    public async Task RechazarSinMotivoVuelveAlFormularioSinLlamarALaApi()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, $"/Pedidos/{id}/Rechazar");

        var respuesta = await cliente.PostAsync($"/Pedidos/{id}/Rechazar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Motivo"] = "",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("El motivo es obligatorio.", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(0, aplicacion.Api.VecesRechazar);
    }

    [Fact]
    public async Task AceptarConFechaValidaRedirige()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Solicitado");
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, $"/Pedidos/{id}/Aceptar");
        var fecha = FechasDeOficina.Hoy().AddDays(3);

        var respuesta = await cliente.PostAsync($"/Pedidos/{id}/Aceptar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FechaEntregaEstimada"] = fecha.ToString("yyyy-MM-dd"),
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(1, aplicacion.Api.VecesAceptar);
        Assert.Equal((id, fecha), aplicacion.Api.UltimaFechaEntrega);
    }

    [Fact]
    public async Task CambiarEntregaEstimadaDesdeUnAceptado()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.PedidoActual = ApiIcarusFalsa.CrearPedido(id, "Aceptado", FechasDeOficina.Hoy().AddDays(3));
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, $"/Pedidos/{id}/EntregaEstimada");
        var fecha = FechasDeOficina.Hoy().AddDays(10);

        var respuesta = await cliente.PostAsync($"/Pedidos/{id}/EntregaEstimada",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FechaEntregaEstimada"] = fecha.ToString("yyyy-MM-dd"),
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(1, aplicacion.Api.VecesActualizarEntrega);
        Assert.Equal((id, fecha), aplicacion.Api.UltimaFechaEntrega);
    }

    [Fact]
    public async Task MarcarNotificacionLeidaVuelveALaBandeja()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.NotificacionesDePedidos = new BandejaNotificacionesApi(
            [new NotificacionPedidoApi(
                id, "PedidoSolicitado", Guid.NewGuid(),
                new(2025, 11, 2, 15, 0, 0, DateTimeKind.Utc), false, null)],
            1);
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, "/Pedidos");

        var respuesta = await cliente.PostAsync($"/Pedidos/Notificaciones/{id}/MarcarLeida",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(1, aplicacion.Api.VecesMarcarLeida);
    }

    [Fact]
    public async Task UnPostSinTokenAntiforgerySeRechaza()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();

        var respuesta = await cliente.PostAsync($"/Pedidos/{id}/Devolver",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Motivo"] = "algo" }));

        Assert.False(respuesta.IsSuccessStatusCode);
        Assert.Equal(0, aplicacion.Api.VecesDevolver);
    }
}
