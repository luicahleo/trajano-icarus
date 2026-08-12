namespace Icarus.BuildingBlocks.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent evento) => _domainEvents.Add(evento);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
