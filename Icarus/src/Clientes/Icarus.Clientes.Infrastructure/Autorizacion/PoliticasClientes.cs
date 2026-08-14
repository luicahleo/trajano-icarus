using Icarus.Clientes.Domain;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

// Políticas de entitlement por funcionalidad (spec). Los endpoints de negocio
// las referencian por nombre; el sondeo del Host las prueba mientras no
// existan.
public static class PoliticasClientes
{
    public const string Prefijo = "Funcionalidad:";

    public static string Para(Funcionalidades funcionalidad) => Prefijo + funcionalidad.ToString();
}
