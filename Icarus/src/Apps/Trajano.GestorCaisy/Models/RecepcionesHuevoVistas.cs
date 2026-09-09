using System.ComponentModel.DataAnnotations;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Models;

// Bandeja de recepción de huevo (SP9C): filtro de estado y paginación sobre la
// lista global de despachos y las notificaciones con su contador. La página de
// la API solo trae items y total: el número y tamaño de página viajan con los
// filtros para la paginación de la vista.
public sealed record BandejaDespachosHuevoVista(
    PaginaDespachosHuevoApi Pagina,
    string? Estado,
    int NumeroPagina,
    int TamanoPagina,
    BandejaNotificacionesDespachoHuevoApi Notificaciones);

public sealed class FiltrosDespachosHuevoVista
{
    public string? Estado { get; set; }

    [Range(1, int.MaxValue)]
    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 20;
}

// Vista de detalle del despacho de huevo: la confirmación de recepción solo se
// habilita sobre un despacho en estado Despachado (spec SP9C).
public sealed record VistaDespachoHuevoDetalle(
    DespachoHuevoDetalleApi Despacho,
    bool PuedeConfirmarse);
