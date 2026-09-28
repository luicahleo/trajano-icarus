using Icarus.ControlAcceso.Domain;
using MediatR;

namespace Icarus.ControlAcceso.Application.Jornadas;

public sealed record ObtenerJornadaQuery(Guid JornadaId) : IRequest<JornadaDetalle>;

public sealed record ValorCorregido(TipoMarcacion Tipo, DateTimeOffset InstanteUtc);

public sealed record MarcacionDetalle(
    Guid Id,
    TipoMarcacion Tipo,
    OrigenMarcacion Origen,
    DateTimeOffset InstanteUtc,
    DateTimeOffset HoraDeclaradaUtc,
    DateTimeOffset CreadaEnUtc,
    Guid? AutorId,
    string? Motivo);

public sealed record RevisionDetalle(
    Guid Id,
    DateTimeOffset InstanteCorreccionUtc,
    int HastaSecuenciaOriginal,
    string Motivo,
    Guid AutorId,
    IReadOnlyList<ValorCorregido> Valores);

public sealed record JornadaDetalle(
    Guid Id,
    Guid TrabajadorId,
    DateOnly FechaBoliviana,
    EstadoJornadaAcceso Estado,
    int Version,
    IReadOnlyList<MarcacionDetalle> Marcaciones,
    IReadOnlyList<RevisionDetalle> Revisiones,
    IReadOnlyList<ValorCorregido> ValoresEfectivos);
