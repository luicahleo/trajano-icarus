using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Icarus.ControlAcceso.Infrastructure;

public sealed class DesignTimeControlAccesoDbContextFactory : IDesignTimeDbContextFactory<ControlAccesoDbContext>
{
    public ControlAccesoDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<ControlAccesoDbContext>()
            .UseSqlServer("Server=localhost;Database=IcarusDiseno;TrustServerCertificate=True")
            .Options;
        return new ControlAccesoDbContext(opciones, new UsuarioActualDiseno());
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
