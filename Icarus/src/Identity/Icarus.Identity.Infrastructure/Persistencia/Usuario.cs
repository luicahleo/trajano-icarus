using Icarus.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Icarus.Identity.Infrastructure.Persistencia;

// Entidad técnica de ASP.NET Identity (paridad con Caserito): el lenguaje
// ubicuo (Rol, reglas) vive en Icarus.Identity.Domain. TrabajadorId es un
// Guid sin FK ni referencia de proyecto: Identity no conoce a Clientes.
public sealed class Usuario : IdentityUser<Guid>
{
    public string Rol { get; set; } = string.Empty;
    public Guid? ClienteId { get; set; }
    public Guid? TrabajadorId { get; set; }
    public bool Activo { get; set; } = true;

    // Cuentas de CAISY (spec SP8): bitmask de funcionalidades globales; para
    // el resto de roles queda en Ninguno.
    public FuncionalidadesCaisy FuncionalidadesCaisy { get; set; } = FuncionalidadesCaisy.Ninguno;
}
