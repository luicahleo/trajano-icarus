using FluentValidation;
using FluentValidation.Results;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Catálogo global de programas de vacunación (spec SP7 y spec 2026-10-30-
// gestor-veterinario-vacunacion). La gestión es del Gestor CAISY con
// GestorVeterinario; el tenant solo consulta publicados activos. Ningún
// handler decide por el rol: lo decide la política del endpoint. Registro de
// vuelo: solo cantidades, nunca nombres de programa o vacuna ni observaciones.
public sealed record CrearProgramaVacunacionCommand(string Nombre, int CantidadAves, string? Observaciones)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.crear",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadAves"] = DatoRegistroVuelo.Entero });
}

public sealed record ActualizarProgramaVacunacionCommand(Guid ProgramaId, string Nombre, int CantidadAves, string? Observaciones)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.actualizar",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadAves"] = DatoRegistroVuelo.Entero });
}

public sealed record ImportarCronogramaExcelCommand(Guid ProgramaId, Stream Contenido)
    : IRequest<int>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.importar-cronograma",
        new Dictionary<string, DatoRegistroVuelo> { ["ItemsImportados"] = DatoRegistroVuelo.Entero });
}

public sealed record PublicarProgramaVacunacionCommand(Guid ProgramaId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.publicar",
        new Dictionary<string, DatoRegistroVuelo> { ["ItemsPublicados"] = DatoRegistroVuelo.Entero });
}

public sealed record EliminarBorradorProgramaVacunacionCommand(Guid ProgramaId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.eliminar-borrador", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record DesactivarProgramaVacunacionCommand(Guid ProgramaId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.desactivar",
        new Dictionary<string, DatoRegistroVuelo> { ["TareasPendientesDesactivadas"] = DatoRegistroVuelo.Entero });
}

// Tenant (cliente y trabajador con Vacunacion): solo publicados activos.
public sealed record ListarProgramasVacunacionQuery : IRequest<IReadOnlyList<ProgramaVacunacionResumen>>;

public sealed record ObtenerProgramaVacunacionQuery(Guid ProgramaId) : IRequest<ProgramaVacunacionDetalle>;

// Gestor: catálogo completo salvo borradores eliminados.
public sealed record ListarProgramasVacunacionGestionQuery
    : IRequest<IReadOnlyList<ProgramaVacunacionGestionResumen>>;

public sealed record ObtenerProgramaVacunacionGestionQuery(Guid ProgramaId)
    : IRequest<ProgramaVacunacionGestionDetalle>;

public sealed class CrearProgramaVacunacionValidator : AbstractValidator<CrearProgramaVacunacionCommand>
{
    public CrearProgramaVacunacionValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CantidadAves).GreaterThan(0);
        RuleFor(x => x.Observaciones).MaximumLength(1000);
    }
}

public sealed class ActualizarProgramaVacunacionValidator : AbstractValidator<ActualizarProgramaVacunacionCommand>
{
    public ActualizarProgramaVacunacionValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CantidadAves).GreaterThan(0);
        RuleFor(x => x.Observaciones).MaximumLength(1000);
    }
}

public sealed class CrearProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CrearProgramaVacunacionCommand, Guid>
{
    public async Task<Guid> Handle(CrearProgramaVacunacionCommand request, CancellationToken cancellationToken)
    {
        if (await programas.ExisteNombreAsync(request.Nombre.Trim(), null, cancellationToken))
            throw new ConflictException("No se pudo registrar el programa de vacunación.");
        var programa = new ProgramaVacunacion(request.Nombre, null, request.CantidadAves, request.Observaciones);
        programas.Agregar(programa);
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.vacunacion.programas.crear",
                ("CantidadAves", DatoRegistroVuelo.Entero)),
            "alta", "aplicada",
            new Dictionary<string, object?> { ["CantidadAves"] = programa.CantidadAves });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return programa.Id;
    }
}

public sealed class ActualizarProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ActualizarProgramaVacunacionCommand>
{
    public async Task Handle(ActualizarProgramaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerParaGestionAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        programa.AsegurarEditable();
        if (await programas.ExisteNombreAsync(request.Nombre.Trim(), programa.Id, cancellationToken))
            throw new ConflictException("No se pudo actualizar el programa de vacunación.");
        programa.ActualizarDatos(request.Nombre, request.CantidadAves, request.Observaciones);
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.vacunacion.programas.actualizar",
                ("CantidadAves", DatoRegistroVuelo.Entero)),
            "edicion", "aplicada",
            new Dictionary<string, object?> { ["CantidadAves"] = programa.CantidadAves });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// Atomicidad (spec SP7): una fila inválida rechaza la importación completa
// con la lista de errores por número de fila (ValidationException -> 400 con
// `errors.Cronograma`), sin guardar nada. Un publicado se rechaza antes de
// leer el archivo.
public sealed class ImportarCronogramaExcelHandler(
    IRepositorioProgramasVacunacion programas, IImportadorCronogramaVacunacion importador,
    IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ImportarCronogramaExcelCommand, int>
{
    public async Task<int> Handle(ImportarCronogramaExcelCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerParaGestionAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        programa.AsegurarEditable();
        var resultado = importador.Importar(request.Contenido);
        if (resultado.Errores.Count > 0)
            throw new ValidationException(resultado.Errores.Select(e =>
                new ValidationFailure("Cronograma", $"Fila {e.Fila}: {e.Mensaje}")));
        programa.ReemplazarCronograma(resultado.Items.Select(i =>
            new DatosItemPlanVacunacion(i.EdadDia, i.Vacuna, i.ModoAplicacion, i.Observaciones, i.Fecha)));
        // La fecha de emisión es la primera fecha del Excel (spec SP7): no se
        // pide en el alta ni en la edición.
        if (resultado.PrimeraFecha is { } primeraFecha)
            programa.EstablecerFechaEmision(primeraFecha);
        // Los ítems nuevos llevan clave Guid generada en el dominio: se registran
        // como Added explícitamente (el DetectChanges de EF Core los marcaría
        // Modified por asumir que ya existen).
        foreach (var item in programa.Items.Where(i => i.EstaActivo))
            programas.AgregarItem(item);
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.vacunacion.programas.importar-cronograma",
                ("ItemsImportados", DatoRegistroVuelo.Entero)),
            "importacion", "aplicada",
            new Dictionary<string, object?> { ["ItemsImportados"] = resultado.Items.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return resultado.Items.Count;
    }
}

public sealed class PublicarProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<PublicarProgramaVacunacionCommand>
{
    public async Task Handle(PublicarProgramaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerParaGestionAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        programa.Publicar();
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.vacunacion.programas.publicar",
                ("ItemsPublicados", DatoRegistroVuelo.Entero)),
            "publicacion", "aplicada",
            new Dictionary<string, object?> { ["ItemsPublicados"] = programa.Items.Count(i => i.EstaActivo) });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// Borrado lógico de un borrador (glosario, regla transversal 1). Un segundo
// intento no lo encuentra (ObtenerParaGestionAsync excluye los eliminados) y
// responde 404: la aplicación de oficina lo trata como éxito ya aplicado.
public sealed class EliminarBorradorProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<EliminarBorradorProgramaVacunacionCommand>
{
    public async Task Handle(EliminarBorradorProgramaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerParaGestionAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        programa.EliminarBorrador();
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.vacunacion.programas.eliminar-borrador"),
            "eliminacion", "aplicada");
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DesactivarProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, IRepositorioTareasVacunacion tareas,
    IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DesactivarProgramaVacunacionCommand>
{
    public async Task Handle(DesactivarProgramaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerParaGestionAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        // La guarda de estado va primero: un borrador no toca ninguna tarea.
        programa.Desactivar();
        // Un programa desactivado deja de ser vigente donde estaba asignado:
        // sus tareas pendientes se desactivan en todos los galpones; las
        // completadas y canceladas quedan como historial sanitario (spec SP7).
        // El conteo solo va al registro de vuelo: el gestor no lo ve.
        var desactivadas = await tareas.DesactivarPendientesDeProgramaAsync(programa.Id, cancellationToken);
        registroVuelo.Decidir(
            DescriptorOperacionRegistroVuelo.Crear("avicola.vacunacion.programas.desactivar",
                ("TareasPendientesDesactivadas", DatoRegistroVuelo.Entero)),
            "desactivacion", "aplicada",
            new Dictionary<string, object?> { ["TareasPendientesDesactivadas"] = desactivadas });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ListarProgramasVacunacionHandler(IRepositorioProgramasVacunacion programas)
    : IRequestHandler<ListarProgramasVacunacionQuery, IReadOnlyList<ProgramaVacunacionResumen>>
{
    public async Task<IReadOnlyList<ProgramaVacunacionResumen>> Handle(
        ListarProgramasVacunacionQuery request, CancellationToken cancellationToken)
    {
        var lista = await programas.ListarPublicadosAsync(cancellationToken);
        return lista.Select(p => new ProgramaVacunacionResumen(
            p.Id, p.Nombre, p.FechaEmision, p.CantidadAves, p.Observaciones, p.EstaActivo)).ToList();
    }
}

public sealed class ObtenerProgramaVacunacionHandler(IRepositorioProgramasVacunacion programas)
    : IRequestHandler<ObtenerProgramaVacunacionQuery, ProgramaVacunacionDetalle>
{
    public async Task<ProgramaVacunacionDetalle> Handle(
        ObtenerProgramaVacunacionQuery request, CancellationToken cancellationToken)
    {
        // Un borrador o un desactivado responden el mismo 404 genérico.
        var programa = await programas.ObtenerPublicadoPorIdAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        return new ProgramaVacunacionDetalle(
            programa.Id, programa.Nombre, programa.FechaEmision, programa.CantidadAves,
            programa.Observaciones, programa.EstaActivo, ProyeccionProgramasVacunacion.ItemsActivos(programa));
    }
}

public sealed class ListarProgramasVacunacionGestionHandler(IRepositorioProgramasVacunacion programas)
    : IRequestHandler<ListarProgramasVacunacionGestionQuery, IReadOnlyList<ProgramaVacunacionGestionResumen>>
{
    public async Task<IReadOnlyList<ProgramaVacunacionGestionResumen>> Handle(
        ListarProgramasVacunacionGestionQuery request, CancellationToken cancellationToken)
    {
        var lista = await programas.ListarParaGestionAsync(cancellationToken);
        return lista.Select(p => new ProgramaVacunacionGestionResumen(
            p.Id, p.Nombre, p.FechaEmision, p.CantidadAves, p.Estado.ToString(), p.EstaActivo,
            p.Items.Count(i => i.EstaActivo))).ToList();
    }
}

public sealed class ObtenerProgramaVacunacionGestionHandler(IRepositorioProgramasVacunacion programas)
    : IRequestHandler<ObtenerProgramaVacunacionGestionQuery, ProgramaVacunacionGestionDetalle>
{
    public async Task<ProgramaVacunacionGestionDetalle> Handle(
        ObtenerProgramaVacunacionGestionQuery request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerParaGestionAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        return new ProgramaVacunacionGestionDetalle(
            programa.Id, programa.Nombre, programa.FechaEmision, programa.CantidadAves, programa.Observaciones,
            programa.Estado.ToString(), programa.EstaActivo, ProyeccionProgramasVacunacion.ItemsActivos(programa));
    }
}

internal static class ProyeccionProgramasVacunacion
{
    public static IReadOnlyList<ItemPlanVacunacionResumen> ItemsActivos(ProgramaVacunacion programa) =>
        programa.Items.Where(i => i.EstaActivo).OrderBy(i => i.EdadDia)
            .Select(i => new ItemPlanVacunacionResumen(i.Id, i.EdadDia, i.Vacuna, i.ModoAplicacion, i.Observaciones, i.Fecha))
            .ToList();
}
