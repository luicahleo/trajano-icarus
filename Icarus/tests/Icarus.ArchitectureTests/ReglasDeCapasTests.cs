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
                typeof(Identity.Domain.Marcador).Assembly,
                typeof(Clientes.Domain.Marcador).Assembly,
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
    public void AplicacionNoDependeDeInfraestructura()
    {
        var resultado = Types
            .InAssemblies(new[]
            {
                typeof(BuildingBlocks.Application.ICurrentUser).Assembly,
                typeof(Identity.Application.Marcador).Assembly,
                typeof(Clientes.Application.Marcador).Assembly,
            })
            .ShouldNot()
            .HaveDependencyOnAny("Icarus.Identity.Infrastructure", "Icarus.Clientes.Infrastructure")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
