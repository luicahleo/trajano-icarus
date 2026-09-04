using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Agregado raíz del Pedido de alimento (spec SP8). Es una solicitud compartida
// del tenant: CreadoPor es solo un id técnico de auditoría y no otorga
// propiedad exclusiva. La máquina de estados se implementa con métodos
// explícitos (sin setters genéricos ni Goto); cada salida comprueba sus
// guardas y registra la transición. SP8B cubre Borrador, Solicitado, Rechazado
// y Aceptado; despacho y recepción llegan en SP8C con métodos nuevos.
public sealed class PedidoAlimento : AggregateRoot
{
    private readonly List<DetallePedidoAlimento> _detalles = [];
    private readonly List<TransicionPedidoAlimento> _historial = [];
    private EntregaPedidoAlimento? _entrega;

    private PedidoAlimento()
    {
    }

    public PedidoAlimento(
        Guid clienteId, Guid creadoPor, IReadOnlyList<DatosDetallePedido> detalles)
    {
        ClienteId = clienteId;
        CreadoPor = creadoPor;
        ReemplazarDetalles(detalles);
    }

    // Para tests que necesitan ids fijos.
    public PedidoAlimento(Guid id, Guid clienteId, Guid creadoPor,
        IReadOnlyList<DatosDetallePedido> detalles)
        : this(clienteId, creadoPor, detalles) => Id = id;

    public Guid ClienteId { get; private set; }

    public Guid CreadoPor { get; private set; }

    public EstadoPedidoAlimento Estado { get; private set; }
        = EstadoPedidoAlimento.Borrador;

    // Borrado lógico transversal del glosario: solo un borrador se desactiva.
    public bool EstaActivo { get; private set; } = true;

    // Concurrency token técnico (rowversion): toda transición es un comando
    // mutable y no debe perderse ante escrituras concurrentes (spec SP8). El
    // setter privado es requerido por el rowversion de EF; no se usa en el
    // dominio.
#pragma warning disable S1144 // Setter técnico para EF rowversion
    public byte[]? Version { get; private set; }
#pragma warning restore S1144

    // Fecha de negocio Bolivia fijada por el servidor al enviar.
    public DateOnly? FechaPedido { get; private set; }

    public DateOnly? FechaEntregaEstimada { get; private set; }

    public IReadOnlyCollection<DetallePedidoAlimento> Detalles => _detalles.AsReadOnly();

    // Lista ordenada por ocurrencia: el historial se lee en orden cronológico.
    public IReadOnlyList<TransicionPedidoAlimento> Historial => _historial.AsReadOnly();

    // Entrega única registrada por CAISY (spec SP8C); null hasta el despacho.
    public EntregaPedidoAlimento? Entrega => _entrega;

    // Suma de subtotales congelados; null mientras el pedido no se haya
    // enviado (el borrador puede construirse sin precios).
    public decimal? TotalSolicitado =>
        _detalles.Count > 0 && _detalles.All(d => d.SubtotalSolicitado is not null)
            ? _detalles.Sum(d => d.SubtotalSolicitado!.Value)
            : null;

    // Cálculo canónico del despacho (spec SP8C): equivalentes realmente
    // entregados por línea × precio congelado al envío. El total informado de
    // la nota se conserva para contraste y nunca lo sustituye.
    public decimal? TotalDespachado =>
        _entrega is null
            ? null
            : _detalles
                .Join(_entrega.Lineas, d => d.TipoAlimento, e => e.TipoAlimento,
                    (d, e) => e.Equivalentes40Kg * (d.PrecioFinalPor40Kg ?? 0m))
                .Sum();

    // Solo el borrador se edita: reemplaza todas las líneas con la misma
    // validación de la creación (una presentación, tipos únicos y compatibles).
    public void EditarDetalles(IReadOnlyList<DatosDetallePedido> detalles)
    {
        AsegurarEstado(EstadoPedidoAlimento.Borrador, "Solo un borrador se puede editar.");
        ReemplazarDetalles(detalles);
    }

    // Borrado lógico (glosario): solo el borrador se desactiva; los demás
    // estados conservan el pedido activo para el historial y el balance.
    public void Desactivar()
    {
        AsegurarEstado(EstadoPedidoAlimento.Borrador, "Solo un borrador se puede desactivar.");
        EstaActivo = false;
    }

    // Envío a CAISY (spec SP8): el servidor fija la fecha de negocio, congela
    // el precio vigente de todas las líneas dentro de la misma transacción y
    // registra la transición. Si falta precio para una línea, falla completo y
    // el borrador queda intacto.
    public void EnviarACaisy(
        DateOnly fechaPedido, Guid actorId, IReadOnlyList<DatosPrecioEnvio> precios)
    {
        AsegurarEstado(EstadoPedidoAlimento.Borrador, "Solo un pedido en borrador se puede enviar.");
        AsegurarCantidadesGranel();
        var congelados = CongelarPrecios(precios);
        Estado = EstadoPedidoAlimento.Solicitado;
        FechaPedido = fechaPedido;
        foreach (var linea in _detalles)
            linea.CongelarPrecio(
                congelados[linea.TipoAlimento].PrecioFinalPor40Kg,
                congelados[linea.TipoAlimento].NotificacionPreciosAlimentosId);
        RegistrarTransicion(EstadoPedidoAlimento.Borrador, EstadoPedidoAlimento.Solicitado, actorId);
    }

    // Devolución para corrección (spec SP8): decisión no terminal, exige
    // motivo, reutiliza el mismo pedido y conserva historial y congelados del
    // último envío hasta el reenvío.
    public void DevolverParaCorreccion(string motivo, Guid actorId)
    {
        AsegurarEstado(EstadoPedidoAlimento.Solicitado, "Solo un pedido solicitado se puede devolver.");
        AsegurarMotivo(motivo);
        Estado = EstadoPedidoAlimento.Borrador;
        RegistrarTransicion(
            EstadoPedidoAlimento.Solicitado, EstadoPedidoAlimento.Borrador, actorId, motivo);
    }

    // Rechazo (spec SP8): decisión terminal, exige motivo.
    public void Rechazar(string motivo, Guid actorId)
    {
        AsegurarEstado(EstadoPedidoAlimento.Solicitado, "Solo un pedido solicitado se puede rechazar.");
        AsegurarMotivo(motivo);
        Estado = EstadoPedidoAlimento.Rechazado;
        RegistrarTransicion(EstadoPedidoAlimento.Solicitado, EstadoPedidoAlimento.Rechazado, actorId, motivo);
    }

    // Aceptación (spec SP8): exige fecha de entrega estimada desde hoy
    // (fecha de negocio que pasa el llamador).
    public void Aceptar(DateOnly fechaEntregaEstimada, DateOnly hoy, Guid actorId)
    {
        AsegurarEstado(EstadoPedidoAlimento.Solicitado, "Solo un pedido solicitado se puede aceptar.");
        AsegurarFechaEntrega(fechaEntregaEstimada, hoy);
        Estado = EstadoPedidoAlimento.Aceptado;
        FechaEntregaEstimada = fechaEntregaEstimada;
        RegistrarTransicion(
            EstadoPedidoAlimento.Solicitado, EstadoPedidoAlimento.Aceptado, actorId,
            null, fechaEntregaEstimada);
    }

    // CAISY puede cambiar la entrega estimada hasta el despacho (SP8C); en
    // SP8B aplica solo sobre un pedido aceptado.
    public void ActualizarEntregaEstimada(DateOnly nuevaFecha, DateOnly hoy, Guid actorId)
    {
        AsegurarEstado(
            EstadoPedidoAlimento.Aceptado,
            "Solo un pedido aceptado permite actualizar la entrega estimada.");
        AsegurarFechaEntrega(nuevaFecha, hoy);
        FechaEntregaEstimada = nuevaFecha;
        RegistrarTransicion(
            EstadoPedidoAlimento.Aceptado, EstadoPedidoAlimento.Aceptado, actorId,
            null, nuevaFecha);
    }

    // Despacho (spec SP8C "Despacho, nota y recepción"): CAISY registra una
    // única entrega con una única nota, solo desde Aceptado. La fecha de
    // despacho la fija el servidor con la fecha de negocio; el número y la
    // fecha de nota son manuales; cada línea solicitada exige su cantidad
    // entregada, entera en la unidad de presentación y sin negativos, con
    // diferencias permitidas contra lo solicitado. Un segundo despacho choca
    // con el estado: los reintentos no duplican la transición ni la nota.
    public void RegistrarDespacho(
        string numeroNota, DateOnly fechaNota, decimal? totalNetoInformado,
        IReadOnlyList<DatosLineaEntrega> lineasEntregadas, DateOnly hoy, Guid actorId)
    {
        AsegurarEstado(EstadoPedidoAlimento.Aceptado, "Solo un pedido aceptado se puede despachar.");
        if (string.IsNullOrWhiteSpace(numeroNota))
            throw new ReglaNegocioException("El número de nota es obligatorio.");
        if (fechaNota == default)
            throw new ReglaNegocioException("La fecha de la nota es obligatoria.");
        var pedidos = lineasEntregadas
            .GroupBy(l => l.Tipo)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.CantidadEntregada));
        var solicitados = _detalles.ToDictionary(d => d.TipoAlimento);
        if (pedidos.Count != solicitados.Count
            || pedidos.Keys.Any(t => !solicitados.ContainsKey(t)))
            throw new ReglaNegocioException(
                solicitados.Keys.Any(t => !pedidos.ContainsKey(t))
                    ? "La entrega debe cubrir todas las líneas del pedido."
                    : "La entrega incluye una línea que no pertenece al pedido.");

        _entrega = new EntregaPedidoAlimento(
            numeroNota.Trim(), fechaNota, hoy, totalNetoInformado,
            pedidos.Select(par => new DetalleEntregaPedidoAlimento(
                par.Key, Presentacion(), par.Value)).ToList());
        Estado = EstadoPedidoAlimento.Despachado;
        RegistrarTransicion(EstadoPedidoAlimento.Aceptado, EstadoPedidoAlimento.Despachado, actorId);
    }

    private void AsegurarEstado(EstadoPedidoAlimento esperado, string mensaje)
    {
        if (Estado != esperado)
            throw new ReglaNegocioException(mensaje);
    }

    private static void AsegurarMotivo(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("El motivo es obligatorio.");
    }

    private static void AsegurarFechaEntrega(DateOnly fechaEntregaEstimada, DateOnly hoy)
    {
        if (fechaEntregaEstimada < hoy)
            throw new ReglaNegocioException("La fecha de entrega estimada debe ser hoy o posterior.");
    }

    // Granel (spec SP8): mínimo de dos toneladas enteras por tipo y seis
    // toneladas enteras en total al enviar; no existen toneladas decimales.
    private void AsegurarCantidadesGranel()
    {
        if (Presentacion() != PresentacionAlimento.Granel)
            return;
        if (_detalles.Any(d => d.CantidadSolicitada < 2))
            throw new ReglaNegocioException("El envío granel exige al menos dos toneladas por tipo.");
        if (_detalles.Sum(d => d.CantidadSolicitada) < 6)
            throw new ReglaNegocioException("El envío granel exige al menos seis toneladas en total.");
    }

    private PresentacionAlimento Presentacion() => _detalles[0].Presentacion;

    // El pedido admite exclusivamente una presentación, tipos únicos y solo
    // tipos compatibles entre sí: fases de levante o de postura, nunca mezcladas
    // (decisión registrada en el plan SP8B; el spec no define otra matriz).
    private void ReemplazarDetalles(IReadOnlyList<DatosDetallePedido> detalles)
    {
        if (detalles.Count == 0)
            throw new ReglaNegocioException("El pedido debe tener al menos una línea.");
        if (detalles.Select(d => d.Presentacion).Distinct().Count() > 1)
            throw new ReglaNegocioException("El pedido solo admite una presentación.");
        if (detalles.GroupBy(d => d.Tipo).Any(g => g.Count() > 1))
            throw new ReglaNegocioException("Cada tipo de alimento solo puede aparecer una vez en el pedido.");
        var levante = detalles.Any(d => d.Tipo is TipoAlimento.Preiniciador
            or TipoAlimento.Iniciador or TipoAlimento.Crecimiento or TipoAlimento.Finalizador);
        var postura = detalles.Any(d => d.Tipo is TipoAlimento.PosturaUno or TipoAlimento.PosturaDos);
        if (levante && postura)
            throw new ReglaNegocioException("El pedido no puede mezclar tipos de levante y de postura.");

        _detalles.Clear();
        foreach (var datos in detalles)
            _detalles.Add(new DetallePedidoAlimento(datos.Tipo, datos.Presentacion, datos.Cantidad));
    }

    // Resuelve y valida el precio de cada línea antes de tocar el estado del
    // pedido: un envío sin precio vigente completo no deja rastro.
    private Dictionary<TipoAlimento, DatosPrecioEnvio> CongelarPrecios(
        IReadOnlyList<DatosPrecioEnvio> precios)
    {
        var indice = precios.ToDictionary(p => (p.Tipo, p.Presentacion));
        foreach (var linea in _detalles)
        {
            if (!indice.ContainsKey((linea.TipoAlimento, linea.Presentacion)))
                throw new ReglaNegocioException("Falta precio vigente para una línea del pedido.");
        }
        return _detalles.ToDictionary(
            d => d.TipoAlimento, d => indice[(d.TipoAlimento, d.Presentacion)]);
    }

    private void RegistrarTransicion(
        EstadoPedidoAlimento origen, EstadoPedidoAlimento destino,
        Guid actorId, string? motivo = null, DateOnly? fechaEntregaEstimada = null) =>
        _historial.Add(new TransicionPedidoAlimento(origen, destino, actorId, motivo, fechaEntregaEstimada));
}

// Línea del borrador: cantidad entera en la unidad natural (bolsas o
// toneladas según la presentación del pedido).
public sealed record DatosDetallePedido(
    TipoAlimento Tipo, PresentacionAlimento Presentacion, int Cantidad);

// Línea manual del despacho (spec SP8C): cantidad entera entregada en la
// unidad natural de la presentación, referida al tipo solicitado.
public sealed record DatosLineaEntrega(TipoAlimento Tipo, int CantidadEntregada);

// Precio vigente resuelto por la Application al enviar: se congela en las
// líneas como snapshot (spec SP8). La identidad del precio es
// (TipoAlimento, Presentacion), igual que en el catálogo.
public sealed record DatosPrecioEnvio(
    TipoAlimento Tipo, PresentacionAlimento Presentacion,
    decimal PrecioFinalPor40Kg, Guid NotificacionPreciosAlimentosId);
