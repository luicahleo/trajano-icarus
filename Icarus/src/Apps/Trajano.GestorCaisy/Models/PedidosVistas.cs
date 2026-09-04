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
// estimada solo se cambia sobre un pedido aceptado.
public sealed record VistaPedidoDetalle(
    PedidoDetalleApi Pedido,
    bool PuedeProcesarse,
    bool PuedeActualizarEntrega);

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
