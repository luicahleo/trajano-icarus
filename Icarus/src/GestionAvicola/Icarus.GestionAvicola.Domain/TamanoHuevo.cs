namespace Icarus.GestionAvicola.Domain;

// Tamaños de huevo del catálogo de CAISY (glosario de dominio, spec SP9).
// Valores estables porque se persisten como entero.
public enum TamanoHuevo
{
    Extra = 0,
    Primera = 1,
    Segunda = 2,
    Tercera = 3,
    Cuarta = 4,
    Quinta = 5,
}
