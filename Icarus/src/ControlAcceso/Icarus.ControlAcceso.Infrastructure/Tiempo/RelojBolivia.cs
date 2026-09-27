using Icarus.ControlAcceso.Application.Tiempo;

namespace Icarus.ControlAcceso.Infrastructure.Tiempo;

public sealed class RelojBolivia : IRelojAcceso
{
    private static readonly TimeZoneInfo ZonaBolivia =
        TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz");

    private readonly TimeProvider _timeProvider;

    public RelojBolivia(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public DateTimeOffset ObtenerInstanteUtc() => _timeProvider.GetUtcNow();

    public DateOnly ObtenerFechaBolivia(DateTimeOffset? instante = null)
    {
        var local = ConvertirABolivia(instante ?? _timeProvider.GetUtcNow());
        return DateOnly.FromDateTime(local.DateTime);
    }

    public TimeOnly ObtenerHoraBolivia(DateTimeOffset? instante = null)
    {
        var local = ConvertirABolivia(instante ?? _timeProvider.GetUtcNow());
        return TimeOnly.FromDateTime(local.DateTime);
    }

    private static DateTimeOffset ConvertirABolivia(DateTimeOffset utc)
    {
        return TimeZoneInfo.ConvertTime(utc, ZonaBolivia);
    }
}
