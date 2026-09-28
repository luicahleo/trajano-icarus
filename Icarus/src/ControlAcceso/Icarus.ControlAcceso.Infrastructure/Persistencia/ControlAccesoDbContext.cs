using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Domain;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Persistencia;

public sealed class ControlAccesoDbContext : DbContext, IUnidadTrabajoControlAcceso
{
    private readonly Guid? _clienteIdActual;

    public ControlAccesoDbContext(DbContextOptions<ControlAccesoDbContext> opciones, ICurrentUser usuarioActual)
        : base(opciones) => _clienteIdActual = usuarioActual.ClienteId;

    public DbSet<JornadaAcceso> JornadasAcceso => Set<JornadaAcceso>();
    public DbSet<ConfiguracionAccesoTrabajador> ConfiguracionAccesoTrabajador => Set<ConfiguracionAccesoTrabajador>();
    public DbSet<PlantillaFacialProtegida> PlantillasFaciales => Set<PlantillaFacialProtegida>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("control_acceso");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ControlAccesoDbContext).Assembly);

        modelBuilder.Entity<JornadaAcceso>().HasQueryFilter(j =>
            j.EstaActivo && (_clienteIdActual == null || j.ClienteId == _clienteIdActual));
        modelBuilder.Entity<ConfiguracionAccesoTrabajador>().HasQueryFilter(c =>
            c.EstaActivo && (_clienteIdActual == null || c.ClienteId == _clienteIdActual));
        modelBuilder.Entity<PlantillaFacialProtegida>().HasQueryFilter(p =>
            _clienteIdActual == null || p.ClienteId == _clienteIdActual);
    }
}
