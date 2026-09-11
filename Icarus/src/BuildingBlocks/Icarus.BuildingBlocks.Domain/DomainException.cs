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

public class ConflictException : DomainException
{
    public ConflictException() { }

    public ConflictException(string mensaje) : base(mensaje) { }

    public ConflictException(string mensaje, Exception interna) : base(mensaje, interna) { }
}

// Cualquier excepción de dominio de cualquier módulo puede implementar esto
// para pedirle al middleware un título de ProblemDetails distinto del
// genérico de su clase base (p. ej. distinguir un subtipo de
// ConflictException). Building Blocks define el contrato; el middleware
// nunca conoce el tipo concreto de ningún módulo vertical.
public interface IExcepcionConTituloPropio
{
    string Titulo { get; }
}
