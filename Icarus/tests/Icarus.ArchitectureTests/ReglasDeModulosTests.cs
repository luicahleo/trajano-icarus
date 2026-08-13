using NetArchTest.Rules;
using Xunit;

namespace Icarus.ArchitectureTests;

public class ReglasDeModulosTests
{
    [Fact]
    public void ModulosNoSeReferencianEntreSi()
    {
        var clientesHaciaIdentity = Types
            .InAssemblies(new[]
            {
                typeof(Clientes.Domain.Cliente).Assembly,
                typeof(Clientes.Application.Clientes.CrearClienteCommand).Assembly,
                typeof(Clientes.Infrastructure.Persistencia.ClientesDbContext).Assembly,
            })
            .ShouldNot().HaveDependencyOn("Icarus.Identity").GetResult();
        var identityHaciaClientes = Types
            .InAssemblies(new[]
            {
                typeof(Identity.Domain.Rol).Assembly,
                typeof(Identity.Application.Sesiones.IniciarSesionCommand).Assembly,
                typeof(Identity.Infrastructure.Persistencia.IdentityDbContext).Assembly,
            })
            .ShouldNot().HaveDependencyOn("Icarus.Clientes").GetResult();

        Assert.True(clientesHaciaIdentity.IsSuccessful,
            string.Join(", ", clientesHaciaIdentity.FailingTypeNames ?? []));
        Assert.True(identityHaciaClientes.IsSuccessful,
            string.Join(", ", identityHaciaClientes.FailingTypeNames ?? []));
    }
}
