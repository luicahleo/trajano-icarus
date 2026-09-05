namespace Icarus.Identity.Domain;

// Roles del sistema cerrado (spec): el Administrador da de alta las cuentas
// y no hay registro público ni rol de testing. GestorCaisy (spec SP8) es un
// rol global de oficina, sin tenant: sus facultades vienen de
// FuncionalidadesCaisy, no del rol.
public enum Rol
{
    Administrador = 0,
    Cliente = 1,
    Trabajador = 2,
    GestorCaisy = 3,
}
