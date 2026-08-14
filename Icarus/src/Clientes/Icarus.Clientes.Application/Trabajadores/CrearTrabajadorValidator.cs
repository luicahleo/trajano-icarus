using FluentValidation;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class CrearTrabajadorValidator : AbstractValidator<CrearTrabajadorCommand>
{
    public CrearTrabajadorValidator()
    {
        RuleFor(c => c.ClienteId).NotEmpty();
        RuleFor(c => c.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(c => c.DocumentoIdentidad).NotEmpty().MaximumLength(32);
        RuleFor(c => c.Cargo).NotEmpty().MaximumLength(100);
        RuleFor(c => c.FechaIngreso).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Contrasena).NotEmpty().MinimumLength(12);
    }
}
