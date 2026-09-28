using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Persistencia;
using MediatR;

namespace Icarus.ControlAcceso.Application.Trabajadores;

public sealed class DefinirHabilitacionHandler : IRequestHandler<DefinirHabilitacionCommand>
{
    private readonly IRepositorioAccesoTrabajadores _repositorio;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly IConsultaElegibilidadAcceso _elegibilidad;
    private readonly ICurrentUser _usuario;

    public DefinirHabilitacionHandler(
        IRepositorioAccesoTrabajadores repositorio,
        IUnidadTrabajoControlAcceso unidad,
        IConsultaElegibilidadAcceso elegibilidad,
        ICurrentUser usuario)
    {
        _repositorio = repositorio;
        _unidad = unidad;
        _elegibilidad = elegibilidad;
        _usuario = usuario;
    }

    public async Task Handle(DefinirHabilitacionCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var elegibilidad = await _elegibilidad.EvaluarAsync(
            clienteId, request.TrabajadorId, cancellationToken);
        if (!elegibilidad.PerteneceAlTenant)
            throw new NotFoundException("Trabajador", request.TrabajadorId);

        var configuracion = await _repositorio.ObtenerOCrearConfiguracionAsync(
            clienteId, request.TrabajadorId, cancellationToken);

        if (request.Habilitado)
            configuracion.Habilitar();
        else
            configuracion.Deshabilitar();

        await _unidad.SaveChangesAsync(cancellationToken);
    }
}
