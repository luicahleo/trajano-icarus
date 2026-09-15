using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Icarus.GestionAvicola.Application;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Application.Mortalidad;
using Icarus.GestionAvicola.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;
public class RegistrarMortalidadHandlerTests
{
    [Fact]
    public async Task RegistraDescontandoYNarrando()
    {
        var g = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), null);
        var gs = Substitute.For<IRepositorioGalpones>(); var ms = Substitute.For<IRepositorioMortalidad>(); var vuelo = Substitute.For<IRegistroVuelo>(); var u = Substitute.For<IUnidadTrabajoGestionAvicola>(); gs.ObtenerPorIdAsync(g.Id, Arg.Any<CancellationToken>()).Returns(g);
        await new RegistrarMortalidadHandler(gs, ms, vuelo, u).Handle(new(g.Id, null, 15, null), CancellationToken.None);
        Assert.Equal(4785, g.GallinasActuales); ms.Received().Agregar(Arg.Is<RegistroMortalidad>(x => x.GallinasVivas == 4785)); vuelo.Received().Decidir(Arg.Is<DescriptorOperacionRegistroVuelo>(d => d.Nombre == "avicola.mortalidad.registrar"), "ajuste_inventario", "aplicada", Arg.Any<IReadOnlyDictionary<string, object?>>());
    }

    [Fact]
    public async Task LaIdempotenciaNarraLaReutilizacionSinEscribirNiCamposNominales()
    {
        var g = new Galpon(Guid.NewGuid(), Guid.NewGuid(), "1", 5000, 4800,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), null);
        var gs = Substitute.For<IRepositorioGalpones>();
        var ms = Substitute.For<IRepositorioMortalidad>();
        var u = Substitute.For<IUnidadTrabajoGestionAvicola>();
        var clave = Guid.NewGuid();
        var existente = new RegistroMortalidad(Guid.NewGuid(), g.Id, g.ClienteId,
            DateOnly.FromDateTime(DateTime.UtcNow), new TimeOnly(8, 0), 15, 4785, clave);
        gs.ObtenerPorIdAsync(g.Id, Arg.Any<CancellationToken>()).Returns(g);
        ms.ObtenerPorIdempotencyKeyAsync(g.Id, clave, Arg.Any<CancellationToken>()).Returns(existente);
        var capturador = new CapturadorSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(capturador).CreateLogger();

        ILoggerFactory fabrica = new SerilogLoggerFactory(logger);
        var registro = new RegistroVuelo(fabrica.CreateLogger<RegistroVuelo>());
        await new RegistrarMortalidadHandler(gs, ms, registro, u)
            .Handle(new(g.Id, null, 15, clave), CancellationToken.None);

        var decision = Assert.Single(capturador.Eventos, e => Prop(e, "EventName") == "operation.decision");
        Assert.Equal("reutilizada", Prop(decision, "Outcome"));
        Assert.DoesNotContain("TrabajadorId", decision.Properties.Keys);
        ms.DidNotReceive().Agregar(Arg.Any<RegistroMortalidad>());
    }

    private static string? Prop(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor)
            ? (valor as ScalarValue)?.Value?.ToString()
            : null;

    private sealed class CapturadorSink : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];
        public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
    }
}
