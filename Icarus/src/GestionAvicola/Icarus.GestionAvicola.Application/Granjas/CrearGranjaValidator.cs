using FluentValidation;

namespace Icarus.GestionAvicola.Application.Granjas;

public sealed class CrearGranjaValidator : AbstractValidator<CrearGranjaCommand>
{
    public CrearGranjaValidator() => RuleFor(c => c.Nombre).NotEmpty().MaximumLength(200);
}
