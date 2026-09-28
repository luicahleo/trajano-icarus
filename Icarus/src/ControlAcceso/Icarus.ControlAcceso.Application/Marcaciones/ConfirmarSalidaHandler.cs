using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using Icarus.ControlAcceso.Application.Trabajadores;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Marcaciones;

public sealed class ConfirmarSalidaHandler
    : IRequestHandler<ConfirmarSalidaCommand, ResultadoMarcacion>
{
    private readonly IRepositorioOperacionesMarcacion _operaciones;
    private readonly IRepositorioJornadasAcceso _jornadas;
    private readonly IRepositorioAccesoTrabajadores _trabajadores;
    private readonly IRepositorioPlantillasFaciales _plantillas;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly IConsultaElegibilidadAcceso _elegibilidad;
    private readonly ICurrentUser _usuario;
    private readonly IRelojAcceso _reloj;

    public ConfirmarSalidaHandler(
        IRepositorioOperacionesMarcacion operaciones,
        IRepositorioJornadasAcceso jornadas,
        IRepositorioAccesoTrabajadores trabajadores,
        IRepositorioPlantillasFaciales plantillas,
        IUnidadTrabajoControlAcceso unidad,
        IConsultaElegibilidadAcceso elegibilidad,
        ICurrentUser usuario,
        IRelojAcceso reloj)
    {
        _operaciones = operaciones;
        _jornadas = jornadas;
        _trabajadores = trabajadores;
        _plantillas = plantillas;
        _unidad = unidad;
        _elegibilidad = elegibilidad;
        _usuario = usuario;
        _reloj = reloj;
    }

    public async Task<ResultadoMarcacion> Handle(
        ConfirmarSalidaCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var ahora = _reloj.ObtenerInstanteUtc();

        var propuesta = await _operaciones.ObtenerPorIdAsync(request.PropuestaId, cancellationToken);
        if (propuesta is null || propuesta.ClienteId != clienteId)
            return Rechazada("propuesta_inexistente");

        // Confirmación idempotente: si ya se registró, no repite el evento.
        if (propuesta.Estado == EstadoOperacionMarcacion.Confirmada)
            return await RegistradaAsync(
                clienteId, propuesta.TrabajadorId, propuesta.Accion, ahora, cancellationToken);

        if (!propuesta.EstaVigente(ahora))
            return Rechazada("propuesta_vencida");

        var trabajadorId = propuesta.TrabajadorId;
        var configuracion = await _trabajadores.ObtenerConfiguracionAsync(
            clienteId, trabajadorId, cancellationToken);
        var plantillaActiva = await _plantillas.ObtenerActivaAsync(
            clienteId, trabajadorId, cancellationToken);
        if (configuracion is null || !configuracion.Habilitado ||
            configuracion.Enrolamiento != EstadoEnrolamiento.Vigente ||
            plantillaActiva is null ||
            plantillaActiva.VersionEnrolamiento != configuracion.VersionEnrolamiento)
            return Rechazada("trabajador_no_habilitado");

        var fecha = _reloj.ObtenerFechaBolivia(ahora);
        var jornada = await _jornadas.ObtenerAsync(clienteId, trabajadorId, fecha, cancellationToken);
        if (jornada?.Marcaciones.LastOrDefault()?.Tipo != TipoMarcacion.Entrada)
            return Rechazada("salida_sin_entrada");

        try
        {
            jornada.RegistrarMarcacion(
                TipoMarcacion.Salida, ahora, fecha, propuesta.ClaveIdempotencia, OrigenMarcacion.Kiosco);
        }
        catch (ReglaNegocioException)
        {
            return Rechazada("secuencia_invalida");
        }

        propuesta.Confirmar(jornada.Id);
        try
        {
            await _unidad.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            return Rechazada("conflicto");
        }

        return await RegistradaAsync(clienteId, trabajadorId, TipoMarcacion.Salida, ahora, cancellationToken);
    }

    private async Task<ResultadoMarcacion> RegistradaAsync(
        Guid clienteId, Guid trabajadorId, TipoMarcacion accion, DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        var nombre = await _elegibilidad.ObtenerNombreTrabajadorAsync(
            clienteId, trabajadorId, cancellationToken);
        return new ResultadoMarcacion(
            EstadoResultadoMarcacion.Registrada, trabajadorId, nombre,
            accion, instante, null, null, null);
    }

    private static ResultadoMarcacion Rechazada(string motivo) =>
        new(EstadoResultadoMarcacion.Rechazada, null, null, null, null, null, null, motivo);
}
