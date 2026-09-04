using Icarus.Identity.Domain;
using Xunit;

namespace Icarus.UnitTests.Identity;

public class ReglasRolTests
{
    [Theory]
    [InlineData(Rol.Cliente)]
    [InlineData(Rol.Trabajador)]
    public void RolesDeEmpresaRequierenCliente(Rol rol)
    {
        Assert.True(ReglasRol.RequiereCliente(rol));
    }

    [Theory]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.GestorCaisy)]
    public void RolesDePlataformaNoRequierenCliente(Rol rol)
    {
        Assert.False(ReglasRol.RequiereCliente(rol));
    }
}
