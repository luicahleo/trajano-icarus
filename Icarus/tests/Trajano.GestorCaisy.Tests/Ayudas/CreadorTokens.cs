using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace Trajano.GestorCaisy.Tests.Ayudas;

// Fabrica access tokens de prueba con la misma estructura que emite
// Trajano-Icarus (claims sub, rol y funcCaisy); la firma no se valida porque
// el lector de la aplicación solo decodifica la carga.
public static class CreadorTokens
{
    public static string Crear(string rol = "GestorCaisy", int? funcCaisy = 1,
        string sujeto = "0198f7a2-3b4c-7d5e-8f90-1a2b3c4d5e6f", DateTimeOffset? expira = null)
    {
        var expiraEn = expira ?? DateTimeOffset.UtcNow.AddMinutes(10);
        var encabezado = Codificar("""{"alg":"HS256","typ":"JWT"}""");
        var carga = Codificar(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["sub"] = sujeto,
            ["rol"] = rol,
            ["funcCaisy"] = funcCaisy?.ToString(),
            ["exp"] = expiraEn.ToUnixTimeSeconds(),
        }));
        return $"{encabezado}.{carga}.firma-de-prueba";
    }

    private static string Codificar(string texto) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(texto));
}
