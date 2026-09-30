using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Application.Kiosco;
using Icarus.ControlAcceso.Application.Notificaciones;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using Icarus.ControlAcceso.Application.Trabajadores;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Marcaciones;

public sealed class RegistrarMarcacionHandler
    : IRequestHandler<RegistrarMarcacionCommand, ResultadoMarcacion>
{
    private static readonly TimeSpan VigenciaPropuesta = TimeSpan.FromSeconds(60);
    private static readonly HashSet<string> RechazosFacialesConcluyentes =
    [
        "sin_coincidencia",
        "ambigua",
        "pad_fallido",
        "sin_rostro",
        "varios_rostros",
        "modelo_incompatible",
    ];

    private readonly IRepositorioOperacionesMarcacion _operaciones;
    private readonly IRepositorioFlujosMarcacion _flujos;
    private readonly IRepositorioCapturasMarcacion _capturas;
    private readonly IRepositorioIncidenciasAcceso _incidencias;
    private readonly INotificacionesInternasAcceso _notificaciones;
    private readonly IRepositorioJornadasAcceso _jornadas;
    private readonly IRepositorioAccesoTrabajadores _trabajadores;
    private readonly IRepositorioPlantillasFaciales _plantillas;
    private readonly IRepositorioSesionesKiosco _sesiones;
    private readonly IProtectorPlantillas _protector;
    private readonly IProveedorIdentidadFacial _proveedor;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly IConsultaElegibilidadAcceso _elegibilidad;
    private readonly ICurrentUser _usuario;
    private readonly ICurrentKioscoSession _sesionKiosco;
    private readonly IRelojAcceso _reloj;

    public RegistrarMarcacionHandler(
        IRepositorioOperacionesMarcacion operaciones,
        IRepositorioFlujosMarcacion flujos,
        IRepositorioCapturasMarcacion capturas,
        IRepositorioIncidenciasAcceso incidencias,
        INotificacionesInternasAcceso notificaciones,
        IRepositorioJornadasAcceso jornadas,
        IRepositorioAccesoTrabajadores trabajadores,
        IRepositorioPlantillasFaciales plantillas,
        IRepositorioSesionesKiosco sesiones,
        IProtectorPlantillas protector,
        IProveedorIdentidadFacial proveedor,
        IUnidadTrabajoControlAcceso unidad,
        IConsultaElegibilidadAcceso elegibilidad,
        ICurrentUser usuario,
        ICurrentKioscoSession sesionKiosco,
        IRelojAcceso reloj)
    {
        _operaciones = operaciones;
        _flujos = flujos;
        _capturas = capturas;
        _incidencias = incidencias;
        _notificaciones = notificaciones;
        _jornadas = jornadas;
        _trabajadores = trabajadores;
        _plantillas = plantillas;
        _sesiones = sesiones;
        _protector = protector;
        _proveedor = proveedor;
        _unidad = unidad;
        _elegibilidad = elegibilidad;
        _usuario = usuario;
        _sesionKiosco = sesionKiosco;
        _reloj = reloj;
    }

    public async Task<ResultadoMarcacion> Handle(
        RegistrarMarcacionCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var sesionKioscoId = _sesionKiosco.SesionKioscoId
            ?? throw new UnauthorizedAccessException("La petición no viene de un kiosco.");
        var ahora = _reloj.ObtenerInstanteUtc();

        // Idempotencia de éxito: una marcación confirmada responde siempre igual.
        var operacionExistente = await _operaciones.ObtenerPorClaveAsync(
            clienteId, request.ClaveIdempotencia, cancellationToken);
        if (operacionExistente is not null)
            return await ReconstruirDesdeOperacionAsync(
                clienteId, operacionExistente, request.Accion, ahora, cancellationToken);

        var flujo = await ResolverFlujoAsync(
            clienteId, sesionKioscoId, request, ahora, cancellationToken);
        if (flujo is null)
            return Rechazada("flujo_invalido", request.FlujoId);

        // Idempotencia de captura: la misma clave devuelve el resultado anterior.
        if (await _capturas.ExistePorClaveAsync(request.ClaveIdempotencia, cancellationToken))
        {
            var intentosConsumidos = await _capturas.ContarRechazosPorFlujoAsync(flujo.Id, cancellationToken);
            return Rechazada("reintentar", flujo.Id, intentosConsumidos);
        }

        if (!flujo.PuedeContinuar(request.Accion))
            return Rechazada("accion_incompatible", flujo.Id);

        var identificacion = await IdentificarAsync(request, cancellationToken);
        if (identificacion.EsErrorTecnico)
            return RechazadaTecnica("proveedor_no_disponible", flujo.Id);

        if (!identificacion.Resultado!.Identificado || identificacion.Resultado.TrabajadorId is not { } trabajadorId)
        {
            var motivo = identificacion.Resultado.Motivo ?? "sin_coincidencia";
            if (!RechazosFacialesConcluyentes.Contains(motivo))
                return RechazadaTecnica(motivo, flujo.Id);

            return await RegistrarRechazoAsync(
                clienteId, flujo, request.ClaveIdempotencia, motivo, ahora, cancellationToken);
        }

        return await RegistrarExitoAsync(
            clienteId, flujo, request.ClaveIdempotencia, trabajadorId, request.Accion,
            ahora, cancellationToken);
    }

    private async Task<FlujoMarcacion?> ResolverFlujoAsync(
        Guid clienteId,
        Guid sesionKioscoId,
        RegistrarMarcacionCommand request,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (request.FlujoId.HasValue)
            return await _flujos.ObtenerPorIdAsync(request.FlujoId.Value, cancellationToken);

        var sesion = await _sesiones.ObtenerPorIdAsync(sesionKioscoId, cancellationToken);
        if (sesion is null || sesion.ClienteId != clienteId)
            return null;

        var flujo = new FlujoMarcacion(clienteId, sesionKioscoId, request.Accion, ahora);
        _flujos.Agregar(flujo);
        return flujo;
    }

    private async Task<(ResultadoIdentificacionFacial? Resultado, bool EsErrorTecnico)> IdentificarAsync(
        RegistrarMarcacionCommand request, CancellationToken cancellationToken)
    {
        List<CandidatoFacial> candidatos;
        try
        {
            candidatos = await ConstruirCandidatosAsync(
                _usuario.ClienteId!.Value, cancellationToken);
            if (candidatos.Count == 0)
                return (ResultadoIdentificacionFacial.SinCoincidencia("sin_candidatos"), false);

            var resultado = await _proveedor.IdentificarAsync(
                new MuestraFacial(request.Muestra, request.Formato), candidatos, cancellationToken);
            return (resultado, false);
        }
        catch (Exception)
        {
            return (null, true);
        }
    }

    private async Task<ResultadoMarcacion> RegistrarRechazoAsync(
        Guid clienteId,
        FlujoMarcacion flujo,
        Guid claveCaptura,
        string motivo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var intentosPrevios = await _capturas.ContarRechazosPorFlujoAsync(flujo.Id, cancellationToken);
        var (capturaNueva, creaIncidencia) = flujo.RegistrarRechazo(
            claveCaptura, motivo, ahora, intentosPrevios);
        _capturas.Agregar(capturaNueva, flujo.Id);

        if (creaIncidencia)
        {
            var rechazos = await _capturas.ListarRechazosPorFlujoAsync(flujo.Id, cancellationToken);
            var primerRechazo = rechazos[0].InstanteUtc;
            var tercerRechazo = rechazos[^1].InstanteUtc;
            var incidencia = new IncidenciaAcceso(
                clienteId, flujo.Id, flujo.SesionKioscoId, flujo.Accion,
                primerRechazo, tercerRechazo, ahora);
            _incidencias.Agregar(incidencia);
            _notificaciones.Agregar(NotificacionInternaAcceso.ParaIncidencia(incidencia.Id, clienteId));
        }

        try
        {
            await _unidad.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            return Rechazada("conflicto", flujo.Id);
        }

        var intentosTotales = intentosPrevios + 1;
        return creaIncidencia
            ? new ResultadoMarcacion(
                EstadoResultadoMarcacion.Incidencia, null, null, null, null, null, null, null,
                flujo.Id, intentosTotales, FlujoMarcacion.MaximoIntentos)
            : Rechazada(motivo, flujo.Id, intentosTotales);
    }

    private async Task<ResultadoMarcacion> RegistrarExitoAsync(
        Guid clienteId,
        FlujoMarcacion flujo,
        Guid claveCaptura,
        Guid trabajadorId,
        TipoMarcacion accion,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        // Revalidación justo antes de confirmar: una plantilla revocada o
        // sustituida no puede crear una marcación.
        var configuracion = await _trabajadores.ObtenerConfiguracionAsync(
            clienteId, trabajadorId, cancellationToken);
        var plantillaActiva = await _plantillas.ObtenerActivaAsync(
            clienteId, trabajadorId, cancellationToken);
        if (configuracion is null || !configuracion.Habilitado ||
            configuracion.Enrolamiento != EstadoEnrolamiento.Vigente ||
            plantillaActiva is null ||
            plantillaActiva.VersionEnrolamiento != configuracion.VersionEnrolamiento)
        {
            return await RegistrarRechazoAsync(
                clienteId, flujo, claveCaptura, "trabajador_no_habilitado", ahora, cancellationToken);
        }

        var fecha = _reloj.ObtenerFechaBolivia(ahora);
        var jornada = await _jornadas.ObtenerAsync(clienteId, trabajadorId, fecha, cancellationToken);
        var hayEntradaAbierta = jornada?.Marcaciones.LastOrDefault()?.Tipo == TipoMarcacion.Entrada;

        if (accion == TipoMarcacion.Salida && !hayEntradaAbierta)
            return Rechazada("salida_sin_entrada", null);

        var capturaExito = flujo.RegistrarExito(claveCaptura, trabajadorId, ahora);
        _capturas.Agregar(capturaExito, flujo.Id);

        if (accion == TipoMarcacion.Entrada && hayEntradaAbierta)
            return await ProponerSalidaAsync(
                clienteId, trabajadorId, claveCaptura, flujo, ahora, cancellationToken);

        return await ConfirmarAsync(
            clienteId, trabajadorId, jornada, accion, claveCaptura, flujo, fecha, ahora, cancellationToken);
    }

    private async Task<ResultadoMarcacion> ReconstruirDesdeOperacionAsync(
        Guid clienteId, OperacionMarcacion existente, TipoMarcacion accion,
        DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        if (existente.Accion != accion)
            return Rechazada("clave_reutilizada", existente.Id);

        if (existente.Estado == EstadoOperacionMarcacion.Reservada)
            return existente.EstaVigente(ahora)
                ? await PropuestaAsync(clienteId, existente, null, cancellationToken)
                : Rechazada("propuesta_vencida", existente.Id);

        if (existente.Estado == EstadoOperacionMarcacion.Confirmada)
        {
            var nombre = await _elegibilidad.ObtenerNombreTrabajadorAsync(
                clienteId, existente.TrabajadorId, cancellationToken);
            return new ResultadoMarcacion(
                EstadoResultadoMarcacion.Registrada, existente.TrabajadorId, nombre,
                existente.Accion, existente.CreadaEnUtc, null, null, null);
        }

        return Rechazada(existente.Motivo ?? "operacion_previa", existente.Id);
    }

    private async Task<ResultadoMarcacion> ProponerSalidaAsync(
        Guid clienteId, Guid trabajadorId, Guid clave, FlujoMarcacion flujo,
        DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var propuesta = OperacionMarcacion.ReservarPropuestaSalida(
            clienteId, trabajadorId, clave, ahora, ahora.Add(VigenciaPropuesta));
        _operaciones.Agregar(propuesta);

        try
        {
            await _unidad.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            return Rechazada("conflicto", flujo.Id);
        }

        return await PropuestaAsync(clienteId, propuesta, flujo.Id, cancellationToken);
    }

    private async Task<ResultadoMarcacion> PropuestaAsync(
        Guid clienteId, OperacionMarcacion propuesta, Guid? flujoId,
        CancellationToken cancellationToken)
    {
        var nombre = await _elegibilidad.ObtenerNombreTrabajadorAsync(
            clienteId, propuesta.TrabajadorId, cancellationToken);
        return new ResultadoMarcacion(
            EstadoResultadoMarcacion.PropuestaSalida, propuesta.TrabajadorId, nombre,
            null, null, propuesta.Id, propuesta.ExpiraEnUtc, null, flujoId);
    }

    private async Task<ResultadoMarcacion> ConfirmarAsync(
        Guid clienteId, Guid trabajadorId, JornadaAcceso? jornada, TipoMarcacion accion,
        Guid clave, FlujoMarcacion flujo, DateOnly fecha, DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        jornada ??= await _jornadas.ObtenerOCrearAsync(
            clienteId, trabajadorId, fecha, cancellationToken);

        if (jornada.ContieneClave(clave))
            return await RegistradaAsync(clienteId, trabajadorId, accion, flujo.Id, ahora, cancellationToken);

        try
        {
            jornada.RegistrarMarcacion(accion, ahora, fecha, clave, OrigenMarcacion.Kiosco);
        }
        catch (ReglaNegocioException)
        {
            return RechazadaSecuencia("secuencia_invalida", flujo.Id);
        }

        _operaciones.Agregar(OperacionMarcacion.Confirmada(
            clienteId, trabajadorId, clave, accion, jornada.Id, ahora));

        try
        {
            await _unidad.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            return Rechazada("conflicto", flujo.Id);
        }

        return await RegistradaAsync(clienteId, trabajadorId, accion, flujo.Id, ahora, cancellationToken);
    }

    private async Task<ResultadoMarcacion> RegistradaAsync(
        Guid clienteId, Guid trabajadorId, TipoMarcacion accion, Guid? flujoId,
        DateTimeOffset instante, CancellationToken cancellationToken)
    {
        var nombre = await _elegibilidad.ObtenerNombreTrabajadorAsync(
            clienteId, trabajadorId, cancellationToken);
        return new ResultadoMarcacion(
            EstadoResultadoMarcacion.Registrada, trabajadorId, nombre,
            accion, instante, null, null, null, flujoId);
    }

    private async Task<List<CandidatoFacial>> ConstruirCandidatosAsync(
        Guid clienteId, CancellationToken cancellationToken)
    {
        var configuraciones = await _trabajadores.ListarHabilitadosAsync(clienteId, cancellationToken);
        var candidatos = new List<CandidatoFacial>();
#pragma warning disable S3267 // El bucle hace descifrado y consultas asíncronas por candidato.
        foreach (var configuracion in configuraciones)
        {
            var plantilla = await _plantillas.ObtenerActivaAsync(
                clienteId, configuracion.TrabajadorId, cancellationToken);
            if (plantilla is null)
                continue;

            var vector = _protector.Recuperar(plantilla);
            if (vector is null)
                continue;

            candidatos.Add(new CandidatoFacial(
                configuracion.TrabajadorId, vector, plantilla.ModeloFormato,
                VersionModelo: 1, plantilla.VersionEnrolamiento));
        }
#pragma warning restore S3267

        return candidatos;
    }

    private static ResultadoMarcacion Rechazada(string motivo, Guid? flujoId, int? intentos = null) =>
        new(EstadoResultadoMarcacion.Rechazada, null, null, null, null, null, null, motivo,
            flujoId, intentos, FlujoMarcacion.MaximoIntentos);

    private static ResultadoMarcacion RechazadaSecuencia(string motivo, Guid flujoId) =>
        new(EstadoResultadoMarcacion.Rechazada, null, null, null, null, null, null, motivo,
            flujoId, null, FlujoMarcacion.MaximoIntentos);

    private static ResultadoMarcacion RechazadaTecnica(string motivo, Guid flujoId) =>
        new(EstadoResultadoMarcacion.Rechazada, null, null, null, null, null, null, motivo,
            flujoId, null, FlujoMarcacion.MaximoIntentos);
}
