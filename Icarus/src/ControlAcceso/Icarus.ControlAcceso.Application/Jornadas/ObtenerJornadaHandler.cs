using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using MediatR;

namespace Icarus.ControlAcceso.Application.Jornadas;

public sealed class ObtenerJornadaHandler : IRequestHandler<ObtenerJornadaQuery, JornadaDetalle>
{
    private readonly IRepositorioJornadasAcceso _jornadas;
    private readonly ICurrentUser _usuario;
    private readonly IRelojAcceso _reloj;

    public ObtenerJornadaHandler(
        IRepositorioJornadasAcceso jornadas, ICurrentUser usuario, IRelojAcceso reloj)
    {
        _jornadas = jornadas;
        _usuario = usuario;
        _reloj = reloj;
    }

    public async Task<JornadaDetalle> Handle(
        ObtenerJornadaQuery request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");

        var jornada = await _jornadas.ObtenerPorIdAsync(request.JornadaId, cancellationToken);
        // El filtro de tenant ya oculta jornadas ajenas; el recurso inexistente
        // y el ajeno responden igual (anti-enumeración).
        if (jornada is null || jornada.ClienteId != clienteId)
            throw new NotFoundException("Jornada", request.JornadaId);

        var hoy = _reloj.ObtenerFechaBolivia();
        var marcaciones = jornada.Marcaciones
            .OrderBy(m => m.InstanteUtc)
            .Select(m => new MarcacionDetalle(
                m.Id, m.Tipo, m.Origen, m.InstanteUtc, m.HoraDeclaradaUtc ?? m.InstanteUtc,
                m.CreadaEnUtc, m.AutorId, m.Motivo))
            .ToList();
        var revisiones = jornada.Revisiones
            .Select(r => new RevisionDetalle(
                r.Id, r.InstanteCorreccionUtc, r.HastaSecuenciaOriginal, r.Motivo, r.AutorId,
                r.ValoresEfectivos
                    .Select(v => new ValorCorregido(v.Tipo, v.InstanteUtc))
                    .ToList()))
            .ToList();
        var efectivos = jornada.ValoresEfectivos(hoy)
            .Select(v => new ValorCorregido(v.Tipo, v.InstanteUtc))
            .ToList();

        return new JornadaDetalle(
            jornada.Id, jornada.TrabajadorId, jornada.FechaBoliviana, jornada.Estado(hoy),
            jornada.Version, marcaciones, revisiones, efectivos);
    }
}
