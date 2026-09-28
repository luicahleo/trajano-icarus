namespace Icarus.ControlAcceso.Infrastructure.Biometria;

public sealed class OpcionesProteccionPlantillas
{
    public const string Seccion = "ControlAcceso:Plantillas";

    public string ClaveCifradoBase64 { get; set; } = string.Empty;
}
