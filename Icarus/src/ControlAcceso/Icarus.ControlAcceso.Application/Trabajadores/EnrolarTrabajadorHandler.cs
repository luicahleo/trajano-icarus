using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Biometria;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Trabajadores;

public sealed class EnrolarTrabajadorHandler
    : IRequestHandler<EnrolarTrabajadorCommand, ResultadoEnrolarTrabajador>
{
    private readonly IRepositorioAccesoTrabajadores _repositorio;
    private readonly IRepositorioPlantillasFaciales _plantillas;
    private readonly IProveedorIdentidadFacial _proveedor;
    private readonly IProtectorPlantillas _protector;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly IConsultaElegibilidadAcceso _elegibilidad;
    private readonly ICurrentUser _usuario;
    private readonly IRelojAcceso _reloj;

    public EnrolarTrabajadorHandler(
        IRepositorioAccesoTrabajadores repositorio,
        IRepositorioPlantillasFaciales plantillas,
        IProveedorIdentidadFacial proveedor,
        IProtectorPlantillas protector,
        IUnidadTrabajoControlAcceso unidad,
        IConsultaElegibilidadAcceso elegibilidad,
        ICurrentUser usuario,
        IRelojAcceso reloj)
    {
        _repositorio = repositorio;
        _plantillas = plantillas;
        _proveedor = proveedor;
        _protector = protector;
        _unidad = unidad;
        _elegibilidad = elegibilidad;
        _usuario = usuario;
        _reloj = reloj;
    }

    public async Task<ResultadoEnrolarTrabajador> Handle(
        EnrolarTrabajadorCommand request, CancellationToken cancellationToken)
    {
        var clienteId = _usuario.ClienteId
            ?? throw new UnauthorizedAccessException("La sesión no pertenece a un cliente.");
        var elegibilidad = await _elegibilidad.EvaluarAsync(
            clienteId, request.TrabajadorId, cancellationToken);
        if (!elegibilidad.PerteneceAlTenant)
            throw new NotFoundException("Trabajador", request.TrabajadorId);

        // Idempotencia: un reintento con la misma clave devuelve el resultado
        // local, sin volver a llamar a biometría ni duplicar la plantilla.
        var existente = await _repositorio.ObtenerOperacionAsync(
            clienteId, request.ClaveIdempotencia, cancellationToken);
        if (existente is not null)
            return existente.Estado == EstadoOperacionEnrolamiento.Confirmada
                ? new ResultadoEnrolarTrabajador(true, existente.VersionEnrolamiento, null)
                : new ResultadoEnrolarTrabajador(false, 0, existente.Motivo);

        var ahora = _reloj.ObtenerInstanteUtc();

        // Extracción y prueba de vida fuera de la transacción de confirmación.
        var extraccion = await _proveedor.ExtraerAsync(
            new MuestraFacial(request.Muestra, request.Formato), cancellationToken);

        if (!extraccion.Exitoso)
        {
            _repositorio.AgregarOperacion(OperacionEnrolamiento.Fallida(
                clienteId, request.TrabajadorId, request.ClaveIdempotencia,
                extraccion.Motivo ?? "extraccion_fallida", ahora));
            await _unidad.SaveChangesAsync(cancellationToken);
            return new ResultadoEnrolarTrabajador(false, 0, extraccion.Motivo);
        }

        var configuracion = await _repositorio.ObtenerOCrearConfiguracionAsync(
            clienteId, request.TrabajadorId, cancellationToken);
        var version = configuracion.VersionEnrolamiento + 1;

        // Sustitución: se revoca la plantilla anterior y se guarda la nueva con
        // una versión mayor; la confirmación habilita automáticamente.
        await _plantillas.RevocarActivaAsync(clienteId, request.TrabajadorId, cancellationToken);
        var protegida = _protector.Proteger(
            extraccion.Vector!, extraccion.ModeloFormato!, clienteId, request.TrabajadorId, version);
        await _plantillas.GuardarAsync(protegida, cancellationToken);

        configuracion.Enrolar(version);
        _repositorio.AgregarOperacion(OperacionEnrolamiento.Confirmada(
            clienteId, request.TrabajadorId, request.ClaveIdempotencia, version, ahora));
        await _unidad.SaveChangesAsync(cancellationToken);

        return new ResultadoEnrolarTrabajador(true, version, null);
    }
}
