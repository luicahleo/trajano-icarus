using FluentValidation;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class RenombrarGranjaValidator : AbstractValidator<RenombrarGranjaCommand>
{
    public RenombrarGranjaValidator() => RuleFor(c => c.Nombre).NotEmpty().MaximumLength(200);
}
