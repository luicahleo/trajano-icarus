using Icarus.ControlAcceso.Application.Biometria;

namespace Icarus.ControlAcceso.Infrastructure.Argos;

// Proveedor por defecto mientras A0 no fije el contrato real: el grafo de DI
// siempre es construible y el kiosco/enrolamiento fallan cerrado sin un doble
// silencioso en producción.
public sealed class ProveedorIdentidadFacialNoDisponible : IProveedorIdentidadFacial
{
    public Task<ResultadoEnrolamiento> ExtraerAsync(
        MuestraFacial muestra, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultadoEnrolamiento.Rechazado("proveedor_no_disponible"));

    public Task<ResultadoIdentificacionFacial> IdentificarAsync(
        MuestraFacial muestra,
        IReadOnlyList<CandidatoFacial> candidatos,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultadoIdentificacionFacial.SinCoincidencia("proveedor_no_disponible"));
}
