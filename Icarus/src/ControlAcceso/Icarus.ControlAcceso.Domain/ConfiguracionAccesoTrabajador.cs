using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

public sealed class ConfiguracionAccesoTrabajador : AggregateRoot
{
    private ConfiguracionAccesoTrabajador()
    {
    }

    public ConfiguracionAccesoTrabajador(Guid clienteId, Guid trabajadorId)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La configuración debe pertenecer a un cliente.");
        if (trabajadorId == Guid.Empty)
            throw new ReglaNegocioException("La configuración debe pertenecer a un trabajador.");

        ClienteId = clienteId;
        TrabajadorId = trabajadorId;
        Habilitado = false;
        Enrolamiento = EstadoEnrolamiento.SinEnrolar;
        VersionEnrolamiento = 0;
    }

    public ConfiguracionAccesoTrabajador(Guid id, Guid clienteId, Guid trabajadorId)
        : this(clienteId, trabajadorId) => Id = id;

    public Guid ClienteId { get; private set; }

    public Guid TrabajadorId { get; private set; }

    public bool Habilitado { get; private set; }

    public EstadoEnrolamiento Enrolamiento { get; private set; }

    public int VersionEnrolamiento { get; private set; }

    public bool EstaActivo { get; private set; } = true;

    public void Habilitar() => Habilitado = true;

    public void Deshabilitar() => Habilitado = false;

    public void Enrolar(int version)
    {
        if (version <= 0)
            throw new ReglaNegocioException("La versión de enrolamiento debe ser mayor a cero.");

        Enrolamiento = EstadoEnrolamiento.Vigente;
        VersionEnrolamiento = version;
        Habilitado = true;
    }

    public void Revocar()
    {
        Enrolamiento = EstadoEnrolamiento.Revocado;
        VersionEnrolamiento = 0;
        Habilitado = false;
    }
}
