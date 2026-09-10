using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Cabecera global de una publicación de precio de huevo (spec SP9): sin
// tenant, versionada e inmutable tras publicarse. Rige desde FechaVigencia
// hasta que otra publicación posterior entra en vigor. Servicio es un único
// valor por publicación (spec SP9, confirmado contra el documento real de
// CAISY: la boleta de recepción usa el mismo valor de servicio para las seis
// filas).
public sealed class PublicacionPrecioHuevo : AggregateRoot
{
    private readonly List<DetallePrecioHuevo> _detalles = [];

    private PublicacionPrecioHuevo()
    {
    }

    public PublicacionPrecioHuevo(DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio)
        : this(fechaNotificacion, fechaVigencia, servicio, [])
    {
    }

    public PublicacionPrecioHuevo(
        DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio,
        IReadOnlyList<DatosDetallePrecioHuevo> detalles)
    {
        AsignarDatos(fechaNotificacion, fechaVigencia, servicio);
        ReemplazarDetalles(detalles);
    }

    // Para tests que necesitan ids fijos.
    public PublicacionPrecioHuevo(Guid id, DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio)
        : this(fechaNotificacion, fechaVigencia, servicio) => Id = id;

    public DateOnly FechaNotificacion { get; private set; }

    public DateOnly FechaVigencia { get; private set; }

    public EstadoPublicacionPrecioHuevo Estado { get; private set; } = EstadoPublicacionPrecioHuevo.Borrador;

    public bool EstaActivo { get; private set; } = true;

#pragma warning disable S1144 // Setter técnico para EF rowversion
    public byte[]? Version { get; private set; }
#pragma warning restore S1144

    public Guid? DocumentoOriginalId { get; private set; }

    // Publicación que reemplaza a esta por corrección (spec SP9D); distinto
    // del reemplazo normal por vencimiento, que no se enlaza. Solo se llena
    // vía CorregirVigente.
    public Guid? PublicacionCorrectivaId { get; private set; }

    public string? Motivo { get; private set; }

    public decimal Servicio { get; private set; }

    public IReadOnlyCollection<DetallePrecioHuevo> Detalles => _detalles.AsReadOnly();

    public void AsignarDocumentoOriginal(Guid documentoOriginalId)
    {
        AsegurarEditable("Solo un borrador acepta un documento original.");
        DocumentoOriginalId = documentoOriginalId;
    }

    public void ActualizarBorrador(
        DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio,
        IReadOnlyList<DatosDetallePrecioHuevo> detalles)
    {
        AsegurarEditable("Una publicación ya no es editable.");
        AsignarDatos(fechaNotificacion, fechaVigencia, servicio);
        ReemplazarDetalles(detalles);
    }

    public void ActualizarBorrador(IReadOnlyList<DatosDetallePrecioHuevo> detalles) =>
        ActualizarBorrador(FechaNotificacion, FechaVigencia, Servicio, detalles);

    public void Publicar()
    {
        AsegurarEditable("La publicación ya está publicada o anulada.");
        if (_detalles.Count == 0)
            throw new ReglaNegocioException("La publicación debe tener al menos un detalle de precio.");
        Estado = EstadoPublicacionPrecioHuevo.Publicada;
    }

    public void DescartarBorrador()
    {
        AsegurarEditable("Solo un borrador se puede descartar.");
        EstaActivo = false;
    }

    public void AnularFutura(DateOnly hoy)
    {
        if (Estado != EstadoPublicacionPrecioHuevo.Publicada)
            throw new ReglaNegocioException("Solo una publicación vigente o futura se puede anular.");
        if (FechaVigencia <= hoy)
            throw new ReglaNegocioException("Una publicación ya efectiva no se puede anular.");
        Estado = EstadoPublicacionPrecioHuevo.Anulada;
    }

    // Corrección de una publicación ya vigente (spec SP9D): a diferencia de
    // AnularFutura, no exige que la vigencia sea futura — el llamador (SP9D,
    // ComandosPreciosHuevo) ya validó que esta es la publicación vigente y
    // que la correctiva no tiene vigencia futura, algo que este método no
    // puede verificar por sí solo porque requiere cargar el otro agregado.
    public void CorregirVigente(Guid publicacionCorrectivaId, string motivo)
    {
        if (Estado != EstadoPublicacionPrecioHuevo.Publicada)
            throw new ReglaNegocioException("Solo una publicación vigente se puede corregir.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("La corrección debe declarar un motivo.");
        Estado = EstadoPublicacionPrecioHuevo.Corregida;
        PublicacionCorrectivaId = publicacionCorrectivaId;
        Motivo = motivo;
    }

    private void AsegurarEditable(string mensaje)
    {
        if (Estado != EstadoPublicacionPrecioHuevo.Borrador)
            throw new ReglaNegocioException(mensaje);
    }

    private void AsignarDatos(DateOnly fechaNotificacion, DateOnly fechaVigencia, decimal servicio)
    {
        if (servicio <= 0)
            throw new ReglaNegocioException("El servicio debe ser mayor que cero.");

        FechaNotificacion = fechaNotificacion;
        FechaVigencia = fechaVigencia;
        Servicio = servicio;
    }

    private void ReemplazarDetalles(IReadOnlyList<DatosDetallePrecioHuevo> detalles)
    {
        var repetidos = detalles.GroupBy(d => d.Tamano).FirstOrDefault(g => g.Count() > 1);
        if (repetidos is not null)
            throw new ReglaNegocioException("Cada tamaño solo puede tener un precio en la publicación.");

        _detalles.Clear();
        foreach (var datos in detalles)
            _detalles.Add(new DetallePrecioHuevo(datos.Tamano, datos.PrecioAlProductor, datos.PrecioActualDocumento));
    }
}

public sealed record DatosDetallePrecioHuevo(
    TamanoHuevo Tamano, decimal PrecioAlProductor, decimal? PrecioActualDocumento = null);
