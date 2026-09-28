using FluentValidation;

namespace Icarus.ControlAcceso.Application.Trabajadores;

public sealed class RevocarRostroValidator : AbstractValidator<RevocarRostroCommand>
{
    public RevocarRostroValidator()
    {
        RuleFor(c => c.TrabajadorId).NotEmpty();
    }
}
