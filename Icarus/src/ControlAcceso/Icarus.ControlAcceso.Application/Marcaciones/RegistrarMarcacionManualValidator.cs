using FluentValidation;

namespace Icarus.ControlAcceso.Application.Marcaciones;

public sealed class RegistrarMarcacionManualValidator
    : AbstractValidator<RegistrarMarcacionManualCommand>
{
    public RegistrarMarcacionManualValidator()
    {
        RuleFor(c => c.TrabajadorId).NotEmpty();
        RuleFor(c => c.ClaveIdempotencia).NotEmpty();
        RuleFor(c => c.Motivo)
            .NotEmpty().WithMessage("El motivo del registro manual es obligatorio.")
            .MaximumLength(500);
    }
}
