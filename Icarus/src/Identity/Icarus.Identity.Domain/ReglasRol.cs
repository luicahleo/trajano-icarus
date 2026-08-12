namespace Icarus.Identity.Domain;

public static class ReglasRol
{
    // Cliente y Trabajador operan sobre una empresa; Administrador y
    // SoporteTecnico son de plataforma y llevan ClienteId nulo (spec).
    public static bool RequiereCliente(Rol rol) => rol is Rol.Cliente or Rol.Trabajador;
}
