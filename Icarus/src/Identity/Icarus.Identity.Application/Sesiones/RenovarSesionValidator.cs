using FluentValidation;

namespace Icarus.Identity.Application.Sesiones;

public sealed class RenovarSesionValidator : AbstractValidator<RenovarSesionCommand>
{
    public RenovarSesionValidator() => RuleFor(c => c.RefreshToken).NotEmpty();
}
