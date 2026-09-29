using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

// Flujo de intentos de marcación de un kiosco. Es la autoridad del contador.
// El navegador solo presenta el estado que devuelve el backend.
public sealed class FlujoMarcacion : AggregateRoot
{
    public const int MaximoIntentos = 3;

    private readonly List<CapturaMarcacion> _capturas = [];

    private FlujoMarcacion()
    {
    }

    public FlujoMarcacion(
        Guid clienteId,
        Guid sesionKioscoId,
        TipoMarcacion accion,
        DateTimeOffset creadaEnUtc)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("El flujo debe pertenecer a un cliente.");
        if (sesionKioscoId == Guid.Empty)
            throw new ReglaNegocioException("El flujo debe vincularse a una sesión de kiosco.");

        ClienteId = clienteId;
        SesionKioscoId = sesionKioscoId;
        Accion = accion;
        CreadaEnUtc = creadaEnUtc;
        Estado = EstadoFlujoMarcacion.Activo;
    }

    public Guid ClienteId { get; private set; }

    public Guid SesionKioscoId { get; private set; }

    public TipoMarcacion Accion { get; private set; }

    public EstadoFlujoMarcacion Estado { get; private set; }

    public DateTimeOffset CreadaEnUtc { get; private set; }

    public DateTimeOffset? FinalizadaEnUtc { get; private set; }

    public IReadOnlyCollection<CapturaMarcacion> Capturas => _capturas;

    public int Version { get; private set; }

    public bool PuedeContinuar(TipoMarcacion accion) =>
        Estado == EstadoFlujoMarcacion.Activo && Accion == accion;

    public (CapturaMarcacion Captura, bool CreaIncidencia) RegistrarRechazo(
        Guid claveCaptura, string motivo, DateTimeOffset instanteUtc, int intentosPrevios)
    {
        if (Estado != EstadoFlujoMarcacion.Activo)
            throw new ReglaNegocioException("El flujo ya no admite capturas.");

        var captura = CapturaMarcacion.Rechazada(claveCaptura, motivo, instanteUtc);
        var intentosTotales = intentosPrevios + 1;
        var creaIncidencia = intentosTotales >= MaximoIntentos;
        if (creaIncidencia)
        {
            Estado = EstadoFlujoMarcacion.FinalizadoPorIncidencia;
            FinalizadaEnUtc = instanteUtc;
        }

        Version++;
        return (captura, creaIncidencia);
    }

    public CapturaMarcacion RegistrarExito(
        Guid claveCaptura, Guid trabajadorId, DateTimeOffset instanteUtc)
    {
        if (Estado != EstadoFlujoMarcacion.Activo)
            throw new ReglaNegocioException("El flujo ya no admite capturas.");

        var captura = CapturaMarcacion.Identificada(claveCaptura, trabajadorId, instanteUtc);
        Estado = EstadoFlujoMarcacion.FinalizadoPorExito;
        FinalizadaEnUtc = instanteUtc;
        Version++;
        return captura;
    }
}
