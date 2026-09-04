namespace Icarus.Clientes.Domain;

// Funcionalidades operativas del trabajador dentro de un módulo (spec). Un
// trabajador no tiene módulos, tiene funcionalidades: el entitlement consulta
// el módulo de cada funcionalidad contra los módulos del cliente. Los valores
// numéricos son estables porque se persisten como entero en
// clientes.trabajadores.Funcionalidades.
#pragma warning disable S2346 // El miembro cero se nombra en español (convención del repo), no "None"
[Flags]
public enum Funcionalidades
{
    Ninguno = 0,
    Granjas = 1,
    Galpones = 2,
    ProduccionHuevos = 4,
    Mortalidad = 8,
    Vacunacion = 16,
    Alimentacion = 32,
    Despachos = 64,
    Precios = 128,
    // SP8: pedidos de alimento hacia CAISY. Bit nuevo sin renumerar los
    // existentes (se persisten como entero en trabajadores.Funcionalidades).
    PedidoAlimento = 256,
}
#pragma warning restore S2346
