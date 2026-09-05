namespace Icarus.Clientes.Domain;

public static class FuncionalidadesTrabajador
{
    public const Funcionalidades Asignables =
        Funcionalidades.ProduccionHuevos | Funcionalidades.Mortalidad
        | Funcionalidades.Vacunacion | Funcionalidades.PedidoAlimento;

    public static bool EsAsignable(Funcionalidades funcionalidad) =>
        funcionalidad is Funcionalidades.ProduccionHuevos or Funcionalidades.Mortalidad
            or Funcionalidades.Vacunacion or Funcionalidades.PedidoAlimento;
}
