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
    string? Granja,
    DateOnly? Desde,
    DateOnly? Hasta,
    int? Numero,
    int NumeroPagina,
    int TamanoPagina,
    BandejaNotificacionesDespachoHuevoApi Notificaciones);

public sealed class FiltrosDespachosHuevoVista
{
    public string? Estado { get; set; }

    // Búsqueda por nombre de granja (contiene). No es un dato personal.
    public string? Granja { get; set; }

    public DateOnly? Desde { get; set; }

    public DateOnly? Hasta { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El folio debe ser un número positivo.")]
    public int? Numero { get; set; }

    [Range(1, int.MaxValue)]
    public int Pagina { get; set; } = 1;

    public int TamanoPagina { get; set; } = 20;
}

// Vista de detalle del despacho de huevo: la confirmación de recepción solo se
// habilita sobre un despacho en estado Despachado (spec SP9C).
public sealed record VistaDespachoHuevoDetalle(
    DespachoHuevoDetalleApi Despacho,
    bool PuedeConfirmarse);

// Esta bandeja es global de CAISY (ClienteId nulo en la consulta). El único
// tipo que se creaba con ese alcance era CreditoInsuficiente, y la corrección
// 2026-09-14 dejó de emitirlo: no se manda ninguna alerta por saldo. Las
// filas ya existentes en base se conservan y caen en la rama por defecto. El
// valor 1 del enum queda reservado y no se renumera. Lo usa tanto la vista
// como el endpoint JSON del sondeo global de la campanita (spec 2026-10-05).
public static class EtiquetasNotificacionDespachoHuevo
{
    public static string Texto(string tipo) => tipo;

    public static string Chip(string tipo) => tipo switch
    {
        _ => "borrador",
    };
}
