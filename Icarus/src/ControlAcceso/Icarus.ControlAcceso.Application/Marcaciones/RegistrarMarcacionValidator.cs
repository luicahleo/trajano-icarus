using FluentValidation;

namespace Icarus.ControlAcceso.Application.Marcaciones;

public sealed class RegistrarMarcacionValidator : AbstractValidator<RegistrarMarcacionCommand>
{
    public RegistrarMarcacionValidator()
    {
        RuleFor(c => c.ClaveIdempotencia).NotEmpty();
        RuleFor(c => c.Muestra).NotEmpty().WithMessage("La captura facial es obligatoria.");
        RuleFor(c => c.Formato).NotEmpty().MaximumLength(100);
    }
}

public sealed class ConfirmarSalidaValidator : AbstractValidator<ConfirmarSalidaCommand>
{
    public ConfirmarSalidaValidator()
    {
        RuleFor(c => c.PropuestaId).NotEmpty();
    }
}
