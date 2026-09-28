using MediatR;

namespace Icarus.ControlAcceso.Application.Trabajadores;

// Habilitar o deshabilitar es reversible y no toca la plantilla protegida.
public sealed record DefinirHabilitacionCommand(Guid TrabajadorId, bool Habilitado) : IRequest;
