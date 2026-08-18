using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Galpones;

public interface IRepositorioGalpones
{
    void Agregar(Galpon galpon);
    Task<Galpon?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GalponResumen>> ListarPorGranjaAsync(Guid granjaId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Galpon>> ListarActivosDeGranjaAsync(Guid granjaId, CancellationToken cancellationToken = default);
    Task<bool> ExisteNumeroAsync(Guid granjaId, string numero, CancellationToken cancellationToken = default);
}

public sealed record GalponResumen(
    Guid Id, string Numero, int CapacidadMaxima, int GallinasActuales,
    DateOnly FechaNacimientoLote, string? Descripcion);
