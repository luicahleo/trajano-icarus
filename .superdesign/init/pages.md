# Árboles de dependencias de páginas principales

## `/admin/vacunacion`

Entrada: `web/src/features/admin/vacunacion/AdminVacunacionPage.tsx`

- `web/src/app/ui/EstadoCarga.tsx`
- `web/src/app/ui/PaginaCabecera.tsx`
- `web/src/app/ui/TablaDatos.tsx`
- `web/src/lib/http.ts`
- `web/src/lib/tipos.ts`
- `web/src/features/avicola/api.ts`
- `web/src/features/avicola/constantes.ts`
- Shell: `web/src/app/AppLayout.tsx`
  - `web/src/app/NavegacionPrincipal.tsx`
  - `web/src/app/navegacion.tsx`
  - `web/src/app/SelectorTema.tsx`
  - `web/src/app/BannerSinConexion.tsx`
  - `web/src/app/offline/PendientesOffline.tsx`
  - `web/src/app/offline/PrecalentadoOffline.tsx`

## `/admin/clientes`

Entrada: `web/src/features/admin/clientes/ClientesListaPage.tsx`

- `web/src/app/ui/DialogoConfirmacion.tsx`
- `web/src/app/ui/EstadoCarga.tsx`
- `web/src/app/ui/PaginaCabecera.tsx`
- `web/src/app/ui/TablaDatos.tsx`
- `web/src/features/admin/clientes/api.ts`
- `web/src/lib/tipos.ts`
- Shell: mismo árbol de `AppLayout` indicado arriba.
## `/avicola/galpones/:galponId`

Entrada: `web/src/features/avicola/GalponPage.tsx`

- `web/src/features/avicola/ProduccionForm.tsx`
- `web/src/features/avicola/MortalidadForm.tsx`
- `web/src/features/avicola/VacunacionNotificacion.tsx`
- `web/src/features/avicola/api.ts`
- `web/src/features/avicola/offline.ts`
- `web/src/app/ui/EstadoCarga.tsx`
- Shell: mismo árbol de `AppLayout` indicado arriba.

## `/trabajadores`

Entrada: `web/src/features/trabajadores/TrabajadoresPage.tsx`

- `web/src/features/trabajadores/api.ts`
- `web/src/app/ui/PaginaCabecera.tsx`
- `web/src/app/ui/TablaDatos.tsx`
- `web/src/app/ui/DialogoConfirmacion.tsx`
- Shell: mismo árbol de `AppLayout` indicado arriba.
