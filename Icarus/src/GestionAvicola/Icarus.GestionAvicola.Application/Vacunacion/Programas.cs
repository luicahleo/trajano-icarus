using FluentValidation;
using FluentValidation.Results;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Registro de vuelo (spec SP7): solo campos no-PII (cantidades). Nunca
// nombres de vacuna, motivos ni observaciones (texto libre).
public sealed record CrearProgramaVacunacionCommand(string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones)
    : IRequest<Guid>, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.crear",
        new Dictionary<string, DatoRegistroVuelo> { ["CantidadAves"] = DatoRegistroVuelo.Entero });
}

public sealed record ActualizarProgramaVacunacionCommand(Guid ProgramaId, string Nombre, DateOnly FechaEmision, int CantidadAves, string? Observaciones)
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

public sealed record DesactivarProgramaVacunacionCommand(Guid ProgramaId) : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.programas.desactivar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed record ListarProgramasVacunacionQuery(bool IncluirInactivos)
    : IRequest<IReadOnlyList<ProgramaVacunacionResumen>>;

public sealed record ObtenerProgramaVacunacionQuery(Guid ProgramaId) : IRequest<ProgramaVacunacionDetalle>;

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
        var programa = new ProgramaVacunacion(request.Nombre, request.FechaEmision, request.CantidadAves, request.Observaciones);
        programas.Agregar(programa);
        registroVuelo.Decidir("avicola.vacunacion.programas.crear", "alta", "aplicada",
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
        var programa = await programas.ObtenerPorIdIncluyendoInactivosAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        if (await programas.ExisteNombreAsync(request.Nombre.Trim(), programa.Id, cancellationToken))
            throw new ConflictException("No se pudo actualizar el programa de vacunación.");
        programa.ActualizarDatos(request.Nombre, request.FechaEmision, request.CantidadAves, request.Observaciones);
        registroVuelo.Decidir("avicola.vacunacion.programas.actualizar", "edicion", "aplicada",
            new Dictionary<string, object?> { ["CantidadAves"] = programa.CantidadAves });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// Atomicidad (spec SP7): una fila inválida rechaza la importación completa
// con la lista de errores por número de fila (ValidationException -> 400 con
// `errors`), sin guardar nada. La columna FECHA del Excel ya se ignoró en el
// importador: la fuente de verdad es EDAD.
public sealed class ImportarCronogramaExcelHandler(
    IRepositorioProgramasVacunacion programas, IImportadorCronogramaVacunacion importador,
    IRegistroVuelo registroVuelo, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<ImportarCronogramaExcelCommand, int>
{
    public async Task<int> Handle(ImportarCronogramaExcelCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerPorIdIncluyendoInactivosAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        var resultado = importador.Importar(request.Contenido);
        if (resultado.Errores.Count > 0)
            throw new ValidationException(resultado.Errores.Select(e =>
                new ValidationFailure("Cronograma", $"Fila {e.Fila}: {e.Mensaje}")));
        programa.ReemplazarCronograma(resultado.Items.Select(i =>
            new DatosItemPlanVacunacion(i.EdadDia, i.Vacuna, i.ModoAplicacion, i.Observaciones)));
        registroVuelo.Decidir("avicola.vacunacion.programas.importar-cronograma", "importacion", "aplicada",
            new Dictionary<string, object?> { ["ItemsImportados"] = resultado.Items.Count });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
        return resultado.Items.Count;
    }
}

public sealed class DesactivarProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<DesactivarProgramaVacunacionCommand>
{
    public async Task Handle(DesactivarProgramaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerPorIdIncluyendoInactivosAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        programa.Desactivar();
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// El catálogo es global (sin tenant). Los inactivos solo los ve el rol de
// plataforma: el nombre del rol es contrato del JWT (GestionAvicola no
// referencia Identity, regla de módulos).
public sealed class ListarProgramasVacunacionHandler(
    IRepositorioProgramasVacunacion programas, ICurrentUser usuario)
    : IRequestHandler<ListarProgramasVacunacionQuery, IReadOnlyList<ProgramaVacunacionResumen>>
{
    public async Task<IReadOnlyList<ProgramaVacunacionResumen>> Handle(
        ListarProgramasVacunacionQuery request, CancellationToken cancellationToken)
    {
        var incluirInactivos = request.IncluirInactivos
            && string.Equals(usuario.Rol, "Administrador", StringComparison.Ordinal);
        var programasLista = await programas.ListarAsync(incluirInactivos, cancellationToken);
        return programasLista.Select(p => new ProgramaVacunacionResumen(
            p.Id, p.Nombre, p.FechaEmision, p.CantidadAves, p.Observaciones, p.EstaActivo)).ToList();
    }
}

public sealed class ObtenerProgramaVacunacionHandler(
    IRepositorioProgramasVacunacion programas, ICurrentUser usuario)
    : IRequestHandler<ObtenerProgramaVacunacionQuery, ProgramaVacunacionDetalle>
{
    public async Task<ProgramaVacunacionDetalle> Handle(
        ObtenerProgramaVacunacionQuery request, CancellationToken cancellationToken)
    {
        var programa = await programas.ObtenerPorIdIncluyendoInactivosAsync(request.ProgramaId, cancellationToken)
            ?? throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        var esAdministrador = string.Equals(usuario.Rol, "Administrador", StringComparison.Ordinal);
        if (!programa.EstaActivo && !esAdministrador)
            throw new NotFoundException("Programa de vacunación", request.ProgramaId);
        return new ProgramaVacunacionDetalle(
            programa.Id, programa.Nombre, programa.FechaEmision, programa.CantidadAves,
            programa.Observaciones, programa.EstaActivo,
            programa.Items.Where(i => i.EstaActivo).OrderBy(i => i.EdadDia)
                .Select(i => new ItemPlanVacunacionResumen(i.Id, i.EdadDia, i.Vacuna, i.ModoAplicacion, i.Observaciones))
                .ToList());
    }
}
