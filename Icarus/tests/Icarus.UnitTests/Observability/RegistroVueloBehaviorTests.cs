using FluentValidation;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using MediatR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Icarus.UnitTests.Observability;

public sealed class RegistroVueloBehaviorTests
{
    [Fact]
    public async Task ValidacionRechazadaEmiteCodigoSeguroYSinRequest()
    {
        var logger = new LoggerCapturador();
        var behavior = new RegistroVueloBehavior<SolicitudRegistrable, string>(new RegistroVuelo(logger));

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new SolicitudRegistrable(), () => throw new ValidationException("dato sensible"), CancellationToken.None));

        var evento = Assert.Single(logger.Eventos, e => e.EventName == "operation.rejected");
        Assert.Equal("validation_failed", evento.Properties["ReasonCode"]);
        Assert.DoesNotContain("Request", evento.Properties.Keys);
    }

    [Fact]
    public async Task ErrorInesperadoEmiteFalloSinExcepcionNiMensaje()
    {
        var logger = new LoggerCapturador();
        var behavior = new RegistroVueloBehavior<SolicitudRegistrable, string>(new RegistroVuelo(logger));
        var error = new InvalidOperationException("correo y contraseña no deben aparecer");

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new SolicitudRegistrable(), () => throw error, CancellationToken.None));

        var evento = Assert.Single(logger.Eventos, e => e.EventName == "operation.failed");
        Assert.DoesNotContain("Exception", evento.Properties.Keys);
        Assert.DoesNotContain("correo", evento.Properties.Values.OfType<string>());
    }

    private sealed record SolicitudRegistrable : IRequest<string>, IOperacionRegistrable
    {
        public DescriptorOperacionRegistroVuelo Registro =>
            new("clientes.prueba", new Dictionary<string, DatoRegistroVuelo>());
    }

    private sealed class LoggerCapturador : ILogger
    {
        public List<EventoCapturado> Eventos { get; } = [];
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> properties &&
                properties.FirstOrDefault(p => p.Key == "EventName") is { Value: string name })
                Eventos.Add(new(name, properties.ToDictionary(p => p.Key, p => p.Value)));
        }
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
    private sealed record EventoCapturado(string EventName, Dictionary<string, object?> Properties);
}
