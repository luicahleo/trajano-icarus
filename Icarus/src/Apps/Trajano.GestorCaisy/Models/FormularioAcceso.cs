using System.ComponentModel.DataAnnotations;

namespace Trajano.GestorCaisy.Models;

public sealed class FormularioAcceso
{
    [Required(ErrorMessage = "Escriba el correo.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escriba la contraseña.")]
    public string Contrasena { get; set; } = string.Empty;
}
