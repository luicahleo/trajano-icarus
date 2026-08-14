namespace Icarus.BuildingBlocks.Application;

public interface ICurrentUser
{
    bool EstaAutenticado { get; }
    Guid? UsuarioId { get; }
    string? Rol { get; }
    Guid? ClienteId { get; }
    Guid? TrabajadorId { get; }
}
