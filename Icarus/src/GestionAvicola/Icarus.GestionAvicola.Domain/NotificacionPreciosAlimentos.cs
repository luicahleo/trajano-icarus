using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Cabecera global de una publicación de precios de CAISY (spec SP8): sin
// tenant, versionada e inmutable tras publicarse. Rige desde VigenteDesde
// hasta que otra publicación posterior entra en vigor (la resolución de la
// vigente vive en Application/DB, no hay procesos programados). Una
// publicación ya efectiva no se edita ni se anula: la corrección es otra
// publicación o una anulación auditada antes de entrar en vigor.
public sealed class NotificacionPreciosAlimentos : AggregateRoot
{
    private readonly List<DetallePrecioAlimento> _detalles = [];

    private NotificacionPreciosAlimentos()
    {
    }

    public NotificacionPreciosAlimentos(
        DateOnly fechaDocumento, DateOnly vigenteDesde,
        decimal aporteCaisy, decimal fondo, decimal servicios)
        : this(fechaDocumento, vigenteDesde, aporteCaisy, fondo, servicios, [])
    {
    }

    public NotificacionPreciosAlimentos(
        DateOnly fechaDocumento, DateOnly vigenteDesde,
        decimal aporteCaisy, decimal fondo, decimal servicios,
        IReadOnlyList<DatosDetallePrecio> detalles)
    {
        AsignarDatos(fechaDocumento, vigenteDesde, aporteCaisy, fondo, servicios);
        ReemplazarDetalles(detalles);
    }

    // Para tests que necesitan ids fijos.
    public NotificacionPreciosAlimentos(Guid id,
        DateOnly fechaDocumento, DateOnly vigenteDesde,
        decimal aporteCaisy, decimal fondo, decimal servicios)
        : this(fechaDocumento, vigenteDesde, aporteCaisy, fondo, servicios) => Id = id;

    public DateOnly FechaDocumento { get; private set; }

    // Puede ser futura (glosario, regla 2): una vigencia no afirma que un
    // hecho ocurrió.
    public DateOnly VigenteDesde { get; private set; }

    public EstadoNotificacionPreciosAlimentos Estado { get; private set; }
        = EstadoNotificacionPreciosAlimentos.Borrador;

    // Referencia técnica al PDF original almacenado de forma privada; SQL solo
    // conserva la clave lógica.
    public Guid? DocumentoOriginalId { get; private set; }

    public decimal AporteCaisy { get; private set; }

    public decimal Fondo { get; private set; }

    public decimal Servicios { get; private set; }

    public IReadOnlyCollection<DetallePrecioAlimento> Detalles => _detalles.AsReadOnly();

    public void AsignarDocumentoOriginal(Guid documentoOriginalId)
    {
        AsegurarEditable("Solo un borrador acepta un documento original.");
        DocumentoOriginalId = documentoOriginalId;
    }

    // Reemplaza la propuesta completa (cabecera y detalles): el borrador es
    // editable hasta publicarse; el PDF solo produce una propuesta inicial.
    public void ActualizarBorrador(
        DateOnly fechaDocumento, DateOnly vigenteDesde,
        decimal aporteCaisy, decimal fondo, decimal servicios,
        IReadOnlyList<DatosDetallePrecio> detalles)
    {
        AsegurarEditable("Una publicación ya no es editable.");
        AsignarDatos(fechaDocumento, vigenteDesde, aporteCaisy, fondo, servicios);
        ReemplazarDetalles(detalles);
    }

    public void ActualizarBorrador(IReadOnlyList<DatosDetallePrecio> detalles) =>
        ActualizarBorrador(FechaDocumento, VigenteDesde, AporteCaisy, Fondo, Servicios, detalles);

    public void Publicar()
    {
        AsegurarEditable("La notificación ya está publicada o anulada.");
        if (_detalles.Count == 0)
            throw new ReglaNegocioException("La notificación debe tener al menos un detalle de precio.");
        Estado = EstadoNotificacionPreciosAlimentos.Publicada;
    }

    // Solo una publicación futura (aún no efectiva) se puede anular; una
    // efectiva queda sellada para siempre.
    public void AnularFutura(DateOnly hoy)
    {
        if (Estado != EstadoNotificacionPreciosAlimentos.Publicada)
            throw new ReglaNegocioException("Solo una publicación vigente o futura se puede anular.");
        if (VigenteDesde <= hoy)
            throw new ReglaNegocioException("Una publicación ya efectiva no se puede anular.");
        Estado = EstadoNotificacionPreciosAlimentos.Anulada;
    }

    private void AsegurarEditable(string mensaje)
    {
        if (Estado != EstadoNotificacionPreciosAlimentos.Borrador)
            throw new ReglaNegocioException(mensaje);
    }

    private void AsignarDatos(
        DateOnly fechaDocumento, DateOnly vigenteDesde,
        decimal aporteCaisy, decimal fondo, decimal servicios)
    {
        if (aporteCaisy <= 0 || fondo <= 0 || servicios <= 0)
            throw new ReglaNegocioException("Los aportes deben ser mayores que cero.");

        FechaDocumento = fechaDocumento;
        VigenteDesde = vigenteDesde;
        AporteCaisy = aporteCaisy;
        Fondo = fondo;
        Servicios = servicios;
    }

    private void ReemplazarDetalles(IReadOnlyList<DatosDetallePrecio> detalles)
    {
        var repetidos = detalles
            .GroupBy(d => (d.TipoAlimento, d.Presentacion))
            .FirstOrDefault(g => g.Count() > 1);
        if (repetidos is not null)
            throw new ReglaNegocioException(
                "Cada tipo y presentación solo puede tener un precio en la notificación.");

        _detalles.Clear();
        foreach (var datos in detalles)
            _detalles.Add(new DetallePrecioAlimento(
                datos.TipoAlimento, datos.Presentacion,
                datos.PrecioFinalPor40Kg, datos.EdadDesdeDias, datos.EdadHastaDias));
    }
}

public sealed record DatosDetallePrecio(
    TipoAlimento TipoAlimento, PresentacionAlimento Presentacion,
    decimal PrecioFinalPor40Kg, int? EdadDesdeDias, int? EdadHastaDias);
