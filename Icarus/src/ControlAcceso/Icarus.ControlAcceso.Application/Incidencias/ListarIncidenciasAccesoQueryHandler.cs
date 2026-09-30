using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Incidencias;

public sealed class ListarIncidenciasAccesoQueryHandler
    : IRequestHandler<ListarIncidenciasAccesoQuery, Pagina<IncidenciaResumen>>
{
    private readonly IRepositorioIncidenciasAcceso _incidencias;
    private readonly ICurrentUser _usuario;

    public ListarIncidenciasAccesoQueryHandler(
        IRepositorioIncidenciasAcceso incidencias,
        ICurrentUser usuario)
    {
        _incidencias = incidencias;
        _usuario = usuario;
    }

    public async Task<Pagina<IncidenciaResumen>> Handle(
        ListarIncidenciasAccesoQuery request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");

        var peticion = new PeticionPaginada(request.Pagina, request.TamanoPagina);
        var items = await _incidencias.ListarAsync(clienteId, request.Estado, cancellationToken);

        var total = items.Count;
        var paginados = items
            .Skip(peticion.Salto)
            .Take(peticion.TamanoNormalizado)
            .Select(i => new IncidenciaResumen(
                i.Id,
                i.FlujoMarcacionId,
                i.Accion,
                i.PrimerRechazoUtc,
                i.TercerRechazoUtc,
                i.Estado,
                i.Version))
            .ToList();

        return new Pagina<IncidenciaResumen>(
            paginados,
            total,
            peticion.PaginaNormalizada,
            peticion.TamanoNormalizado);
    }
}
