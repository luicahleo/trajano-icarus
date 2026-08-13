namespace Icarus.Clientes.Infrastructure.Autorizacion;

// Políticas de entitlement por módulo (spec). Los endpoints de negocio las
// referencian por nombre; el sondeo del Host las prueba mientras no existan.
public static class PoliticasClientes
{
    public const string RequiereGestionAvicola = "Modulo:GestionAvicola";
    public const string RequiereControlAcceso = "Modulo:ControlAcceso";
}
