using Icarus.BuildingBlocks.Domain;

namespace Icarus.Clientes.Domain;

// Agregado (spec): un trabajador pertenece a un único cliente, siempre
// (ClienteId obligatorio). Anti-PII: Nombre y DocumentoIdentidad son datos
// sensibles y nunca van a logs ni mensajes de error. Ninguna fecha admite
// futuro (regla transversal del glosario, validada en dominio).
public sealed class Trabajador : AggregateRoot
{
    private Trabajador()
    {
    }

    public Trabajador(
        Guid clienteId, string nombre, string documentoIdentidad, string cargo, DateOnly fechaIngreso)
    {
        if (clienteId == Guid.Empty)
            throw new ReglaNegocioException("El trabajador debe pertenecer a un cliente.");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaNegocioException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(documentoIdentidad))
            throw new ReglaNegocioException("El documento de identidad es obligatorio.");
        if (string.IsNullOrWhiteSpace(cargo))
            throw new ReglaNegocioException("El cargo es obligatorio.");
        if (fechaIngreso > Hoy())
            throw new ReglaNegocioException("La fecha de ingreso no puede ser futura.");

        ClienteId = clienteId;
        Nombre = nombre.Trim();
        DocumentoIdentidad = documentoIdentidad.Trim();
        Cargo = cargo.Trim();
        FechaIngreso = fechaIngreso;
        EstaActivo = true;
    }

    // Para semillas y tests que necesitan ids fijos.
    public Trabajador(
        Guid id, Guid clienteId, string nombre, string documentoIdentidad, string cargo, DateOnly fechaIngreso)
        : this(clienteId, nombre, documentoIdentidad, cargo, fechaIngreso) => Id = id;

    public Guid ClienteId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string DocumentoIdentidad { get; private set; } = string.Empty;

    public string Cargo { get; private set; } = string.Empty;

    public DateOnly FechaIngreso { get; private set; }

    public DateOnly? FechaCese { get; private set; }

    public bool EstaActivo { get; private set; }

    public void Cesar(DateOnly fechaCese)
    {
        if (fechaCese > Hoy())
            throw new ReglaNegocioException("La fecha de cese no puede ser futura.");
        if (fechaCese < FechaIngreso)
            throw new ReglaNegocioException("La fecha de cese no puede ser anterior al ingreso.");
        FechaCese = fechaCese;
    }

    // Soft delete (glosario): nunca borrado físico.
    public void Desactivar() => EstaActivo = false;

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}
