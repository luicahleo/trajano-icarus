using FluentValidation;

namespace Icarus.Identity.Application.Sesiones;

public sealed class IniciarSesionValidator : AbstractValidator<IniciarSesionCommand>
{
    public IniciarSesionValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Contrasena).NotEmpty();
    }
}
