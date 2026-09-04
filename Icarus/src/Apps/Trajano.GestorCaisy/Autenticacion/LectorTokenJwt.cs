using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace Trajano.GestorCaisy.Autenticacion;

public sealed record EncabezadoToken(
    string SujetoId, string Rol, int? FuncionalidadesCaisy, DateTimeOffset? ExpiraEn);

// Lector del access token emitido por Trajano-Icarus. Solo decodifica la
// carga: el token se acaba de recibir de la API por TLS y su validez la
// controla la API misma (401 y renovación), no esta aplicación.
public static class LectorTokenJwt
{
    public static EncabezadoToken? Leer(string token)
    {
        var segmentos = token.Split('.');
        if (segmentos.Length != 3)
            return null;
        try
        {
            var carga = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(segmentos[1]));
            using var documento = JsonDocument.Parse(carga);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object
                || !raiz.TryGetProperty("sub", out var sub)
                || !raiz.TryGetProperty("rol", out var rol)
                || sub.GetString() is not { } sujeto
                || rol.GetString() is not { } valorRol)
                return null;

            int? funcionalidades = null;
            if (raiz.TryGetProperty("funcCaisy", out var funcCaisy)
                && int.TryParse(funcCaisy.GetString(), out var mascara))
                funcionalidades = mascara;

            DateTimeOffset? expira = null;
            if (raiz.TryGetProperty("exp", out var exp) && exp.ValueKind == JsonValueKind.Number)
                expira = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());

            return new EncabezadoToken(sujeto, valorRol, funcionalidades, expira);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
