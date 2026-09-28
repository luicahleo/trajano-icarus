namespace Icarus.ControlAcceso.Application.Autorizacion;

// Puerto que el Host implementa consultando a Clientes. ControlAcceso no
// referencia Clientes ni Identity: solo ve el resultado de elegibilidad.
public interface IConsultaElegibilidadAcceso
{
    Task<ElegibilidadAcceso> EvaluarAsync(
        Guid clienteId, Guid? trabajadorId, CancellationToken cancellationToken = default);

    Task<string?> ObtenerNombreTrabajadorAsync(
        Guid clienteId, Guid trabajadorId, CancellationToken cancellationToken = default);
}

public sealed record ElegibilidadAcceso(
    bool PerteneceAlTenant,
    bool ClienteActivo,
    bool ModuloHabilitado,
    bool TrabajadorActivo,
    DateOnly? FechaCese)
{
    public bool PuedeAdministrar =>
        PerteneceAlTenant && ClienteActivo && ModuloHabilitado;

    public bool PuedeMarcar =>
        PerteneceAlTenant && ClienteActivo && ModuloHabilitado
        && TrabajadorActivo && !FechaCese.HasValue;
}
