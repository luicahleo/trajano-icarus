namespace Icarus.Host.Autorizacion;

public static class PoliticasKiosco
{
    // Exige el esquema de cookie del kiosco; un JWT administrativo no la
    // satisface, y la cookie del kiosco no satisface las políticas normales.
    public const string Autenticado = "KioscoAutenticado";
}
