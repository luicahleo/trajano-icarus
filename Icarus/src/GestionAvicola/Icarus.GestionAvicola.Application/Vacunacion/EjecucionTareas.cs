using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Vacunacion;

// Completar cierra algo que pudo ocurrir ayer (spec SP7): la fecha la informa
// el usuario (por defecto hoy, nunca futura — lo valida el dominio). La
// segunda llamada sobre la misma tarea es 409 por estado: la operación es
// naturalmente idempotente y no hace falta IdempotencyKey. CompletadaPor es
// el id del usuario actual, nunca el nombre (anti-PII).
public sealed record CompletarTareaVacunacionCommand(
    Guid TareaId, DateOnly? FechaAplicacion, int? AvesVacunadas, string? Observaciones)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.completar",
        new Dictionary<string, DatoRegistroVuelo> { ["AvesVacunadas"] = DatoRegistroVuelo.Entero });
}

public sealed record CancelarTareaVacunacionCommand(Guid TareaId, string? Motivo)
    : IRequest, IOperacionRegistrable
{
    public DescriptorOperacionRegistroVuelo Registro { get; } = new(
        "avicola.vacunacion.cancelar", new Dictionary<string, DatoRegistroVuelo>());
}

public sealed class CompletarTareaVacunacionValidator : AbstractValidator<CompletarTareaVacunacionCommand>
{
    public CompletarTareaVacunacionValidator()
    {
        RuleFor(x => x.AvesVacunadas).GreaterThan(0).When(x => x.AvesVacunadas.HasValue);
        RuleFor(x => x.Observaciones).MaximumLength(1000);
    }
}

public sealed class CancelarTareaVacunacionValidator : AbstractValidator<CancelarTareaVacunacionCommand>
{
    public CancelarTareaVacunacionValidator() => RuleFor(x => x.Motivo).MaximumLength(500);
}

public sealed class CompletarTareaVacunacionHandler(
    IRepositorioTareasVacunacion tareas, ICurrentUser usuario, IRegistroVuelo registroVuelo,
    IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CompletarTareaVacunacionCommand>
{
    public async Task Handle(CompletarTareaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var tarea = await tareas.ObtenerPorIdAsync(request.TareaId, cancellationToken)
            ?? throw new NotFoundException("Tarea de vacunación no encontrada.");
        if (tarea.Estado != EstadoTareaVacunacion.Pendiente)
            throw new ConflictException("No se pudo completar la tarea de vacunación.");
        tarea.Completar(
            request.FechaAplicacion ?? DateOnly.FromDateTime(DateTime.UtcNow),
            request.AvesVacunadas, usuario.UsuarioId ?? Guid.Empty, request.Observaciones);
        if (request.AvesVacunadas is int aves)
            registroVuelo.Decidir("avicola.vacunacion.completar", "aplicacion", "aplicada",
                new Dictionary<string, object?> { ["AvesVacunadas"] = aves });
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}

// Cancelar es decisión de gestión (spec SP7): el endpoint la limita al rol
// Cliente; aquí solo importa el estado.
public sealed class CancelarTareaVacunacionHandler(
    IRepositorioTareasVacunacion tareas, IUnidadTrabajoGestionAvicola unidadTrabajo)
    : IRequestHandler<CancelarTareaVacunacionCommand>
{
    public async Task Handle(CancelarTareaVacunacionCommand request, CancellationToken cancellationToken)
    {
        var tarea = await tareas.ObtenerPorIdAsync(request.TareaId, cancellationToken)
            ?? throw new NotFoundException("Tarea de vacunación no encontrada.");
        if (tarea.Estado != EstadoTareaVacunacion.Pendiente)
            throw new ConflictException("No se pudo cancelar la tarea de vacunación.");
        tarea.Cancelar(request.Motivo);
        await unidadTrabajo.SaveChangesAsync(cancellationToken);
    }
}
