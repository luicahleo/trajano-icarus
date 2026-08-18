using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.Granjas;

public interface IRepositorioGranjas
{
    void Agregar(Granja granja);
    Task<Granja?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Granja?> ObtenerActivaDelTenantAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GranjaResumen>> ListarDelTenantAsync(CancellationToken cancellationToken = default);
    Task<bool> ExisteNombreAsync(Guid clienteId, string nombre, CancellationToken cancellationToken = default);
}

public sealed record GranjaResumen(Guid Id, string Nombre);
