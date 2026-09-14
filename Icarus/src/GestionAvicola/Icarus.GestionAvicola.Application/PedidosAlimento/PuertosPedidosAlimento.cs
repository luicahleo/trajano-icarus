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

    // Listado del tenant (spec 2026-09-14): filtros y paginación resueltos en
    // SQL, con el conteo antes del Skip/Take. El filtro de tenant del DbContext
    // acota el alcance al cliente de la sesión.
    Task<(IReadOnlyList<PedidoAlimento> Items, int Total)> ListarPaginadoTenantAsync(
        Guid? granjaId, EstadoPedidoAlimento? estado, PresentacionAlimento? presentacion,
        DateOnly? desde, DateOnly? hasta, Guid? creadoPorTrabajadorId, int? numero,
        int saltar, int tomar, CancellationToken cancellationToken = default);

    // Bandeja global de CAISY (spec SP8/2026-09-14): filtros por estado,
    // presentación, granja (por nombre), rango de fechas y folio, con
    // paginación. Devuelve además los nombres de las granjas de la página para
    // que el DTO no tenga que resolverlos (y CAISY nunca vea personas).
    Task<(IReadOnlyList<PedidoAlimento> Items, int Total, IReadOnlyDictionary<Guid, string> Granjas)>
        ListarPaginadoCaisyAsync(
            EstadoPedidoAlimento? estado, PresentacionAlimento? presentacion, string? granja,
            DateOnly? desde, DateOnly? hasta, int? numero,
            int saltar, int tomar, CancellationToken cancellationToken = default);

    // Cuenta los pedidos del cliente con envío dentro de la semana indicada
    // y bloquea el rango leído (UPDLOCK con semántica serializable) hasta el
    // fin de la transacción: dos envíos concurrentes no superan el límite.
    Task<int> ContarEnviadosEnSemanaBloqueandoAsync(
        Guid clienteId, DateOnly desde, DateOnly hasta,
        CancellationToken cancellationToken = default);

    // Mismo conteo sin bloqueo para mostrar el cupo en la bandeja.
    Task<int> ContarEnviadosEnSemanaAsync(
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
