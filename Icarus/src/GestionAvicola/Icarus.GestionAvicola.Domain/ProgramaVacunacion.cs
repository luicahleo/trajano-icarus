using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Catálogo global de planes de vacunación (spec SP7): lo emite CAISY y lo
// gestiona el Gestor CAISY con GestorVeterinario (spec 2026-10-30); no lleva
// ClienteId. Ciclo Borrador -> Publicado: el borrador se edita, recibe el
// cronograma y se elimina con borrado lógico; el publicado es inmutable, es
// el único asignable y solo admite desactivarse. El papel agrupa varias
// vacunas del mismo día en una fila: la EdadDia no se repite entre ítems
// activos. Las tareas ya materializadas en galpones tienen snapshot y no se
// tocan al cambiar el catálogo.
public sealed class ProgramaVacunacion : AggregateRoot
{
    private const string MensajeNoEditable = "Un programa publicado ya no es editable.";

    private readonly List<ItemPlanVacunacion> _items = [];

    private ProgramaVacunacion()
    {
    }

    public ProgramaVacunacion(string nombre, DateOnly? fechaEmision, int cantidadAves, string? observaciones)
    {
        AsignarDatos(nombre, cantidadAves, observaciones);
        if (fechaEmision is not null)
            EstablecerFechaEmision(fechaEmision.Value);
        EstaActivo = true;
    }

    // Para la semilla y tests que necesitan ids fijos.
    public ProgramaVacunacion(Guid id, string nombre, DateOnly? fechaEmision, int cantidadAves, string? observaciones)
        : this(nombre, fechaEmision, cantidadAves, observaciones) => Id = id;

    public string Nombre { get; private set; } = string.Empty;

    // La fecha de emisión no se pide en el alta: la establece la primera fecha
    // del Excel al importar el cronograma (spec SP7). Null hasta ese momento.
    public DateOnly? FechaEmision { get; private set; }

    public int CantidadAves { get; private set; }

    public string? Observaciones { get; private set; }

    public EstadoProgramaVacunacion Estado { get; private set; } = EstadoProgramaVacunacion.Borrador;

    public bool EstaActivo { get; private set; }

    // Solo un publicado activo se asigna a galpones y lo ven cliente y trabajador.
    public bool EsAsignable => EstaActivo && Estado == EstadoProgramaVacunacion.Publicado;

    public IReadOnlyCollection<ItemPlanVacunacion> Items => _items.AsReadOnly();

    public void ActualizarDatos(string nombre, int cantidadAves, string? observaciones)
    {
        AsegurarEditable();
        AsignarDatos(nombre, cantidadAves, observaciones);
    }

    // La emisión sale del cronograma (primera fecha del Excel), no del editor.
    public void EstablecerFechaEmision(DateOnly fecha)
    {
        AsegurarEditable();
        if (fecha > Hoy())
            throw new ReglaNegocioException("La fecha de emisión no puede ser futura.");
        FechaEmision = fecha;
    }

    public void ReemplazarCronograma(IEnumerable<DatosItemPlanVacunacion> items)
    {
        AsegurarEditable();
        var lista = items.ToList();
        if (lista.Count == 0)
            throw new ReglaNegocioException("El cronograma debe tener al menos un ítem.");
        if (lista.Select(i => i.EdadDia).Distinct().Count() != lista.Count)
            throw new ReglaNegocioException("El cronograma no puede repetir la edad en días entre ítems.");

        foreach (var item in _items.Where(i => i.EstaActivo))
            item.Desactivar();
        foreach (var datos in lista)
            _items.Add(new ItemPlanVacunacion(datos.EdadDia, datos.Vacuna, datos.ModoAplicacion, datos.Observaciones, datos.Fecha));
    }

    // Público para que la importación falle antes de leer el Excel.
    public void AsegurarEditable()
    {
        if (Estado != EstadoProgramaVacunacion.Borrador)
            throw new ReglaNegocioException(MensajeNoEditable);
    }

    // Sin versionado ni vigencia (decisiones 4 y 5): publicar vuelve asignable
    // el borrador y lo sella. Exige cronograma para que cada publicado tenga
    // al menos un ítem activo.
    public void Publicar()
    {
        if (Estado == EstadoProgramaVacunacion.Publicado)
            throw new ReglaNegocioException("El programa ya está publicado.");
        if (!EstaActivo)
            throw new ReglaNegocioException("Un borrador eliminado no se puede publicar.");
        if (!_items.Any(i => i.EstaActivo))
            throw new ReglaNegocioException("El programa debe tener un cronograma antes de publicarse.");
        Estado = EstadoProgramaVacunacion.Publicado;
    }

    // Borrado lógico (glosario, regla transversal 1): el borrador queda
    // inactivo y deja de verse. Es inocuo repetirlo.
    public void EliminarBorrador()
    {
        if (Estado != EstadoProgramaVacunacion.Borrador)
            throw new ReglaNegocioException("Un programa publicado no se elimina: se desactiva.");
        EstaActivo = false;
    }

    // Solo un publicado se desactiva: deja de ser asignable y el handler
    // desactiva sus tareas pendientes en todos los galpones (spec SP7). Es
    // inocuo repetirlo.
    public void Desactivar()
    {
        if (Estado != EstadoProgramaVacunacion.Publicado)
            throw new ReglaNegocioException("Un borrador no se desactiva: se elimina.");
        EstaActivo = false;
    }

    private void AsignarDatos(string nombre, int cantidadAves, string? observaciones)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre del programa es obligatorio.");
        if (cantidadAves <= 0)
            throw new ReglaNegocioException("La cantidad de aves debe ser mayor que cero.");

        Nombre = nombre.Trim();
        CantidadAves = cantidadAves;
        Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
    }

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
