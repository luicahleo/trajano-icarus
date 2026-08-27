using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Asignación (spec SP7): materializa una tarea por ítem activo del programa
// con snapshot y FechaProgramada = FechaNacimientoLote + EdadDia. Las
// pendientes del plan anterior se desactivan (soft delete); las completadas y
// canceladas quedan como historial. Un galpón tiene a lo sumo un plan
// vigente: se deriva de las tareas pendientes, sin campo extra en Galpon.
public sealed record AsignarPlanVacunacionCommand(Guid GalponId, Guid ProgramaId)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.asignar",
        new Dictionary<string, DatoRegistroVuelo>
        {
            ["TareasCreadas"] = DatoRegistroVuelo.Entero,
            ["TareasPendientesDesactivadas"] = DatoRegistroVuelo.Entero,
        });
}

public sealed record QuitarPlanVacunacionCommand(Guid GalponId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.quitar-plan",
        new Dictionary<string, DatoRegistroVuelo> { ["TareasPendientesDesactivadas"] = DatoRegistroVuelo.Entero });
}

public sealed class AsignarPlanVacunacionHandler(
    IRepositorioGalpones galpones, IRepositorioProgramasVacunacion programas,
    IRepositorioTareasVacunacion tareas, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<AsignarPlanVacunacionCommand>
{
    public async Task Handle(AsignarPlanVacunacionCommand request, CancellationToken cancellationToken)
    {
        // El filtro global garantiza galpón activo del tenant; id ajeno = 404.
        var galpon = await galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);
        // ObtenerPorIdAsync respeta EstaActivo: un programa inactivo no es asignable (404 genérico).
        var programa = await programas.ObtenerPorIdAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        var items = programa.Items.Where(i => i.EstaActivo).ToList();
        if (items.Count == 0)
            throw new ConflictException("No se pudo asignar el plan de vacunación.");

        var desactivadas = await tareas.DesactivarPendientesDeGalponAsync(galpon.Id, cancellationToken);
        foreach (var item in items)
            tareas.Agregar(new TareaVacunacion(
                galpon.Id, galpon.ClienteId, programa.Id, item.Id,
                item.EdadDia, item.Vacuna, item.ModoAplicacion, item.Observaciones,
                galpon.FechaNacimientoLote.AddDays(item.EdadDia)));

        registroVuelo.Decidir("avicola.vacunacion.asignar", "asignacion", "aplicada",
            new Dictionary<string, object?>
            {
                ["TareasCreadas"] = items.Count,
                ["TareasPendientesDesactivadas"] = desactivadas,
            });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class QuitarPlanVacunacionHandler(
    IRepositorioGalpones galpones, IRepositorioTareasVacunacion tareas,
    IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<QuitarPlanVacunacionCommand>
{
    public async Task Handle(QuitarPlanVacunacionCommand request, CancellationToken cancellationToken)
    {
        var galpon = await galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken)
            ?? throw new NotFoundException("Galpon", request.GalponId);
        var desactivadas = await tareas.DesactivarPendientesDeGalponAsync(galpon.Id, cancellationToken);
        registroVuelo.Decidir("avicola.vacunacion.quitar-plan", "quitar", "aplicada",
            new Dictionary<string, object?> { ["TareasPendientesDesactivadas"] = desactivadas });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
