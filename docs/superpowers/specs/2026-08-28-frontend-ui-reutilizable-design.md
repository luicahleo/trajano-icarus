# Frontend reutilizable y homogéneo — Diseño

Fecha: 2026-08-28
Estado: aprobado en brainstorming con el usuario

## Contexto

El frontend de `web/` tiene tres problemas que se refuerzan entre sí:

1. **Código no homogéneo.** Varios archivos están minificados en una sola línea
   (`GalponesPage.tsx`, `EficienciaPage.tsx`, `RegistrarRecogidaDialog.tsx`,
   `EditarBajasDialog.tsx`, `EditarRecogidaDialog.tsx`, `GalponAcciones.tsx`,
   `constantes.ts`, `AdminVacunacionPage.tsx`) y aun los archivos formateados
   tienen líneas que superan el `printWidth: 100` (AppLayout:57, LoginPage:75,
   TrabajadoresPage:189, GalponPage:99-106). Prettier no se corrió al
   commitear. Los contenedores de página alternan entre `Box sx={{ p: 4 }}`,
   `Container` y `Container sx={{ py: 2 }}`, y cada página repite el trío
   carga/error/vacío con estilos distintos.
2. **Cero reutilización.** La cabecera de página (título + botón de acción) se
   duplica en 6 páginas; el diálogo de confirmación genérico en 5; el campo de
   contraseña con el ojo mostrar/ocultar en 3 (`LoginPage`,
   `ClienteNuevoPage`, `TrabajadoresPage`); las tablas en 2; el mapeo de
   errores de validación del servidor a `setError` en 2; `hoyIso` está
   duplicado como `fechaDeHoy` en `TrabajadoresPage`.
3. **Aspecto de plantilla por defecto de MUI.** Tablas sin hover ni densidad,
   sin estado vacío (con cero filas se pinta una tabla con solo los encabezados),
   `CircularProgress` crudo sin centrar, listas planas.

El tema existente (`web/src/app/theme.ts`, paleta pino/terracota/crema con
fuentes Open Sans + Prompt) ya tiene identidad propia y no se toca: el problema
es que cada página aplica el layout y los estados a mano, en lugar de delegar en
el tema y en unos pocos ensambles reutilizables.

## Decisión de fondo: reusar MUI, no inventar

El usuario pidió explícitamente reutilizar los componentes de MUI en lugar de
crear una capa paralela. Se aplica esa regla con dos matices que el diseño
sigue:

- **La homogeneidad visual se resuelve en el tema** (`components` de MUI en
  `theme.ts`), no con componentes nuevos. Los overrides de tema hacen que toda
  tabla, diálogo, chip, lista y campo se vean iguales y con identidad propia en
  todas las páginas a la vez, sin tocar cada página.
- **La reutilización de ensambles repetidos es composición, no una librería
  paralela.** Cuando el mismo grupo de piezas MUI aparece 5 veces (diálogo de
  confirmación, campo de contraseña, cabecera de página), se extrae un
  componente compuesto que usa MUI por dentro. Esto es el patrón idiomático de
  MUI y reduce la duplicación real; no añade ninguna dependencia.

## Decisiones

### 1. Overrides de tema en `theme.ts`

Se amplían los `components` existentes, siempre con la paleta actual:

- `MuiTableCell`: padding consistente; celdas de encabezado con mayúsculas,
  letter-spacing, peso 700, color secundario y borde inferior doble.
- `MuiTableRow`: hover suave con `alpha(colores.pinoClaro, 0.4)` y transición.
- `MuiDialog`: radio de papel redondeado (20) y diálogos con identidad propia.
- `MuiDialogTitle`: tipografía Prompt, peso 600.
- `MuiDialogActions`: padding y `gap` consistentes.
- `MuiOutlinedInput`: mantener radio; borde enfocado y hover en color pino.
- `MuiChip`: radio 8 y peso 600.
- `MuiAlert`: radio 12.
- `MuiListItem`: hover suave y radio para las listas de registros y vacunación.

Ningún override cambia la paleta ni las fuentes. No se agregan colores nuevos.

### 2. Componentes compuestos en `src/app/ui/`

Carpeta nueva `web/src/app/ui/` para los ensambles que se repiten. Cada uno usa
piezas MUI por dentro y expone una API mínima:

- **`PaginaCabecera`**: título + subtítulo opcional + área de acciones a la
  derecha. Reemplaza el patrón `Stack direction="row" + Typography h4 + Button`.
- **`DialogoConfirmacion`**: props `abierto`, `titulo`, `mensaje`,
  `etiquetaConfirmar` (por defecto «Confirmar»), `color`, `pendiente`,
  `onCancelar`, `onConfirmar`. Los botones mantienen los nombres accesibles
  actuales («Cancelar» / «Confirmar») para no romper los tests.
- **`CampoContrasena`**: `TextField` MUI con toggle de visibilidad
  (mostrar/ocultar) y `slotProps` coherentes. Compatible con `react-hook-form`
  vía props `register`-style: acepta los mismos props que `TextField`.
- **`EstadoCarga`**: centraliza el trío carga/error/contenido: si `cargando`,
  `CircularProgress` centrado; si `error`, `Alert` con reintento opcional; si
  no, los hijos. Props `cargando`, `error`, `mensajeError`, `onReintentar`.

No se crea ningún wrapper de tabla: las dos tablas existentes usan `Table` de
MUI directamente, con la misma estructura y una fila de estado vacío con
`colSpan` y mensaje específico por página.

### 3. Contenedores de página unificados

Todas las páginas de listado y detalle usan `Container maxWidth="lg"` con
padding vertical estándar. `LoginPage`, `AvicolaInicioPage` y el splash de
bienvenida conservan su layout propio (centrado), porque son pantallas
diferentes por naturaleza.

### 4. Formato homogéneo

Se corre prettier sobre `web/` y se reformatea a mano los archivos minificados
(desagregar imports y JSX de una línea). Queda prohibido commitear con
`format:check` en rojo para estos archivos.

### 5. Lógica duplicada menor

- `TrabajadoresPage.fechaDeHoy` se reemplaza por `hoyIso` de
  `features/avicola/constantes.ts`. `hoyIso` se reformatea pero no cambia de
  comportamiento.
- La clave `['clientes']` se extrae a `features/admin/clientes/api.ts` (o
  constante local única) para no repetir `CLAVE_CLIENTES` en dos páginas.
- El mapeo de errores de validación servidor→formulario se conserva en cada
  formulario (el mensaje y el campo dependen del esquema); no se abstrae.

## Pruebas de aceptación (frontend)

- `npm run format:check` en verde (ningún archivo queda minificado ni con
  líneas que excedan el ancho).
- La suite de tests existente queda en verde: los nombres accesibles y los
  roles (`dialog`, `button`, `link`) que fijan los tests se conservan.
- `ClientesListaPage` y `TrabajadoresPage` muestran un estado vacío con
  mensaje cuando la lista está vacía, en vez de una tabla con solo encabezados.
- Los diálogos de confirmación de clientes, trabajadores, galpón y registros
  usan `DialogoConfirmacion` y conservan el comportamiento y los textos.
- Las páginas de listado muestran cabecera con título y acciones mediante
  `PaginaCabecera`.
- `LoginPage`, `ClienteNuevoPage` y `TrabajadoresPage` usan `CampoContrasena`.
- `npm run lint`, `npm run test` y `npm run build` en verde.

## Verificación

- En `web/`: `npm run format:check`, `npm run lint`, `npm run test`,
  `npm run build`.
- Puerta completa del repo: `./verify.ps1` (Docker corriendo para los tests de
  integración del backend) antes del commit y push.

## Fuera de alcance

- Cambiar la paleta, las fuentes o el tema visual actual.
- Añadir dependencias nuevas (MUI X, TanStack Table, u otra librería de UI,
  formularios, estado remoto o iconos).
- Refactorizar la lógica de negocio, los `queryKey`, la API o los contratos.
- Toques funcionales fuera del alcance visual (paginación, ordenamiento de
  columnas, búsqueda).
- Backend, PWA y configuración de Vite.
- Cambiar textos existentes que fijan los tests.
