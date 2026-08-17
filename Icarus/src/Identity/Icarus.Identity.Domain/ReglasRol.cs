namespace Icarus.Identity.Domain;

public static class ReglasRol
{
    // Solo Cliente y Trabajador operan sobre una empresa; el Administrador es
    // de plataforma y lleva ClienteId nulo (spec).
    public static bool RequiereCliente(Rol rol) => rol is Rol.Cliente or Rol.Trabajador;

    public static bool RequiereCliente(string? rol) =>
        Enum.TryParse<Rol>(rol, out var rolParseado) && RequiereCliente(rolParseado);
}
