namespace Icarus.BuildingBlocks.Domain;

// Violación de una regla de negocio del dominio (el middleware la mapea a 400).
// Mensajes genéricos por la regla anti-PII: nunca incluir datos del trabajador,
// documentos ni credenciales.
public sealed class ReglaNegocioException : DomainException
{
    public ReglaNegocioException() { }

    public ReglaNegocioException(string mensaje) : base(mensaje) { }

    public ReglaNegocioException(string mensaje, Exception interna) : base(mensaje, interna) { }
}
