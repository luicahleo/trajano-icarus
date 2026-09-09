using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Línea del despacho por tamaño (spec SP9). PrecioProductorCongelado y
// PublicacionPrecioHuevoId quedan nulos en Borrador; Despachar los fija.
public sealed class DetalleDespachoHuevo : Entity
{
    // Unidad de despacho a CAISY (glosario de dominio): una amarra son 180
    // huevos. Constante pública porque RepositorioBalanceCreditoHuevo la
    // necesita en una proyección SQL que no puede depender de CantidadHuevos
    // (propiedad calculada, no traducible de forma fiable).
    public const int HuevosPorAmarra = 180;

    private DetalleDespachoHuevo()
    {
    }

    public DetalleDespachoHuevo(TamanoHuevo tamano, int cantidadAmarras, int unidadesSueltas)
    {
        if (cantidadAmarras < 0)
            throw new ReglaNegocioException("La cantidad de amarras no puede ser negativa.");
        if (unidadesSueltas is < 0 or > 179)
            throw new ReglaNegocioException("Las unidades sueltas deben estar entre 0 y 179.");
        if (cantidadAmarras == 0 && unidadesSueltas == 0)
            throw new ReglaNegocioException("Cada línea debe declarar una cantidad mayor que cero.");

        Tamano = tamano;
        CantidadAmarras = cantidadAmarras;
        UnidadesSueltas = unidadesSueltas;
    }

    public TamanoHuevo Tamano { get; private set; }

    public int CantidadAmarras { get; private set; }

    public int UnidadesSueltas { get; private set; }

    public int CantidadHuevos => CantidadAmarras * HuevosPorAmarra + UnidadesSueltas;

    public decimal? PrecioProductorCongelado { get; private set; }

    public Guid? PublicacionPrecioHuevoId { get; private set; }

    public decimal? Subtotal => PrecioProductorCongelado is { } precio
        ? CantidadHuevos * precio
        : null;

    internal void CongelarPrecio(decimal precioAlProductor, Guid publicacionPrecioHuevoId)
    {
        PrecioProductorCongelado = precioAlProductor;
        PublicacionPrecioHuevoId = publicacionPrecioHuevoId;
    }
}
