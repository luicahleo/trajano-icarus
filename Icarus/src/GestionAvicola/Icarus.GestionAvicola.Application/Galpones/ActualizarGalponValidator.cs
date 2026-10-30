using FluentValidation;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed class ActualizarGalponValidator : AbstractValidator<ActualizarGalponCommand>
{ public ActualizarGalponValidator() { RuleFor(c => c.Numero).NotEmpty().MaximumLength(10); RuleFor(c => c.CapacidadMaxima).GreaterThan(0); RuleFor(c => c.Descripcion).MaximumLength(500); RuleFor(c => c.FechaNacimientoLote).Must(f => f <= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("La fecha de nacimiento del lote no puede ser futura."); RuleFor(c => c.FechaDescarteLote).Must(f => f == null || f <= DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("La fecha de descarte del lote no puede ser futura."); } }
