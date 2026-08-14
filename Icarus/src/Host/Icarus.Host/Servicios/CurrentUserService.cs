using System.Security.Claims;
using Icarus.BuildingBlocks.Application;
using Icarus.Identity.Domain;

namespace Icarus.Host.Servicios;

public sealed class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Usuario => _accessor.HttpContext?.User;

    public bool EstaAutenticado => Usuario?.Identity?.IsAuthenticated == true && UsuarioId is not null;

    public Guid? UsuarioId =>
        Guid.TryParse(Usuario?.FindFirstValue(ClaimsIdentidad.Subject), out var id) ? id : null;

    public string? Rol => Usuario?.FindFirstValue(ClaimsIdentidad.Rol);

    public Guid? ClienteId =>
        Guid.TryParse(Usuario?.FindFirstValue(ClaimsIdentidad.ClienteId), out var id) ? id : null;

    public Guid? TrabajadorId =>
        Guid.TryParse(Usuario?.FindFirstValue(ClaimsIdentidad.TrabajadorId), out var id) ? id : null;
}
