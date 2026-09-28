using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Persistencia;

public interface IRepositorioJornadasAcceso
{
    void Agregar(JornadaAcceso jornada);

    Task<JornadaAcceso?> ObtenerAsync(
        Guid clienteId,
        Guid trabajadorId,
        DateOnly fechaBoliviana,
        CancellationToken cancellationToken = default);

    Task<JornadaAcceso?> ObtenerPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Pagina<JornadaAcceso>> ListarAsync(
        Guid clienteId,
        Guid? trabajadorId,
        DateOnly? desde,
        DateOnly? hasta,
        PeticionPaginada paginacion,
        CancellationToken cancellationToken = default);
}
