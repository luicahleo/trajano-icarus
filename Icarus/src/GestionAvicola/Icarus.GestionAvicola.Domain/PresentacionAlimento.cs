namespace Icarus.GestionAvicola.Domain;

// Presentación comercial del alimento (spec SP8): bolsa cerrada de 40 kg o
// granel en toneladas enteras. La presentación NO es parte de la identidad del
// producto: SJ-1 bolsa y SJ-1 granel son dos detalles del mismo tipo. Valores
// estables porque se persisten como entero.
public enum PresentacionAlimento
{
    Bolsa = 0,
    Granel = 1,
}
