namespace Icarus.Identity.Application.Usuarios;

public interface IRegistradorUsuarios
{
    // null: no se pudo registrar (email en uso u otra restricción). El handler
    // lo traduce a un conflicto genérico, sin revelar la causa (anti-PII).
    Task<Guid?> RegistrarAsync(
        string email, string contrasena, string rol, Guid? clienteId, Guid? trabajadorId,
        CancellationToken cancellationToken = default);
}
