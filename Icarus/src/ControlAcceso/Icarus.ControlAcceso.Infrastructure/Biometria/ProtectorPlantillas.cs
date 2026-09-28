using System.Security.Cryptography;
using System.Text;
using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Domain;
using Microsoft.Extensions.Options;

namespace Icarus.ControlAcceso.Infrastructure.Biometria;

public sealed class ProtectorPlantillas : IProtectorPlantillas
{
    private readonly byte[] _clave;

    public ProtectorPlantillas(IOptions<OpcionesProteccionPlantillas> opciones)
    {
        var claveBase64 = opciones.Value.ClaveCifradoBase64;
        if (string.IsNullOrWhiteSpace(claveBase64))
            throw new InvalidOperationException("La clave de cifrado de plantillas no está configurada.");

        _clave = Convert.FromBase64String(claveBase64);
        if (_clave.Length != 32)
            throw new InvalidOperationException("La clave de cifrado de plantillas debe tener 256 bits (32 bytes).");
    }

    public PlantillaFacialProtegida Proteger(
        byte[] vector,
        string modeloFormato,
        Guid clienteId,
        Guid trabajadorId,
        int versionEnrolamiento,
        int versionClave = 1)
    {
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        var tag = new byte[16];
        var contenidoCifrado = new byte[vector.Length];
        var datosAsociados = DatosAsociados(clienteId, trabajadorId, versionEnrolamiento, modeloFormato, versionClave);

        using (var aes = new AesGcm(_clave, 16))
        {
            aes.Encrypt(nonce, vector, contenidoCifrado, tag, datosAsociados);
        }

        return new PlantillaFacialProtegida(
            clienteId,
            trabajadorId,
            contenidoCifrado,
            nonce,
            tag,
            versionClave,
            modeloFormato,
            versionEnrolamiento);
    }

    public byte[]? Recuperar(PlantillaFacialProtegida protegida)
    {
        var datosAsociados = DatosAsociados(
            protegida.ClienteId,
            protegida.TrabajadorId,
            protegida.VersionEnrolamiento,
            protegida.ModeloFormato,
            protegida.VersionClave);
        var vector = new byte[protegida.ContenidoCifrado.Length];

        try
        {
            using (var aes = new AesGcm(_clave, 16))
            {
                aes.Decrypt(
                    protegida.Nonce,
                    protegida.ContenidoCifrado,
                    protegida.Tag,
                    vector,
                    datosAsociados);
            }

            return vector;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // Método de prueba: permite verificar que una clave incorrecta no descifra.
    public static byte[]? Recuperar(PlantillaFacialProtegida protegida, byte[] clave)
    {
        var datosAsociados = DatosAsociados(
            protegida.ClienteId,
            protegida.TrabajadorId,
            protegida.VersionEnrolamiento,
            protegida.ModeloFormato,
            protegida.VersionClave);
        var vector = new byte[protegida.ContenidoCifrado.Length];

        try
        {
            using (var aes = new AesGcm(clave, 16))
            {
                aes.Decrypt(
                    protegida.Nonce,
                    protegida.ContenidoCifrado,
                    protegida.Tag,
                    vector,
                    datosAsociados);
            }

            return vector;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static byte[] DatosAsociados(
        Guid clienteId,
        Guid trabajadorId,
        int versionEnrolamiento,
        string modeloFormato,
        int versionClave) =>
        Encoding.UTF8.GetBytes(
            $"{clienteId:N}|{trabajadorId:N}|{versionEnrolamiento}|{modeloFormato}|{versionClave}");
}
