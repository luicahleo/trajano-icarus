using System.Net;
using System.Text;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Integracion;

public class FlujoPreciosHuevoTests
{
    [Fact]
    public async Task SinLaFuncionalidadLaRutaDePreciosHuevoQuedaProhibida()
    {
        using var aplicacion = new AplicacionDePruebas();
        // Solo GestorPedidoAlimento (bit 1): no alcanza para la bandeja de huevo.
        var cliente = await aplicacion.AccederAsync(funcCaisy: 1);

        var respuesta = await cliente.GetAsync("/PreciosHuevo");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Sesion/Denegado", respuesta.Headers.Location!.AbsolutePath);
        var denegado = await cliente.GetStringAsync("/Sesion/Denegado");
        Assert.Contains("No tiene acceso", denegado);
    }

    [Fact]
    public async Task ListaVaciaMuestraElEstadoVacioYLaAccionDeImportar()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);

        var html = await cliente.GetStringAsync("/PreciosHuevo");

        Assert.Contains("Aún no hay publicaciones", html);
        Assert.Contains("Importar Excel", html);
    }

    [Fact]
    public async Task ListaMuestraLasFilasConEstadoYAcciones()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        aplicacion.Api.ResumenesHuevo.Add(new(
            Guid.NewGuid(), new(2025, 11, 2), new(2025, 12, 1), "Publicada", 6, true));

        var html = await cliente.GetStringAsync("/PreciosHuevo");

        Assert.Contains("Publicada", html);
        Assert.Contains("02/11/2025", html);
        Assert.Contains("Ver", html);
    }

    [Fact]
    public async Task DetallesMuestraServicioYTamanosDelBorrador()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");

        var html = await cliente.GetStringAsync($"/PreciosHuevo/{id}");

        Assert.Contains("Primera", html);
        Assert.Contains("Extra", html);
        Assert.Contains("Servicio", html);
        Assert.Contains("Precio al productor", html);
        Assert.Contains("Precio unitario", html);
        Assert.Contains($"/PreciosHuevo/{id}/Editar", html);
        Assert.Contains($"/PreciosHuevo/{id}/Publicar", html);
        Assert.Contains($"/PreciosHuevo/{id}/DocumentoOriginal", html);
    }

    [Fact]
    public async Task ImportarMuestraFormularioYRedirigeAlBorrador()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, "/PreciosHuevo/Importar");

        var html = await cliente.GetStringAsync("/PreciosHuevo/Importar");
        Assert.Contains("type=\"file\"", html);
        Assert.Contains("name=\"archivo\"", html);

        var respuesta = await cliente.PostAsync("/PreciosHuevo/Importar", ContenidoMultiparte(token));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/PreciosHuevo/{aplicacion.Api.IdDeImportacionHuevo}",
            respuesta.Headers.Location?.OriginalString);
        Assert.Equal(1, aplicacion.Api.VecesImportarHuevo);
        Assert.NotNull(aplicacion.Api.UltimoExcelImportado);
    }

    [Fact]
    public async Task EditarMuestraFilasDelBorradorYGuardaLosCambios()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");

        var html = await cliente.GetStringAsync($"/PreciosHuevo/{id}/Editar");
        Assert.Contains("name=\"Detalles[0].Tamano\"", html);
        Assert.Contains("name=\"Detalles[0].PrecioAlProductor\"", html);

        var token = AplicacionDePruebas.ExtraerTokenAntiforgery(html)!;
        var respuesta = await cliente.PostAsync($"/PreciosHuevo/{id}/Editar", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["PublicacionId"] = id.ToString(),
                ["FechaNotificacion"] = "2025-11-02",
                ["FechaVigencia"] = "2025-12-01",
                ["Servicio"] = "0.55",
                ["Detalles[0].Tamano"] = "Primera",
                ["Detalles[0].PrecioAlProductor"] = "0.046",
                ["Detalles[0].PrecioActualDocumento"] = "0.045",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/PreciosHuevo/{id}", respuesta.Headers.Location?.OriginalString);
        var comando = aplicacion.Api.UltimoComandoHuevo!;
        Assert.Equal(0.55m, comando.Servicio);
        var detalle = Assert.Single(comando.Detalles);
        Assert.Equal("Primera", detalle.Tamano);
        Assert.Equal(0.046m, detalle.PrecioAlProductor);
    }

    [Fact]
    public async Task PublicarPideConfirmacionYAlConfirmarRedirige()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");

        var confirmacion = await cliente.GetStringAsync($"/PreciosHuevo/{id}/Publicar");
        Assert.Contains("confirmar", confirmacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Primera", confirmacion);

        var token = AplicacionDePruebas.ExtraerTokenAntiforgery(confirmacion)!;
        var respuesta = await cliente.PostAsync($"/PreciosHuevo/{id}/Publicar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/PreciosHuevo/{id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(1, aplicacion.Api.VecesPublicarHuevo);
    }

    [Fact]
    public async Task DescartarUnBorradorPideConfirmacionYRedirigeAlHistorial()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Borrador");

        var confirmacion = await cliente.GetStringAsync($"/PreciosHuevo/{id}/Descartar");
        Assert.Contains("Descartar", confirmacion);
        Assert.Contains("Primera", confirmacion);

        var token = AplicacionDePruebas.ExtraerTokenAntiforgery(confirmacion)!;
        var respuesta = await cliente.PostAsync($"/PreciosHuevo/{id}/Descartar",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/PreciosHuevo", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(1, aplicacion.Api.VecesDescartarHuevo);
        Assert.Equal(id, aplicacion.Api.UltimoDescartadoHuevo);
    }

    [Fact]
    public async Task DescargaElDocumentoOriginal()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(id, "Publicada");

        var respuesta = await cliente.GetAsync($"/PreciosHuevo/{id}/DocumentoOriginal");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", respuesta.Content.Headers.ContentDisposition?.ToString());
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal(aplicacion.Api.ContenidoExcel, bytes);
    }

    [Fact]
    public async Task PublicacionEfectivaNoSeBorraNiSeAnulaYOfreceCorreccion()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 2);
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleHuevoActual = ApiIcarusFalsa.CrearDetalleHuevo(
            id, "Publicada", fechaVigencia: "2025-01-01");

        var html = await cliente.GetStringAsync($"/PreciosHuevo/{id}");

        Assert.DoesNotContain($"/PreciosHuevo/{id}/Anular", html);
        Assert.DoesNotContain($"/PreciosHuevo/{id}/Descartar", html);
        Assert.DoesNotContain($"/PreciosHuevo/{id}/Editar", html);
        Assert.Contains("inmutable", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/PreciosHuevo/Importar", html);
    }

    [Fact]
    public async Task ElMenuMuestraPreciosDeHuevoSoloConLaFuncionalidad()
    {
        using var aplicacion = new AplicacionDePruebas();
        var soloHuevo = await aplicacion.AccederAsync(funcCaisy: 2);

        var htmlHuevo = await soloHuevo.GetStringAsync("/PreciosHuevo");
        Assert.Contains("Precios de huevo", htmlHuevo);
        Assert.DoesNotContain("Precios de alimento", htmlHuevo);

        using var otraAplicacion = new AplicacionDePruebas();
        var soloAlimento = await otraAplicacion.AccederAsync(funcCaisy: 1);
        var htmlAlimento = await soloAlimento.GetStringAsync("/Precios");
        Assert.Contains("Precios de alimento", htmlAlimento);
        Assert.DoesNotContain("Precios de huevo", htmlAlimento);
    }

    private static MultipartFormDataContent ContenidoMultiparte(string token)
    {
        var contenido = new MultipartFormDataContent();
        contenido.Add(new StringContent(token), "__RequestVerificationToken");
        var archivo = new StreamContent(
            new MemoryStream("PK\x03\x04 planilla de precios de prueba"u8.ToArray()));
        archivo.Headers.ContentType = new(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(archivo, "archivo", "precios-huevo.xlsx");
        return contenido;
    }
}
