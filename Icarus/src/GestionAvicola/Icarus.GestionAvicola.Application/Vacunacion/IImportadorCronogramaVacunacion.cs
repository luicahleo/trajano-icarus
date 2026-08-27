namespace Icarus.GestionAvicola.Application.Vacunacion;

// Parseo del Excel del plan (formato del papel de CAISY, spec SP7). La
// implementación vive en Infrastructure (ClosedXML); Application solo ve
// ítems o errores por número de fila. La atomicidad la decide el handler: si
// hay errores no se guarda nada.
public interface IImportadorCronogramaVacunacion
{
    ResultadoImportacionCronograma Importar(Stream contenido);
}

public sealed record ItemCronogramaImportado(
    int EdadDia, string Vacuna, string? ModoAplicacion, string? Observaciones, DateOnly? Fecha = null);

public sealed record ErrorFilaImportacion(int Fila, string Mensaje);

public sealed record ResultadoImportacionCronograma(
    IReadOnlyList<ItemCronogramaImportado> Items, IReadOnlyList<ErrorFilaImportacion> Errores,
    DateOnly? PrimeraFecha = null);
