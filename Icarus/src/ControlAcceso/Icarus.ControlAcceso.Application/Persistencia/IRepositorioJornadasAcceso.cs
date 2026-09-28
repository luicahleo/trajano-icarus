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

    // Obtiene la jornada del día o la crea resolviendo la carrera entre la
    // marcación de kiosco y el registro manual (clave única por cliente,
    // trabajador y fecha). Nunca devuelve dos jornadas para la misma fecha.
    Task<JornadaAcceso> ObtenerOCrearAsync(
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
