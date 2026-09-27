using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Domain;

namespace Icarus.UnitTests.ControlAcceso;

public class CorreccionJornadaTests
{
    private static readonly Guid Cliente = Guid.NewGuid();
    private static readonly Guid Trabajador = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 9, 27);
    private static readonly Guid Autor = Guid.NewGuid();

    [Fact]
    public void Correccion_NoBorraMarcacionesOriginales()
    {
        var jornada = JornadaConEntradaSalida();
        var original = jornada.Marcaciones.ToList();

        jornada.Corregir(
            Instant(18, 0),
            Hoy,
            versionEsperada: 2,
            "Olvidé marcar salida",
            Autor,
            (TipoMarcacion.Entrada, Instant(8, 0)),
            (TipoMarcacion.Salida, Instant(17, 0)));

        Assert.Equal(2, jornada.Marcaciones.Count);
        Assert.Equal(original, jornada.Marcaciones);
    }

    [Fact]
    public void Correccion_ConVersionObsoleta_Rechazada()
    {
        var jornada = JornadaConEntradaSalida();

        var ex = Assert.Throws<ConflictException>(() =>
            jornada.Corregir(
                Instant(18, 0),
                Hoy,
                versionEsperada: 0,
                "Motivo",
                Autor,
                (TipoMarcacion.Entrada, Instant(8, 0)),
                (TipoMarcacion.Salida, Instant(17, 0))));

        Assert.NotNull(ex);
    }

    [Fact]
    public void Correccion_Futura_Rechazada()
    {
        var manana = Hoy.AddDays(1);
        var jornada = new JornadaAcceso(Cliente, Trabajador, manana);

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            jornada.Corregir(
                Instant(18, 0),
                Hoy,
                versionEsperada: 0,
                "Motivo",
                Autor,
                (TipoMarcacion.Entrada, Instant(8, 0)),
                (TipoMarcacion.Salida, Instant(17, 0))));

        Assert.Contains("futur", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Correccion_Vacia_AnulaJornada()
    {
        var jornada = JornadaConEntradaSalida();

        jornada.Corregir(
            Instant(18, 0),
            Hoy,
            versionEsperada: 2,
            "Jornada equivocada",
            Autor);

        Assert.Single(jornada.Revisiones);
        Assert.Empty(jornada.ValoresEfectivos(Hoy));
        Assert.Equal(EstadoJornadaAcceso.Completa, jornada.Estado(Hoy));
    }

    [Fact]
    public void Correccion_NuevaMarcacionPosterior_NoDesapareceTrasRevisionAnterior()
    {
        var jornada = JornadaConEntradaSalida();
        jornada.Corregir(
            Instant(18, 0),
            Hoy,
            versionEsperada: 2,
            "Ajuste parcial",
            Autor,
            (TipoMarcacion.Entrada, Instant(8, 0)),
            (TipoMarcacion.Salida, Instant(12, 0)));

        jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(13, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);
        jornada.RegistrarMarcacion(TipoMarcacion.Salida, Instant(17, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);

        var efectivos = jornada.ValoresEfectivos(Hoy).ToList();
        Assert.Equal(4, efectivos.Count);
    }

    [Fact]
    public void Correccion_SecuenciaInvalida_Rechazada()
    {
        var jornada = JornadaConEntradaSalida();

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            jornada.Corregir(
                Instant(18, 0),
                Hoy,
                versionEsperada: 2,
                "Motivo",
                Autor,
                (TipoMarcacion.Salida, Instant(8, 0)),
                (TipoMarcacion.Entrada, Instant(17, 0))));

        Assert.Contains("secuencia", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Correccion_IntervaloSolapado_Rechazado()
    {
        var jornada = JornadaConEntradaSalida();

        var ex = Assert.Throws<ReglaNegocioException>(() =>
            jornada.Corregir(
                Instant(18, 0),
                Hoy,
                versionEsperada: 2,
                "Motivo",
                Autor,
                (TipoMarcacion.Entrada, Instant(8, 0)),
                (TipoMarcacion.Salida, Instant(13, 0)),
                (TipoMarcacion.Entrada, Instant(12, 0)),
                (TipoMarcacion.Salida, Instant(17, 0))));

        Assert.Contains("intervalo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static JornadaAcceso JornadaConEntradaSalida()
    {
        var jornada = new JornadaAcceso(Cliente, Trabajador, Hoy);
        jornada.RegistrarMarcacion(TipoMarcacion.Entrada, Instant(8, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);
        jornada.RegistrarMarcacion(TipoMarcacion.Salida, Instant(12, 0), Hoy, Clave(), OrigenMarcacion.Kiosco);
        return jornada;
    }

    private static DateTimeOffset Instant(int hora, int minuto)
        => new(2026, 9, 27, hora, minuto, 0, TimeSpan.Zero);

    private static Guid Clave() => Guid.NewGuid();
}
