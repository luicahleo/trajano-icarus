using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

// Registro funcional de un rechazo facial persistente tras agotar los intentos.
// Nace sin trabajador asignado; el cliente lo resuelve o descarta con motivo.
public sealed class IncidenciaAcceso : AggregateRoot
{
    private IncidenciaAcceso()
    {
    }

    public IncidenciaAcceso(
        Guid clienteId,
        Guid flujoMarcacionId,
        Guid sesionKioscoId,
        TipoMarcacion accion,
        DateTimeOffset primerRechazoUtc,
        DateTimeOffset tercerRechazoUtc,
        DateTimeOffset creadaEnUtc)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La incidencia debe pertenecer a un cliente.");
        if (flujoMarcacionId == Guid.Empty)
            throw new ReglaNegocioException("La incidencia debe vincularse a un flujo.");
        if (sesionKioscoId == Guid.Empty)
            throw new ReglaNegocioException("La incidencia debe vincularse a una sesión de kiosco.");
        if (tercerRechazoUtc < primerRechazoUtc)
            throw new ReglaNegocioException("El tercer rechazo no puede ser anterior al primero.");

        ClienteId = clienteId;
        FlujoMarcacionId = flujoMarcacionId;
        SesionKioscoId = sesionKioscoId;
        Accion = accion;
        PrimerRechazoUtc = primerRechazoUtc;
        TercerRechazoUtc = tercerRechazoUtc;
        Estado = EstadoIncidenciaAcceso.Pendiente;
        CreadaEnUtc = creadaEnUtc;
    }

    public Guid ClienteId { get; private set; }

    public Guid FlujoMarcacionId { get; private set; }

    public Guid SesionKioscoId { get; private set; }

    public TipoMarcacion Accion { get; private set; }

    public DateTimeOffset PrimerRechazoUtc { get; private set; }

    public DateTimeOffset TercerRechazoUtc { get; private set; }

    public EstadoIncidenciaAcceso Estado { get; private set; }

    public Guid? TrabajadorId { get; private set; }

    public Guid? JornadaId { get; private set; }

    public string? MotivoResolucion { get; private set; }

    public DateTimeOffset? ResueltaEnUtc { get; private set; }

    public DateTimeOffset CreadaEnUtc { get; private set; }

    public int Version { get; private set; }

    public void Resolver(Guid trabajadorId, Guid jornadaId, string motivo, DateTimeOffset instanteUtc)
    {
        if (Estado != EstadoIncidenciaAcceso.Pendiente)
            throw new ReglaNegocioException("La incidencia ya fue resuelta o descartada.");
        if (trabajadorId == Guid.Empty)
            throw new ReglaNegocioException("La resolución requiere un trabajador.");
        if (jornadaId == Guid.Empty)
            throw new ReglaNegocioException("La resolución requiere una jornada.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("La resolución requiere un motivo.");

        Estado = EstadoIncidenciaAcceso.Resuelta;
        TrabajadorId = trabajadorId;
        JornadaId = jornadaId;
        MotivoResolucion = motivo.Trim();
        ResueltaEnUtc = instanteUtc;
        Version++;
    }

    public void Descartar(string motivo, DateTimeOffset instanteUtc)
    {
        if (Estado != EstadoIncidenciaAcceso.Pendiente)
            throw new ReglaNegocioException("La incidencia ya fue resuelta o descartada.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("El descarte requiere un motivo.");

        Estado = EstadoIncidenciaAcceso.Descartada;
        MotivoResolucion = motivo.Trim();
        ResueltaEnUtc = instanteUtc;
        Version++;
    }
}
