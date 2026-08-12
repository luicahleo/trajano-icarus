using Icarus.BuildingBlocks.Domain;
using Xunit;

namespace Icarus.UnitTests.BuildingBlocks;

public class AggregateRootTests
{
    private sealed record EventoFalso(DateTime OcurridoEn) : IDomainEvent;

    private sealed class AgregadoFalso : AggregateRoot
    {
        public void Disparar() => AddDomainEvent(new EventoFalso(DateTime.UtcNow));
    }

    [Fact]
    public void AgregarEventoLoExponeEnDomainEvents()
    {
        var agregado = new AgregadoFalso();
        agregado.Disparar();
        Assert.Single(agregado.DomainEvents);
    }

    [Fact]
    public void ClearDomainEventsVaciaLaColeccion()
    {
        var agregado = new AgregadoFalso();
        agregado.Disparar();
        agregado.ClearDomainEvents();
        Assert.Empty(agregado.DomainEvents);
    }
}
