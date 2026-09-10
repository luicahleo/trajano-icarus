using Icarus.BuildingBlocks.Domain;

namespace Icarus.GestionAvicola.Domain;

// Compensa, en el saldo de crédito del cliente, la diferencia de precio
// entre una publicación errónea y su correctiva para un despacho ya
// Recibido (spec SP9D). Agregado propio (no una sub-entidad de DespachoHuevo)
// porque RepositorioBalanceCreditoHuevo lo suma igual que despachos y
// pedidos, con una consulta que cruza clientes. Inmutable: es un registro
// histórico de una corrección ya aplicada, no algo que se edite.
public sealed class AjusteCreditoHuevo : AggregateRoot
{
    private AjusteCreditoHuevo()
    {
    }

    public AjusteCreditoHuevo(
        Guid clienteId, Guid despachoHuevoId, Guid publicacionErroneaId, Guid publicacionCorrectivaId,
        decimal monto, string motivo, Guid actorId)
    {
        if (monto == 0)
            throw new ReglaNegocioException("El monto del ajuste no puede ser cero.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaNegocioException("El ajuste debe declarar un motivo.");

        ClienteId = clienteId;
        DespachoHuevoId = despachoHuevoId;
        PublicacionErroneaId = publicacionErroneaId;
        PublicacionCorrectivaId = publicacionCorrectivaId;
        Monto = monto;
        Motivo = motivo;
        ActorId = actorId;
        CreadoEnUtc = DateTime.UtcNow;
    }

    public Guid ClienteId { get; private set; }

    public Guid DespachoHuevoId { get; private set; }

    public Guid PublicacionErroneaId { get; private set; }

    public Guid PublicacionCorrectivaId { get; private set; }

    public decimal Monto { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public Guid ActorId { get; private set; }

    public DateTime CreadoEnUtc { get; private set; }
}
