using Icarus.ControlAcceso.Application.Tiempo;
using Icarus.ControlAcceso.Infrastructure.Tiempo;

namespace Icarus.UnitTests.ControlAcceso;

public class RelojBoliviaTests
{
    private static TimeProvider RelojFijo(DateTimeOffset utc)
        => new ProveedorTiempoFijo(utc);

    [Fact]
    public void TresCincuentaYNueveUtc_EsDiaAnteriorEnBolivia()
    {
        // 2026-09-27 03:59:59 UTC = 2026-09-26 23:59:59 BO (UTC-4)
        var instante = new DateTimeOffset(2026, 9, 27, 3, 59, 59, TimeSpan.Zero);
        var reloj = new RelojBolivia(RelojFijo(instante));

        Assert.Equal(new DateOnly(2026, 9, 26), reloj.ObtenerFechaBolivia(instante));
        Assert.Equal(new TimeOnly(23, 59, 59), reloj.ObtenerHoraBolivia(instante));
    }

    [Fact]
    public void CuatroUtc_EsNuevoDiaEnBolivia()
    {
        // 2026-09-27 04:00:00 UTC = 2026-09-27 00:00:00 BO
        var instante = new DateTimeOffset(2026, 9, 27, 4, 0, 0, TimeSpan.Zero);
        var reloj = new RelojBolivia(RelojFijo(instante));

        Assert.Equal(new DateOnly(2026, 9, 27), reloj.ObtenerFechaBolivia(instante));
        Assert.Equal(new TimeOnly(0, 0, 0), reloj.ObtenerHoraBolivia(instante));
    }

    [Fact]
    public void SinInstante_DevuelveValorDelProveedorActual()
    {
        var instante = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        var reloj = new RelojBolivia(RelojFijo(instante));

        Assert.Equal(new DateOnly(2026, 9, 27), reloj.ObtenerFechaBolivia());
        Assert.Equal(new TimeOnly(8, 0, 0), reloj.ObtenerHoraBolivia());
    }

    private sealed class ProveedorTiempoFijo : TimeProvider
    {
        private readonly DateTimeOffset _utc;
        public ProveedorTiempoFijo(DateTimeOffset utc) => _utc = utc;
        public override DateTimeOffset GetUtcNow() => _utc;
    }
}
