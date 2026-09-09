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

    Task<IReadOnlyList<DespachoHuevo>> ListarDelTenantAsync(
        CancellationToken cancellationToken = default);

    // Bandeja global de CAISY (spec SP9C): filtro por estado con paginación,
    // igual que pedidos de alimento. El filtro de tenant del DbContext deja
    // ver los despachos de todos los tenants a las cuentas sin tenant y la
    // política de CAISY autoriza el acceso.
    Task<(IReadOnlyList<DespachoHuevo> Items, int Total)> ListarPaginadoCaisyAsync(
        EstadoDespachoHuevo? estado, int saltar, int tomar,
        CancellationToken cancellationToken = default);
}
