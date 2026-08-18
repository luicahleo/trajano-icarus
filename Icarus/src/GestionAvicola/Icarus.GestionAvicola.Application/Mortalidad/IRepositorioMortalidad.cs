using Icarus.GestionAvicola.Domain;
namespace Icarus.GestionAvicola.Application.Mortalidad;
public interface IRepositorioMortalidad { void Agregar(RegistroMortalidad r); Task<RegistroMortalidad?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default); Task<IReadOnlyList<RegistroMortalidad>> ListarPorDiaAsync(Guid galponId, DateOnly fecha, CancellationToken ct = default); Task<IReadOnlyList<RegistroMortalidad>> ListarPorRangoAsync(Guid galponId, DateOnly desde, DateOnly hasta, CancellationToken ct = default); Task<RegistroMortalidad?> ObtenerPorIdempotencyKeyAsync(Guid galponId, Guid key, CancellationToken ct = default); }
public sealed record MortalidadResumen(Guid Id, DateOnly Fecha, TimeOnly Hora, int CantidadMuertas, int GallinasVivas);
public sealed record MortalidadDiaResumen(Guid GalponId, DateOnly Fecha, IReadOnlyList<MortalidadResumen> Registros, int TotalMuertas);
