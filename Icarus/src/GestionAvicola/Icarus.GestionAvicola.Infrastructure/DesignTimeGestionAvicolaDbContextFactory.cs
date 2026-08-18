using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Icarus.GestionAvicola.Infrastructure;

public sealed class DesignTimeGestionAvicolaDbContextFactory : IDesignTimeDbContextFactory<GestionAvicolaDbContext>
{
    public GestionAvicolaDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<GestionAvicolaDbContext>().UseSqlServer("Server=localhost;Database=IcarusDiseno;TrustServerCertificate=True").Options;
        return new GestionAvicolaDbContext(opciones, new UsuarioActualDiseno());
    }

    private sealed class UsuarioActualDiseno : ICurrentUser
    {
        public bool EstaAutenticado => false;
        public Guid? UsuarioId => null;
        public string? Rol => null;
        public Guid? ClienteId => null;
        public Guid? TrabajadorId => null;
    }
}
