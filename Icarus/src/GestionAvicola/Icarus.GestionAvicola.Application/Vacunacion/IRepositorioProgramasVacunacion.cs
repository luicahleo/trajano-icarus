using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Catálogo global (sin tenant, spec SP7). Dos audiencias (spec 2026-10-30):
// el tenant (cliente y trabajador con Vacunacion) solo ve publicados activos,
// y el Gestor CAISY con GestorVeterinario ve el catálogo completo salvo los
// borradores eliminados. El filtro del contexto es solo EstaActivo.
public interface IRepositorioProgramasVacunacion
{
    void Agregar(ProgramaVacunacion programa);

    // Los ítems nuevos del último ReemplazarCronograma se registran como Added
    // explícitamente (spec SP7): con clave Guid generada en el dominio, el
    // DetectChanges de EF Core no distingue "nuevo" de "existente".
    void AgregarItem(ItemPlanVacunacion item);

    // Tenant: publicado y activo, con sus ítems activos.
    Task<ProgramaVacunacion?> ObtenerPublicadoPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramaVacunacion>> ListarPublicadosAsync(CancellationToken cancellationToken = default);

    // Gestor: borradores activos, publicados activos e inactivos, con todos
    // sus ítems (también los reemplazados). Nunca los borradores eliminados.
    Task<ProgramaVacunacion?> ObtenerParaGestionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramaVacunacion>> ListarParaGestionAsync(CancellationToken cancellationToken = default);

    // Unicidad incluyendo inactivos (spec SP7): el soft delete no libera el nombre.
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluyendoId = null, CancellationToken cancellationToken = default);

    // Nombres del catálogo (incluidos inactivos) para decorar tareas: el
    // historial sanitario no pierde el nombre aunque el programa se desactive.
    Task<IReadOnlyDictionary<Guid, string>> ObtenerNombresAsync(CancellationToken cancellationToken = default);
}

public sealed record ItemPlanVacunacionResumen(
    Guid Id, int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones, DateOnly? Fecha);

// Contratos del tenant: sin cambios de forma respecto de SP7.
public sealed record ProgramaVacunacionResumen(
    Guid Id, string Nombre, DateOnly? FechaEmision, int CantidadAves, string? Observaciones, bool EstaActivo);

public sealed record ProgramaVacunacionDetalle(
    Guid Id, string Nombre, DateOnly? FechaEmision, int CantidadAves, string? Observaciones,
    bool EstaActivo, IReadOnlyList<ItemPlanVacunacionResumen> Items);

// Contratos del gestor: nunca llevan datos de galpones, clientes ni tareas
// (ContratosGestionVacunacionTests).
public sealed record ProgramaVacunacionGestionResumen(
    Guid Id, string Nombre, DateOnly? FechaEmision, int CantidadAves, string Estado, bool EstaActivo,
    int CantidadItems);

public sealed record ProgramaVacunacionGestionDetalle(
    Guid Id, string Nombre, DateOnly? FechaEmision, int CantidadAves, string? Observaciones,
    string Estado, bool EstaActivo, IReadOnlyList<ItemPlanVacunacionResumen> Items);
