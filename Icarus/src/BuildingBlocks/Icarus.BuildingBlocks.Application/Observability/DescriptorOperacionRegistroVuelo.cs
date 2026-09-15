namespace Icarus.BuildingBlocks.Application.Observability;

public sealed record DescriptorOperacionRegistroVuelo(
    string Nombre,
    IReadOnlyDictionary<string, DatoRegistroVuelo> CamposPermitidos)
{
    /// <summary>Construye un descriptor con su lista cerrada de campos. La
    /// ausencia de descriptor en una decisión nunca autoriza campos libres.</summary>
    public static DescriptorOperacionRegistroVuelo Crear(
        string nombre, params (string Nombre, DatoRegistroVuelo Tipo)[] camposPermitidos) =>
        new(nombre, camposPermitidos.ToDictionary(
            c => c.Nombre, c => c.Tipo, StringComparer.OrdinalIgnoreCase));
}
