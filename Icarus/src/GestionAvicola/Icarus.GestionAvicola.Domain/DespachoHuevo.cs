using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Agregado raíz del despacho de huevo (spec SP9). Es un registro del tenant
// (Cliente o Trabajador con la funcionalidad DespachoHuevo); CreadoPor es
// solo auditoría técnica. SP9B cubre Borrador y Despachado; SP9C agrega
// ConfirmarRecepcion.
public sealed class DespachoHuevo : AggregateRoot
{
    private readonly List<DetalleDespachoHuevo> _detalles = [];
    private readonly List<TransicionDespachoHuevo> _historial = [];
    private DocumentoDespachoHuevo? _documentoNota;

    private DespachoHuevo()
    {
    }

    public DespachoHuevo(
        Guid clienteId, Guid granjaId, Guid creadoPor,
        IReadOnlyList<DatosDetalleDespachoHuevo> detalles)
    {
        ClienteId = clienteId;
        GranjaId = granjaId;
        CreadoPor = creadoPor;
        ReemplazarDetalles(detalles);
    }

    // Para tests que necesitan ids fijos.
    public DespachoHuevo(Guid id, Guid clienteId, Guid granjaId, Guid creadoPor,
        IReadOnlyList<DatosDetalleDespachoHuevo> detalles)
        : this(clienteId, granjaId, creadoPor, detalles) => Id = id;

    public Guid ClienteId { get; private set; }

    public Guid GranjaId { get; private set; }

    public Guid CreadoPor { get; private set; }

    public EstadoDespachoHuevo Estado { get; private set; } = EstadoDespachoHuevo.Borrador;

    public bool EstaActivo { get; private set; } = true;

#pragma warning disable S1144 // Setter técnico para EF rowversion
    public byte[]? Version { get; private set; }
#pragma warning restore S1144

    public DateOnly? FechaDespacho { get; private set; }

    public DateOnly? FechaRecepcion { get; private set; }

    public DocumentoDespachoHuevo? DocumentoNota => _documentoNota;

    public IReadOnlyCollection<DetalleDespachoHuevo> Detalles => _detalles.AsReadOnly();

    public IReadOnlyList<TransicionDespachoHuevo> Historial => _historial.AsReadOnly();

    public int TotalAmarras => _detalles.Sum(d => d.CantidadAmarras);

    public int TotalHuevos => _detalles.Sum(d => d.CantidadHuevos);

    // Null mientras el despacho no se haya enviado (el borrador puede
    // construirse sin precios).
    public decimal? TotalBs =>
        _detalles.Count > 0 && _detalles.All(d => d.Subtotal is not null)
            ? _detalles.Sum(d => d.Subtotal!.Value)
            : null;

    // Solo el borrador se edita: reemplaza todas las líneas.
    public void EditarDetalles(IReadOnlyList<DatosDetalleDespachoHuevo> detalles)
    {
        AsegurarEstado(EstadoDespachoHuevo.Borrador, "Solo un borrador se puede editar.");
        ReemplazarDetalles(detalles);
    }

    // Borrado lógico (glosario): solo el borrador se desactiva.
    public void Desactivar()
    {
        AsegurarEstado(EstadoDespachoHuevo.Borrador, "Solo un borrador se puede desactivar.");
        EstaActivo = false;
    }

    // Envío a CAISY (spec SP9): el servidor fija la fecha de negocio, congela
    // el precio unitario vigente (productor + servicio) de cada línea con
    // cantidad y exige la foto de la nota de entrega en la misma operación.
    // Si falta precio para una línea, el envío falla completo y el borrador
    // queda intacto.
    public void Despachar(
        DateOnly fechaDespacho, Guid actorId,
        IReadOnlyList<DatosPrecioDespachoHuevo> precios, DatosDocumentoNota documento)
    {
        AsegurarEstado(EstadoDespachoHuevo.Borrador, "Solo un despacho en borrador se puede enviar.");
        var congelados = CongelarPrecios(precios);
        _documentoNota = new DocumentoDespachoHuevo(
            documento.ClaveOriginal, documento.ClaveVista, documento.Mime,
            documento.TamanoBytes, documento.TamanoVistaBytes,
            documento.HashSha256, documento.NombreSeguro);
        foreach (var linea in _detalles)
            linea.CongelarPrecio(congelados[linea.Tamano].PrecioUnitario, congelados[linea.Tamano].PublicacionPrecioHuevoId);
        Estado = EstadoDespachoHuevo.Despachado;
        FechaDespacho = fechaDespacho;
        RegistrarTransicion(EstadoDespachoHuevo.Borrador, EstadoDespachoHuevo.Despachado, actorId);
    }

    // Confirmación de CAISY (spec SP9): sin recuento por línea — el
    // transportista solo verifica visualmente, no hay reconteo formal en el
    // sistema. El monto ya quedó congelado al despachar; esta operación solo
    // cierra el estado y fija la fecha de recepción (fecha de negocio del
    // servidor, la resuelve el llamador).
    public void ConfirmarRecepcion(DateOnly fechaRecepcion, Guid actorId)
    {
        AsegurarEstado(EstadoDespachoHuevo.Despachado, "Solo un despacho despachado se puede recibir.");
        Estado = EstadoDespachoHuevo.Recibido;
        FechaRecepcion = fechaRecepcion;
        RegistrarTransicion(EstadoDespachoHuevo.Despachado, EstadoDespachoHuevo.Recibido, actorId);
    }

    private void AsegurarEstado(EstadoDespachoHuevo esperado, string mensaje)
    {
        if (Estado != esperado)
            throw new ReglaNegocioException(mensaje);
    }

    private void ReemplazarDetalles(IReadOnlyList<DatosDetalleDespachoHuevo> detalles)
    {
        if (detalles.Count == 0)
            throw new ReglaNegocioException("El despacho debe tener al menos una línea.");
        if (detalles.GroupBy(d => d.Tamano).Any(g => g.Count() > 1))
            throw new ReglaNegocioException("Cada tamaño solo puede aparecer una vez en el despacho.");

        _detalles.Clear();
        foreach (var datos in detalles)
            _detalles.Add(new DetalleDespachoHuevo(datos.Tamano, datos.CantidadAmarras, datos.UnidadesSueltas));
    }

    // Resuelve y valida el precio de cada línea antes de tocar el estado
    // (spec SP9, mismo patrón que PedidoAlimento.CongelarPrecios): un envío
    // sin precio vigente completo no deja rastro.
    private Dictionary<TamanoHuevo, DatosPrecioDespachoHuevo> CongelarPrecios(
        IReadOnlyList<DatosPrecioDespachoHuevo> precios)
    {
        var indice = precios.ToDictionary(p => p.Tamano);
        foreach (var linea in _detalles)
            if (!indice.ContainsKey(linea.Tamano))
                throw new ReglaNegocioException("Falta precio vigente para una línea del despacho.");
        return indice;
    }

    private void RegistrarTransicion(EstadoDespachoHuevo origen, EstadoDespachoHuevo destino, Guid actorId) =>
        _historial.Add(new TransicionDespachoHuevo(origen, destino, actorId));
}

public sealed record DatosDetalleDespachoHuevo(TamanoHuevo Tamano, int CantidadAmarras, int UnidadesSueltas);

public sealed record DatosPrecioDespachoHuevo(
    TamanoHuevo Tamano, decimal PrecioUnitario, Guid PublicacionPrecioHuevoId);
