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

    [Fact]
    public void GestionAvicolaNoSeReferenciaConOtrosModulos()
    {
        var avicolaHaciaOtros = Types
            .InAssemblies(new[]
            {
                typeof(GestionAvicola.Domain.Granja).Assembly,
                typeof(GestionAvicola.Application.Granjas.CrearGranjaCommand).Assembly,
                typeof(GestionAvicola.Infrastructure.Persistencia.GestionAvicolaDbContext).Assembly,
            })
            .ShouldNot().HaveDependencyOnAny("Icarus.Clientes", "Icarus.Identity").GetResult();
        var otrosHaciaAvicola = Types
            .InAssemblies(new[]
            {
                typeof(Clientes.Domain.Cliente).Assembly,
                typeof(Clientes.Application.Clientes.CrearClienteCommand).Assembly,
                typeof(Clientes.Infrastructure.Persistencia.ClientesDbContext).Assembly,
                typeof(Identity.Domain.Rol).Assembly,
                typeof(Identity.Application.Sesiones.IniciarSesionCommand).Assembly,
                typeof(Identity.Infrastructure.Persistencia.IdentityDbContext).Assembly,
            })
            .ShouldNot().HaveDependencyOn("Icarus.GestionAvicola").GetResult();

        Assert.True(avicolaHaciaOtros.IsSuccessful,
            string.Join(", ", avicolaHaciaOtros.FailingTypeNames ?? []));
        Assert.True(otrosHaciaAvicola.IsSuccessful,
            string.Join(", ", otrosHaciaAvicola.FailingTypeNames ?? []));
    }

    // Trajano-GestorCaisy es un desplegable independiente que consume la API de
    // Trajano-Icarus por HTTP (spec SP8): sin referencia a proyectos del
    // backend, sin DbContext ni acceso SQL directo.
    [Fact]
    public void GestorCaisyNoDependeDelBackend()
    {
        var resultado = Types
            .InAssembly(typeof(Program).Assembly)
            .ShouldNot().HaveDependencyOnAny(
                "Icarus", "Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
