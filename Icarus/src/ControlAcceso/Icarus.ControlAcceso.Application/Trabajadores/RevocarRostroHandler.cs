using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Application.Persistencia;
using MediatR;

namespace Icarus.ControlAcceso.Application.Trabajadores;

public sealed class RevocarRostroHandler : IRequestHandler<RevocarRostroCommand>
{
    private readonly IRepositorioAccesoTrabajadores _repositorio;
    private readonly IRepositorioPlantillasFaciales _plantillas;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly IConsultaElegibilidadAcceso _elegibilidad;
    private readonly ICurrentUser _usuario;

    public RevocarRostroHandler(
        IRepositorioAccesoTrabajadores repositorio,
        IRepositorioPlantillasFaciales plantillas,
        IUnidadTrabajoControlAcceso unidad,
        IConsultaElegibilidadAcceso elegibilidad,
        ICurrentUser usuario)
    {
        _repositorio = repositorio;
        _plantillas = plantillas;
        _unidad = unidad;
        _elegibilidad = elegibilidad;
        _usuario = usuario;
    }

    public async Task Handle(RevocarRostroCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var elegibilidad = await _elegibilidad.EvaluarAsync(
            clienteId, request.TrabajadorId, cancellationToken);
        if (!elegibilidad.PerteneceAlTenant)
            throw new NotFoundException("Trabajador", request.TrabajadorId);

        var configuracion = await _repositorio.ObtenerConfiguracionAsync(
            clienteId, request.TrabajadorId, cancellationToken);
        if (configuracion is null)
            throw new NotFoundException("Trabajador", request.TrabajadorId);

        configuracion.Revocar();
        await _plantillas.RevocarActivaAsync(clienteId, request.TrabajadorId, cancellationToken);
        await _unidad.SaveChangesAsync(cancellationToken);
    }
}
