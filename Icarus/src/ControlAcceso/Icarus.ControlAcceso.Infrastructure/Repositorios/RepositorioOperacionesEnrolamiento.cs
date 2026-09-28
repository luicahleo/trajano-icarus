using Icarus.BuildingBlocks.Domain;
using Icarus.ControlAcceso.Application.Trabajadores;
using Icarus.ControlAcceso.Domain;
using Icarus.ControlAcceso.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.ControlAcceso.Infrastructure.Repositorios;

public sealed class RepositorioOperacionesEnrolamiento(ControlAccesoDbContext db)
    : IRepositorioAccesoTrabajadores
{
    public Task<ConfiguracionAccesoTrabajador?> ObtenerConfiguracionAsync(
        Guid clienteId, Guid trabajadorId, CancellationToken cancellationToken = default) =>
        db.ConfiguracionAccesoTrabajador.FirstOrDefaultAsync(
            c => c.ClienteId == clienteId && c.TrabajadorId == trabajadorId, cancellationToken);

    public async Task<IReadOnlyList<ConfiguracionAccesoTrabajador>> ListarHabilitadosAsync(
        Guid clienteId, CancellationToken cancellationToken = default) =>
        await db.ConfiguracionAccesoTrabajador
            .AsNoTracking()
            .Where(c => c.ClienteId == clienteId
                        && c.Habilitado
                        && c.Enrolamiento == EstadoEnrolamiento.Vigente)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ConfiguracionAccesoTrabajador>> ListarPorClienteAsync(
        Guid clienteId, CancellationToken cancellationToken = default) =>
        await db.ConfiguracionAccesoTrabajador
            .AsNoTracking()
            .Where(c => c.ClienteId == clienteId)
            .ToListAsync(cancellationToken);

    public async Task<ConfiguracionAccesoTrabajador> ObtenerOCrearConfiguracionAsync(
        Guid clienteId, Guid trabajadorId, CancellationToken cancellationToken = default)
    {
        var existente = await ObtenerConfiguracionAsync(clienteId, trabajadorId, cancellationToken);
        if (existente is not null)
            return existente;

        var nueva = new ConfiguracionAccesoTrabajador(clienteId, trabajadorId);
        db.ConfiguracionAccesoTrabajador.Add(nueva);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return nueva;
        }
        catch (DbUpdateException)
        {
            db.Entry(nueva).State = EntityState.Detached;
            var ganadora = await ObtenerConfiguracionAsync(clienteId, trabajadorId, cancellationToken);
            if (ganadora is null)
                throw new ConflictException("El registro cambió mientras se guardaba; reintente.");
            return ganadora;
        }
    }

    public void AgregarOperacion(OperacionEnrolamiento operacion) =>
        db.OperacionesEnrolamiento.Add(operacion);

    public Task<OperacionEnrolamiento?> ObtenerOperacionAsync(
        Guid clienteId, Guid claveIdempotencia, CancellationToken cancellationToken = default) =>
        db.OperacionesEnrolamiento
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.ClienteId == clienteId && o.ClaveIdempotencia == claveIdempotencia,
                cancellationToken);
}
