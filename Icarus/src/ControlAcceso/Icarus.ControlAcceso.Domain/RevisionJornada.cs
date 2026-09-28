using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

public sealed class RevisionJornada : Entity
{
    private readonly List<ValorEfectivo> _valores = [];

    private RevisionJornada()
    {
    }

    public RevisionJornada(
        Guid jornadaAccesoId,
        DateTimeOffset instanteCorreccionUtc,
        int hastaSecuenciaOriginal,
        string motivo,
        Guid autorId,
        IEnumerable<ValorEfectivo> valoresEfectivos)
    {
        if (jornadaAccesoId == Guid.Empty)
            throw new ReglaNegocioException("La revisión debe pertenecer a una jornada.");

        JornadaAccesoId = jornadaAccesoId;
        InstanteCorreccionUtc = instanteCorreccionUtc;
        HastaSecuenciaOriginal = hastaSecuenciaOriginal;
        Motivo = motivo;
        AutorId = autorId;
        _valores = valoresEfectivos.ToList();
    }

#pragma warning disable S1144 // Setter técnico para EF
    public Guid JornadaAccesoId { get; private set; }
#pragma warning restore S1144

    public DateTimeOffset InstanteCorreccionUtc { get; private set; }

    public int HastaSecuenciaOriginal { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public Guid AutorId { get; private set; }

    public IReadOnlyCollection<ValorEfectivo> ValoresEfectivos => _valores;
}
