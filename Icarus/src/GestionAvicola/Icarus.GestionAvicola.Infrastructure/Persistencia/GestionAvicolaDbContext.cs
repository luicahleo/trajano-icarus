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
    public DbSet<RegistroProduccion> RegistrosProduccion => Set<RegistroProduccion>();
    public DbSet<RegistroMortalidad> RegistrosMortalidad => Set<RegistroMortalidad>();
    public DbSet<ProgramaVacunacion> ProgramasVacunacion => Set<ProgramaVacunacion>();
    public DbSet<ItemPlanVacunacion> ItemsPlanVacunacion => Set<ItemPlanVacunacion>();
    public DbSet<TareaVacunacion> TareasVacunacion => Set<TareaVacunacion>();
    public DbSet<NotificacionPreciosAlimentos> NotificacionesPreciosAlimentos => Set<NotificacionPreciosAlimentos>();
    public DbSet<PedidoAlimento> PedidosAlimento => Set<PedidoAlimento>();
    // Sin filtro de tenant (spec SP8): el alcance incluye la bandeja global
    // de CAISY (ClienteId nulo) y cada consulta del repositorio pasa el
    // alcance explícito.
    public DbSet<NotificacionInterna> NotificacionesInternas => Set<NotificacionInterna>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("gestion_avicola");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GestionAvicolaDbContext).Assembly);
        modelBuilder.Entity<Granja>().HasQueryFilter(g => g.EstaActivo && (_clienteIdActual == null || g.ClienteId == _clienteIdActual));
        modelBuilder.Entity<Galpon>().HasQueryFilter(g => g.EstaActivo && (_clienteIdActual == null || g.ClienteId == _clienteIdActual));
        modelBuilder.Entity<RegistroProduccion>().HasQueryFilter(r => r.EstaActivo && (_clienteIdActual == null || r.ClienteId == _clienteIdActual));
        modelBuilder.Entity<RegistroMortalidad>().HasQueryFilter(r => r.EstaActivo && (_clienteIdActual == null || r.ClienteId == _clienteIdActual));
        // Catálogo global (spec SP7): sin filtro de tenant, solo EstaActivo.
        modelBuilder.Entity<ProgramaVacunacion>().HasQueryFilter(p => p.EstaActivo);
        modelBuilder.Entity<ItemPlanVacunacion>().HasQueryFilter(i => i.EstaActivo);
        modelBuilder.Entity<TareaVacunacion>().HasQueryFilter(t =>
            t.EstaActivo && (_clienteIdActual == null || t.ClienteId == _clienteIdActual));
        // Catálogo global de precios (spec SP8): sin filtro de tenant, solo
        // EstaActivo; el acceso se autoriza con la política de CAISY.
        modelBuilder.Entity<NotificacionPreciosAlimentos>().HasQueryFilter(n => n.EstaActivo);
        // Pedidos compartidos del tenant (spec SP8): cualquier cuenta del
        // tenant los ve; las cuentas sin tenant (CAISY) consultan con
        // repositorios explícitos autorizados por su política.
        modelBuilder.Entity<PedidoAlimento>().HasQueryFilter(p =>
            p.EstaActivo && (_clienteIdActual == null || p.ClienteId == _clienteIdActual));
    }
}
