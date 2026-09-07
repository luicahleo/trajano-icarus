namespace Icarus.GestionAvicola.Domain;

// Catálogo de códigos del Departamento Industrial de CAISY (spec SP8): el
// código distingue tipo y presentación (B = bolsa, G = granel). Lo usa el
// importador del documento de precios y la UI de pedidos como identificador
// visible del producto.
public static class CatalogoAlimentosCaisy
{
    private static readonly IReadOnlyDictionary<string, (TipoAlimento Tipo, PresentacionAlimento Presentacion)> Productos =
        new Dictionary<string, (TipoAlimento, PresentacionAlimento)>(StringComparer.OrdinalIgnoreCase)
        {
            ["SJ-PRE"] = (TipoAlimento.Preiniciador, PresentacionAlimento.Bolsa),
            ["SJ-PREG"] = (TipoAlimento.Preiniciador, PresentacionAlimento.Granel),
            ["SJ-1B"] = (TipoAlimento.Iniciador, PresentacionAlimento.Bolsa),
            ["SJ-1G"] = (TipoAlimento.Iniciador, PresentacionAlimento.Granel),
            ["SJ-2B"] = (TipoAlimento.Crecimiento, PresentacionAlimento.Bolsa),
            ["SJ-2G"] = (TipoAlimento.Crecimiento, PresentacionAlimento.Granel),
            ["SJ-3B"] = (TipoAlimento.Finalizador, PresentacionAlimento.Bolsa),
            ["SJ-3G"] = (TipoAlimento.Finalizador, PresentacionAlimento.Granel),
            ["SJ-P1B"] = (TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa),
            ["SJ-P1G"] = (TipoAlimento.PosturaUno, PresentacionAlimento.Granel),
            ["SJ-P2B"] = (TipoAlimento.PosturaDos, PresentacionAlimento.Bolsa),
            ["SJ-P2G"] = (TipoAlimento.PosturaDos, PresentacionAlimento.Granel),
        };

    public static (TipoAlimento Tipo, PresentacionAlimento Presentacion)? BuscarPorCodigo(string codigo) =>
        Productos.TryGetValue(codigo, out var producto) ? producto : null;

    public static string CodigoDe(TipoAlimento tipo, PresentacionAlimento presentacion) =>
        Productos.First(p => p.Value == (tipo, presentacion)).Key;
}
