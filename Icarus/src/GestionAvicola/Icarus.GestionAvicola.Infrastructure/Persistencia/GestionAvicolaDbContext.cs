using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Domain;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Persistencia;

public sealed class GestionAvicolaDbContext : DbContext, IUnidadTrabajoGestionAvicola
{
    private readonly Guid? _clienteIdActual;

    public GestionAvicolaDbContext(DbContextOptions<GestionAvicolaDbContext> opciones, ICurrentUser usuarioActual)
        : base(opciones) => _clienteIdActual = usuarioActual.ClienteId;

    public DbSet<Granja> Granjas => Set<Granja>();
    public DbSet<Galpon> Galpones => Set<Galpon>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("gestion_avicola");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GestionAvicolaDbContext).Assembly);
        modelBuilder.Entity<Granja>().HasQueryFilter(g => g.EstaActivo && (_clienteIdActual == null || g.ClienteId == _clienteIdActual));
        modelBuilder.Entity<Galpon>().HasQueryFilter(g => g.EstaActivo && (_clienteIdActual == null || g.ClienteId == _clienteIdActual));
    }
}
