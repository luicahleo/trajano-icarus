using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Catálogo global de planes de vacunación (spec SP7): lo emite CAISY y hoy lo
// sube el Administrador; no lleva ClienteId. El papel agrupa varias vacunas
// del mismo día en una fila: la EdadDia no se repite entre ítems activos. El
// cronograma se reemplaza en bloque; las tareas ya materializadas en galpones
// tienen snapshot y no se tocan. Un programa desactivado no es asignable.
public sealed class ProgramaVacunacion : AggregateRoot
{
    private readonly List<ItemPlanVacunacion> _items = [];

    private ProgramaVacunacion()
    {
    }

    public ProgramaVacunacion(string nombre, DateOnly fechaEmision, int cantidadAves, string? observaciones)
    {
        AsignarDatos(nombre, fechaEmision, cantidadAves, observaciones);
        EstaActivo = true;
    }

    // Para la semilla y tests que necesitan ids fijos.
    public ProgramaVacunacion(Guid id, string nombre, DateOnly fechaEmision, int cantidadAves, string? observaciones)
        : this(nombre, fechaEmision, cantidadAves, observaciones) => Id = id;

    public string Nombre { get; private set; } = string.Empty;

    public DateOnly FechaEmision { get; private set; }

    public int CantidadAves { get; private set; }

    public string? Observaciones { get; private set; }

    public bool EstaActivo { get; private set; }

    public IReadOnlyCollection<ItemPlanVacunacion> Items => _items.AsReadOnly();

    public void ActualizarDatos(string nombre, DateOnly fechaEmision, int cantidadAves, string? observaciones) =>
        AsignarDatos(nombre, fechaEmision, cantidadAves, observaciones);

    public void ReemplazarCronograma(IEnumerable<DatosItemPlanVacunacion> items)
    {
        var lista = items.ToList();
        if (lista.Count == 0)
            throw new ReglaNegocioException("El cronograma debe tener al menos un ítem.");
        if (lista.Select(i => i.EdadDia).Distinct().Count() != lista.Count)
            throw new ReglaNegocioException("El cronograma no puede repetir la edad en días entre ítems.");

        foreach (var item in _items.Where(i => i.EstaActivo))
            item.Desactivar();
        foreach (var datos in lista)
            _items.Add(new ItemPlanVacunacion(datos.EdadDia, datos.Vacuna, datos.ModoAplicacion, datos.Observaciones));
    }

    public void Desactivar() => EstaActivo = false;

    private void AsignarDatos(string nombre, DateOnly fechaEmision, int cantidadAves, string? observaciones)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre del programa es obligatorio.");
        if (fechaEmision > Hoy())
            throw new ReglaNegocioException("La fecha de emisión no puede ser futura.");
        if (cantidadAves <= 0)
            throw new ReglaNegocioException("La cantidad de aves debe ser mayor que cero.");

        Nombre = nombre.Trim();
        FechaEmision = fechaEmision;
        CantidadAves = cantidadAves;
        Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
    }

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
