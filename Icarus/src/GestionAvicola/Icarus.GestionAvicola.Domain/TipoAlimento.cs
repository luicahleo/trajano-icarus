namespace Icarus.GestionAvicola.Domain;

// Tipos de alimento del catálogo de CAISY (spec SP8). Los códigos legacy
// SJ-PRE, SJ-1, SJ-2, SJ-3, SJ-P1 y SJ-P2 se traducen a nombres en español; el
// mapeo de texto vive en el importador de Infrastructure. Los valores son
// estables porque se persisten como entero.
public enum TipoAlimento
{
    Preiniciador = 0, // SJ-PRE
    Iniciador = 1,    // SJ-1
    Crecimiento = 2,  // SJ-2
    Finalizador = 3,  // SJ-3
    PosturaUno = 4,   // SJ-P1
    PosturaDos = 5,   // SJ-P2
}
