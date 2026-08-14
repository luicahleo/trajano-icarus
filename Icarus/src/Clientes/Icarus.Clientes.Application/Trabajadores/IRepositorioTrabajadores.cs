using Icarus.Clientes.Domain;

namespace Icarus.Clientes.Application.Trabajadores;

public interface IRepositorioTrabajadores
{
    void Agregar(Trabajador trabajador);

    // Respeta los filtros globales (tenant + activos): un rol Cliente solo
    // encuentra trabajadores de su empresa; un id ajeno devuelve null, igual
    // que uno inexistente (anti-enumeración).
    Task<Trabajador?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrabajadorResumen>> ListarPorClienteAsync(
        Guid clienteId, CancellationToken cancellationToken = default);

    Task<bool> ExisteDocumentoAsync(
        Guid clienteId, string documentoIdentidad, CancellationToken cancellationToken = default);
}

public sealed record TrabajadorResumen(
    Guid Id, string Nombre, string DocumentoIdentidad, string Cargo,
    DateOnly FechaIngreso, DateOnly? FechaCese, IReadOnlyList<string> Funcionalidades);
