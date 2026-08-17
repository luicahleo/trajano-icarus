using System.Collections.Concurrent;
using System.Diagnostics;
using Icarus.BuildingBlocks.Application.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Icarus.BuildingBlocks.Observability;

public sealed class SaveChangesRegistroVueloInterceptor : SaveChangesInterceptor
{
    private readonly IRegistroVuelo _registro;
    private readonly string _contexto;
    private readonly ConcurrentDictionary<DbContext, Stopwatch> _relojes = new();

    public SaveChangesRegistroVueloInterceptor(IRegistroVuelo registro, DescriptorContextoPersistencia descriptor)
        => (_registro, _contexto) = (registro, descriptor.Nombre);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null) _relojes[eventData.Context] = Stopwatch.StartNew();
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        RegistrarCompletado(eventData.Context, result);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) _relojes[eventData.Context] = Stopwatch.StartNew();
        return ValueTask.FromResult(result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        RegistrarCompletado(eventData.Context, result);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        RegistrarFallido(eventData.Context);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RegistrarFallido(eventData.Context);
        return Task.CompletedTask;
    }

    private void RegistrarCompletado(DbContext? contexto, int filas)
    {
        if (contexto is null || !_relojes.TryRemove(contexto, out var reloj)) return;
        _registro.PersistenciaCompletada(_contexto, filas, reloj.ElapsedMilliseconds);
    }

    private void RegistrarFallido(DbContext? contexto)
    {
        if (contexto is null || !_relojes.TryRemove(contexto, out var reloj)) return;
        _registro.PersistenciaFallida(_contexto, reloj.ElapsedMilliseconds);
    }
}
