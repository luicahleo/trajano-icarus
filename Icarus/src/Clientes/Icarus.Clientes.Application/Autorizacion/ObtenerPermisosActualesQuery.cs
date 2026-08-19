using MediatR;

namespace Icarus.Clientes.Application.Autorizacion;

// Permisos efectivos del usuario actual: el cliente recibe los módulos de su
// tenant y todas las funcionalidades de esos módulos; el trabajador, solo sus
// funcionalidades asignadas.
public sealed record ObtenerPermisosActualesQuery(Guid ClienteId, Guid? TrabajadorId)
    : IRequest<PermisosActuales>;

public sealed record PermisosActuales(
    IReadOnlyList<string> Modulos, IReadOnlyList<string> Funcionalidades);

public sealed class ObtenerPermisosActualesHandler
    : IRequestHandler<ObtenerPermisosActualesQuery, PermisosActuales>
{
    private readonly IConsultaPermisosActuales _consulta;

    public ObtenerPermisosActualesHandler(IConsultaPermisosActuales consulta) => _consulta = consulta;

    public Task<PermisosActuales> Handle(
        ObtenerPermisosActualesQuery request, CancellationToken cancellationToken) =>
        _consulta.ObtenerAsync(request.ClienteId, request.TrabajadorId, cancellationToken);
}
