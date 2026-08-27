using Icarus.Clientes.Domain;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

// Políticas de entitlement por funcionalidad (spec). Los endpoints de negocio
// las referencian por nombre; el sondeo del Host las prueba mientras no
// existan.
public static class PoliticasClientes
{
    public const string Prefijo = "Funcionalidad:";

    // Lectura del catálogo de vacunación: funcionalidad Vacunacion o rol de
    // plataforma (spec SP7). Se registra en AddClientesInfraestructura.
    public const string CatalogoVacunacion = "CatalogoVacunacion";

    public static string Para(Funcionalidades funcionalidad) => Prefijo + funcionalidad.ToString();
}
