using System.Diagnostics;
using Icarus.BuildingBlocks.Application.Observability;
using Microsoft.Extensions.Logging;

namespace Icarus.BuildingBlocks.Observability;

public sealed class RegistroVuelo : IRegistroVuelo
{
    private static readonly HashSet<string> NombresProhibidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "Contrasena", "PasswordHash", "Token", "Cookie", "Secret", "Credential",
        "Biometria", "Biometric", "Email", "Correo", "NIT", "IdentificadorFiscal", "Documento",
        "Nombre", "RazonSocial", "Telefono", "Direccion", "TrabajadorId", "Request", "Response",
        "Entity", "Sql", "Exception", "Mensaje", "ValidationMessage",
    };

    private readonly ILogger _logger;

    public RegistroVuelo(ILogger<RegistroVuelo> logger) : this((ILogger)logger) { }

    public RegistroVuelo(ILogger logger) => _logger = logger;

    public IOperacionVuelo Iniciar(DescriptorOperacionRegistroVuelo descriptor)
    {
        var operacion = new Operacion(this, descriptor);
        Escribir(LogLevel.Information, "operation.started", descriptor.Nombre, "start", null, null, null);
        return operacion;
    }

    public void Decidir(string operacion, string codigo, string resultado,
        IReadOnlyDictionary<string, object?>? campos = null) =>
        Escribir(LogLevel.Information, "operation.decision", operacion, "decision", resultado, codigo, campos);

    public void PersistenciaCompletada(string contexto, int filas, long duracionMs) =>
        Escribir(LogLevel.Information, "persistence.save_changes.completed", contexto, "persistence", "succeeded",
            null, new Dictionary<string, object?> { ["PersistenceContext"] = contexto, ["RowsAffected"] = filas },
            new DescriptorOperacionRegistroVuelo(contexto, new Dictionary<string, DatoRegistroVuelo>
            {
                ["PersistenceContext"] = DatoRegistroVuelo.Texto, ["RowsAffected"] = DatoRegistroVuelo.Entero,
            }), duracionMs);

    public void PersistenciaFallida(string contexto, long duracionMs) =>
        Escribir(LogLevel.Error, "persistence.save_changes.failed", contexto, "persistence", "failed", null,
            new Dictionary<string, object?> { ["PersistenceContext"] = contexto },
            new DescriptorOperacionRegistroVuelo(contexto, new Dictionary<string, DatoRegistroVuelo>
            {
                ["PersistenceContext"] = DatoRegistroVuelo.Texto,
            }), duracionMs);

    public void TransaccionTerminada(string contexto, bool confirmada) =>
        Escribir(confirmada ? LogLevel.Information : LogLevel.Warning,
            confirmada ? "transaction.committed" : "transaction.rolled_back", contexto, "transaction",
            confirmada ? "committed" : "rolled_back", null,
            new Dictionary<string, object?> { ["PersistenceContext"] = contexto },
            new DescriptorOperacionRegistroVuelo(contexto, new Dictionary<string, DatoRegistroVuelo>
            {
                ["PersistenceContext"] = DatoRegistroVuelo.Texto,
            }));

    public ICompensacionVuelo IniciarCompensacion(string operacion)
    {
        Escribir(LogLevel.Warning, "operation.compensation.started", operacion, "compensation", null, null,
            new Dictionary<string, object?> { ["CompensationKind"] = "logical" });
        return new Compensacion(this, operacion);
    }

    private void Escribir(LogLevel nivel, string evento, string operacion, string fase, string? resultado,
        string? codigo, IReadOnlyDictionary<string, object?>? campos, DescriptorOperacionRegistroVuelo? descriptor = null,
        long? duracion = null)
    {
        var propiedades = new Dictionary<string, object?>
        {
            ["EventName"] = evento,
            ["Operation"] = operacion,
            ["Phase"] = fase,
        };
        if (resultado is not null) propiedades["Outcome"] = resultado;
        if (codigo is not null) propiedades["ReasonCode"] = codigo;
        if (duracion is not null) propiedades["DurationMs"] = duracion.Value;
        if (campos is not null)
        {
            foreach (var campo in campos)
            {
                if (descriptor is null || !descriptor.CamposPermitidos.TryGetValue(campo.Key, out var tipo) ||
                    NombresProhibidos.Contains(campo.Key) || !EsTipoValido(campo.Value, tipo))
                    continue;
                propiedades[campo.Key] = campo.Value;
            }
        }

        try
        {
            _logger.Log(nivel, new EventId(0, evento), propiedades, null,
                static (state, _) => state["EventName"]?.ToString() ?? "evento de operación");
        }
        catch
        {
            // El registro nunca debe alterar el resultado funcional.
        }
    }

    private static bool EsTipoValido(object? valor, DatoRegistroVuelo dato) => dato.Tipo switch
    {
        TipoDatoRegistroVuelo.Booleano => valor is bool,
        TipoDatoRegistroVuelo.Entero => valor is int or long,
        TipoDatoRegistroVuelo.Decimal => valor is decimal or double or float,
        TipoDatoRegistroVuelo.Texto => valor is string,
        TipoDatoRegistroVuelo.IdentificadorTecnico => valor is Guid or string,
        _ => false,
    };

    private sealed class Operacion : IOperacionVuelo
    {
        private readonly RegistroVuelo _registro;
        private readonly DescriptorOperacionRegistroVuelo _descriptor;
        private readonly Stopwatch _reloj = Stopwatch.StartNew();
        private bool _finalizada;

        public Operacion(RegistroVuelo registro, DescriptorOperacionRegistroVuelo descriptor)
        {
            _registro = registro;
            _descriptor = descriptor;
        }

        public void Decidir(string codigo, string resultado, IReadOnlyDictionary<string, object?>? campos = null) =>
            _registro.Escribir(LogLevel.Information, "operation.decision", _descriptor.Nombre, "decision", resultado,
                codigo, campos, _descriptor);

        public void Completar() => Finalizar("operation.completed", "end", "succeeded");
        public void Rechazar(string codigo) => Finalizar("operation.rejected", "end", "rejected", codigo);
        public void Fallar() => Finalizar("operation.failed", "end", "failed");

        public void Dispose()
        {
            if (!_finalizada) Fallar();
        }

        private void Finalizar(string evento, string fase, string resultado, string? codigo = null)
        {
            if (_finalizada) return;
            _finalizada = true;
            var nivel = LogLevel.Information;
            if (resultado == "rejected") nivel = LogLevel.Warning;
            else if (resultado == "failed") nivel = LogLevel.Error;
            _registro.Escribir(nivel,
                evento, _descriptor.Nombre, fase, resultado, codigo, null, _descriptor,
                _reloj.ElapsedMilliseconds);
        }
    }

    private sealed class Compensacion : ICompensacionVuelo
    {
        private readonly RegistroVuelo _registro;
        private readonly string _operacion;
        private bool _finalizada;
        public Compensacion(RegistroVuelo registro, string operacion) => (_registro, _operacion) = (registro, operacion);
        public void Completar() => Finalizar("operation.compensation.completed", LogLevel.Warning, "compensated");
        public void Fallar() => Finalizar("operation.compensation.failed", LogLevel.Error, "failed");
        public void Dispose() { if (!_finalizada) Fallar(); }
        private void Finalizar(string evento, LogLevel nivel, string resultado)
        {
            if (_finalizada) return;
            _finalizada = true;
            _registro.Escribir(nivel, evento, _operacion, "compensation", resultado, null,
                new Dictionary<string, object?> { ["CompensationKind"] = "logical" });
        }
    }
}
