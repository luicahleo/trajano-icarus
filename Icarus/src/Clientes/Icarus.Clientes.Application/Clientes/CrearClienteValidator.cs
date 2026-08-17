using FluentValidation;
using Icarus.Clientes.Application;
using Icarus.Clientes.Domain;

namespace Icarus.Clientes.Application.Clientes;

public sealed class CrearClienteValidator : AbstractValidator<CrearClienteCommand>
{
    public CrearClienteValidator()
    {
        RuleFor(c => c.RazonSocial).NotEmpty().MaximumLength(200);
        RuleFor(c => c.IdentificadorFiscal)
            .NotEmpty().WithMessage("El NIT es obligatorio.")
            .Must(NitBoliviano.TieneFormatoValido)
            .WithMessage("El NIT debe contener solo dígitos y tener como máximo 15 caracteres.");
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        ReglasContrasena.Aplicar(RuleFor(c => c.Contrasena));
    }
}
