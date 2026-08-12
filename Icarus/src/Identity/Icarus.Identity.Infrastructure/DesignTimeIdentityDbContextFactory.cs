using Icarus.Identity.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Icarus.Identity.Infrastructure;

// Patrón de Caserito: permite correr `dotnet ef` sin levantar el Host; la
// cadena es ficticia, solo se usa para generar la migración, nunca para
// conectar.
public sealed class DesignTimeIdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=localhost;Database=IcarusDiseno;TrustServerCertificate=True")
            .Options;
        return new IdentityDbContext(opciones);
    }
}
