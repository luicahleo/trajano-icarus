using FluentValidation;

namespace Icarus.ControlAcceso.Application.Jornadas;

public sealed class CorregirJornadaValidator : AbstractValidator<CorregirJornadaCommand>
{
    public CorregirJornadaValidator()
    {
        RuleFor(c => c.JornadaId).NotEmpty();
        RuleFor(c => c.Motivo)
            .NotEmpty().WithMessage("El motivo de la corrección es obligatorio.")
            .MaximumLength(500);
        RuleFor(c => c.Valores).NotNull();
    }
}
