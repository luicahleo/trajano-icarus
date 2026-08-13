using FluentValidation;
using Icarus.Clientes.Domain;

namespace Icarus.Clientes.Application.Clientes;

public sealed class DefinirModulosClienteValidator : AbstractValidator<DefinirModulosClienteCommand>
{
    public DefinirModulosClienteValidator()
    {
        RuleFor(c => c.ClienteId).NotEmpty();
        RuleFor(c => c.Modulos).NotNull();
        RuleForEach(c => c.Modulos)
            .Must(nombre =>
                Enum.TryParse<Modulos>(nombre, ignoreCase: true, out var modulo) && modulo != Modulos.Ninguno)
            .WithMessage("Módulo inválido.");
    }
}
