using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Una fila de precio dentro de una Notificación de Precios (spec SP8). El
// precio canónico es el precio final por 40 kg: incluye aporte CAISY, fondo y
// servicios; es el único valor que se congela en los pedidos.
public sealed class DetallePrecioAlimento : Entity
{
    private DetallePrecioAlimento()
    {
    }

    public DetallePrecioAlimento(
        TipoAlimento tipoAlimento, PresentacionAlimento presentacion,
        decimal precioFinalPor40Kg, int? edadDesdeDias, int? edadHastaDias)
    {
        if (precioFinalPor40Kg <= 0)
            throw new ReglaNegocioException("El precio final por 40 kg debe ser mayor que cero.");
        if (edadDesdeDias is <= 0 || edadHastaDias is <= 0
            || (edadDesdeDias.HasValue && edadHastaDias.HasValue && edadDesdeDias > edadHastaDias))
            throw new ReglaNegocioException("El rango de edad debe ser coherente.");

        TipoAlimento = tipoAlimento;
        Presentacion = presentacion;
        PrecioFinalPor40Kg = precioFinalPor40Kg;
        EdadDesdeDias = edadDesdeDias;
        EdadHastaDias = edadHastaDias;
    }

    public TipoAlimento TipoAlimento { get; private set; }

    public PresentacionAlimento Presentacion { get; private set; }

    public decimal PrecioFinalPor40Kg { get; private set; }

    // Las franjas de edad del PDF son informativas: no bloquean pedidos.
    public int? EdadDesdeDias { get; private set; }

    public int? EdadHastaDias { get; private set; }
}
