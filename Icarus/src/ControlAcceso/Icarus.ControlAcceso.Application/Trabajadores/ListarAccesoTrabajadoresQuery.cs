using Icarus.BuildingBlocks.Application;
using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Trabajadores;

// Estado de acceso por trabajador para la administración web. Sin nombres ni
// documentos: el Host los resuelve con la consulta de trabajadores.
public sealed record ListarAccesoTrabajadoresQuery : IRequest<IReadOnlyList<AccesoTrabajadorResumen>>;

public sealed record AccesoTrabajadorResumen(
    Guid TrabajadorId,
    bool Habilitado,
    EstadoEnrolamiento Enrolamiento,
    int VersionEnrolamiento);
