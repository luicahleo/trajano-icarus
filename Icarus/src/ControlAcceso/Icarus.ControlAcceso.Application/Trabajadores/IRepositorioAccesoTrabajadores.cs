using Icarus.ControlAcceso.Domain;

namespace Icarus.ControlAcceso.Application.Trabajadores;

public interface IRepositorioAccesoTrabajadores
{
    Task<ConfiguracionAccesoTrabajador?> ObtenerConfiguracionAsync(
        Guid clienteId, Guid trabajadorId, CancellationToken cancellationToken = default);

    // Trabajadores habilitados y con enrolamiento vigente del tenant: la BD
    // concreta sus candidatos sin exponerlos.
    Task<IReadOnlyList<ConfiguracionAccesoTrabajador>> ListarHabilitadosAsync(
        Guid clienteId, CancellationToken cancellationToken = default);

    // Toda la configuración de acceso del tenant, para la administración web.
    Task<IReadOnlyList<ConfiguracionAccesoTrabajador>> ListarPorClienteAsync(
        Guid clienteId, CancellationToken cancellationToken = default);

    // Obtiene o crea la configuración del trabajador resolviendo la carrera de
    // creación (clave única por cliente y trabajador).
    Task<ConfiguracionAccesoTrabajador> ObtenerOCrearConfiguracionAsync(
        Guid clienteId, Guid trabajadorId, CancellationToken cancellationToken = default);

    void AgregarOperacion(OperacionEnrolamiento operacion);

    Task<OperacionEnrolamiento?> ObtenerOperacionAsync(
        Guid clienteId, Guid claveIdempotencia, CancellationToken cancellationToken = default);
}
