using Icarus.BuildingBlocks.Domain;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Application.Trabajadores;
using Icarus.Identity.Application.Usuarios;
using Icarus.Identity.Domain;
using MediatR;

namespace Icarus.Host.Servicios;

// Orquestación de la cuenta embebida (spec). Clientes no referencia Identity:
// este servicio es el único punto que une ambos módulos. Si la cuenta no se
// puede registrar, la entidad recién creada se deja no operativa (soft delete,
// glosario) y se devuelve un conflicto genérico (anti-PII). Email y contrasena
// nunca van a logs ni a mensajes de error.
public sealed class AltaCuentasServicio
{
    private readonly ISender _mediator;
    private readonly IRegistradorUsuarios _registrador;

    public AltaCuentasServicio(ISender mediator, IRegistradorUsuarios registrador)
    {
        _mediator = mediator;
        _registrador = registrador;
    }

    public async Task<Guid> CrearClienteConCuentaAsync(
        CrearClienteCommand comando, CancellationToken cancellationToken)
    {
        var clienteId = await _mediator.Send(comando, cancellationToken);
        var cuentaId = await _registrador.RegistrarAsync(
            comando.Email, comando.Contrasena, nameof(Rol.Cliente), clienteId, null, cancellationToken);
        if (cuentaId is null)
        {
            // Compensación por soft delete: el cliente recién creado queda no
            // operativo (invisible para las consultas normales).
            await _mediator.Send(new SuspenderClienteCommand(clienteId), cancellationToken);
            throw new ConflictException("No se pudo registrar el cliente.");
        }
        return clienteId;
    }

    public async Task<Guid> CrearTrabajadorConCuentaAsync(
        CrearTrabajadorCommand comando, CancellationToken cancellationToken)
    {
        var trabajadorId = await _mediator.Send(comando, cancellationToken);
        var cuentaId = await _registrador.RegistrarAsync(
            comando.Email, comando.Contrasena, nameof(Rol.Trabajador), comando.ClienteId, trabajadorId,
            cancellationToken);
        if (cuentaId is null)
        {
            await _mediator.Send(new DesactivarTrabajadorCommand(trabajadorId), cancellationToken);
            throw new ConflictException("No se pudo registrar el trabajador.");
        }
        return trabajadorId;
    }
}
