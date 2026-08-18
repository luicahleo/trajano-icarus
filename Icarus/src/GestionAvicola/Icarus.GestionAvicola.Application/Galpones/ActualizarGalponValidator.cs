using FluentValidation;
namespace Icarus.GestionAvicola.Application.Galpones;
public sealed class ActualizarGalponValidator : AbstractValidator<ActualizarGalponCommand>
{ public ActualizarGalponValidator() { RuleFor(c => c.Numero).NotEmpty().MaximumLength(10); RuleFor(c => c.CapacidadMaxima).GreaterThan(0); RuleFor(c => c.Descripcion).MaximumLength(500); } }
