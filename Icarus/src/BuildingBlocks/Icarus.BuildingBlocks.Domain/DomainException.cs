namespace Icarus.BuildingBlocks.Domain;

// Mensajes genéricos por la regla anti-PII: nunca incluir datos del trabajador,
// documentos ni credenciales.
public abstract class DomainException : Exception
{
    protected DomainException() { }

    protected DomainException(string mensaje) : base(mensaje) { }

    protected DomainException(string mensaje, Exception interna) : base(mensaje, interna) { }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException() { }

    public NotFoundException(string mensaje) : base(mensaje) { }

    public NotFoundException(string mensaje, Exception interna) : base(mensaje, interna) { }

    public NotFoundException(string entidad, Guid id)
        : base($"{entidad} no encontrado.")
    {
        Entidad = entidad;
        EntidadId = id;
    }

    public string Entidad { get; } = string.Empty;
    public Guid EntidadId { get; }
}

public sealed class ConflictException : DomainException
{
    public ConflictException() { }

    public ConflictException(string mensaje) : base(mensaje) { }

    public ConflictException(string mensaje, Exception interna) : base(mensaje, interna) { }
}
