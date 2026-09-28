using Icarus.Clientes.Application.Autorizacion;
using Icarus.ControlAcceso.Application.Autorizacion;

namespace Icarus.Host.Servicios;

// Adaptador del Host: el puerto de ControlAcceso permanece libre de
// referencias a Clientes; esta implementación traduce desde la consulta
// estrecha del módulo Clientes.
public sealed class ConsultaElegibilidadAcceso : IConsultaElegibilidadAcceso
{
    private readonly IConsultaElegibilidadControlAcceso _consulta;

    public ConsultaElegibilidadAcceso(IConsultaElegibilidadControlAcceso consulta) =>
        _consulta = consulta;

    public async Task<ElegibilidadAcceso> EvaluarAsync(
        Guid clienteId, Guid? trabajadorId, CancellationToken cancellationToken = default)
    {
        var r = await _consulta.EvaluarAsync(clienteId, trabajadorId, cancellationToken);
        return new ElegibilidadAcceso(
            r.PerteneceAlTenant,
            r.ClienteActivo,
            r.ModuloControlAccesoHabilitado,
            r.TrabajadorActivo,
            r.FechaCeseTrabajador);
    }

    public Task<string?> ObtenerNombreTrabajadorAsync(
        Guid clienteId, Guid trabajadorId, CancellationToken cancellationToken = default) =>
        _consulta.ObtenerNombreTrabajadorAsync(clienteId, trabajadorId, cancellationToken);
}
