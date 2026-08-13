using Icarus.Clientes.Domain;

namespace Icarus.Clientes.Application.Clientes;

public interface IRepositorioClientes
{
    void Agregar(Cliente cliente);

    // Respeta los filtros globales (tenant + activos): un rol Cliente solo
    // encuentra su propia empresa; un clienteId ajeno devuelve null, igual que
    // uno inexistente (anti-enumeración).
    Task<Cliente?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Ignora los filtros globales: gestión del Administrador (suspender,
    // reactivar, asignar módulos), que debe alcanzar también clientes
    // suspendidos.
    Task<Cliente?> ObtenerGestionablePorIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Ignora los filtros globales: la lista del Administrador incluye
    // suspendidos.
    Task<IReadOnlyList<ClienteResumen>> ListarTodosAsync(CancellationToken cancellationToken = default);

    // Ignora los filtros globales: la unicidad del identificador fiscal es
    // global, también contra clientes suspendidos.
    Task<bool> ExisteIdentificadorFiscalAsync(
        string identificadorFiscal, CancellationToken cancellationToken = default);
}

public sealed record ClienteResumen(
    Guid Id, string RazonSocial, string IdentificadorFiscal, bool EstaActivo, IReadOnlyList<string> Modulos);
