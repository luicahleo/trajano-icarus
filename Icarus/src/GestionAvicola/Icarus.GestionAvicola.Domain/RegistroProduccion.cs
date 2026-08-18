using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

public sealed class RegistroProduccion : AggregateRoot
{
    private RegistroProduccion() { }
    public RegistroProduccion(Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora, int cantidadMaples, int unidadesIncompletas, int maplesDescarte, int unidadesDescarte, int gallinasVivas, Guid? idempotencyKey)
    {
        if (galponId == Guid.Empty) throw new ReglaNegocioException("La recogida debe pertenecer a un galpón.");
        if (clienteId == Guid.Empty) throw new ReglaNegocioException("La recogida debe pertenecer a un cliente.");
        if (fecha > Hoy()) throw new ReglaNegocioException("La fecha de la recogida no puede ser futura.");
        ValidarCantidades(cantidadMaples, unidadesIncompletas, maplesDescarte, unidadesDescarte);
        if (gallinasVivas < 0) throw new ReglaNegocioException("Las gallinas vivas no pueden ser negativas.");
        GalponId = galponId; ClienteId = clienteId; Fecha = fecha; Hora = hora;
        CantidadMaples = cantidadMaples; UnidadesIncompletas = unidadesIncompletas;
        MaplesDescarte = maplesDescarte; UnidadesDescarte = unidadesDescarte;
        GallinasVivas = gallinasVivas; IdempotencyKey = idempotencyKey; EstaActivo = true;
    }
    public RegistroProduccion(Guid id, Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora, int cantidadMaples, int unidadesIncompletas, int maplesDescarte, int unidadesDescarte, int gallinasVivas, Guid? idempotencyKey) : this(galponId, clienteId, fecha, hora, cantidadMaples, unidadesIncompletas, maplesDescarte, unidadesDescarte, gallinasVivas, idempotencyKey) => Id = id;
    public Guid GalponId { get; private set; }
    public Guid ClienteId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public TimeOnly Hora { get; private set; }
    public int CantidadMaples { get; private set; }
    public int UnidadesIncompletas { get; private set; }
    public int MaplesDescarte { get; private set; }
    public int UnidadesDescarte { get; private set; }
    public int GallinasVivas { get; private set; }
    public Guid? IdempotencyKey { get; private set; }
    public bool EstaActivo { get; private set; }
    public int TotalHuevosVendibles() => CantidadMaples * Maple.HuevosPorMaple + UnidadesIncompletas;
    public int TotalHuevosDescarte() => MaplesDescarte * Maple.HuevosPorMaple + UnidadesDescarte;
    public void Editar(int cantidadMaples, int unidadesIncompletas, int maplesDescarte, int unidadesDescarte, TimeOnly hora)
    {
        ExigirDiaAbierto(); ValidarCantidades(cantidadMaples, unidadesIncompletas, maplesDescarte, unidadesDescarte);
        CantidadMaples = cantidadMaples; UnidadesIncompletas = unidadesIncompletas; MaplesDescarte = maplesDescarte; UnidadesDescarte = unidadesDescarte; Hora = hora;
    }
    public void Desactivar() { ExigirDiaAbierto(); EstaActivo = false; }
    private void ExigirDiaAbierto() { if (Fecha < Hoy()) throw new ReglaNegocioException("El registro está sellado: solo se puede corregir el mismo día."); }
    private static void ValidarCantidades(int maples, int sueltos, int descarteMaples, int descarteSueltos)
    {
        if (maples < 0 || descarteMaples < 0)
            throw new ReglaNegocioException("Los maples no pueden ser negativos.");
        if (sueltos < 0 || sueltos >= Maple.HuevosPorMaple || descarteSueltos < 0 || descarteSueltos >= Maple.HuevosPorMaple)
            throw new ReglaNegocioException("Las unidades sueltas deben estar entre 0 y 29.");
    }
    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
