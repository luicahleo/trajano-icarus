namespace Icarus.ControlAcceso.Domain;

public sealed class ValorEfectivo
{
    public ValorEfectivo(TipoMarcacion tipo, DateTimeOffset instanteUtc)
    {
        Tipo = tipo;
        InstanteUtc = instanteUtc;
    }

    public TipoMarcacion Tipo { get; }

    public DateTimeOffset InstanteUtc { get; }
}
