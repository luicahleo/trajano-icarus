using System.Linq.Expressions;
using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.GestionAvicola.Infrastructure.Repositorios;

// Cálculo directo contra el DbContext (spec SP9): cruza DespachoHuevo y
// PedidoAlimento/RecepcionPedidoAlimento, dos agregados del mismo módulo. El
// cálculo usa los campos primitivos (CantidadAmarras, UnidadesSueltas,
// PrecioUnitarioCongelado) en vez de las propiedades calculadas del dominio
// (CantidadHuevos, Subtotal) porque estas últimas no garantizan traducirse a
// SQL de forma fiable. DetalleDespachoHuevo.HuevosPorAmarra es un const: EF
// Core lo traduce como literal en la proyección, no como acceso a miembro en
// tiempo de ejecución.
public sealed class RepositorioBalanceCreditoHuevo(GestionAvicolaDbContext db) : IRepositorioBalanceCreditoHuevo
{
    public async Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        // Corrección 2026-09-14: sin filtro de fecha. El crédito nace cuando
        // CAISY recibe el huevo. El filtro que vivía acá ocultaba dinero que
        // ya era del cliente, y no protegía ninguna regla: el saldo no valida
        // nada. Sobrevive invertido en ObtenerRecibidoRecienteAsync, como
        // referencia visible.
        var ingresos = await SumarIngresosAsync(clienteId, null, cancellationToken);

        var recibidoReal = await db.PedidosAlimento
            .Where(p => p.ClienteId == clienteId
                && (p.Estado == EstadoPedidoAlimento.RecibidoConforme
                    || p.Estado == EstadoPedidoAlimento.RecibidoConDiferencias))
            .Select(p => p.Recepcion!.TotalRecibido)
            .SumAsync(cancellationToken);

        // Ajustes de corrección (spec SP9D): compensan un error real, no un
        // ingreso sujeto al desfase de dos semanas de ReglasCreditoHuevo.
        var ajustes = await db.AjustesCreditoHuevo
            .Where(a => a.ClienteId == clienteId)
            .SumAsync(a => a.Monto, cancellationToken);

        // Corrección 2026-09-14: el saldo es la cuenta real. No entra el
        // alimento pedido y todavía no recibido: ese alimento no llegó, no
        // consumió crédito, y el pedido todavía puede ser rechazado o
        // devuelto por CAISY. El componente «comprometido pendiente» que
        // vivía acá existía solo para blindar la advertencia de crédito
        // insuficiente al enviar, retirada por esta misma corrección.
        return ingresos - recibidoReal + ajustes;
    }

    public Task<decimal> ObtenerRecibidoRecienteAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        var fechaCorte = hoy.AddDays(-ReglasCreditoHuevo.DiasReferenciaCredito);
        return SumarIngresosAsync(clienteId, d => d.FechaRecepcion > fechaCorte, cancellationToken);
    }

    // Una sola copia de la aritmética de ingresos, que es la parte frágil de
    // este archivo (ver comentario de clase): el saldo la usa sin filtro de
    // fecha y el reciente con uno. Duplicarla invitaría a que las dos cifras
    // se desincronicen en silencio.
    private Task<decimal> SumarIngresosAsync(
        Guid clienteId, Expression<Func<DespachoHuevo, bool>>? filtroFecha,
        CancellationToken cancellationToken)
    {
        var despachos = db.DespachosHuevo
            .Where(d => d.ClienteId == clienteId && d.Estado == EstadoDespachoHuevo.Recibido
                && d.FechaRecepcion != null);
        if (filtroFecha is not null)
            despachos = despachos.Where(filtroFecha);
        return despachos
            .SelectMany(d => d.Detalles)
            .Where(det => det.PrecioUnitarioCongelado != null)
            .SumAsync(det =>
                (det.CantidadAmarras * DetalleDespachoHuevo.HuevosPorAmarra + det.UnidadesSueltas)
                    * det.PrecioUnitarioCongelado!.Value,
                cancellationToken);
    }

    // Materializa las filas (columnas simples, sin cómputo) y recién después
    // convierte CreadoEnUtc a DateOnly en memoria: DateOnly.FromDateTime
    // dentro de un Select traducido a SQL es una fuente de errores de
    // traducción de EF Core en otras partes de este mismo archivo (ver
    // comentario de clase), así que se evita a propósito.
    public async Task<IReadOnlyList<AjusteCreditoHuevoResumen>> ObtenerAjustesAsync(
        Guid clienteId, CancellationToken cancellationToken = default)
    {
        var ajustes = await db.AjustesCreditoHuevo
            .Where(a => a.ClienteId == clienteId)
            .OrderByDescending(a => a.CreadoEnUtc)
            .ToListAsync(cancellationToken);
        return ajustes
            .Select(a => new AjusteCreditoHuevoResumen(
                a.Id, a.Monto, a.Motivo, DateOnly.FromDateTime(a.CreadoEnUtc)))
            .ToList();
    }
}
