using FluentValidation;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class CesarTrabajadorValidator : AbstractValidator<CesarTrabajadorCommand>
{
    public CesarTrabajadorValidator()
    {
        RuleFor(c => c.TrabajadorId).NotEmpty();
        RuleFor(c => c.FechaCese).NotEmpty();
    }
}
