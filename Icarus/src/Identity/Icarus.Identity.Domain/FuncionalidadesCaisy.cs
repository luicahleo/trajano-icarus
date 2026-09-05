using Icarus.BuildingBlocks.Domain;

namespace Icarus.Identity.Domain;

// Funcionalidades globales componibles de las cuentas de CAISY (spec SP8):
// "GestorPedidoAlimento" es una función de la aplicación de oficina, no un rol
// independiente; futuras cuentas combinarán funciones sin crear roles nuevos.
// Los valores numéricos son estables porque se persisten como entero en
// identity.usuarios.FuncionalidadesCaisy.
#pragma warning disable S2346 // El miembro cero se nombra en español (convención del repo), no "None"
[Flags]
public enum FuncionalidadesCaisy
{
    Ninguno = 0,
    GestorPedidoAlimento = 1,
}
#pragma warning restore S2346

public static class ReglasFuncionalidadesCaisy
{
    public static bool EsValida(string nombre) =>
        !string.IsNullOrWhiteSpace(nombre)
        && Enum.TryParse<FuncionalidadesCaisy>(nombre, ignoreCase: true, out var funcionalidad)
        && funcionalidad is not FuncionalidadesCaisy.Ninguno
        && Enum.IsDefined(typeof(FuncionalidadesCaisy), funcionalidad);

    // Rechaza el lote completo si algún nombre no es una funcionalidad definida.
    public static FuncionalidadesCaisy Combinar(IEnumerable<string> nombres)
    {
        var combinadas = FuncionalidadesCaisy.Ninguno;
        foreach (var nombre in nombres)
        {
            if (!EsValida(nombre))
                throw new ReglaNegocioException("Funcionalidad de CAISY no definida.");
            combinadas |= Enum.Parse<FuncionalidadesCaisy>(nombre, ignoreCase: true);
        }
        return combinadas;
    }
}
