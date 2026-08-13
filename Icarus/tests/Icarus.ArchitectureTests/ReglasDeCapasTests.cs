using NetArchTest.Rules;
using Xunit;

namespace Icarus.ArchitectureTests;

public class ReglasDeCapasTests
{
    [Fact]
    public void DominioNoDependeDeLibrerias()
    {
        var resultado = Types
            .InAssemblies(new[]
            {
                typeof(BuildingBlocks.Domain.Entity).Assembly,
                typeof(Identity.Domain.Rol).Assembly,
                typeof(Clientes.Domain.Cliente).Assembly,
            })
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions",
                "Serilog",
                "MediatR",
                "FluentValidation")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void InfraestructuraNoDependeDelHost()
    {
        var resultado = Types
            .InAssembly(typeof(Identity.Infrastructure.Persistencia.IdentityDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Icarus.Host")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }

    [Fact]
    public void AplicacionNoDependeDeInfraestructura()
    {
        var resultado = Types
            .InAssemblies(new[]
            {
                typeof(BuildingBlocks.Application.ICurrentUser).Assembly,
                typeof(Identity.Application.Sesiones.IniciarSesionCommand).Assembly,
                typeof(Clientes.Application.Marcador).Assembly,
            })
            .ShouldNot()
            .HaveDependencyOnAny("Icarus.Identity.Infrastructure", "Icarus.Clientes.Infrastructure")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
