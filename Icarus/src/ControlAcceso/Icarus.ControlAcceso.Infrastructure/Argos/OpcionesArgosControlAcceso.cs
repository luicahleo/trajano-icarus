namespace Icarus.ControlAcceso.Infrastructure.Argos;

public sealed class OpcionesArgosControlAcceso
{
    public const string Seccion = "ArgosControlAcceso";

    public string Url { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
}
