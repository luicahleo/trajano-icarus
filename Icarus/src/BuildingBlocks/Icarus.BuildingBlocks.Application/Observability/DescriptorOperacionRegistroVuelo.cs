namespace Icarus.BuildingBlocks.Application.Observability;

public sealed record DescriptorOperacionRegistroVuelo(
    string Nombre,
    IReadOnlyDictionary<string, DatoRegistroVuelo> CamposPermitidos);
