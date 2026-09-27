using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

public sealed class Marcacion : Entity
{
    private Marcacion()
    {
    }

    public Marcacion(TipoMarcacion tipo, DateTimeOffset instanteUtc, Guid claveIdempotencia, OrigenMarcacion origen)
    {
        Tipo = tipo;
        InstanteUtc = instanteUtc;
        ClaveIdempotencia = claveIdempotencia;
        Origen = origen;
    }

    public TipoMarcacion Tipo { get; private set; }

    public DateTimeOffset InstanteUtc { get; private set; }

    public Guid ClaveIdempotencia { get; private set; }

    public OrigenMarcacion Origen { get; private set; }
}
