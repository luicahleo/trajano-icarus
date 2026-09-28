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

#pragma warning disable S1144 // Setter técnico para EF
    public Guid JornadaAccesoId { get; private set; }
#pragma warning restore S1144

    public TipoMarcacion Tipo { get; private set; }

    public DateTimeOffset InstanteUtc { get; private set; }

    public Guid ClaveIdempotencia { get; private set; }

    public OrigenMarcacion Origen { get; private set; }
}
