using System.Text;
using Icarus.ControlAcceso.Application.Biometria;

namespace Icarus.ControlAcceso.Infrastructure.Argos;

// Proveedor determinista para pruebas y desarrollo: no hay biometría real.
// Reconoce prefijos de control para simular PAD y errores; dos muestras con el
// mismo contenido identifican al mismo trabajador. Nunca se registra.
public sealed class ProveedorIdentidadFacialDoble : IProveedorIdentidadFacial
{
    private const string Modelo = "doble-v1";
    private const int VersionModelo = 1;

    private static readonly byte[] PadFalla = Encoding.ASCII.GetBytes("PAD_FALLA");
    private static readonly byte[] SinRostro = Encoding.ASCII.GetBytes("SIN_ROSTRO");
    private static readonly byte[] VariosRostros = Encoding.ASCII.GetBytes("VARIOS_ROSTROS");

    public Task<ResultadoEnrolamiento> ExtraerAsync(
        MuestraFacial muestra, CancellationToken cancellationToken = default)
    {
        var motivo = MotivoDeControl(muestra.Contenido);
        if (motivo is not null)
            return Task.FromResult(ResultadoEnrolamiento.Rechazado(motivo));

        return Task.FromResult(
            ResultadoEnrolamiento.Exito(muestra.Contenido, Modelo, VersionModelo));
    }

    public Task<ResultadoIdentificacionFacial> IdentificarAsync(
        MuestraFacial muestra,
        IReadOnlyList<CandidatoFacial> candidatos,
        CancellationToken cancellationToken = default)
    {
        var motivo = MotivoDeControl(muestra.Contenido);
        if (motivo is not null)
            return Task.FromResult(ResultadoIdentificacionFacial.SinCoincidencia(motivo));

        var compatibles = candidatos
            .Where(c => string.Equals(c.ModeloFormato, Modelo, StringComparison.Ordinal))
            .Where(c => c.Vector.AsSpan().SequenceEqual(muestra.Contenido))
            .ToList();

        if (compatibles.Count == 0)
        {
            var hayCompatibles = candidatos.Any(c =>
                string.Equals(c.ModeloFormato, Modelo, StringComparison.Ordinal));
            return Task.FromResult(ResultadoIdentificacionFacial.SinCoincidencia(
                hayCompatibles ? "sin_coincidencia" : "modelo_incompatible"));
        }

        if (compatibles.Count > 1)
            return Task.FromResult(ResultadoIdentificacionFacial.SinCoincidencia("ambigua"));

        return Task.FromResult(ResultadoIdentificacionFacial.Coincide(compatibles[0].TrabajadorId));
    }

    private static string? MotivoDeControl(byte[] contenido)
    {
        if (contenido.AsSpan().StartsWith(PadFalla))
            return "pad_fallido";
        if (contenido.AsSpan().StartsWith(SinRostro))
            return "sin_rostro";
        if (contenido.AsSpan().StartsWith(VariosRostros))
            return "varios_rostros";
        return null;
    }
}
