namespace Icarus.ControlAcceso.Infrastructure.Argos;

public sealed class OpcionesBiometria
{
    public const string Seccion = "ControlAcceso:Biometria";

    // Habilita el proveedor determinista de pruebas. En producción debe quedar
    // en false: mientras A0 no fije el contrato real no hay adaptador HTTP y el
    // enrolamiento y el kiosco fallan cerrado.
    public bool UsarDoble { get; set; }

    public int MaximoCandidatos { get; set; } = 200;
}
