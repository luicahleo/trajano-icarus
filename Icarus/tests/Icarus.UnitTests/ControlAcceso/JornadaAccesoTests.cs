using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Domain;

namespace Icarus.UnitTests.ControlAcceso;

public class JornadaAccesoTests
{
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly Guid Trabajador = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 9, 27);

    [Fact]
    public void SalidaInicial_Rechazada()
    {
        var jornada = NuevaJornada();

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            jornada.RegistrarMarcacion(TipoMarcacion.Salida, Instant(8, 0), Hoy, Clave(), OrigenMarcacion.Kiosco));

        Assert.Contains("entrada", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FechaBolivianaDelEvento_DistintaALaJornada_Rechazada()
    {
        var jornada = NuevaJornada();
        var ayer = Hoy.AddDays(-1);

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), ayer, Clave(), OrigenMarcacion.Kiosco));

        Assert.Contains("fecha", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Entrada_SinSalida_AbiertaHoyEIncompletaManana()
    {
        var jornada = NuevaJornada();

        jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);

        Assert.Equal(EstadoJornadaAcceso.Abierta, jornada.Estado(Hoy));
        Assert.Equal(EstadoJornadaAcceso.Incompleta, jornada.Estado(Hoy.AddDays(1)));
    }

    [Fact]
    public void DosPares_Completos()
    {
        var jornada = NuevaJornada();

        jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);
        jornada.RegistrarMarcacion(TipoMarcacion.Salida, Instant(12, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);
        jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(13, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);
        jornada.RegistrarMarcacion(TipoMarcacion.Salida, Instant(17, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);

        Assert.Equal(EstadoJornadaAcceso.Completa, jornada.Estado(Hoy));
        Assert.Equal(4, jornada.Marcaciones.Count);
    }

    [Fact]
    public void EntradaRepetidaMismoDia_Rechazada()
    {
        var jornada = NuevaJornada();
        jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(9, 0), Hoy, Clave(), OrigenMarcacion.Kiosco));

        Assert.Contains("salida", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SalidaRepetida_Rechazada()
    {
        var jornada = NuevaJornada();
        jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);
        jornada.RegistrarMarcacion(TipoMarcacion.Salida, Instant(12, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            jornada.RegistrarMarcacion(TipoMarcacion.Salida, Instant(13, 0), Hoy, Clave(), OrigenMarcacion.Kiosco));

        Assert.Contains("entrada", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntervalosSolapados_Rechazados()
    {
        var jornada = NuevaJornada();
        jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);
        jornada.RegistrarMarcacion(TipoMarcacion.Salida, Instant(12, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(11, 0), Hoy, Clave(), OrigenMarcacion.Kiosco));

        Assert.Contains("intervalo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AyerIncompleto_NoBloqueaHoy()
    {
        var ayer = Hoy.AddDays(-1);
        var jornadaAyer = new JornadaAcceso(Cliente, Trabajador, ayer);
        jornadaAyer.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), ayer, Clave(), OrigenMarcacion.Kiosco);

        var jornadaHoy = NuevaJornada();

        jornadaHoy.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);

        Assert.Equal(EstadoJornadaAcceso.Abierta, jornadaHoy.Estado(Hoy));
    }

    [Fact]
    public void MarcacionManual_DistingueOrigen()
    {
        var jornada = NuevaJornada();

        var marcacion = jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), Hoy, Clave(), OrigenMarcacion.ManualCliente);

        Assert.Equal(OrigenMarcacion.ManualCliente, marcacion.Origen);
    }

    private static JornadaAcceso NuevaJornada() => new(Cliente, Trabajador, Hoy);

    private static DateTimeOffset Instant(int hora, int minuto)
        => new(2026, 9, 27, hora, minuto, 0, TimeSpan.Zero);

    private static Guid Clave() => Guid.NewGuid();
}
