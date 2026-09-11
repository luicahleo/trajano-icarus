// Formatos de dinero compartidos por las features. Estaban duplicados en
// despacho-huevo/constantes.ts y pedidos-alimento/constantes.ts, y esa
// duplicación fue justo la que dejó el saldo de crédito con dos decimales
// mientras el precio por huevo ya usaba cuatro. Una sola definición evita
// que las dos vistas de la misma cifra vuelvan a separarse.

// Dos decimales: montos que se leen como dinero (totales de pedido, precios
// por bolsa o por 40 kg, subtotales de línea).
export function formatoMoneda(valor: number): string {
  return new Intl.NumberFormat('es-BO', {
    style: 'currency',
    currency: 'BOB',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(valor);
}

// Cuatro decimales: cifras que el cliente tiene que poder reproducir a mano,
// igual que la publicación de precios de CAISY. El precio por huevo y todo el
// crédito por despachos de huevo (un total derivado de esos precios por
// cantidades) van por acá: redondear a dos haría que la cuenta no cierre.
export function formatoMonedaExacta(valor: number): string {
  return new Intl.NumberFormat('es-BO', {
    style: 'currency',
    currency: 'BOB',
    minimumFractionDigits: 4,
    maximumFractionDigits: 4,
  }).format(valor);
}
