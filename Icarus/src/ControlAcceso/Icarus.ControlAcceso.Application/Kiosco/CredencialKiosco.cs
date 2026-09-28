using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Icarus.ControlAcceso.Application.Kiosco;

// La credencial del kiosco es un token aleatorio que solo conoce la cookie; en
// la base se conserva únicamente su hash. Nunca se registra el token en logs.
public static class CredencialKiosco
{
    private const int BytesToken = 32;

    public static string GenerarToken() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(BytesToken));

    public static string CalcularHash(string token)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("La credencial no puede estar vacía.", nameof(token));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
