using Icarus.BuildingBlocks.Domain;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Application.Trabajadores;
using Icarus.Identity.Application.RegistroCuentas;
using Icarus.Identity.Domain;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly IRegistroVuelo _registro;

    public AltaCuentasServicio(ISender mediator, IRegistradorUsuarios registrador, IRegistroVuelo? registro = null)
    {
        _mediator = mediator;
        _registrador = registrador;
        _registro = registro ?? new RegistroVuelo(NullLogger.Instance);
    }

    public async Task<Guid> CrearClienteConCuentaAsync(
        CrearClienteCommand comando, CancellationToken cancellationToken)
    {
        using var operacion = _registro.Iniciar(new DescriptorOperacionRegistroVuelo(
            "clientes.alta_con_cuenta", new Dictionary<string, DatoRegistroVuelo>()));
        try
        {
            await AsegurarEmailDisponibleAsync(comando.Email, cancellationToken, operacion);
            var clienteId = await _mediator.Send(comando, cancellationToken);
            var cuentaId = await _registrador.RegistrarAsync(
                comando.Email, comando.Contrasena, nameof(Rol.Cliente), clienteId, null, cancellationToken);
            if (cuentaId is null)
            {
                operacion.Decidir("identity_rejected", "rejected");
                using var compensacion = _registro.IniciarCompensacion("clientes.suspender_alta_incompleta");
                await _mediator.Send(new SuspenderClienteCommand(clienteId), cancellationToken);
                compensacion.Completar();
                operacion.Rechazar("identity_rejected");
                throw new ConflictException("No se pudo registrar el cliente.");
            }
            operacion.Completar();
            return clienteId;
        }
        catch (ConflictException)
        {
            operacion.Rechazar("business_rejected");
            throw;
        }
        catch { operacion.Fallar(); throw; }
    }

    public async Task<Guid> CrearTrabajadorConCuentaAsync(
        CrearTrabajadorCommand comando, CancellationToken cancellationToken)
    {
        await AsegurarEmailDisponibleAsync(comando.Email, cancellationToken, null);
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

    private async Task AsegurarEmailDisponibleAsync(string email, CancellationToken cancellationToken,
        IOperacionVuelo? operacion)
    {
        if (await _registrador.EstaEmailRegistradoAsync(email, cancellationToken))
        {
            operacion?.Decidir("account_identifier_unavailable", "rejected");
            operacion?.Rechazar("account_identifier_unavailable");
            throw new ConflictException("No se pudo registrar la cuenta.");
        }
        operacion?.Decidir("account_identifier_available", "succeeded");
    }
}
