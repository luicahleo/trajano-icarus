using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Marcaciones;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Incidencias;

public sealed class ResolverIncidenciaAccesoCommandHandler
    : IRequestHandler<ResolverIncidenciaAccesoCommand, Guid>
{
    private readonly IRepositorioIncidenciasAcceso _incidencias;
    private readonly IRepositorioJornadasAcceso _jornadas;
    private readonly IConsultaElegibilidadAcceso _elegibilidad;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly ICurrentUser _usuario;
    private readonly IRelojAcceso _reloj;

    public ResolverIncidenciaAccesoCommandHandler(
        IRepositorioIncidenciasAcceso incidencias,
        IRepositorioJornadasAcceso jornadas,
        IConsultaElegibilidadAcceso elegibilidad,
        IUnidadTrabajoControlAcceso unidad,
        ICurrentUser usuario,
        IRelojAcceso reloj)
    {
        _incidencias = incidencias;
        _jornadas = jornadas;
        _elegibilidad = elegibilidad;
        _unidad = unidad;
        _usuario = usuario;
        _reloj = reloj;
    }

    public async Task<Guid> Handle(
        ResolverIncidenciaAccesoCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var autorId = _usuario.UsuarioId
            ?? throw new UnauthorizedAccessException("La resolución requiere un usuario.");

        var incidencia = await _incidencias.ObtenerPorIdAsync(request.IncidenciaId, cancellationToken);
        if (incidencia is null || incidencia.ClienteId != clienteId)
            throw new NotFoundException("Incidencia", request.IncidenciaId);

        if (incidencia.Estado == EstadoIncidenciaAcceso.Resuelta)
            return incidencia.JornadaId ?? throw new InvalidOperationException("Incidencia resuelta sin jornada.");
        if (incidencia.Estado == EstadoIncidenciaAcceso.Descartada)
            throw new ConflictException("La incidencia ya fue descartada.");

        if (string.IsNullOrWhiteSpace(request.Motivo))
            throw new ReglaNegocioException("El motivo de la resolución es obligatorio.");

        if (request.VersionEsperada.HasValue && request.VersionEsperada.Value != incidencia.Version)
            throw new ConflictException("El registro cambió mientras se guardaba; reintente.");

        var elegibilidad = await _elegibilidad.EvaluarAsync(
            clienteId, request.TrabajadorId, cancellationToken);
        if (!elegibilidad.PerteneceAlTenant)
            throw new NotFoundException("Trabajador", request.TrabajadorId);

        var ahora = _reloj.ObtenerInstanteUtc();
        if (request.HoraDeclaradaUtc > ahora.AddMinutes(1))
            throw new ReglaNegocioException("No se admiten marcaciones futuras.");

        var fechaBoliviana = _reloj.ObtenerFechaBolivia(request.HoraDeclaradaUtc);
        var jornada = await _jornadas.ObtenerOCrearAsync(
            clienteId, request.TrabajadorId, fechaBoliviana, cancellationToken);

        if (jornada.ContieneClave(request.ClaveIdempotencia))
        {
            // La marcación ya existe; se completa la resolución si quedó pendiente.
            if (incidencia.Estado == EstadoIncidenciaAcceso.Pendiente)
            {
                incidencia.Resolver(request.TrabajadorId, jornada.Id, request.Motivo, ahora);
                await _unidad.SaveChangesAsync(cancellationToken);
            }

            return jornada.Id;
        }

        jornada.RegistrarMarcacionManual(
            request.Tipo,
            request.HoraDeclaradaUtc,
            fechaBoliviana,
            request.ClaveIdempotencia,
            autorId,
            request.Motivo,
            ahora);

        incidencia.Resolver(request.TrabajadorId, jornada.Id, request.Motivo, ahora);

        await _unidad.SaveChangesAsync(cancellationToken);
        return jornada.Id;
    }
}
