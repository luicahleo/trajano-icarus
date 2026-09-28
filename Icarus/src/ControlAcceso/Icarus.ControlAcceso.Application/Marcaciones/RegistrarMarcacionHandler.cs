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

public sealed class RegistrarMarcacionHandler
    : IRequestHandler<RegistrarMarcacionCommand, ResultadoMarcacion>
{
    private static readonly TimeSpan VigenciaPropuesta = TimeSpan.FromSeconds(60);

    private readonly IRepositorioOperacionesMarcacion _operaciones;
    private readonly IRepositorioJornadasAcceso _jornadas;
    private readonly IRepositorioAccesoTrabajadores _trabajadores;
    private readonly IRepositorioPlantillasFaciales _plantillas;
    private readonly IProtectorPlantillas _protector;
    private readonly IProveedorIdentidadFacial _proveedor;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly IConsultaElegibilidadAcceso _elegibilidad;
    private readonly ICurrentUser _usuario;
    private readonly IRelojAcceso _reloj;

    public RegistrarMarcacionHandler(
        IRepositorioOperacionesMarcacion operaciones,
        IRepositorioJornadasAcceso jornadas,
        IRepositorioAccesoTrabajadores trabajadores,
        IRepositorioPlantillasFaciales plantillas,
        IProtectorPlantillas protector,
        IProveedorIdentidadFacial proveedor,
        IUnidadTrabajoControlAcceso unidad,
        IConsultaElegibilidadAcceso elegibilidad,
        ICurrentUser usuario,
        IRelojAcceso reloj)
    {
        _operaciones = operaciones;
        _jornadas = jornadas;
        _trabajadores = trabajadores;
        _plantillas = plantillas;
        _protector = protector;
        _proveedor = proveedor;
        _unidad = unidad;
        _elegibilidad = elegibilidad;
        _usuario = usuario;
        _reloj = reloj;
    }

    public async Task<ResultadoMarcacion> Handle(
        RegistrarMarcacionCommand request, CancellationToken cancellationToken)
    {
        // La sesión del kiosco aporta el tenant; el dispositivo nunca lo elige.
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var ahora = _reloj.ObtenerInstanteUtc();

        // Idempotencia: un reintento no repite identificación ni marcación.
        var existente = await _operaciones.ObtenerPorClaveAsync(
            clienteId, request.ClaveIdempotencia, cancellationToken);
        if (existente is not null)
            return await ReintentoAsync(clienteId, existente, request.Accion, ahora, cancellationToken);

        var candidatos = await ConstruirCandidatosAsync(clienteId, cancellationToken);
        if (candidatos.Count == 0)
            return Rechazada("sin_candidatos");

        var identificacion = await _proveedor.IdentificarAsync(
            new MuestraFacial(request.Muestra, request.Formato), candidatos, cancellationToken);
        if (!identificacion.Identificado || identificacion.TrabajadorId is not { } trabajadorId)
            return Rechazada(identificacion.Motivo ?? "sin_coincidencia");

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
            return Rechazada("trabajador_no_habilitado");

        var fecha = _reloj.ObtenerFechaBolivia(ahora);
        var jornada = await _jornadas.ObtenerAsync(clienteId, trabajadorId, fecha, cancellationToken);
        var hayEntradaAbierta = jornada?.Marcaciones.LastOrDefault()?.Tipo == TipoMarcacion.Entrada;

        if (request.Accion == TipoMarcacion.Salida && !hayEntradaAbierta)
            return Rechazada("salida_sin_entrada");

        if (request.Accion == TipoMarcacion.Entrada && hayEntradaAbierta)
            return await ProponerSalidaAsync(
                clienteId, trabajadorId, request.ClaveIdempotencia, ahora, cancellationToken);

        return await ConfirmarAsync(
            clienteId, trabajadorId, jornada, request.Accion,
            request.ClaveIdempotencia, fecha, ahora, cancellationToken);
    }

    private async Task<ResultadoMarcacion> ReintentoAsync(
        Guid clienteId, OperacionMarcacion existente, TipoMarcacion accion,
        DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        if (existente.Accion != accion)
            return Rechazada("clave_reutilizada");

        if (existente.Estado == EstadoOperacionMarcacion.Reservada)
            return existente.EstaVigente(ahora)
                ? await PropuestaAsync(clienteId, existente, cancellationToken)
                : Rechazada("propuesta_vencida");

        if (existente.Estado == EstadoOperacionMarcacion.Confirmada)
        {
            var nombre = await _elegibilidad.ObtenerNombreTrabajadorAsync(
                clienteId, existente.TrabajadorId, cancellationToken);
            return new ResultadoMarcacion(
                EstadoResultadoMarcacion.Registrada, existente.TrabajadorId, nombre,
                existente.Accion, existente.CreadaEnUtc, null, null, null);
        }

        return Rechazada(existente.Motivo ?? "operacion_previa");
    }

    private async Task<ResultadoMarcacion> ProponerSalidaAsync(
        Guid clienteId, Guid trabajadorId, Guid clave, DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var propuesta = OperacionMarcacion.ReservarPropuestaSalida(
            clienteId, trabajadorId, clave, ahora, ahora.Add(VigenciaPropuesta));
        _operaciones.Agregar(propuesta);
        await _unidad.SaveChangesAsync(cancellationToken);
        return await PropuestaAsync(clienteId, propuesta, cancellationToken);
    }

    private async Task<ResultadoMarcacion> PropuestaAsync(
        Guid clienteId, OperacionMarcacion propuesta, CancellationToken cancellationToken)
    {
        var nombre = await _elegibilidad.ObtenerNombreTrabajadorAsync(
            clienteId, propuesta.TrabajadorId, cancellationToken);
        return new ResultadoMarcacion(
            EstadoResultadoMarcacion.PropuestaSalida, propuesta.TrabajadorId, nombre,
            null, null, propuesta.Id, propuesta.ExpiraEnUtc, null);
    }

    private async Task<ResultadoMarcacion> ConfirmarAsync(
        Guid clienteId, Guid trabajadorId, JornadaAcceso? jornada, TipoMarcacion accion,
        Guid clave, DateOnly fecha, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        jornada ??= await _jornadas.ObtenerOCrearAsync(
            clienteId, trabajadorId, fecha, cancellationToken);

        if (jornada.ContieneClave(clave))
            return await RegistradaAsync(clienteId, trabajadorId, accion, ahora, cancellationToken);

        try
        {
            jornada.RegistrarMarcacion(accion, ahora, fecha, clave, OrigenMarcacion.Kiosco);
        }
        catch (ReglaNegocioException)
        {
            // Otra petición cambió la secuencia entre la lectura y la escritura.
            return Rechazada("secuencia_invalida");
        }

        _operaciones.Agregar(OperacionMarcacion.Confirmada(
            clienteId, trabajadorId, clave, accion, jornada.Id, ahora));

        try
        {
            await _unidad.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            return Rechazada("conflicto");
        }

        return await RegistradaAsync(clienteId, trabajadorId, accion, ahora, cancellationToken);
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

    private static ResultadoMarcacion Rechazada(string motivo) =>
        new(EstadoResultadoMarcacion.Rechazada, null, null, null, null, null, null, motivo);
}
