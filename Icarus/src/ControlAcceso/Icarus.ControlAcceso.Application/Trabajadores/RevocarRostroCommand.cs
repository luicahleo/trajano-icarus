using MediatR;

namespace Icarus.ControlAcceso.Application.Trabajadores;

// Revocación local atómica: invalida la versión y elimina el contenido cifrado
// activo. No espera a ARGOS ni envía un DELETE remoto.
public sealed record RevocarRostroCommand(Guid TrabajadorId) : IRequest;
