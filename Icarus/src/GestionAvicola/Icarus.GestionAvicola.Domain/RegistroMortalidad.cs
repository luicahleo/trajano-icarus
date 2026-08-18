using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

public sealed class RegistroMortalidad : AggregateRoot
{
    private RegistroMortalidad() { }
    public RegistroMortalidad(Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora, int cantidadMuertas, int gallinasVivas, Guid? idempotencyKey)
    {
        if (galponId == Guid.Empty) throw new ReglaNegocioException("La mortalidad debe pertenecer a un galpón.");
        if (clienteId == Guid.Empty) throw new ReglaNegocioException("La mortalidad debe pertenecer a un cliente.");
        if (fecha > Hoy()) throw new ReglaNegocioException("La fecha de la mortalidad no puede ser futura.");
        Validar(cantidadMuertas, gallinasVivas);
        GalponId = galponId; ClienteId = clienteId; Fecha = fecha; Hora = hora; CantidadMuertas = cantidadMuertas; GallinasVivas = gallinasVivas; IdempotencyKey = idempotencyKey; EstaActivo = true;
    }
    public RegistroMortalidad(Guid id, Guid galponId, Guid clienteId, DateOnly fecha, TimeOnly hora, int cantidadMuertas, int gallinasVivas, Guid? idempotencyKey) : this(galponId, clienteId, fecha, hora, cantidadMuertas, gallinasVivas, idempotencyKey) => Id = id;
    public Guid GalponId { get; private set; }
    public Guid ClienteId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public TimeOnly Hora { get; private set; }
    public int CantidadMuertas { get; private set; }
    public int GallinasVivas { get; private set; }
    public Guid? IdempotencyKey { get; private set; }
    public bool EstaActivo { get; private set; }
    public void Editar(int cantidadMuertas, TimeOnly hora, int gallinasVivas) { ExigirDiaAbierto(); Validar(cantidadMuertas, gallinasVivas); CantidadMuertas = cantidadMuertas; Hora = hora; GallinasVivas = gallinasVivas; }
    public void Desactivar() { ExigirDiaAbierto(); EstaActivo = false; }
    private void ExigirDiaAbierto() { if (Fecha < Hoy()) throw new ReglaNegocioException("El registro está sellado: solo se puede corregir el mismo día."); }
    private static void Validar(int muertas, int vivas)
    {
        if (muertas <= 0)
            throw new ReglaNegocioException("La cantidad de muertas debe ser mayor que cero.");
        if (vivas < 0)
            throw new ReglaNegocioException("Las gallinas vivas no pueden ser negativas.");
    }
    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
