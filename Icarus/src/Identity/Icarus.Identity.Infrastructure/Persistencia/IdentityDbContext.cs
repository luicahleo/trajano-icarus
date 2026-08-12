using Icarus.BuildingBlocks.Application;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Identity.Infrastructure.Persistencia;

// IdentityUserContext (no IdentityDbContext de ASP.NET): el rol es una columna
// de Usuario, no se usan tablas AspNetRoles/AspNetRoleClaims.
public sealed class IdentityDbContext : IdentityUserContext<Usuario, Guid>, IUnitOfWork
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        builder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
