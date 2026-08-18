namespace Icarus.GestionAvicola.Domain;

public static class EficienciaPostura
{
    public const decimal UmbralDescarte = 70m;

    public static decimal Calcular(int totalVendible, int gallinasVivas) =>
        gallinasVivas <= 0 ? 0m : Math.Round(totalVendible * 100m / gallinasVivas, 2);

    public static bool EstaBajoUmbral(decimal eficiencia) => eficiencia < UmbralDescarte;
}
