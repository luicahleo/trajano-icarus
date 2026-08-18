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
                typeof(GestionAvicola.Domain.Granja).Assembly,
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
            .InAssemblies(new[]
            {
                typeof(Identity.Infrastructure.Persistencia.IdentityDbContext).Assembly,
                typeof(Clientes.Infrastructure.Persistencia.ClientesDbContext).Assembly,
                typeof(GestionAvicola.Infrastructure.Persistencia.GestionAvicolaDbContext).Assembly,
            })
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
                typeof(Clientes.Application.Clientes.CrearClienteCommand).Assembly,
                typeof(GestionAvicola.Application.Granjas.CrearGranjaCommand).Assembly,
            })
            .ShouldNot()
            .HaveDependencyOnAny("Icarus.Identity.Infrastructure", "Icarus.Clientes.Infrastructure", "Icarus.GestionAvicola.Infrastructure")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
