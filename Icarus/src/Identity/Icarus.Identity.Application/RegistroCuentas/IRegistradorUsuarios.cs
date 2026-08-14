namespace Icarus.Identity.Application.RegistroCuentas;

// Registro de cuentas para el alta embebida del Host (spec): ya no es un CRUD
// de usuarios, es un servicio de cuentas que consume únicamente
// AltaCuentasServicio.
public interface IRegistradorUsuarios
{
    // null: no se pudo registrar (email en uso u otra restricción). El caller
    // lo traduce a un conflicto genérico, sin revelar la causa (anti-PII).
    Task<Guid?> RegistrarAsync(
        string email, string contrasena, string rol, Guid? clienteId, Guid? trabajadorId,
        CancellationToken cancellationToken = default);
}
