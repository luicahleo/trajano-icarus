using Icarus.BuildingBlocks.Domain;

namespace Icarus.Clientes.Domain;

// Agregado raíz (spec). EstaActivo implementa a la vez el estado
// activo/suspendido del spec y el soft delete transversal del glosario:
// suspender nunca borra la fila. Los módulos habilitados los asigna solo el
// Administrador y son la base del entitlement de los endpoints de negocio.
public sealed class Cliente : AggregateRoot
{
    private Cliente()
    {
    }

    public Cliente(string razonSocial, string identificadorFiscal)
    {
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new ReglaNegocioException("La razón social es obligatoria.");
        if (string.IsNullOrWhiteSpace(identificadorFiscal))
            throw new ReglaNegocioException("El identificador fiscal es obligatorio.");
        if (!NitBoliviano.TieneFormatoValido(identificadorFiscal.Trim()))
            throw new ReglaNegocioException("El NIT debe contener solo dígitos y tener como máximo 15 caracteres.");

        RazonSocial = razonSocial.Trim();
        IdentificadorFiscal = identificadorFiscal.Trim();
        EstaActivo = true;
        ModulosHabilitados = Modulos.Ninguno;
    }

    // Para semillas y tests que necesitan ids fijos (el claim clienteId del
    // usuario semilla debe coincidir con el Id del cliente sembrado).
    public Cliente(Guid id, string razonSocial, string identificadorFiscal)
        : this(razonSocial, identificadorFiscal) => Id = id;

    public string RazonSocial { get; private set; } = string.Empty;

    public string IdentificadorFiscal { get; private set; } = string.Empty;

    public bool EstaActivo { get; private set; }

    public Modulos ModulosHabilitados { get; private set; }

    public void Suspender() => EstaActivo = false;

    public void Reactivar() => EstaActivo = true;

    public void DefinirModulos(Modulos modulos) => ModulosHabilitados = modulos;

    public bool TieneModulo(Modulos modulo) =>
        modulo != Modulos.Ninguno && ModulosHabilitados.HasFlag(modulo);
}
