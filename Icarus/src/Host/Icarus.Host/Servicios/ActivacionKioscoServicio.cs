using Icarus.ControlAcceso.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Kiosco;
using Icarus.ControlAcceso.Application.Persistencia;
using Icarus.ControlAcceso.Application.Tiempo;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Kiosco;
using Icarus.Identity.Application.Sesiones;
using Icarus.Identity.Domain;
using Microsoft.Extensions.Options;

namespace Icarus.Host.Servicios;

// Orquesta la activación y el cierre del kiosco. Vive en el Host porque cruza
// la verificación de credenciales de Identity con la persistencia de
// ControlAcceso; el módulo no referencia a Identity.
public sealed class ActivacionKioscoServicio
{
    private readonly IVerificadorCredenciales _verificador;
    private readonly IConsultaElegibilidadAcceso _elegibilidad;
    private readonly IRepositorioSesionesKiosco _sesiones;
    private readonly IUnidadTrabajoControlAcceso _unidad;
    private readonly IRelojAcceso _reloj;
    private readonly IOptions<OpcionesKiosco> _opciones;

    public ActivacionKioscoServicio(
        IVerificadorCredenciales verificador,
        IConsultaElegibilidadAcceso elegibilidad,
        IRepositorioSesionesKiosco sesiones,
        IUnidadTrabajoControlAcceso unidad,
        IRelojAcceso reloj,
        IOptions<OpcionesKiosco> opciones)
    {
        _verificador = verificador;
        _elegibilidad = elegibilidad;
        _sesiones = sesiones;
        _unidad = unidad;
        _reloj = reloj;
        _opciones = opciones;
    }

    public async Task<ResultadoActivacionKiosco?> ActivarAsync(
        string email, string contrasena, CancellationToken cancellationToken)
    {
        // Solo un Cliente del tenant con el módulo habilitado activa el kiosco.
        // El cliente nunca viaja en el cuerpo: se deriva de las credenciales.
        var credencial = await _verificador.VerificarAsync(email, contrasena, cancellationToken);
        if (credencial is null || credencial.ClienteId is not { } clienteId)
            return null;
        if (!string.Equals(credencial.Rol, nameof(Rol.Cliente), StringComparison.Ordinal))
            return null;

        var elegibilidad = await _elegibilidad.EvaluarAsync(clienteId, null, cancellationToken);
        if (!elegibilidad.PuedeAdministrar)
            return null;

        var ahora = _reloj.ObtenerInstanteUtc();
        var token = CredencialKiosco.GenerarToken();
        var sesion = new SesionKiosco(
            clienteId,
            CredencialKiosco.CalcularHash(token),
            ahora,
            ahora.AddHours(_opciones.Value.HorasVigencia));

        // Reemplazo transaccional: un solo kiosco vigente por tenant.
        await _sesiones.RevocarVigentesDeClienteAsync(clienteId, ahora, cancellationToken);
        _sesiones.Agregar(sesion);
        await _unidad.SaveChangesAsync(cancellationToken);

        return new ResultadoActivacionKiosco(sesion.Id, sesion.ExpiraEnUtc, token);
    }

    public async Task<bool> CerrarAsync(
        Guid sesionId, string email, string contrasena, CancellationToken cancellationToken)
    {
        var sesion = await _sesiones.ObtenerPorIdAsync(sesionId, cancellationToken);
        if (sesion is null || !sesion.EstaActiva)
            return false;

        // Salir del modo kiosco exige reautenticarse como el mismo cliente.
        var credencial = await _verificador.VerificarAsync(email, contrasena, cancellationToken);
        if (credencial is null || credencial.ClienteId != sesion.ClienteId)
            return false;
        if (!string.Equals(credencial.Rol, nameof(Rol.Cliente), StringComparison.Ordinal))
            return false;

        var elegibilidad = await _elegibilidad.EvaluarAsync(sesion.ClienteId, null, cancellationToken);
        if (!elegibilidad.PuedeAdministrar)
            return false;

        sesion.Revocar(_reloj.ObtenerInstanteUtc());
        await _unidad.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record ResultadoActivacionKiosco(
    Guid SesionId, DateTimeOffset ExpiraEnUtc, string Token);
