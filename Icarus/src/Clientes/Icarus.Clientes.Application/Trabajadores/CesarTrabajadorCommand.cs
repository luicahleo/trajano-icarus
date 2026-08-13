using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed record CesarTrabajadorCommand(Guid TrabajadorId, DateOnly FechaCese) : IRequest;
