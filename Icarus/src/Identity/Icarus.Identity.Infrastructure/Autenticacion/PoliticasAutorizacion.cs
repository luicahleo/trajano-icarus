namespace Icarus.Identity.Infrastructure.Autenticacion;

public static class PoliticasAutorizacion
{
    public const string SoloAdministrador = "SoloAdministrador";

    // Gestión de trabajadores: Administrador, y Cliente sobre su propia
    // empresa (el filtro de tenant del módulo Clientes acota la segunda parte).
    public const string GestionTrabajadores = "GestionTrabajadores";

    // Operaciones de gestión que el trabajador no ejecuta aunque tenga la
    // funcionalidad (spec SP7: cancelar tareas de vacunación).
    public const string SoloCliente = "SoloCliente";
}
