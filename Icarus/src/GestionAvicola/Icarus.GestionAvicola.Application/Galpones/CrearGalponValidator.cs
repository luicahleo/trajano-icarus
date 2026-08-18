using FluentValidation;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed class CrearGalponValidator : AbstractValidator<CrearGalponCommand>
{
    public CrearGalponValidator() { RuleFor(c => c.Numero).NotEmpty().MaximumLength(10); RuleFor(c => c.CapacidadMaxima).GreaterThan(0); RuleFor(c => c.GallinasActuales).GreaterThanOrEqualTo(0); RuleFor(c => c.FechaNacimientoLote).Must(f => f <= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("La fecha de nacimiento del lote no puede ser futura."); RuleFor(c => c.Descripcion).MaximumLength(500); }
}
