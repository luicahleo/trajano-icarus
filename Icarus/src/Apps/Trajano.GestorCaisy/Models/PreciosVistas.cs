using System.Text.Json.Serialization;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Models;

// Vista de detalle con las acciones habilitadas según el estado del agregado:
// solo el borrador se edita y solo una publicación futura se anula.
public sealed record VistaDetalles(
    NotificacionPreciosDetalleApi Notificacion, bool PuedeEditarse, bool PuedeAnularse);

public sealed class FilaDetalleVista
{
    public string TipoAlimento { get; set; } = string.Empty;

    public string Presentacion { get; set; } = string.Empty;

    [JsonRequired]
    public decimal PrecioFinalPor40Kg { get; set; }

    public decimal? PrecioActualDocumento { get; set; }

    public int? EdadDesdeDias { get; set; }

    public int? EdadHastaDias { get; set; }
}

public sealed class FormularioBorradorVista
{
    [JsonRequired]
    public Guid NotificacionId { get; set; }

    [JsonRequired]
    public DateOnly FechaDocumento { get; set; }

    // Una vigencia sí puede ser futura (glosario, regla transversal 2).
    [JsonRequired]
    public DateOnly VigenteDesde { get; set; }

    [JsonRequired]
    public decimal AporteCaisy { get; set; }

    [JsonRequired]
    public decimal Fondo { get; set; }

    [JsonRequired]
    public decimal Servicios { get; set; }

    public List<FilaDetalleVista> Detalles { get; set; } = [];
}
