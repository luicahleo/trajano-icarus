namespace Icarus.Host.Autorizacion;

// Nombres de las políticas de ControlAcceso. Los endpoints y los tests las
// referencian por estas constantes; el registro vive en Program.cs.
public static class PoliticasControlAcceso
{
    public const string ClienteConControlAcceso = "ClienteConControlAcceso";
    public const string TrabajadorElegibleParaMarcar = "TrabajadorElegibleParaMarcar";
}
