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
}
