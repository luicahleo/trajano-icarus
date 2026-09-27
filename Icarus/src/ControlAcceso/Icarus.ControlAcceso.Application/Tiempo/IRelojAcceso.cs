namespace Icarus.ControlAcceso.Application.Tiempo;

public interface IRelojAcceso
{
    DateTimeOffset ObtenerInstanteUtc();

    DateOnly ObtenerFechaBolivia(DateTimeOffset? instante = null);

    TimeOnly ObtenerHoraBolivia(DateTimeOffset? instante = null);
}
