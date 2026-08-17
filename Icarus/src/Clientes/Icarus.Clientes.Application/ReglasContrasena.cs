using FluentValidation;

namespace Icarus.Clientes.Application;

internal static class ReglasContrasena
{
    public static void Aplicar<T>(IRuleBuilder<T, string> regla) => regla
        .NotEmpty().WithMessage("La contraseña es obligatoria.")
        .MinimumLength(12).WithMessage("La contraseña debe tener al menos 12 caracteres.")
        .Matches("[A-Z]").WithMessage("La contraseña debe incluir una mayúscula.")
        .Matches("[a-z]").WithMessage("La contraseña debe incluir una minúscula.")
        .Matches("[0-9]").WithMessage("La contraseña debe incluir un número.")
        .Matches("[^a-zA-Z0-9]").WithMessage("La contraseña debe incluir un símbolo.");
}
