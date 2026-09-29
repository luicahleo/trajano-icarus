using System.Security.Claims;
using Icarus.ControlAcceso.Application.Kiosco;
using Icarus.ControlAcceso.Infrastructure.Kiosco;

namespace Icarus.Host.Servicios;

public sealed class CurrentKioscoSessionService : ICurrentKioscoSession
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentKioscoSessionService(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? SesionKioscoId =>
        Guid.TryParse(_accessor.HttpContext?.User.FindFirstValue(ClaimsKiosco.Subject), out var id)
            ? id
            : null;
}
