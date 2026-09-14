using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.DespachosHuevo;

// Puerto del despacho de huevo (spec SP9). Sin cupo ni conteo bloqueable: a
// diferencia de pedidos de alimento, no hay límite semanal de despachos, así
// que tampoco hay transacción explícita del envío.
public interface IRepositorioDespachosHuevo
{
    void Agregar(DespachoHuevo despacho);

    // Los detalles recreados por EditarDetalles llevan clave Guid generada en
    // el dominio: se registran como Added explícitamente (el DetectChanges de
    // EF Core los marcaría Modified por asumir que ya existen).
    void AgregarDetalle(DetalleDespachoHuevo detalle);

    Task<DespachoHuevo?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<DespachoHuevo?> ObtenerConHistorialAsync(
        Guid id, CancellationToken cancellationToken = default);

    // Listado del tenant (spec 2026-09-14): filtros y paginación en SQL, con
    // el conteo antes del Skip/Take. El filtro de tenant del DbContext acota el
    // alcance al cliente de la sesión.
    Task<(IReadOnlyList<DespachoHuevo> Items, int Total)> ListarPaginadoTenantAsync(
        Guid? granjaId, EstadoDespachoHuevo? estado, DateOnly? desde, DateOnly? hasta,
        Guid? creadoPorTrabajadorId, int? numero,
        int saltar, int tomar, CancellationToken cancellationToken = default);

    // Bandeja global de CAISY (spec SP9C/2026-09-14): filtros por estado,
    // granja (por nombre), rango de fechas y folio, con paginación. Devuelve
    // además los nombres de las granjas de la página; CAISY nunca ve personas.
    Task<(IReadOnlyList<DespachoHuevo> Items, int Total, IReadOnlyDictionary<Guid, string> Granjas)>
        ListarPaginadoCaisyAsync(
            EstadoDespachoHuevo? estado, string? granja, DateOnly? desde, DateOnly? hasta,
            int? numero, int saltar, int tomar, CancellationToken cancellationToken = default);

    // Despachos ya recibidos que congelaron una línea con esta publicación
    // (spec SP9D): usado tanto por la vista previa de una corrección como
    // por el comando que la aplica.
    Task<IReadOnlyList<DespachoHuevo>> ListarRecibidosPorPublicacionAsync(
        Guid publicacionId, CancellationToken cancellationToken = default);
}
