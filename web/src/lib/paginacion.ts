// Contrato de paginación de la PWA (spec 2026-09-14): espeja el contrato del
// backend (PeticionPaginada y Pagina<T> de BuildingBlocks.Application) para
// que los listados hablen el mismo idioma que la API.
export interface PeticionPaginada {
  pagina: number;
  tamanoPagina: number;
}

export interface Pagina<T> {
  items: T[];
  total: number;
  numeroPagina: number;
  tamanoPagina: number;
}

export const TAMANO_PAGINA_POR_DEFECTO = 20;

// El backend acota el tamaño a 1–100; la UI solo necesita saber cuántas
// páginas hay para el total y el tamaño vigentes.
export function contarPaginas(total: number, tamanoPagina: number): number {
  return Math.max(1, Math.ceil(total / Math.max(tamanoPagina, 1)));
}

// Query string común de los listados filtrables: página, tamaño y solo los
// filtros con valor (vacío significa «sin filtrar»).
export function consultaPaginada(
  peticion: PeticionPaginada,
  filtros: Record<string, string | undefined> = {},
): string {
  const parametros = new URLSearchParams({
    pagina: String(peticion.pagina),
    tamanoPagina: String(peticion.tamanoPagina),
  });
  for (const [clave, valor] of Object.entries(filtros)) {
    if (valor) parametros.set(clave, valor);
  }
  return parametros.toString();
}
