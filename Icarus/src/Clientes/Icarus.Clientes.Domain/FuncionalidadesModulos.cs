namespace Icarus.Clientes.Domain;

// Relación declarativa módulo -> funcionalidades (spec). Todas las
// funcionalidades de negocio pertenecen a GestionAvicola; ControlAcceso queda
// previsto, sin funcionalidades todavía.
public static class FuncionalidadesModulos
{
    public static Modulos ModuloDe(Funcionalidades funcionalidad) => funcionalidad switch
    {
        Funcionalidades.Granjas => Modulos.GestionAvicola,
        Funcionalidades.Galpones => Modulos.GestionAvicola,
        Funcionalidades.ProduccionHuevos => Modulos.GestionAvicola,
        Funcionalidades.Mortalidad => Modulos.GestionAvicola,
        Funcionalidades.Vacunacion => Modulos.GestionAvicola,
        Funcionalidades.Alimentacion => Modulos.GestionAvicola,
        Funcionalidades.Despachos => Modulos.GestionAvicola,
        Funcionalidades.Precios => Modulos.GestionAvicola,
        Funcionalidades.PedidoAlimento => Modulos.GestionAvicola,
        _ => Modulos.Ninguno,
    };

    public static Funcionalidades FuncionalidadesDelModulo(Modulos modulo) => modulo switch
    {
        Modulos.GestionAvicola => Funcionalidades.Granjas | Funcionalidades.Galpones
            | Funcionalidades.ProduccionHuevos | Funcionalidades.Mortalidad
            | Funcionalidades.Vacunacion | Funcionalidades.Alimentacion
            | Funcionalidades.Despachos | Funcionalidades.Precios
            | Funcionalidades.PedidoAlimento,
        _ => Funcionalidades.Ninguno,
    };
}
