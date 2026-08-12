using Icarus.BuildingBlocks.Domain;
using Icarus.Identity.Domain;
using MediatR;

namespace Icarus.Identity.Application.Usuarios;

public sealed class CrearUsuarioHandler : IRequestHandler<CrearUsuarioCommand, Guid>
{
    private readonly IRegistradorUsuarios _registrador;

    public CrearUsuarioHandler(IRegistradorUsuarios registrador) => _registrador = registrador;

    public async Task<Guid> Handle(CrearUsuarioCommand request, CancellationToken cancellationToken)
    {
        // Normaliza el rol a su nombre canónico ("cliente" -> "Cliente").
        var rol = Enum.Parse<Rol>(request.Rol, ignoreCase: true).ToString();

        return await _registrador.RegistrarAsync(
                request.Email, request.Contrasena, rol, request.ClienteId, request.TrabajadorId, cancellationToken)
            ?? throw new ConflictException("No se pudo registrar la cuenta.");
    }
}
