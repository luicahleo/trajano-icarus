using MediatR;

namespace Icarus.ControlAcceso.Application.Jornadas;

// Ajuste auditado de una jornada existente. Una lista vacía de valores deja la
// jornada sin marcaciones efectivas (anulación), conservando los originales.
public sealed record CorregirJornadaCommand(
    Guid JornadaId,
    int VersionEsperada,
    string Motivo,
    IReadOnlyList<ValorCorregido> Valores) : IRequest<Guid>;
