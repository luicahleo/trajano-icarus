using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Trajano.GestorCaisy.Servicios;

namespace Trajano.GestorCaisy.Models;

// Vista de detalle de la publicación de precio de huevo (spec SP9A) con las
// acciones habilitadas según el estado del agregado y la fecha de oficina:
// solo el borrador se edita o se descarta y solo una publicación futura se
// anula. Una publicación efectiva es inmutable: no ofrece editar, descartar
// ni anular, solo la guía a una corrección nueva.
public sealed record VistaDetallesHuevo(
    PublicacionPrecioHuevoDetalleApi Publicacion,
    bool PuedeEditarse,
    bool PuedeAnularse,
    bool PuedeDescartarse,
    bool EsPublicacionEfectiva)
{
    public static VistaDetallesHuevo Crear(PublicacionPrecioHuevoDetalleApi publicacion)
    {
        var hoy = FechasDeOficina.Hoy();
        return new VistaDetallesHuevo(
            publicacion,
            PuedeEditarse: publicacion.Estado == "Borrador",
            PuedeAnularse: publicacion.Estado == "Publicada"
                && publicacion.FechaVigencia > hoy,
            PuedeDescartarse: publicacion.Estado == "Borrador",
            EsPublicacionEfectiva: publicacion.Estado == "Publicada"
                && publicacion.FechaVigencia <= hoy);
    }
}

public sealed class FilaDetalleHuevoVista
{
    public string Tamano { get; set; } = string.Empty;

    [JsonRequired]
    public decimal PrecioAlProductor { get; set; }

    public decimal? PrecioActualDocumento { get; set; }
}

public sealed class FormularioBorradorHuevoVista
{
    [JsonRequired]
    public Guid PublicacionId { get; set; }

    [JsonRequired]
    public DateOnly FechaNotificacion { get; set; }

    // Una vigencia sí puede ser futura (glosario, regla transversal 2).
    [JsonRequired]
    public DateOnly FechaVigencia { get; set; }

    [JsonRequired]
    public decimal Servicio { get; set; }

    public List<FilaDetalleHuevoVista> Detalles { get; set; } = [];
}

public sealed record VistaCorregirHuevo(
    PublicacionPrecioHuevoDetalleApi Vigente,
    PublicacionPrecioHuevoDetalleApi Correctiva,
    VistaPreviaCorreccionHuevoApi Previa,
    FormularioCorregirHuevoVista Formulario);

public sealed class FormularioCorregirHuevoVista
{
    [JsonRequired]
    public Guid CorrectivaId { get; set; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}
