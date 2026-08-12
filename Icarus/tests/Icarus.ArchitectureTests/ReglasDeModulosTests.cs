using NetArchTest.Rules;
using Xunit;

namespace Icarus.ArchitectureTests;

public class ReglasDeModulosTests
{
    [Fact]
    public void ModulosNoSeReferencianEntreSi()
    {
        var clientesHaciaIdentity = Types
            .InAssembly(typeof(Clientes.Domain.Marcador).Assembly)
            .ShouldNot().HaveDependencyOn("Icarus.Identity").GetResult();
        var identityHaciaClientes = Types
            .InAssembly(typeof(Identity.Domain.Marcador).Assembly)
            .ShouldNot().HaveDependencyOn("Icarus.Clientes").GetResult();

        Assert.True(clientesHaciaIdentity.IsSuccessful,
            string.Join(", ", clientesHaciaIdentity.FailingTypeNames ?? []));
        Assert.True(identityHaciaClientes.IsSuccessful,
            string.Join(", ", identityHaciaClientes.FailingTypeNames ?? []));
    }
}
