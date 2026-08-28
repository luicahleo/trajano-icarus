# Frontend reutilizable y homogéneo — Plan de implementación

**Objetivo:** hacer que el frontend de `web/` sea homogéneo, reutilizable y con
identidad propia: centralizar la homogeneidad visual en el tema de MUI, extraer
los pocos ensambles que de verdad se repiten en `src/app/ui/` y dejar el
formato en verde con prettier. Sin dependencias nuevas.

**Arquitectura:** overrides de `components` en `theme.ts` (tablas, diálogos,
chips, listas, campos) para que todo se vea igual sin tocar cada página; cuatro
componentes compuestos que usan MUI por dentro (`PaginaCabecera`,
`DialogoConfirmacion`, `CampoContrasena`, `EstadoCarga`); las tablas siguen
siendo `Table` de MUI directa con estado vacío por `colSpan`. Los tests
existentes fijan nombres accesibles y roles, y son la red de seguridad.

**Tecnologías:** React 19, TypeScript estricto, MUI 9 + Emotion, Vitest y
Testing Library. Sin dependencias nuevas.

**Spec:**
`docs/superpowers/specs/2026-08-28-frontend-ui-reutilizable-design.md`.

## Restricciones globales

- TDD estricto para los componentes nuevos: test en rojo por el motivo correcto,
  implementación mínima, test en verde.
- No cambiar textos, nombres accesibles ni roles que fijan los tests.
- No cambiar paleta, fuentes, `queryKey`, lógica de negocio ni contratos.
- No añadir dependencias.
- Preservar los cambios ajenos; no usar `--no-verify`.
- Ejecutar la puerta completa `./verify.ps1` antes de cada commit y push a
  `develop`; no crear rama ni pull request.
- En `web/`: `npm run format:check`, `npm run lint`, `npm run test`,
  `npm run build`.

---

## Tarea 1: spec y plan — pendiente

- [ ] Escribir
  `docs/superpowers/specs/2026-08-28-frontend-ui-reutilizable-design.md`.
- [ ] Escribir `docs/superpowers/plans/2026-08-28-frontend-ui-reutilizable.md`.
- [ ] Confirmar el alcance con el usuario antes de tocar código.
- [ ] Gates documentales: mojibake, enlaces y `git diff --check`.
- [ ] Commit: `docs: spec y plan para frontend reutilizable y homogeneo`.

## Tarea 2: overrides de tema — pendiente

**Archivos:**

- Modificar: `web/src/app/theme.ts`

- [ ] Añadir overrides de `MuiTableCell` (encabezado con mayúsculas,
  letter-spacing, peso 700, borde inferior doble), `MuiTableRow` (hover con
  `alpha(colores.pinoClaro, 0.4)` y transición), `MuiDialog` (radio de papel 20),
  `MuiDialogTitle` (Prompt 600), `MuiDialogActions` (padding y `gap`),
  `MuiOutlinedInput` (borde enfocado y hover en pino), `MuiChip` (radio 8, peso
  600), `MuiAlert` (radio 12) y `MuiListItem` (hover suave y radio).
- [ ] Verificar: `npm run build` y `npm run test` en `web/`.
- [ ] Commit: `feat(web): overrides de tema para homogeneizar tablas, dialogos, chips y listas`.

## Tarea 3: `PaginaCabecera` — pendiente

**Archivos:**

- Crear: `web/src/app/ui/PaginaCabecera.tsx`
- Crear: `web/src/app/ui/PaginaCabecera.test.tsx`

- [ ] Test en rojo: renderiza el título, el subtítulo opcional y las acciones a
  la derecha.
- [ ] Implementar con `Stack`, `Typography` y `Box` de MUI.
- [ ] Test en verde; `npm run lint` y `npm run test -- PaginaCabecera`.

## Tarea 4: `DialogoConfirmacion` — pendiente

**Archivos:**

- Crear: `web/src/app/ui/DialogoConfirmacion.tsx`
- Crear: `web/src/app/ui/DialogoConfirmacion.test.tsx`

- [ ] Test en rojo: con `abierto` renderiza el diálogo con título, mensaje,
  botones «Cancelar» y «Confirmar» (o la etiqueta pasada); «Confirmar» con el
  `color` y `disabled` cuando `pendiente`; cancela y confirma llaman sus
  callbacks; cerrado no renderiza diálogo.
- [ ] Implementar con `Dialog`, `DialogTitle`, `DialogContent`,
  `DialogActions` y `Button` de MUI.
- [ ] Test en verde; `npm run lint` y `npm run test -- DialogoConfirmacion`.

## Tarea 5: `CampoContrasena` — pendiente

**Archivos:**

- Crear: `web/src/app/ui/CampoContrasena.tsx`
- Crear: `web/src/app/ui/CampoContrasena.test.tsx`

- [ ] Test en rojo: renderiza `TextField` de contraseña con etiqueta, el toggle
  muestra/oculta el valor (cambia `type`) y propaga el resto de props
  (`register`, error, helperText).
- [ ] Implementar con `TextField`, `IconButton` e `InputAdornment` de MUI y los
  iconos `VisibilityRounded`/`VisibilityOffRounded` ya usados.
- [ ] Test en verde; `npm run lint` y `npm run test -- CampoContrasena`.

## Tarea 6: `EstadoCarga` — pendiente

**Archivos:**

- Crear: `web/src/app/ui/EstadoCarga.tsx`
- Crear: `web/src/app/ui/EstadoCarga.test.tsx`

- [ ] Test en rojo: `cargando` muestra `CircularProgress`; `error` muestra el
  `Alert` con mensaje y el botón de reintento cuando `onReintentar` existe; sin
  carga ni error renderiza los hijos.
- [ ] Implementar con `CircularProgress`, `Alert` y `Button` de MUI.
- [ ] Test en verde; `npm run lint` y `npm run test -- EstadoCarga`.

## Tarea 7: aplicar a las páginas de clientes — pendiente

**Archivos:**

- Modificar: `web/src/features/admin/clientes/ClientesListaPage.tsx`
- Modificar: `web/src/features/admin/clientes/ClienteDetallePage.tsx`
- Modificar: `web/src/features/admin/clientes/ClienteNuevoPage.tsx`
- Modificar: `web/src/features/admin/clientes/api.ts`

- [ ] `ClientesListaPage`: usar `PaginaCabecera`, `EstadoCarga`,
  `DialogoConfirmacion`, contenedor `Container maxWidth="lg"` y fila de estado
  vacío con `colSpan`.
- [ ] `ClienteDetallePage`: usar `PaginaCabecera`, `DialogoConfirmacion` y el
  contenedor unificado.
- [ ] `ClienteNuevoPage`: usar `PaginaCabecera`, `CampoContrasena` y el
  contenedor unificado.
- [ ] Extraer `CLAVE_CLIENTES` a `api.ts` y usarla en ambas páginas.
- [ ] Suite dirigida en verde: `npm run test -- clientes`.
- [ ] Commit: `refactor(web): aplicar componentes ui reutilizables en clientes`.

## Tarea 8: aplicar a login y trabajadores — pendiente

**Archivos:**

- Modificar: `web/src/features/auth/LoginPage.tsx`
- Modificar: `web/src/features/trabajadores/TrabajadoresPage.tsx`

- [ ] `LoginPage`: usar `CampoContrasena`; conserva su layout centrado.
- [ ] `TrabajadoresPage`: usar `PaginaCabecera`, `EstadoCarga`,
  `DialogoConfirmacion` (cese, desactivación), `CampoContrasena` (alta,
  confirmación), estado vacío en la tabla y `hoyIso` en lugar de `fechaDeHoy`.
- [ ] Suite dirigida en verde: `npm run test -- LoginPage` y
  `npm run test -- TrabajadoresPage`.
- [ ] Commit: `refactor(web): aplicar componentes ui reutilizables en login y trabajadores`.

## Tarea 9: desminificar y reformatear el módulo avícola — pendiente

**Archivos:**

- Modificar: `web/src/features/avicola/GalponesPage.tsx`
- Modificar: `web/src/features/avicola/GalponPage.tsx`
- Modificar: `web/src/features/avicola/EficienciaPage.tsx`
- Modificar: `web/src/features/avicola/RegistrarRecogidaDialog.tsx`
- Modificar: `web/src/features/avicola/RegistrarBajasDialog.tsx`
- Modificar: `web/src/features/avicola/EditarRecogidaDialog.tsx`
- Modificar: `web/src/features/avicola/EditarBajasDialog.tsx`
- Modificar: `web/src/features/avicola/AsignarPlanDialog.tsx`
- Modificar: `web/src/features/avicola/CompletarTareaDialog.tsx`
- Modificar: `web/src/features/avicola/CancelarTareaDialog.tsx`
- Modificar: `web/src/features/avicola/GalponAcciones.tsx`
- Modificar: `web/src/features/avicola/TarjetaGalpon.tsx`
- Modificar: `web/src/features/avicola/VacunacionNotificacion.tsx`
- Modificar: `web/src/features/avicola/constantes.ts`

- [ ] Reformatear a mano los archivos de una línea (imports y JSX
  desagregados, sin cambiar comportamiento).
- [ ] `GalponesPage`: aplicar `PaginaCabecera`, contenedor `Container` y
  estados consistentes; conservar `TarjetaGalpon` y el estado vacío con botón.
- [ ] `constantes.hoyIso`: reformatear sin cambiar el comportamiento.
- [ ] Suite avícola en verde: `npm run test -- avicola`.
- [ ] Commit: `refactor(web): desminificar y homogeneizar el modulo avicola`.

## Tarea 10: aplicar a administración de vacunación — pendiente

**Archivos:**

- Modificar: `web/src/features/admin/vacunacion/AdminVacunacionPage.tsx`

- [ ] Desagregar imports y aplicar `PaginaCabecera`, `EstadoCarga` y el
  contenedor unificado.
- [ ] Test dirigido en verde: `npm run test -- AdminVacunacionPage`.
- [ ] Commit: `refactor(web): homogeneizar la pagina de programas de vacunacion`.

## Tarea 11: unificar contenedores y pulir páginas restantes — pendiente

**Archivos:**

- Modificar: `web/src/app/InicioPage.tsx`
- Modificar: `web/src/app/NotFoundPage.tsx`
- Modificar: `web/src/app/Proximamente.tsx`
- Modificar: `web/src/features/avicola/AvicolaInicioPage.tsx`

- [ ] Contenedores y espaciados consistentes sin cambiar textos ni roles.
- [ ] `npm run test` completo en verde.

## Tarea 12: integración y puerta — pendiente

- [ ] `npx prettier --write web` y `npm run format:check` en `web/`.
- [ ] `npm run lint`, `npm run test`, `npm run build` en `web/`.
- [ ] `./verify.ps1` (Docker corriendo) en verde.
- [ ] Revisar el diff propio y pushear a `develop`.
