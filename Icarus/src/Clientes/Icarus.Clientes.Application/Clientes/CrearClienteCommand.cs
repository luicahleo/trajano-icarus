using MediatR;

namespace Icarus.Clientes.Application.Clientes;

// Sistema cerrado (spec): crear clientes es exclusivo del Administrador
// (endpoint con política en el Host).
public sealed record CrearClienteCommand(string RazonSocial, string IdentificadorFiscal) : IRequest<Guid>;
