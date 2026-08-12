using Icarus.BuildingBlocks.Domain;
using Xunit;

namespace Icarus.UnitTests.BuildingBlocks;

public class EntityTests
{
    private sealed class EntidadFalsa : Entity
    {
        public EntidadFalsa() { }
        public EntidadFalsa(Guid id) => Id = id;
    }

    private sealed class OtraEntidad : Entity
    {
        public OtraEntidad(Guid id) => Id = id;
    }

    [Fact]
    public void DosEntidadesConMismoIdYTipoSonIguales()
    {
        var id = Guid.NewGuid();
        Assert.Equal(new EntidadFalsa(id), new EntidadFalsa(id));
    }

    [Fact]
    public void EntidadesConDistintoIdNoSonIguales()
    {
        Assert.NotEqual(new EntidadFalsa(), new EntidadFalsa());
    }

    [Fact]
    public void EntidadesDeDistintoTipoNoSonIgualesAunqueCompartanId()
    {
        var id = Guid.NewGuid();
        Entity a = new EntidadFalsa(id);
        Entity b = new OtraEntidad(id);
        Assert.NotEqual(a, b);
    }
}
