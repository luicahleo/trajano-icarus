using FluentValidation;
using Icarus.Clientes.Domain;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class DefinirFuncionalidadesTrabajadorValidator
    : AbstractValidator<DefinirFuncionalidadesTrabajadorCommand>
{
    public DefinirFuncionalidadesTrabajadorValidator()
    {
        RuleFor(c => c.ClienteId).NotEmpty();
        RuleFor(c => c.TrabajadorId).NotEmpty();
        RuleFor(c => c.Funcionalidades).NotNull();
        RuleForEach(c => c.Funcionalidades)
            .Must(nombre =>
                Enum.TryParse<Funcionalidades>(nombre, ignoreCase: true, out var funcionalidad)
                && FuncionalidadesTrabajador.EsAsignable(funcionalidad))
            .WithMessage("Funcionalidad inválida.");
    }
}
