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

        // Para el kiosco el instante del evento es también su creación real.
        CreadaEnUtc = instanteUtc;
    }

#pragma warning disable S1144 // Setter técnico para EF
    public Guid JornadaAccesoId { get; private set; }
#pragma warning restore S1144

    public TipoMarcacion Tipo { get; private set; }

    public DateTimeOffset InstanteUtc { get; private set; }

    public Guid ClaveIdempotencia { get; private set; }

    public OrigenMarcacion Origen { get; private set; }

    // Metadatos exclusivos del registro manual: hora declarada por el cliente,
    // autor, motivo y el instante real de creación del servidor. Nulos en una
    // marcación de kiosco.
    public DateTimeOffset? HoraDeclaradaUtc { get; private set; }

    public DateTimeOffset CreadaEnUtc { get; private set; }

    public Guid? AutorId { get; private set; }

    public string? Motivo { get; private set; }

    internal void MarcarComoManual(
        Guid autorId, string motivo, DateTimeOffset horaDeclaradaUtc, DateTimeOffset creadaEnUtc)
    {
        Origen = OrigenMarcacion.ManualCliente;
        InstanteUtc = horaDeclaradaUtc;
        HoraDeclaradaUtc = horaDeclaradaUtc;
        AutorId = autorId;
        Motivo = motivo;
        CreadaEnUtc = creadaEnUtc;
    }
}
