namespace Icarus.BuildingBlocks.Application.Observability;

public enum TipoDatoRegistroVuelo
{
    Booleano,
    Entero,
    Decimal,
    Texto,
    IdentificadorTecnico,
}

public sealed record DatoRegistroVuelo(TipoDatoRegistroVuelo Tipo)
{
    public static DatoRegistroVuelo Booleano { get; } = new(TipoDatoRegistroVuelo.Booleano);
    public static DatoRegistroVuelo Entero { get; } = new(TipoDatoRegistroVuelo.Entero);
    public static DatoRegistroVuelo Decimal { get; } = new(TipoDatoRegistroVuelo.Decimal);
    public static DatoRegistroVuelo Texto { get; } = new(TipoDatoRegistroVuelo.Texto);
    public static DatoRegistroVuelo Identificador { get; } = new(TipoDatoRegistroVuelo.IdentificadorTecnico);
}
