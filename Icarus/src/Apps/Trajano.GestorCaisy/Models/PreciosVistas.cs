using System.Text.Json.Serialization;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Models;

// Vista de detalle con las acciones habilitadas según el estado del agregado
// y la fecha de negocio (spec SP8): solo el borrador se edita o se descarta y
// solo una publicación futura se anula. Una publicación efectiva es inmutable:
// no ofrece editar, descartar ni anular, solo la guía a una corrección nueva.
public sealed record VistaDetalles(
    NotificacionPreciosDetalleApi Notificacion,
    bool PuedeEditarse,
    bool PuedeAnularse,
    bool PuedeDescartarse,
    bool EsPublicacionEfectiva)
{
    public static VistaDetalles Crear(NotificacionPreciosDetalleApi notificacion)
    {
        var hoy = FechasDeOficina.Hoy();
        return new VistaDetalles(
            notificacion,
            PuedeEditarse: notificacion.Estado == "Borrador",
            PuedeAnularse: notificacion.Estado == "Publicada"
                && notificacion.VigenteDesde > hoy,
            PuedeDescartarse: notificacion.Estado == "Borrador",
            EsPublicacionEfectiva: notificacion.Estado == "Publicada"
                && notificacion.VigenteDesde <= hoy);
    }
}

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

// Historial de precios de alimento con la publicación vigente destacada
// (spec 2026-10-05): la vigente es la primera Publicada con vigencia ya
// iniciada en una lista ya ordenada por vigencia descendente — el mismo
// criterio que ObtenerVigenteAsync en el backend, sin llamar a la API de
// nuevo.
public sealed record VistaHistorialPrecios(
    IReadOnlyList<NotificacionPreciosResumenApi> Notificaciones, Guid? VigenteId)
{
    public static VistaHistorialPrecios Crear(IReadOnlyList<NotificacionPreciosResumenApi> notificaciones)
    {
        var hoy = FechasDeOficina.Hoy();
        var vigente = notificaciones.FirstOrDefault(
            n => n.Estado == "Publicada" && n.VigenteDesde <= hoy);
        return new VistaHistorialPrecios(notificaciones, vigente?.Id);
    }
}
