using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Incidencias;

public sealed class ObtenerIncidenciaAccesoQueryHandler
    : IRequestHandler<ObtenerIncidenciaAccesoQuery, IncidenciaDetalle>
{
    private readonly IRepositorioIncidenciasAcceso _incidencias;
    private readonly ICurrentUser _usuario;

    public ObtenerIncidenciaAccesoQueryHandler(
        IRepositorioIncidenciasAcceso incidencias,
        ICurrentUser usuario)
    {
        _incidencias = incidencias;
        _usuario = usuario;
    }

    public async Task<IncidenciaDetalle> Handle(
        ObtenerIncidenciaAccesoQuery request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");

        var incidencia = await _incidencias.ObtenerPorIdAsync(request.Id, cancellationToken);
        if (incidencia is null || incidencia.ClienteId != clienteId)
            throw new NotFoundException("Incidencia", request.Id);

        return new IncidenciaDetalle(
            incidencia.Id,
            incidencia.FlujoMarcacionId,
            incidencia.SesionKioscoId,
            incidencia.Accion,
            incidencia.PrimerRechazoUtc,
            incidencia.TercerRechazoUtc,
            incidencia.Estado,
            incidencia.TrabajadorId,
            incidencia.JornadaId,
            incidencia.MotivoResolucion,
            incidencia.ResueltaEnUtc,
            incidencia.Version);
    }
}
