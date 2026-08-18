using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

public sealed class Granja : AggregateRoot
{
    private Granja()
    {
    }

    public Granja(Guid clienteId, string nombre)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La granja debe pertenecer a un cliente.");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre de la granja es obligatorio.");

        ClienteId = clienteId;
        Nombre = nombre.Trim();
        EstaActivo = true;
    }

    public Granja(Guid id, Guid clienteId, string nombre)
        : this(clienteId, nombre) => Id = id;

    public Guid ClienteId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public bool EstaActivo { get; private set; }

    public void Renombrar(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre de la granja es obligatorio.");
        Nombre = nombre.Trim();
    }

    public void Desactivar() => EstaActivo = false;
}
