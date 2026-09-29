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
    public DbSet<SesionKiosco> SesionesKiosco => Set<SesionKiosco>();
    public DbSet<OperacionEnrolamiento> OperacionesEnrolamiento => Set<OperacionEnrolamiento>();
    public DbSet<OperacionMarcacion> OperacionesMarcacion => Set<OperacionMarcacion>();
    public DbSet<FlujoMarcacion> FlujosMarcacion => Set<FlujoMarcacion>();
    public DbSet<CapturaMarcacion> CapturasMarcacion => Set<CapturaMarcacion>();
    public DbSet<IncidenciaAcceso> IncidenciasAcceso => Set<IncidenciaAcceso>();

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
        modelBuilder.Entity<SesionKiosco>().HasQueryFilter(s =>
            _clienteIdActual == null || s.ClienteId == _clienteIdActual);
        modelBuilder.Entity<OperacionEnrolamiento>().HasQueryFilter(o =>
            _clienteIdActual == null || o.ClienteId == _clienteIdActual);
        modelBuilder.Entity<OperacionMarcacion>().HasQueryFilter(o =>
            _clienteIdActual == null || o.ClienteId == _clienteIdActual);
        modelBuilder.Entity<FlujoMarcacion>().HasQueryFilter(f =>
            _clienteIdActual == null || f.ClienteId == _clienteIdActual);
        modelBuilder.Entity<IncidenciaAcceso>().HasQueryFilter(i =>
            _clienteIdActual == null || i.ClienteId == _clienteIdActual);
    }
}
