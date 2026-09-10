using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Application.DespachosHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Icarus.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FechasNegocio = Icarus.GestionAvicola.Application.PreciosHuevo.FechasNegocio;

namespace Icarus.IntegrationTests;

// SP9D (spec: "Corrección de una publicación vigente"): el caso dudoso de la
// revisión final — una correctiva con LA MISMA FechaVigencia que la errónea.
// En la misma transacción la correctiva pasa de Borrador (0) a Publicada (1)
// y la errónea de Publicada (1) a Corregida (3), contra el índice único
// filtrado [Estado]=1 AND [EstaActivo]=1 en FechaVigencia. Este test ejecuta
// el comando completo por HTTP y confirma (o refuta) que el orden de
// tracking de EF no colisiona con ese índice. La errónea se siembra con
// vigencia «hoy»: es la mayor vigencia posible no futura, así nadie de la
// base compartida la sombra como vigente.
[Collection(IntegracionCollection.Nombre)]
public class CorreccionPrecioHuevoTests
{
    private readonly IdentityFactory _factory;

    public CorreccionPrecioHuevoTests(IdentityFactory factory) => _factory = factory;

    private async Task<string> CrearCuentaCaisyAsync()
    {
        var anonimo = _factory.CreateClient();
        var tokenAdmin = await LoginComo(anonimo, SemillaIdentidad.EmailAdmin);
        var emailCaisy = $"correccion-huevo-{Guid.NewGuid():N}@icarus.test";
        var alta = await anonimo.SendAsync(Pedido(HttpMethod.Post, "/api/usuarios-caisy/", tokenAdmin,
            JsonContent.Create(new
            {
                email = emailCaisy,
                contrasena = "Clave-Caisy-123",
                funcionalidades = new[] { "GestorRecepcionHuevos" },
            })));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        return await LoginComo(anonimo, emailCaisy, "Clave-Caisy-123");
    }

    private static async Task<string> LoginComo(HttpClient cliente, string email, string? contrasena = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/identidad/sesion",
            new { email, contrasena = contrasena ?? IdentityFactory.ContrasenaDePrueba });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return cuerpo.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Pedido(
        HttpMethod metodo, string url, string token, HttpContent? contenido = null) =>
        new(metodo, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = contenido,
        };

    [Fact]
    public async Task CorregirConCorrectivaDeLaMismaVigenciaPublicaCorrigeYAjustaSinColisionDeIndice()
    {
        var hoy = FechasNegocio.Hoy();
        var actorId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();

        // Publicación errónea vigente (Publicada) y correctiva en borrador
        // con LA MISMA fecha de vigencia y precio distinto. Ambas cubren los
        // seis tamaños a los precios estándar del resto de la suite: lo que
        // queda Publicada con vigencia «hoy» tras la corrección sigue
        // sirviendo como vigente a los tests que corren después en la base
        // compartida (sus libros usan exactamente estos precios).
        var detallesErronea = new[]
        {
            new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 1.50m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Primera, 1.40m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Segunda, 1.30m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Tercera, 1.20m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Cuarta, 1.10m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Quinta, 1.00m),
        };
        var detallesCorrectiva = new[]
        {
            new DatosDetallePrecioHuevo(TamanoHuevo.Extra, 1.60m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Primera, 1.40m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Segunda, 1.30m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Tercera, 1.20m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Cuarta, 1.10m),
            new DatosDetallePrecioHuevo(TamanoHuevo.Quinta, 1.00m),
        };
        var erronea = new PublicacionPrecioHuevo(hoy.AddDays(-1), hoy, 0.05m, detallesErronea);
        erronea.Publicar();
        var correctiva = new PublicacionPrecioHuevo(hoy.AddDays(-1), hoy, 0.05m, detallesCorrectiva);

        // Despacho Recibido que usó la errónea: 1 amarra (180 huevos)
        // congelados a 1,40 Bs.
        var despacho = new DespachoHuevo(clienteId, Guid.NewGuid(), actorId,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Extra, 1, 0)]);
        despacho.Despachar(hoy, actorId,
            [new DatosPrecioDespachoHuevo(TamanoHuevo.Extra, 1.40m, erronea.Id)],
            new DatosDocumentoNota(
                Guid.NewGuid(), Guid.NewGuid(), "image/jpeg", 1024, 512, "hash-sha256", "nota.jpg"));
        despacho.ConfirmarRecepcion(hoy, actorId);

        using (var alcance = _factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
            db.AddRange(erronea, correctiva, despacho);
            await db.SaveChangesAsync();
        }

        var token = await CrearCuentaCaisyAsync();
        var respuesta = await _factory.CreateClient().SendAsync(Pedido(
            HttpMethod.Post, "/api/precios-huevo-caisy/corregir", token,
            JsonContent.Create(new
            {
                publicacionErroneaId = erronea.Id,
                publicacionCorrectivaId = correctiva.Id,
                motivo = "Precio mal digitado.",
            })));

        // Sin excepción de índice único (ni de ningún otro tipo): la
        // corrección se aplica de una.
        var cuerpoError = respuesta.StatusCode == HttpStatusCode.NoContent
            ? string.Empty
            : await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.NoContent,
            $"Esperaba 204 NoContent, llegó {(int)respuesta.StatusCode}: {cuerpoError}");

        using var alcanceVerificacion = _factory.Services.CreateScope();
        var dbVerificacion = alcanceVerificacion.ServiceProvider
            .GetRequiredService<GestionAvicolaDbContext>();
        var erroneaGuardada = await dbVerificacion.PublicacionesPreciosHuevo
            .SingleAsync(p => p.Id == erronea.Id);
        Assert.Equal(EstadoPublicacionPrecioHuevo.Corregida, erroneaGuardada.Estado);
        Assert.Equal(correctiva.Id, erroneaGuardada.PublicacionCorrectivaId);
        var correctivaGuardada = await dbVerificacion.PublicacionesPreciosHuevo
            .SingleAsync(p => p.Id == correctiva.Id);
        Assert.Equal(EstadoPublicacionPrecioHuevo.Publicada, correctivaGuardada.Estado);

        // El ajuste queda persistido: (1,60 + 0,05 - 1,40) × 180 huevos = 45.
        var ajuste = await dbVerificacion.AjustesCreditoHuevo
            .SingleAsync(a => a.DespachoHuevoId == despacho.Id);
        Assert.Equal(45m, ajuste.Monto);

        // ...y reflejado en el saldo: el despacho se recibió hoy (dentro del
        // desfase de 14 días no aporta), así que el saldo es solo el ajuste.
        var repositorioBalance = alcanceVerificacion.ServiceProvider
            .GetRequiredService<IRepositorioBalanceCreditoHuevo>();
        var saldo = await repositorioBalance.ObtenerSaldoDisponibleAsync(clienteId, hoy);
        Assert.Equal(45m, saldo);
    }
}
