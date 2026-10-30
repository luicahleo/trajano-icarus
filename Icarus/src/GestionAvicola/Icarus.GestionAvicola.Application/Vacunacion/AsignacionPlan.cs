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
        // Solo un publicado activo es asignable (spec 2026-10-30). El
        // repositorio ya lo filtra; la segunda comprobación es defensa en
        // profundidad. Borrador, desactivado e inexistente: el mismo 404.
        var programa = await programas.ObtenerPublicadoPorIdAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        if (!programa.EsAsignable)
            throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        // Publicar exige cronograma: un publicado siempre tiene ítems activos.
        var items = programa.Items.Where(i => i.EstaActivo).ToList();

        var desactivadas = await tareas.DesactivarPendientesDeGalponAsync(galpon.Id, cancellationToken);
        foreach (var item in items)
            tareas.Agregar(new TareaVacunacion(
                galpon.Id, galpon.ClienteId, programa.Id, item.Id,
                item.EdadDia, item.Vacuna, item.ModoAplicacion, item.Observaciones,
                item.Fecha ?? galpon.FechaNacimientoLote.AddDays(item.EdadDia)));

        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.vacunacion.asignar",
                ("TareasCreadas", DatoRegistroVuelo.Entero),
                ("TareasPendientesDesactivadas", DatoRegistroVuelo.Entero)),
            "asignacion", "aplicada",
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
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.vacunacion.quitar-plan",
                ("TareasPendientesDesactivadas", DatoRegistroVuelo.Entero)),
            "quitar", "aplicada",
            new Dictionary<string, object?> { ["TareasPendientesDesactivadas"] = desactivadas });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
