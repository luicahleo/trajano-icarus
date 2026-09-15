namespace Trajano.GestorCaisy.Observabilidad;

/// <summary>Contrato local que transporta la plantilla técnica de una llamada
/// saliente en <see cref="HttpRequestMessage.Options"/>. La plantilla la aporta
/// el código de la operación que conoce la ruta; nunca se deriva de la URI
/// concreta ni de datos del formulario o de la respuesta.</summary>
public static class MetadatosPeticionApi
{
    public const string RutaSinResolver = "unmatched";

    private static readonly HttpRequestOptionsKey<string> Clave =
        new("Trajano.Observabilidad.PlantillaApi");

    public static void EstablecerRuta(this HttpRequestMessage peticion, string plantilla) =>
        peticion.Options.Set(Clave, plantilla);

    public static string RutaSegura(this HttpRequestMessage peticion) =>
        peticion.Options.TryGetValue(Clave, out var plantilla) && !string.IsNullOrWhiteSpace(plantilla)
            ? plantilla
            : RutaSinResolver;
}
