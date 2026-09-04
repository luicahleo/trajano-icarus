using Icarus.GestionAvicola.Domain;

namespace Icarus.GestionAvicola.Application.PedidosAlimento;

// Puertos del Pedido de alimento (spec SP8). Las consultas de tenant pasan por
// el filtro del DbContext; el conteo bloqueable del límite semanal serializa
// los envíos concurrentes del mismo cliente dentro de la transacción del
// envío (comprobación y envío atómicos).
public interface IRepositorioPedidosAlimento
{
    void Agregar(PedidoAlimento pedido);

    // Los detalles recreados por EditarDetalles llevan clave Guid generada en
    // el dominio: se registran como Added explícitamente (el DetectChanges de
    // EF Core los marcaría Modified por asumir que ya existen).
    void AgregarDetalle(DetallePedidoAlimento detalle);

    Task<PedidoAlimento?> ObtenerPorIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    // El detalle de la bandeja incluye el historial de transiciones.
    Task<PedidoAlimento?> ObtenerConHistorialAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PedidoAlimento>> ListarAsync(
        CancellationToken cancellationToken = default);

    // Cuenta los pedidos del cliente con envío dentro de la semana indicada
    // y bloquea el rango leído (UPDLOCK con semántica serializable) hasta el
    // fin de la transacción: dos envíos concurrentes no superan el límite.
    Task<int> ContarEnviadosEnSemanaBloqueandoAsync(
        Guid clienteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default);

    // Transacción del envío: se confirma al final o se descarta al liberar.
    Task<ITransaccionPedidos> IniciarTransaccionAsync(
        CancellationToken cancellationToken = default);
}

public interface ITransaccionPedidos : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken cancellationToken = default);
}

// Límite semanal configurable sin cambiar código (spec SP8): se valida al
// arrancar; el valor inicial es tres pedidos enviados por cliente y semana ISO.
public sealed class OpcionesPedidosAlimento
{
    public const string Seccion = "PedidosAlimento";

    public int MaximoPorSemana { get; set; } = 3;
}
