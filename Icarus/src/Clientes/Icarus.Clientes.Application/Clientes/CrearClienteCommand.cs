using MediatR;
using Icarus.BuildingBlocks.Application.Observability;

namespace Icarus.Clientes.Application.Clientes;

// Alta embebida (spec): el Host recibe email y contrasena y crea la cuenta de
// acceso, mientras este handler solo crea el cliente. Email y contrasena no
// van a logs ni a mensajes de error (anti-PII).
public sealed record CrearClienteCommand(
    string RazonSocial, string IdentificadorFiscal, string Email, string Contrasena) : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "clientes.crear", new Dictionary<string, DatoRegistroVuelo>());
}
