using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using MediatR;

namespace Icarus.ControlAcceso.Application.Jornadas;

public sealed class ListarJornadasHandler
    : IRequestHandler<ListarJornadasQuery, Pagina<JornadaResumen>>
{
    private readonly IRepositorioJornadasAcceso _jornadas;
    private readonly ICurrentUser _usuario;
    private readonly IRelojAcceso _reloj;

    public ListarJornadasHandler(
        IRepositorioJornadasAcceso jornadas, ICurrentUser usuario, IRelojAcceso reloj)
    {
        _jornadas = jornadas;
        _usuario = usuario;
        _reloj = reloj;
    }

    public async Task<Pagina<JornadaResumen>> Handle(
        ListarJornadasQuery request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var hoy = _reloj.ObtenerFechaBolivia();

        var pagina = await _jornadas.ListarAsync(
            clienteId,
            request.TrabajadorId,
            request.Desde,
            request.Hasta,
            new PeticionPaginada(request.Pagina, request.TamanoPagina),
            cancellationToken);

        var items = pagina.Items
            .Select(j => new JornadaResumen(
                j.Id, j.TrabajadorId, j.FechaBoliviana, j.Estado(hoy), j.Marcaciones.Count, j.Version))
            .ToList();

        return new Pagina<JornadaResumen>(
            items, pagina.Total, pagina.NumeroPagina, pagina.TamanoPagina);
    }
}
