namespace Icarus.Identity.Infrastructure.Autenticacion;

public static class PoliticasAutorizacion
{
    public const string SoloAdministrador = "SoloAdministrador";

    // Gestión de trabajadores: Administrador, y Cliente sobre su propia
    // empresa (el filtro de tenant del módulo Clientes acota la segunda parte).
    public const string GestionTrabajadores = "GestionTrabajadores";
}
