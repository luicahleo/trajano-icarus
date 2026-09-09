using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Una fila de precio dentro de una publicación (spec SP9). PrecioAlProductor
// es el monto que se congela al despachar; el Servicio vive en la cabecera
// (Publicacion.Servicio), no por fila.
public sealed class DetallePrecioHuevo : Entity
{
    private DetallePrecioHuevo()
    {
    }

    public DetallePrecioHuevo(
        TamanoHuevo tamano, decimal precioAlProductor, decimal? precioActualDocumento = null)
    {
        if (precioAlProductor <= 0)
            throw new ReglaNegocioException("El precio al productor debe ser mayor que cero.");

        Tamano = tamano;
        PrecioAlProductor = precioAlProductor;
        PrecioActualDocumento = precioActualDocumento;
    }

    public TamanoHuevo Tamano { get; private set; }

    public decimal PrecioAlProductor { get; private set; }

    // Columna «Precio Actual» del documento (spec SP9): control de
    // publicación contra la vigente; nunca sustituye a PrecioAlProductor.
    public decimal? PrecioActualDocumento { get; private set; }
}
