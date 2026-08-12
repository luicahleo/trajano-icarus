using System.Security.Claims;
using Icarus.BuildingBlocks.Application;

namespace Icarus.Host.Servicios;

public sealed class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Usuario => _accessor.HttpContext?.User;

    public bool EstaAutenticado => Usuario?.Identity?.IsAuthenticated == true && UsuarioId is not null;

    public Guid? UsuarioId =>
        Guid.TryParse(Usuario?.FindFirstValue("sub"), out var id) ? id : null;

    public string? Rol => Usuario?.FindFirstValue("rol");

    public Guid? ClienteId =>
        Guid.TryParse(Usuario?.FindFirstValue("clienteId"), out var id) ? id : null;
}
