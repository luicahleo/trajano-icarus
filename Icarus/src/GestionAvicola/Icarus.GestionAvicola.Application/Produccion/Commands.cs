using FluentValidation;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Application.Galpones;
using Icarus.GestionAvicola.Domain;
using MediatR;

namespace Icarus.GestionAvicola.Application.Produccion;

public sealed record RegistrarProduccionCommand(Guid GalponId, TimeOnly? Hora, int CantidadMaples, int UnidadesIncompletas, int MaplesDescarte, int UnidadesDescarte, Guid? IdempotencyKey) : IRequest<Guid>, IOperacionRegistrable { public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.produccion.registrar", new Dictionary<string, DatoRegistroVuelo>()); }
public sealed record EditarProduccionCommand(Guid ProduccionId, TimeOnly Hora, int CantidadMaples, int UnidadesIncompletas, int MaplesDescarte, int UnidadesDescarte) : IRequest, IOperacionRegistrable { public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.produccion.editar", new Dictionary<string, DatoRegistroVuelo>()); }
public sealed record DesactivarProduccionCommand(Guid ProduccionId) : IRequest, IOperacionRegistrable { public DescriptorOperacionRegistroVuelo Registro { get; } = new("avicola.produccion.desactivar", new Dictionary<string, DatoRegistroVuelo>()); }
public sealed record ListarProduccionPorDiaQuery(Guid GalponId, DateOnly? Fecha) : IRequest<ProduccionDiaResumen>;
public sealed class RegistrarProduccionValidator : AbstractValidator<RegistrarProduccionCommand> { public RegistrarProduccionValidator() { RuleFor(x => x.CantidadMaples).GreaterThanOrEqualTo(0); RuleFor(x => x.MaplesDescarte).GreaterThanOrEqualTo(0); RuleFor(x => x.UnidadesIncompletas).InclusiveBetween(0, Maple.HuevosPorMaple - 1); RuleFor(x => x.UnidadesDescarte).InclusiveBetween(0, Maple.HuevosPorMaple - 1); } }
public sealed class EditarProduccionValidator : AbstractValidator<EditarProduccionCommand> { public EditarProduccionValidator() { RuleFor(x => x.CantidadMaples).GreaterThanOrEqualTo(0); RuleFor(x => x.MaplesDescarte).GreaterThanOrEqualTo(0); RuleFor(x => x.UnidadesIncompletas).InclusiveBetween(0, Maple.HuevosPorMaple - 1); RuleFor(x => x.UnidadesDescarte).InclusiveBetween(0, Maple.HuevosPorMaple - 1); } }
public sealed class RegistrarProduccionHandler(IRepositorioGalpones galpones, IRepositorioProduccion produccion, IUnidadTrabajoGestionAvicola unidadTrabajo) : IRequestHandler<RegistrarProduccionCommand, Guid>
{
    public async Task<Guid> Handle(RegistrarProduccionCommand request, CancellationToken cancellationToken)
    {
        var galpon = await galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken) ?? throw new NotFoundException("Galpon", request.GalponId);
        if (request.IdempotencyKey is Guid key)
        {
            var existente = await produccion.ObtenerPorIdempotencyKeyAsync(galpon.Id, key, cancellationToken);
            if (existente is not null) return existente.Id;
        }
        var ahora = DateTime.UtcNow;
        var registro = new RegistroProduccion(galpon.Id, galpon.ClienteId, DateOnly.FromDateTime(ahora), request.Hora ?? TimeOnly.FromDateTime(ahora), request.CantidadMaples, request.UnidadesIncompletas, request.MaplesDescarte, request.UnidadesDescarte, galpon.GallinasActuales, request.IdempotencyKey);
        produccion.Agregar(registro); await unidadTrabajo.SaveChangesAsync(cancellationToken); return registro.Id;
    }
}
public sealed class EditarProduccionHandler(IRepositorioProduccion produccion, IUnidadTrabajoGestionAvicola unidadTrabajo) : IRequestHandler<EditarProduccionCommand>
{ public async Task Handle(EditarProduccionCommand request, CancellationToken cancellationToken) { var registro = await produccion.ObtenerPorIdAsync(request.ProduccionId, cancellationToken) ?? throw new NotFoundException("Registro de producción", request.ProduccionId); registro.Editar(request.CantidadMaples, request.UnidadesIncompletas, request.MaplesDescarte, request.UnidadesDescarte, request.Hora); await unidadTrabajo.SaveChangesAsync(cancellationToken); } }
public sealed class DesactivarProduccionHandler(IRepositorioProduccion produccion, IUnidadTrabajoGestionAvicola unidadTrabajo) : IRequestHandler<DesactivarProduccionCommand>
{ public async Task Handle(DesactivarProduccionCommand request, CancellationToken cancellationToken) { var registro = await produccion.ObtenerPorIdAsync(request.ProduccionId, cancellationToken) ?? throw new NotFoundException("Registro de producción", request.ProduccionId); registro.Desactivar(); await unidadTrabajo.SaveChangesAsync(cancellationToken); } }
public sealed class ListarProduccionPorDiaHandler(IRepositorioGalpones galpones, IRepositorioProduccion produccion) : IRequestHandler<ListarProduccionPorDiaQuery, ProduccionDiaResumen>
{ public async Task<ProduccionDiaResumen> Handle(ListarProduccionPorDiaQuery request, CancellationToken cancellationToken) { var galpon = await galpones.ObtenerPorIdAsync(request.GalponId, cancellationToken) ?? throw new NotFoundException("Galpon", request.GalponId); var fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow); var rs = await produccion.ListarPorDiaAsync(galpon.Id, fecha, cancellationToken); return new(galpon.Id, fecha, rs.Select(r => new RecogidaResumen(r.Id, r.Fecha, r.Hora, r.CantidadMaples, r.UnidadesIncompletas, r.MaplesDescarte, r.UnidadesDescarte, r.GallinasVivas, r.TotalHuevosVendibles(), r.TotalHuevosDescarte())).ToList(), rs.Sum(r => r.CantidadMaples), rs.Sum(r => r.UnidadesIncompletas), rs.Sum(r => r.TotalHuevosVendibles()), rs.Sum(r => r.MaplesDescarte), rs.Sum(r => r.UnidadesDescarte), rs.Sum(r => r.TotalHuevosDescarte())); } }
