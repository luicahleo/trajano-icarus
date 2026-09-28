using FluentValidation;

namespace Icarus.ControlAcceso.Application.Trabajadores;

public sealed class DefinirHabilitacionValidator : AbstractValidator<DefinirHabilitacionCommand>
{
    public DefinirHabilitacionValidator()
    {
        RuleFor(c => c.TrabajadorId).NotEmpty();
    }
}
