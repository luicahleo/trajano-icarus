using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Catálogo global (sin tenant, spec SP7): el filtro del contexto es solo
// EstaActivo. Los métodos "IncluyendoInactivos" ignoran ese filtro y son para
// el rol de plataforma (Administrador), que gestiona el catálogo; los
// operativos (ObtenerPorIdAsync, ListarAsync con incluirInactivos: false) son
// para cliente y trabajador.
public interface IRepositorioProgramasVacunacion
{
    void Agregar(ProgramaVacunacion programa);

    // Los ítems nuevos del último ReemplazarCronograma se registran como Added
    // explícitamente (spec SP7): con clave Guid generada en el dominio, el
    // DetectChanges de EF Core no distingue "nuevo" de "existente".
    void AgregarItem(ItemPlanVacunacion item);

    Task<ProgramaVacunacion?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProgramaVacunacion?> ObtenerPorIdIncluyendoInactivosAsync(Guid id, CancellationToken cancellationToken = default);

    // Unicidad incluyendo inactivos (spec SP7): el soft delete no libera el nombre.
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluyendoId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramaVacunacion>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
}

public sealed record ItemPlanVacunacionResumen(
    Guid Id, int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones);

public sealed record ProgramaVacunacionResumen(
    Guid Id, string Nombre, DateOnly? FechaEmision, int CantidadAves, string? Observaciones, bool EstaActivo);

public sealed record ProgramaVacunacionDetalle(
    Guid Id, string Nombre, DateOnly? FechaEmision, int CantidadAves, string? Observaciones,
    bool EstaActivo, IReadOnlyList<ItemPlanVacunacionResumen> Items);
