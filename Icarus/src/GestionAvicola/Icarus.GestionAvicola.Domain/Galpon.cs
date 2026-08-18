using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

public sealed class Galpon : AggregateRoot
{
    private Galpon()
    {
    }

    public Galpon(
        Guid granjaId, Guid clienteId, string numero, int capacidadMaxima, int gallinasActuales,
        DateOnly fechaNacimientoLote, string? descripcion)
    {
        if (granjaId == Guid.Empty)
            throw new ReglaNegocioException("El galpón debe pertenecer a una granja.");
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("El galpón debe pertenecer a un cliente.");
        if (string.IsNullOrWhiteSpace(numero))
            throw new ReglaNegocioException("El número del galpón es obligatorio.");
        if (capacidadMaxima <= 0)
            throw new ReglaNegocioException("La capacidad máxima debe ser mayor que cero.");
        if (fechaNacimientoLote > Hoy())
            throw new ReglaNegocioException("La fecha de nacimiento del lote no puede ser futura.");

        GranjaId = granjaId;
        ClienteId = clienteId;
        Numero = numero.Trim();
        CapacidadMaxima = capacidadMaxima;
        FechaNacimientoLote = fechaNacimientoLote;
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        EstaActivo = true;
        AjustarInventarioGallinas(gallinasActuales);
    }

    public Galpon(
        Guid id, Guid granjaId, Guid clienteId, string numero, int capacidadMaxima,
        int gallinasActuales, DateOnly fechaNacimientoLote, string? descripcion)
        : this(granjaId, clienteId, numero, capacidadMaxima, gallinasActuales, fechaNacimientoLote, descripcion)
        => Id = id;

    public Guid GranjaId { get; private set; }

    public Guid ClienteId { get; private set; }

    public string Numero { get; private set; } = string.Empty;

    public int CapacidadMaxima { get; private set; }

    public int GallinasActuales { get; private set; }

    public DateOnly FechaNacimientoLote { get; private set; }

    public string? Descripcion { get; private set; }

    public bool EstaActivo { get; private set; }

    public void ActualizarDatos(string numero, string? descripcion, int capacidadMaxima)
    {
        if (string.IsNullOrWhiteSpace(numero))
            throw new ReglaNegocioException("El número del galpón es obligatorio.");
        if (capacidadMaxima <= 0)
            throw new ReglaNegocioException("La capacidad máxima debe ser mayor que cero.");
        if (capacidadMaxima < GallinasActuales)
            throw new ReglaNegocioException(
                "La capacidad máxima no puede ser menor que las gallinas actuales.");

        Numero = numero.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        CapacidadMaxima = capacidadMaxima;
    }

    public void AjustarInventarioGallinas(int nuevoTotal)
    {
        if (nuevoTotal < 0)
            throw new ReglaNegocioException("Las gallinas actuales no pueden ser negativas.");
        if (nuevoTotal > CapacidadMaxima)
            throw new ReglaNegocioException(
                "Las gallinas actuales no pueden superar la capacidad máxima.");
        GallinasActuales = nuevoTotal;
    }

    public void Desactivar() => EstaActivo = false;

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
