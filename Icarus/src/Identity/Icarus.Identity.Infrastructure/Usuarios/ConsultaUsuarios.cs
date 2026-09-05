using Icarus.Identity.Application.Sesiones;
using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Identity.Infrastructure.Usuarios;

public sealed class ConsultaUsuarios : IConsultaUsuarios
{
    private readonly IdentityDbContext _db;

    public ConsultaUsuarios(IdentityDbContext db) => _db = db;

    public async Task<UsuarioResumen?> ObtenerPorIdAsync(
        Guid usuarioId, CancellationToken cancellationToken = default) =>
        await _db.Users
            .Where(u => u.Id == usuarioId && u.Activo)
            .Select(u => new UsuarioResumen(
                u.Id, u.Email ?? string.Empty, u.Rol, u.ClienteId, u.TrabajadorId,
                u.FuncionalidadesCaisy))
            .SingleOrDefaultAsync(cancellationToken);
}
