namespace Icarus.Clientes.Domain;

// Módulos de negocio que el Administrador habilita por cliente (spec). Flags:
// un cliente puede tener varios módulos a la vez. Los valores numéricos son
// estables porque se persisten como entero en clientes.ModulosHabilitados.
#pragma warning disable S2346 // El miembro cero se nombra en español (convención del repo), no "None"
[Flags]
public enum Modulos
{
    Ninguno = 0,
    GestionAvicola = 1,
    ControlAcceso = 2,
}
#pragma warning restore S2346
