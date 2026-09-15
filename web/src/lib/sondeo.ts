// Cada cuánto se pregunta por novedades. El endpoint responde 304 cuando nada
// cambió, así que un sondeo sin noticias cuesta una respuesta vacía.
// Treinta segundos: una novedad de pedido o de despacho no es urgente al
// segundo, y media hora sería demasiado para un badge.
export const INTERVALO_SONDEO_MS = 30_000;
