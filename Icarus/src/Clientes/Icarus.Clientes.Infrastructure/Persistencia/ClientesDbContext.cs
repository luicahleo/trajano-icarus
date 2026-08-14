using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Clientes.Infrastructure.Persistencia;

// Filtros globales de EF Core (spec): soft delete (EstaActivo, regla
// transversal del glosario) y tenant (ClienteId del claim, vía ICurrentUser).
// El rol de plataforma (Administrador) lleva ClienteId nulo y ve todos los
// tenants.
public sealed class ClientesDbContext : DbContext, IUnitOfWork
{
    private readonly Guid? _clienteIdActual;

    public ClientesDbContext(DbContextOptions<ClientesDbContext> opciones, ICurrentUser usuarioActual)
        : base(opciones) => _clienteIdActual = usuarioActual.ClienteId;

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Trabajador> Trabajadores => Set<Trabajador>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("clientes");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClientesDbContext).Assembly);

        // Sin ".Value" sobre el nullable: EF evalúa los valores capturados al
        // extraer los parámetros del filtro, y ".Value" lanza
        // InvalidOperationException cuando ClienteId es null (roles de
        // plataforma). Verificado en el scratch con tests de integración.
        modelBuilder.Entity<Cliente>().HasQueryFilter(c =>
            c.EstaActivo && (_clienteIdActual == null || c.Id == _clienteIdActual));
        modelBuilder.Entity<Trabajador>().HasQueryFilter(t =>
            t.EstaActivo && (_clienteIdActual == null || t.ClienteId == _clienteIdActual));
    }
}
