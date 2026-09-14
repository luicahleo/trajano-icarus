namespace Icarus.BuildingBlocks.Application;

// Contrato único de paginación (spec 2026-09-14). Los límites son los que ya
// usaba ListarPedidosCaisyValidator: no se inventaron acá, se unificaron.
// La normalización vive en el record y no en cada handler para que una página
// inválida no llegue nunca a SQL Server como un OFFSET negativo.
public sealed record PeticionPaginada(int Pagina = 1, int TamanoPagina = PeticionPaginada.TamanoPorDefecto)
{
    public const int TamanoPorDefecto = 20;
    public const int TamanoMaximo = 100;

    public int PaginaNormalizada => Math.Max(Pagina, 1);

    public int TamanoNormalizado => TamanoPagina switch
    {
        < 1 => TamanoPorDefecto,
        > TamanoMaximo => TamanoMaximo,
        _ => TamanoPagina,
    };

    public int Salto => (PaginaNormalizada - 1) * TamanoNormalizado;
}

// Total es el conteo SIN paginar: la UI lo necesita para saber cuántas
// páginas hay, y es lo único que obliga a una segunda consulta.
public sealed record Pagina<T>(
    IReadOnlyList<T> Items, int Total, int NumeroPagina, int TamanoPagina);
