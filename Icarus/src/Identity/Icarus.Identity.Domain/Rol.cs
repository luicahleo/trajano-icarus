namespace Icarus.Identity.Domain;

// Roles del sistema cerrado (spec): el Administrador da de alta las cuentas
// y no hay registro público ni rol de testing.
public enum Rol
{
    Administrador = 0,
    SoporteTecnico = 1,
    Cliente = 2,
    Trabajador = 3,
}
