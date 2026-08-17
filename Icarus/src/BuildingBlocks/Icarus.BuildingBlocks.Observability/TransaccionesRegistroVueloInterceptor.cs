using Microsoft.EntityFrameworkCore.Diagnostics;
using Icarus.BuildingBlocks.Application.Observability;
using System.Data.Common;

namespace Icarus.BuildingBlocks.Observability;

public sealed class TransaccionesRegistroVueloInterceptor : DbTransactionInterceptor
{
    private readonly IRegistroVuelo _registro;
    private readonly string _contexto;

    public TransaccionesRegistroVueloInterceptor(IRegistroVuelo registro, DescriptorContextoPersistencia descriptor)
        => (_registro, _contexto) = (registro, descriptor.Nombre);

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        => _registro.TransaccionTerminada(_contexto, true);

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        => _registro.TransaccionTerminada(_contexto, false);
}
