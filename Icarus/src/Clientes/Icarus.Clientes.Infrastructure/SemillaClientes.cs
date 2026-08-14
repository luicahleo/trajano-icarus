using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.Clientes.Infrastructure;

// Datos de prueba, SOLO entornos dev/test. Razón social, identificador fiscal
// y documento son ficticios (anti-PII). Los ids fijos los pasa el Host desde
// SemillaIdentidad para que el claim clienteId de las cuentas semilla coincida
// con el cliente sembrado: Clientes no referencia a Identity (aislamiento de
// módulos forzado por los tests de arquitectura).
public static class SemillaClientes
{
    public const string RazonSocialDemo = "Granja Demo S.A.C.";
    public const string IdentificadorFiscalDemo = "900000001";
    public const string DocumentoTrabajadorDemo = "90000001";

    public static async Task SembrarAsync(IServiceProvider servicios, Guid clienteDemoId, Guid trabajadorDemoId)
    {
        var db = servicios.GetRequiredService<ClientesDbContext>();
        if (await db.Clientes.IgnoreQueryFilters().AnyAsync(c => c.Id == clienteDemoId))
            return;

        // El cliente demo tiene un módulo habilitado para poder probar el
        // entitlement en dev y en los tests de integración.
        var cliente = new Cliente(clienteDemoId, RazonSocialDemo, IdentificadorFiscalDemo);
        cliente.DefinirModulos(Modulos.GestionAvicola);
        db.Clientes.Add(cliente);

        // El trabajador demo tiene al menos una funcionalidad asignada para
        // poder probar el entitlement por rol en dev y en los tests.
        var trabajador = new Trabajador(
            trabajadorDemoId, clienteDemoId, "Trabajador Demo", DocumentoTrabajadorDemo,
            "Operario", new DateOnly(2026, 1, 15));
        trabajador.DefinirFuncionalidades(Funcionalidades.Granjas);
        db.Trabajadores.Add(trabajador);

        await db.SaveChangesAsync();
    }
}
