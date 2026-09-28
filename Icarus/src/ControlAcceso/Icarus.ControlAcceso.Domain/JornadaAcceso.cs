using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

public sealed class JornadaAcceso : AggregateRoot
{
    private readonly List<Marcacion> _marcaciones = [];
    private readonly List<RevisionJornada> _revisiones = [];

    private JornadaAcceso()
    {
    }

    public JornadaAcceso(Guid clienteId, Guid trabajadorId, DateOnly fechaBoliviana)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("La jornada debe pertenecer a un cliente.");
        if (trabajadorId == Guid.Empty)
            throw new ReglaNegocioException("La jornada debe pertenecer a un trabajador.");

        ClienteId = clienteId;
        TrabajadorId = trabajadorId;
        FechaBoliviana = fechaBoliviana;
    }

    public JornadaAcceso(Guid id, Guid clienteId, Guid trabajadorId, DateOnly fechaBoliviana)
        : this(clienteId, trabajadorId, fechaBoliviana) => Id = id;

    public Guid ClienteId { get; private set; }

    public Guid TrabajadorId { get; private set; }

    public DateOnly FechaBoliviana { get; private set; }

    public bool EstaActivo { get; private set; } = true;

    public IReadOnlyCollection<Marcacion> Marcaciones => _marcaciones;

    public IReadOnlyCollection<RevisionJornada> Revisiones => _revisiones;

    public int Version { get; private set; }

    public Marcacion RegistrarMarcacion(
        TipoMarcacion tipo,
        DateTimeOffset instanteUtc,
        DateOnly fechaBoliviana,
        Guid claveIdempotencia,
        OrigenMarcacion origen)
    {
        if (fechaBoliviana != FechaBoliviana)
            throw new ReglaNegocioException("La fecha de la marcación no corresponde a la jornada.");

        var esperado = SiguienteTipoEsperado();
        if (tipo != esperado)
            throw new ReglaNegocioException($"Se esperaba una {esperado.ToString().ToLowerInvariant()}.");

        if (tipo == TipoMarcacion.Salida)
        {
            var entrada = _marcaciones.Last(m => m.Tipo == TipoMarcacion.Entrada);
            var nuevoIntervalo = new IntervaloAcceso(entrada.InstanteUtc, instanteUtc);
            if (IntervalosCerrados().Any(existente => nuevoIntervalo.SeSolapaCon(existente)))
                throw new ReglaNegocioException("El intervalo se solapa con una marcación anterior.");
        }
        else
        {
            if (IntervalosCerrados().Any(i => i.Entrada < instanteUtc && instanteUtc < i.Salida))
                throw new ReglaNegocioException("La entrada cae dentro de un intervalo anterior.");
        }

        var marcacion = new Marcacion(tipo, instanteUtc, claveIdempotencia, origen);
        _marcaciones.Add(marcacion);
        Version++;
        return marcacion;
    }

    public RevisionJornada Corregir(
        DateTimeOffset instanteCorreccionUtc,
        DateOnly hoyBoliviana,
        int versionEsperada,
        string motivo,
        Guid autorId,
        params (TipoMarcacion Tipo, DateTimeOffset InstanteUtc)[] valoresEfectivos)
    {
        if (FechaBoliviana > hoyBoliviana)
            throw new ReglaNegocioException("No se pueden corregir jornadas futuras.");
        if (versionEsperada != Version)
            throw new ConflictException("El registro cambió mientras se guardaba; reintente.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("El motivo de la corrección es obligatorio.");
        if (autorId == Guid.Empty)
            throw new ReglaNegocioException("La corrección requiere un autor.");

        ValidarValoresEfectivos(valoresEfectivos);

        var revision = new RevisionJornada(
            Id,
            instanteCorreccionUtc,
            _marcaciones.Count,
            motivo.Trim(),
            autorId,
            valoresEfectivos.Select(v => new ValorEfectivo(v.Tipo, v.InstanteUtc)));

        _revisiones.Add(revision);
        Version++;
        return revision;
    }

    public void Desactivar() => EstaActivo = false;

    public EstadoJornadaAcceso Estado(DateOnly hoyBoliviana)
    {
        var ultimo = _marcaciones.LastOrDefault();
        if (ultimo is null || ultimo.Tipo == TipoMarcacion.Salida)
            return EstadoJornadaAcceso.Completa;

        return FechaBoliviana == hoyBoliviana
            ? EstadoJornadaAcceso.Abierta
            : EstadoJornadaAcceso.Incompleta;
    }

    public IEnumerable<(TipoMarcacion Tipo, DateTimeOffset InstanteUtc)> ValoresEfectivos(DateOnly hoyBoliviana)
    {
        var ultimaRevision = _revisiones.LastOrDefault();
        if (ultimaRevision is not null)
        {
            foreach (var valor in ultimaRevision.ValoresEfectivos)
                yield return (valor.Tipo, valor.InstanteUtc);

            foreach (var marcacion in _marcaciones.Skip(ultimaRevision.HastaSecuenciaOriginal))
                yield return (marcacion.Tipo, marcacion.InstanteUtc);
        }
        else
        {
            foreach (var marcacion in _marcaciones)
                yield return (marcacion.Tipo, marcacion.InstanteUtc);
        }
    }

    private TipoMarcacion SiguienteTipoEsperado()
    {
        var ultimo = _marcaciones.LastOrDefault();
        return ultimo?.Tipo == TipoMarcacion.Entrada
            ? TipoMarcacion.Salida
            : TipoMarcacion.Entrada;
    }

    private IEnumerable<IntervaloAcceso> IntervalosCerrados()
    {
        for (int i = 0; i + 1 < _marcaciones.Count; i += 2)
        {
            yield return new IntervaloAcceso(_marcaciones[i].InstanteUtc, _marcaciones[i + 1].InstanteUtc);
        }
    }

    private static void ValidarValoresEfectivos((TipoMarcacion Tipo, DateTimeOffset InstanteUtc)[] valores)
    {
        var efectivos = valores.ToList();
        if (efectivos.Count == 0)
            return;

        for (int i = 0; i < efectivos.Count; i++)
        {
            var esperado = i % 2 == 0 ? TipoMarcacion.Entrada : TipoMarcacion.Salida;
            if (efectivos[i].Tipo != esperado)
                throw new ReglaNegocioException("La secuencia de corrección debe alternar entrada y salida.");
        }

        if (efectivos.Count % 2 != 0)
            return;

        var intervalos = new List<IntervaloAcceso>();
        for (int i = 0; i < efectivos.Count; i += 2)
        {
            var intervalo = new IntervaloAcceso(efectivos[i].InstanteUtc, efectivos[i + 1].InstanteUtc);
            if (intervalos.Any(existente => intervalo.SeSolapaCon(existente)))
                throw new ReglaNegocioException("Los intervalos de corrección no pueden solaparse.");
            intervalos.Add(intervalo);
        }
    }
}
