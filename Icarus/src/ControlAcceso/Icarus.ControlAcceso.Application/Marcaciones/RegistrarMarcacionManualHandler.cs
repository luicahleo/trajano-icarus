using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Marcaciones;

public sealed class RegistrarMarcacionManualHandler
    : IRequestHandler<RegistrarMarcacionManualCommand, Guid>
{
    private readonly IRepositorioJornadasAcceso _jornadas;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly IConsultaElegibilidadAcceso _elegibilidad;
    private readonly ICurrentUser _usuario;
    private readonly IRelojAcceso _reloj;

    public RegistrarMarcacionManualHandler(
        IRepositorioJornadasAcceso jornadas,
        IUnidadTrabajoControlAcceso unidad,
        IConsultaElegibilidadAcceso elegibilidad,
        ICurrentUser usuario,
        IRelojAcceso reloj)
    {
        _jornadas = jornadas;
        _unidad = unidad;
        _elegibilidad = elegibilidad;
        _usuario = usuario;
        _reloj = reloj;
    }

    public async Task<Guid> Handle(
        RegistrarMarcacionManualCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var autorId = _usuario.UsuarioId
            ?? throw new UnauthorizedAccessException("El registro manual requiere un usuario.");

        // El trabajador debe pertenecer al tenant; no se confía en el id recibido.
        var elegibilidad = await _elegibilidad.EvaluarAsync(
            clienteId, request.TrabajadorId, cancellationToken);
        if (!elegibilidad.PerteneceAlTenant)
            throw new NotFoundException("Trabajador", request.TrabajadorId);

        var creadaEnUtc = _reloj.ObtenerInstanteUtc();
        // Se rechaza antes de crear la jornada: una hora futura no debe dejar
        // una jornada vacía del día siguiente.
        if (request.HoraDeclaradaUtc > creadaEnUtc.AddMinutes(1))
            throw new ReglaNegocioException("No se admiten marcaciones futuras.");
        var fechaBoliviana = _reloj.ObtenerFechaBolivia(request.HoraDeclaradaUtc);

        var jornada = await _jornadas.ObtenerOCrearAsync(
            clienteId, request.TrabajadorId, fechaBoliviana, cancellationToken);

        // Reintento idempotente: la misma clave no duplica la marcación.
        if (jornada.ContieneClave(request.ClaveIdempotencia))
            return jornada.Id;

        jornada.RegistrarMarcacionManual(
            request.Tipo,
            request.HoraDeclaradaUtc,
            fechaBoliviana,
            request.ClaveIdempotencia,
            autorId,
            request.Motivo,
            creadaEnUtc);

        await _unidad.SaveChangesAsync(cancellationToken);
        return jornada.Id;
    }
}
