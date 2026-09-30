using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Incidencias;

public sealed class DescartarIncidenciaAccesoCommandHandler
    : IRequestHandler<DescartarIncidenciaAccesoCommand>
{
    private readonly IRepositorioIncidenciasAcceso _incidencias;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly ICurrentUser _usuario;
    private readonly IRelojAcceso _reloj;

    public DescartarIncidenciaAccesoCommandHandler(
        IRepositorioIncidenciasAcceso incidencias,
        IUnidadTrabajoControlAcceso unidad,
        ICurrentUser usuario,
        IRelojAcceso reloj)
    {
        _incidencias = incidencias;
        _unidad = unidad;
        _usuario = usuario;
        _reloj = reloj;
    }

    public async Task Handle(
        DescartarIncidenciaAccesoCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");

        var incidencia = await _incidencias.ObtenerPorIdAsync(request.IncidenciaId, cancellationToken);
        if (incidencia is null || incidencia.ClienteId != clienteId)
            throw new NotFoundException("Incidencia", request.IncidenciaId);

        if (incidencia.Estado == EstadoIncidenciaAcceso.Descartada)
            return;
        if (incidencia.Estado == EstadoIncidenciaAcceso.Resuelta)
            throw new ConflictException("La incidencia ya fue resuelta.");

        if (string.IsNullOrWhiteSpace(request.Motivo))
            throw new ReglaNegocioException("El motivo del descarte es obligatorio.");

        if (request.VersionEsperada.HasValue && request.VersionEsperada.Value != incidencia.Version)
            throw new ConflictException("El registro cambió mientras se guardaba; reintente.");

        var ahora = _reloj.ObtenerInstanteUtc();
        incidencia.Descartar(request.Motivo, ahora);

        await _unidad.SaveChangesAsync(cancellationToken);
    }
}
