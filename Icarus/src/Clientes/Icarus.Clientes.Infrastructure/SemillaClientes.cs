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
    public const string RazonSocialC1 = "Cliente Uno S.R.L.";
    public const string IdentificadorFiscalC1 = "900000002";
    public const string DocumentoTrabajadorT1 = "90000002";

    public static async Task SembrarAsync(
        IServiceProvider servicios, Guid clienteDemoId, Guid trabajadorDemoId,
        Guid clienteC1Id, Guid trabajadorT1Id)
    {
        var db = servicios.GetRequiredService<ClientesDbContext>();
        if (!await db.Clientes.IgnoreQueryFilters().AnyAsync(c => c.Id == clienteDemoId))
        {
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
            trabajador.DefinirFuncionalidades(Funcionalidades.ProduccionHuevos);
            db.Trabajadores.Add(trabajador);
        }

        if (!await db.Clientes.IgnoreQueryFilters().AnyAsync(c => c.Id == clienteC1Id))
        {
            // Segundo cliente de prueba para verificar el aislamiento por tenant
            // (c1@icarus.test) con su trabajador (t1@icarus.test).
            var cliente = new Cliente(clienteC1Id, RazonSocialC1, IdentificadorFiscalC1);
            cliente.DefinirModulos(Modulos.GestionAvicola);
            db.Clientes.Add(cliente);

            var trabajador = new Trabajador(
                trabajadorT1Id, clienteC1Id, "Trabajador Uno", DocumentoTrabajadorT1,
                "Operario", new DateOnly(2026, 2, 1));
            trabajador.DefinirFuncionalidades(Funcionalidades.ProduccionHuevos);
            db.Trabajadores.Add(trabajador);
        }

        await db.SaveChangesAsync();
    }

    // Tenants extra del escenario de desarrollo (spec
    // 2026-09-12-semilla-escenario-credito-huevo): SOLO bajo IsDevelopment().
    // A diferencia de los tenants de la base común, sus trabajadores reciben
    // TODAS las funcionalidades: son para probar el módulo completo a mano.
    // Los trabajadores de la base común conservan solo ProduccionHuevos porque
    // las pruebas de entitlement dependen de esa limitación.
    public static async Task SembrarDesarrolloAsync(
        IServiceProvider servicios,
        Guid clienteC2Id, Guid trabajadorT2Id,
        Guid clienteC3Id, Guid trabajadorT3Id,
        Guid clienteC4Id, Guid trabajadorT4Id)
    {
        var db = servicios.GetRequiredService<ClientesDbContext>();

        await AgregarSiNoExiste(db, clienteC2Id, trabajadorT2Id,
            "Cliente Dos S.R.L.", "900000003", "Trabajador Dos", "90000003");
        await AgregarSiNoExiste(db, clienteC3Id, trabajadorT3Id,
            "Cliente Tres S.R.L.", "900000004", "Trabajador Tres", "90000004");
        await AgregarSiNoExiste(db, clienteC4Id, trabajadorT4Id,
            "Cliente Cuatro S.R.L.", "900000005", "Trabajador Cuatro", "90000005");

        await db.SaveChangesAsync();
    }

    private static async Task AgregarSiNoExiste(
        ClientesDbContext db, Guid clienteId, Guid trabajadorId,
        string razonSocial, string identificadorFiscal,
        string nombreTrabajador, string documentoTrabajador)
    {
        if (await db.Clientes.IgnoreQueryFilters().AnyAsync(c => c.Id == clienteId))
            return;

        var cliente = new Cliente(clienteId, razonSocial, identificadorFiscal);
        cliente.DefinirModulos(Modulos.GestionAvicola);
        db.Clientes.Add(cliente);

        var trabajador = new Trabajador(
            trabajadorId, clienteId, nombreTrabajador, documentoTrabajador,
            "Operario", new DateOnly(2026, 3, 2));
        trabajador.DefinirFuncionalidades(TodasLasFuncionalidades);
        db.Trabajadores.Add(trabajador);
    }

    private static Funcionalidades TodasLasFuncionalidades =>
        Enum.GetValues<Funcionalidades>()
            .Aggregate(Funcionalidades.Ninguno, (acumulado, valor) => acumulado | valor);
}
