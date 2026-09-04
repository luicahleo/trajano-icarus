namespace Icarus.Identity.Application.UsuariosCaisy;

using Icarus.Identity.Domain;

// Puerto para la administración de cuentas CAISY (spec SP8): el Administrador
// de plataforma crea, desactiva y asigna funcionalidades; nunca se crean
// cuentas CAISY desde la aplicación de oficina. null en CrearAsync: cuenta
// rechazada (correo en uso u otra restricción técnica) — el caller traduce a
// conflicto genérico sin revelar la causa (anti-PII). false: cuenta
// inexistente o inoperativa para los demás métodos.
public interface ICuentasCaisy
{
    Task<Guid?> CrearAsync(
        string email, string contrasena, FuncionalidadesCaisy funcionalidades,
        CancellationToken cancellationToken = default);

    Task<bool> DesactivarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<bool> DefinirFuncionalidadesAsync(
        Guid usuarioId, FuncionalidadesCaisy funcionalidades,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsuarioCaisyResumen>> ListarAsync(CancellationToken cancellationToken = default);
}
