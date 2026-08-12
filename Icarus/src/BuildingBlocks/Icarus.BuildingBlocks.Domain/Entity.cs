namespace Icarus.BuildingBlocks.Domain;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj)
    {
        if (obj is not Entity otro || otro.GetType() != GetType())
            return false;
        return Id == otro.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();
}
