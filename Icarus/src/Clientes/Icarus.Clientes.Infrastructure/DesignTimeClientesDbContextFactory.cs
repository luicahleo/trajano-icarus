using Icarus.BuildingBlocks.Application;
using Icarus.Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Icarus.Clientes.Infrastructure;

// Permite correr dotnet ef sin levantar el Host (patrón de Caserito). La
// cadena es ficticia: solo se usa para generar migraciones, nunca conecta.
public sealed class DesignTimeClientesDbContextFactory : IDesignTimeDbContextFactory<ClientesDbContext>
{
    public ClientesDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<ClientesDbContext>()
            .UseSqlServer("Server=localhost;Database=IcarusDiseno;TrustServerCertificate=True")
            .Options;
        return new ClientesDbContext(opciones, new UsuarioActualDiseno());
    }

    // Sin usuario en tiempo de diseño: ClienteId nulo deja los filtros de
    // tenant abiertos, igual que un rol de plataforma.
    private sealed class UsuarioActualDiseno : ICurrentUser
    {
        public bool EstaAutenticado => false;

        public Guid? UsuarioId => null;

        public string? Rol => null;

        public Guid? ClienteId => null;

        public Guid? TrabajadorId => null;
    }
}
