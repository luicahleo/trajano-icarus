using FluentValidation;

namespace Icarus.Clientes.Application.Clientes;

public sealed class CrearClienteValidator : AbstractValidator<CrearClienteCommand>
{
    public CrearClienteValidator()
    {
        RuleFor(c => c.RazonSocial).NotEmpty().MaximumLength(200);
        RuleFor(c => c.IdentificadorFiscal).NotEmpty().MaximumLength(32);
    }
}
