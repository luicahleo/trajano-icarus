using System.Net;
using Trajano.GestorCaisy.Servicios;
using Trajano.GestorCaisy.Tests.Ayudas;

namespace Trajano.GestorCaisy.Tests.Integracion;

public class FlujoProgramasVacunacionTests
{
    private const int BitVeterinario = 4;

    [Fact]
    public async Task SinLaFuncionalidadLaSeccionQuedaProhibida()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: 3);

        var respuesta = await cliente.GetAsync("/ProgramasVacunacion");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Sesion/Denegado", respuesta.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task ListaVaciaOfreceCrearElPrimerPrograma()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: BitVeterinario);

        var html = await cliente.GetStringAsync("/ProgramasVacunacion");

        Assert.Contains("Aún no hay programas de vacunación", html);
        Assert.Contains("/ProgramasVacunacion/Nuevo", html);
    }

    [Fact]
    public async Task ListaMuestraEstadosConTexto()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: BitVeterinario);
        aplicacion.Api.ResumenesVacunacion.Add(new(Guid.NewGuid(), "PLAN BORRADOR", null, 1000, "Borrador", true, 0));
        aplicacion.Api.ResumenesVacunacion.Add(new(Guid.NewGuid(), "PLAN VIEJO", new(2026, 8, 6), 1000, "Publicado", false, 4));

        var html = await cliente.GetStringAsync("/ProgramasVacunacion");

        Assert.Contains("PLAN BORRADOR", html);
        Assert.Contains("chip--borrador", html);
        Assert.Contains("Inactivo", html);
        Assert.Contains("06/08/2026", html);
    }

    [Fact]
    public async Task DetalleDeUnBorradorOfreceSusAcciones()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: BitVeterinario);
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleVacunacionActual = ApiIcarusFalsa.CrearDetalleVacunacion(id, "Borrador");

        var html = await cliente.GetStringAsync($"/ProgramasVacunacion/{id}");

        Assert.Contains("VACUNA 1", html);
        Assert.Contains($"/ProgramasVacunacion/{id}/Editar", html);
        Assert.Contains($"/ProgramasVacunacion/{id}/Cronograma", html);
        Assert.Contains($"/ProgramasVacunacion/{id}/Publicar", html);
        Assert.Contains($"/ProgramasVacunacion/{id}/Eliminar", html);
        Assert.DoesNotContain($"/ProgramasVacunacion/{id}/Desactivar", html);
    }

    [Fact]
    public async Task DetalleDeUnPublicadoSoloOfreceDesactivar()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: BitVeterinario);
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleVacunacionActual = ApiIcarusFalsa.CrearDetalleVacunacion(id, "Publicado");

        var html = await cliente.GetStringAsync($"/ProgramasVacunacion/{id}");

        Assert.Contains($"/ProgramasVacunacion/{id}/Desactivar", html);
        Assert.DoesNotContain($"/ProgramasVacunacion/{id}/Editar", html);
        Assert.DoesNotContain($"/ProgramasVacunacion/{id}/Eliminar", html);
    }

    [Fact]
    public async Task ImportarConFilasInvalidasMuestraLosErroresPorFila()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: BitVeterinario);
        var id = Guid.NewGuid();
        aplicacion.Api.ErrorDeVacunacion = new ErrorApiException(400, "Solicitud inválida",
            erroresValidacion: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Cronograma"] =
                [
                    "Fila 2: La edad debe ser un número entero mayor que cero.",
                    "Fila 7: La vacuna es obligatoria.",
                ],
            });
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, $"/ProgramasVacunacion/{id}/Cronograma");

        var respuesta = await cliente.PostAsync($"/ProgramasVacunacion/{id}/Cronograma", ContenidoMultiparte(token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("Fila 2: La edad debe ser un número entero mayor que cero.", html);
        Assert.Contains("Fila 7: La vacuna es obligatoria.", html);
    }

    [Fact]
    public async Task ImportarValidoRedirigeAlDetalle()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: BitVeterinario);
        var id = Guid.NewGuid();
        var token = await AplicacionDePruebas.TokenAntiforgeryAsync(cliente, $"/ProgramasVacunacion/{id}/Cronograma");

        var respuesta = await cliente.PostAsync($"/ProgramasVacunacion/{id}/Cronograma", ContenidoMultiparte(token));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"/ProgramasVacunacion/{id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(1, aplicacion.Api.VecesImportarCronograma);
    }

    [Fact]
    public async Task LaConfirmacionDeDesactivarAdvierteDelEfectoSobreLasTareas()
    {
        using var aplicacion = new AplicacionDePruebas();
        var cliente = await aplicacion.AccederAsync(funcCaisy: BitVeterinario);
        var id = Guid.NewGuid();
        aplicacion.Api.DetalleVacunacionActual = ApiIcarusFalsa.CrearDetalleVacunacion(id, "Publicado");

        var html = await cliente.GetStringAsync($"/ProgramasVacunacion/{id}/Desactivar");

        Assert.Contains("tareas pendientes", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
    }

    private static MultipartFormDataContent ContenidoMultiparte(string token)
    {
        var contenido = new MultipartFormDataContent();
        contenido.Add(new StringContent(token), "__RequestVerificationToken");
        var archivo = new StreamContent(new MemoryStream("PK\x03\x04 cronograma de prueba"u8.ToArray()));
        archivo.Headers.ContentType = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(archivo, "archivo", "cronograma.xlsx");
        return contenido;
    }
}
