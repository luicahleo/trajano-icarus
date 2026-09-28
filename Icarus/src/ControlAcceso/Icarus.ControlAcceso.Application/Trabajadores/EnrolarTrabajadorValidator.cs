using FluentValidation;

namespace Icarus.ControlAcceso.Application.Trabajadores;

public sealed class EnrolarTrabajadorValidator : AbstractValidator<EnrolarTrabajadorCommand>
{
    public EnrolarTrabajadorValidator()
    {
        RuleFor(c => c.TrabajadorId).NotEmpty();
        RuleFor(c => c.ClaveIdempotencia).NotEmpty();
        RuleFor(c => c.Muestra).NotEmpty().WithMessage("La captura facial es obligatoria.");
        RuleFor(c => c.Formato).NotEmpty().MaximumLength(100);
    }
}
