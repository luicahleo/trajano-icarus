using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Models;

// Bandeja de pedidos entrantes (SP8B): filtros y paginación sobre la lista
// global y las notificaciones sin leer con su contador.
public sealed record BandejaPedidosVista(
    PaginaPedidosApi Pagina,
    string? Estado,
    string? Presentacion,
    BandejaNotificacionesApi Notificaciones);

public sealed class FiltrosPedidosVista
{
    public string? Estado { get; set; }

    public string? Presentacion { get; set; }

    [Range(1, int.MaxValue)]
    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 20;
}

// Vista de detalle con las decisiones habilitadas según el estado (spec SP8):
// solo un pedido solicitado se puede devolver, rechazar o aceptar; la entrega
// estimada solo se cambia sobre un pedido aceptado; el despacho con nota solo
// se registra sobre un pedido aceptado (SP8C).
public sealed record VistaPedidoDetalle(
    PedidoDetalleApi Pedido,
    bool PuedeProcesarse,
    bool PuedeActualizarEntrega,
    bool PuedeDespacharse);

public sealed class FormularioMotivoVista
{
    [JsonRequired]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}

public sealed class FormularioEntregaVista
{
    [JsonRequired]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "La fecha de entrega estimada es obligatoria.")]
    [JsonRequired]
    public DateOnly FechaEntregaEstimada { get; set; }
}

// Línea editable del formulario de despacho (SP8C): la cantidad solicitada se
// muestra como referencia y la entregada es el dato manual de la nota.
public sealed class LineaDespachoVista
{
    [JsonRequired]
    public string TipoAlimento { get; set; } = string.Empty;

    [JsonRequired]
    public int CantidadSolicitada { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "La cantidad entregada no puede ser negativa.")]
    public int CantidadEntregada { get; set; }
}

// Formulario del despacho (SP8C): nota manual, líneas y las imágenes de
// respaldo (páginas o reverso) que se suben tras registrar la entrega.
public sealed class FormularioDespachoVista
{
    [JsonRequired]
    public Guid Id { get; set; }

    [Required(ErrorMessage = "El número de nota es obligatorio.")]
    [StringLength(100, ErrorMessage = "El número de nota no puede superar los 100 caracteres.")]
    public string NumeroNota { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de la nota es obligatoria.")]
    [JsonRequired]
    public DateOnly FechaNota { get; set; }

    public decimal? TotalInformado { get; set; }

    [JsonRequired]
    public List<LineaDespachoVista> Lineas { get; set; } = [];

    public List<IFormFile> Archivos { get; set; } = [];
}
