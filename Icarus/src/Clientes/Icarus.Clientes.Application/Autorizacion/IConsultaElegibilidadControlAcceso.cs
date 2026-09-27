namespace Icarus.Clientes.Application.Autorizacion;

// Elegibilidad del tenant y del trabajador para el módulo ControlAcceso.
// El Host adapta este resultado al puerto de ControlAcceso; Clientes no
// conoce los roles ni las políticas del módulo consumidor.
public interface IConsultaElegibilidadControlAcceso
{
    Task<ElegibilidadControlAcceso> EvaluarAsync(
        Guid clienteId, Guid? trabajadorId, CancellationToken cancellationToken = default);
}

public sealed record ElegibilidadControlAcceso(
    bool PerteneceAlTenant,
    bool ClienteActivo,
    bool ModuloControlAccesoHabilitado,
    bool TrabajadorActivo,
    DateOnly? FechaCeseTrabajador)
{
    public bool PuedeAdministrar =>
        PerteneceAlTenant && ClienteActivo && ModuloControlAccesoHabilitado;

    public bool PuedeMarcar =>
        PerteneceAlTenant && ClienteActivo && ModuloControlAccesoHabilitado
        && TrabajadorActivo && !FechaCeseTrabajador.HasValue;
}
