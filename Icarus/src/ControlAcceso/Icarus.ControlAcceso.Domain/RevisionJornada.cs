using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

public sealed class RevisionJornada : Entity
{
    private readonly List<ValorEfectivo> _valores = [];

    private RevisionJornada()
    {
    }

    public RevisionJornada(
        DateTimeOffset instanteCorreccionUtc,
        int hastaSecuenciaOriginal,
        string motivo,
        Guid autorId,
        IEnumerable<ValorEfectivo> valoresEfectivos)
    {
        InstanteCorreccionUtc = instanteCorreccionUtc;
        HastaSecuenciaOriginal = hastaSecuenciaOriginal;
        Motivo = motivo;
        AutorId = autorId;
        _valores = valoresEfectivos.ToList();
    }

    public DateTimeOffset InstanteCorreccionUtc { get; private set; }

    public int HastaSecuenciaOriginal { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public Guid AutorId { get; private set; }

    public IReadOnlyCollection<ValorEfectivo> ValoresEfectivos => _valores.AsReadOnly();
}
