using FluentValidation;
using Icarus.Identity.Domain;

namespace Icarus.Identity.Application.Usuarios;

public sealed class CrearUsuarioValidator : AbstractValidator<CrearUsuarioCommand>
{
    public CrearUsuarioValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Contrasena).NotEmpty().MinimumLength(12);
        RuleFor(c => c.Rol).NotEmpty()
            .Must(rol => Enum.TryParse<Rol>(rol, ignoreCase: true, out _))
            .WithMessage("Rol inválido.");
        RuleFor(c => c.ClienteId).NotNull()
            .When(c => Enum.TryParse<Rol>(c.Rol, ignoreCase: true, out var rol) && ReglasRol.RequiereCliente(rol))
            .WithMessage("El rol exige una empresa asignada.");
    }
}
