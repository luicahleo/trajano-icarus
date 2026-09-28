using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using MediatR;

namespace Icarus.ControlAcceso.Application.Jornadas;

public sealed class CorregirJornadaHandler : IRequestHandler<CorregirJornadaCommand, Guid>
{
    private readonly IRepositorioJornadasAcceso _jornadas;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly ICurrentUser _usuario;
    private readonly IRelojAcceso _reloj;

    public CorregirJornadaHandler(
        IRepositorioJornadasAcceso jornadas,
        IUnidadTrabajoControlAcceso unidad,
        ICurrentUser usuario,
        IRelojAcceso reloj)
    {
        _jornadas = jornadas;
        _unidad = unidad;
        _usuario = usuario;
        _reloj = reloj;
    }

    public async Task<Guid> Handle(CorregirJornadaCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var autorId = _usuario.UsuarioId
            ?? throw new UnauthorizedAccessException("La corrección requiere un usuario.");

        var jornada = await _jornadas.ObtenerPorIdAsync(request.JornadaId, cancellationToken);
        if (jornada is null || jornada.ClienteId != clienteId)
            throw new NotFoundException("Jornada", request.JornadaId);

        var ahora = _reloj.ObtenerInstanteUtc();
        // No se corrige hacia el futuro: la ventana declarada no puede superar
        // el instante real, con una tolerancia mínima por relojes.
        if (request.Valores.Any(v => v.InstanteUtc > ahora.AddMinutes(1)))
            throw new ReglaNegocioException("No se puede corregir a un instante futuro.");

        jornada.Corregir(
            ahora,
            _reloj.ObtenerFechaBolivia(),
            request.VersionEsperada,
            request.Motivo,
            autorId,
            request.Valores.Select(v => (v.Tipo, v.InstanteUtc)).ToArray());

        await _unidad.SaveChangesAsync(cancellationToken);
        return jornada.Id;
    }
}
