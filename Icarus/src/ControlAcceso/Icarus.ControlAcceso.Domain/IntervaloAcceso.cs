using Icarus.BuildingBlocks.Domain;

namespace Icarus.ControlAcceso.Domain;

public sealed class IntervaloAcceso
{
    public IntervaloAcceso(DateTimeOffset entrada, DateTimeOffset salida)
    {
        if (salida <= entrada)
            throw new ReglaNegocioException("La salida debe ser posterior a la entrada.");

        Entrada = entrada;
        Salida = salida;
    }

    public DateTimeOffset Entrada { get; }
    public DateTimeOffset Salida { get; }

    public bool SeSolapaCon(IntervaloAcceso otro)
    {
        return Entrada < otro.Salida && otro.Entrada < Salida;
    }
}
