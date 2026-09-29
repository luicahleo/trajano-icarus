namespace Icarus.ControlAcceso.Application.Kiosco;

// Sesión de kiosco autenticada en la petición actual. Solo disponible en
// endpoints que usan el esquema de cookie del kiosco.
public interface ICurrentKioscoSession
{
    Guid? SesionKioscoId { get; }
}
