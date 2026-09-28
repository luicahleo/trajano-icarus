using Icarus.BuildingBlocks.Application;
using MediatR;

namespace Icarus.ControlAcceso.Application.Trabajadores;

public sealed class ListarAccesoTrabajadoresHandler
    : IRequestHandler<ListarAccesoTrabajadoresQuery, IReadOnlyList<AccesoTrabajadorResumen>>
{
    private readonly IRepositorioAccesoTrabajadores _repositorio;
    private readonly ICurrentUser _usuario;

    public ListarAccesoTrabajadoresHandler(
        IRepositorioAccesoTrabajadores repositorio, ICurrentUser usuario)
    {
        _repositorio = repositorio;
        _usuario = usuario;
    }

    public async Task<IReadOnlyList<AccesoTrabajadorResumen>> Handle(
        ListarAccesoTrabajadoresQuery request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");

        var configuraciones = await _repositorio.ListarPorClienteAsync(clienteId, cancellationToken);
        return configuraciones
            .Select(c => new AccesoTrabajadorResumen(
                c.TrabajadorId, c.Habilitado, c.Enrolamiento, c.VersionEnrolamiento))
            .ToList();
    }
}
