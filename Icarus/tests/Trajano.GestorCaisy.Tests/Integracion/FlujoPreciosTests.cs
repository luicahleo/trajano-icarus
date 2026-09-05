using System.Net;
using System.Text;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Integracion;

public class FlujoPreciosTests
{
    [Fact]
    public async Task ListaVaciaMuestraElEstadoVacioYLaAccionDeImportar()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();

        var html = await cliente.GetStringAsync("/Precios");

        Assert.Contains("Aún no hay notificaciones", html);
        Assert.Contains("Importar PDF", html);
    }

    [Fact]
    public async Task ListaMuestraLasFilasConEstadoYAcciones()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        aplicacion.Api.Resumenes.Add(new(
            Guid.NewGuid(), new(2025, 11, 2), new(2025, 12, 1), "Publicada", 12, true));

        var html = await cliente.GetStringAsync("/Precios");

        Assert.Contains("Publicada", html);
        Assert.Contains("02/11/2025", html);
        Assert.Contains("Ver", html);
    }

    [Fact]
    public async Task DetallesMuestraAportesYDetallesDelBorrador()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Borrador");

        var html = await cliente.GetStringAsync($"/Precios/{id}");

        Assert.Contains("Preiniciador", html);
        Assert.Contains("Bolsa", html);
        Assert.Contains("PosturaDos", html);
        Assert.Contains("Granel", html);
        Assert.Contains("Aporte CAISY", html);
        Assert.Contains("/Precios/Importar", html);
        Assert.Contains($"/Precios/{id}/Editar", html);
        Assert.Contains($"/Precios/{id}/Publicar", html);
        Assert.Contains($"/Precios/{id}/DocumentoOriginal", html);
    }

    [Fact]
    public async Task ImportarMuestraFormularioYRedirigeAlBorrador()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, "/Precios/Importar");

        var html = await cliente.GetStringAsync("/Precios/Importar");
        Assert.Contains("type=\"file\"", html);
        Assert.Contains("name=\"archivo\"", html);

        var respuesta = await cliente.PostAsync("/Precios/Importar", ContenidoMultiparte(token));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/Precios/{aplicacion.Api.IdDeImportacion}",
            respuesta.Headers.Location?.OriginalString);
        Assert.Equal(1, aplicacion.Api.VecesImportar);
        Assert.NotNull(aplicacion.Api.UltimoPdfImportado);
    }

    [Fact]
    public async Task PublicarPideConfirmacionYAlConfirmarRedirige()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Borrador");

        var confirmacion = await cliente.GetStringAsync($"/Precios/{id}/Publicar");
        Assert.Contains("confirmar", confirmacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Preiniciador", confirmacion);

        var token = AplicacionDePruebas.ExtraerTokenAntiforgery(confirmacion)!;
        var respuesta = await cliente.PostAsync($"/Precios/{id}/Publicar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/Precios/{id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(1, aplicacion.Api.VecesPublicar);
    }

    [Fact]
    public async Task ConflictoAlPublicarMuestraElMensajeYVuelveAConfirmar()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Borrador");
        aplicacion.Api.ErrorDePublicar = new ErrorApiException(409, "Conflicto con el estado actual");
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, $"/Precios/{id}/Publicar");

        var respuesta = await cliente.PostAsync($"/Precios/{id}/Publicar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var confirmacion = await cliente.GetStringAsync(respuesta.Headers.Location!.OriginalString);
        Assert.Contains("Conflicto con el estado actual", confirmacion);
    }

    [Fact]
    public async Task AnularUnaPublicacionFutura()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleActual = ApiIcarusFalsa.CrearDetalle(
            id, "Publicada", vigenteDesde: "2999-01-01");
        var html = await cliente.GetStringAsync($"/Precios/{id}");
        Assert.Contains($"/Precios/{id}/Anular", html);

        var token = AplicacionDePruebas.ExtraerTokenAntiforgery(html)!;
        var respuesta = await cliente.PostAsync($"/Precios/{id}/Anular",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(1, aplicacion.Api.VecesAnular);
    }

    [Fact]
    public async Task EditarMuestraFilasDelBorradorYGuardaLosCambios()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Borrador");

        var html = await cliente.GetStringAsync($"/Precios/{id}/Editar");
        Assert.Contains("name=\"Detalles[0].TipoAlimento\"", html);
        Assert.Contains("name=\"Detalles[0].PrecioFinalPor40Kg\"", html);

        var token = AplicacionDePruebas.ExtraerTokenAntiforgery(html)!;
        var respuesta = await cliente.PostAsync($"/Precios/{id}/Editar", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["NotificacionId"] = id.ToString(),
                ["FechaDocumento"] = "2025-11-02",
                ["VigenteDesde"] = "2025-12-01",
                ["AporteCaisy"] = "1.35",
                ["Fondo"] = "0.60",
                ["Servicios"] = "0.75",
                ["Detalles[0].TipoAlimento"] = "Preiniciador",
                ["Detalles[0].Presentacion"] = "Bolsa",
                ["Detalles[0].PrecioFinalPor40Kg"] = "119.90",
                ["Detalles[0].PrecioActualDocumento"] = "115.00",
                ["Detalles[0].EdadDesdeDias"] = "1",
                ["Detalles[0].EdadHastaDias"] = "21",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/Precios/{id}", respuesta.Headers.Location?.OriginalString);
        var comando = aplicacion.Api.UltimoComando!;
        Assert.Equal(1.35m, comando.AporteCaisy);
        var detalle = Assert.Single(comando.Detalles);
        Assert.Equal("Preiniciador", detalle.TipoAlimento);
        Assert.Equal(119.90m, detalle.PrecioFinalPor40Kg);
    }

    [Fact]
    public async Task DescargaElDocumentoOriginal()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleActual = ApiIcarusFalsa.CrearDetalle(id, "Publicada");

        var respuesta = await cliente.GetAsync($"/Precios/{id}/DocumentoOriginal");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", respuesta.Content.Headers.ContentDisposition?.ToString());
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal(aplicacion.Api.ContenidoPdf, bytes);
    }

    [Fact]
    public async Task RecursoInexistenteMuestraLaPaginaGenerica()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        aplicacion.Api.ErrorDeObtener = new ErrorApiException(404, "Recurso no encontrado");

        var respuesta = await cliente.GetAsync($"/Precios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("No se encontró", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SesionExpiradaRedirigeAAcceder()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        aplicacion.Api.ErrorDeListar = new ErrorApiException(401, "No autorizado");

        var respuesta = await cliente.GetAsync("/Precios");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Sesion/Acceder", respuesta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ErrorDelBackendMuestraLaPaginaGenerica()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync();
        aplicacion.Api.ErrorDeListar = new ErrorApiException(500, "Error interno");

        var respuesta = await cliente.GetAsync("/Precios");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.StartsWith("/Sesion/Error", respuesta.Headers.Location?.OriginalString);
    }

    private static MultipartFormDataContent ContenidoMultiparte(string token)
    {
        var contenido = new MultipartFormDataContent();
        contenido.Add(new StringContent(token), "__RequestVerificationToken");
        var archivo = new StreamContent(
            new MemoryStream("%PDF-1.7 notificacion de prueba"u8.ToArray()));
        archivo.Headers.ContentType = new("application/pdf");
        contenido.Add(archivo, "archivo", "notificacion.pdf");
        return contenido;
    }
}
