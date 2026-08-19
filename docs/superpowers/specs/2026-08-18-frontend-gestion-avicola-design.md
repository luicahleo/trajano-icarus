# Frontend Gestión Avícola (SP5/SP6) — Diseño

Interfaz de la PWA para probar y usar lo implementado en SP5 (granjas y
galpones) y SP6 (producción diaria, mortalidad y eficiencia). Diseño validado
en brainstorming con el usuario el 2026-08-18, decisión por decisión.

## Contexto

El backend de Gestión Avícola está completo y verificado contra el código:
`Icarus/src/Host/Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs` expone
granjas, galpones, producción, mortalidad y eficiencia con las políticas de
funcionalidad previstas (`Granjas`, `Galpones`, `ProduccionHuevos`,
`Mortalidad`). No hace falta rediseñar ni ampliar esos endpoints.

El frontend (`web/`, React 19 + MUI 9 + TanStack Query + RHF/zod + React
Router 7) no tiene nada avícola: ni tipos, ni rutas, ni guarda por
funcionalidad, ni soporte responsive, ni offline de datos. El usuario
principal de este incremento es el **trabajador recolector** con el celular
en el galpón; el cliente (granjero) administra granja y galpones.

Reglas del dominio que la UI debe hacer visibles (glosario y specs SP5/SP6):

- Un cliente tiene **una sola granja activa**; cada galpón alberga un lote.
- Conteo en **maples (30 huevos) + unidades sueltas (< 30)**, igual para el
  huevo vendible y para el de descarte. El descarte no entra al vendible ni
  a la eficiencia.
- Recogidas múltiples por galpón y día; el total del día es la suma.
- La **fecha la fija el servidor**; edición y desactivación solo el mismo
  día; pasada la medianoche el día queda **sellado** (solo lectura).
- **Eficiencia diaria** = vendible del día ÷ gallinas vivas (snapshot);
  bajo el **70 %** el lote se considera para descarte. Métrica derivada,
  nunca persistida.
- La mortalidad descuenta el inventario del galpón; editarla repone y
  descuenta la nueva cantidad.
- **Idempotencia**: cada recogida/mortalidad acepta `IdempotencyKey`
  generada por el cliente.

## Decisiones

### 1. Navegación y jerarquía: galpones como entrada directa

La sección "Gestión Avícola" abre directo en la lista de galpones de la
granja activa; la granja aparece como encabezado con su nombre (renombrable
por el cliente). Como un cliente tiene una sola granja activa, una jerarquía
explícita granja → galpones sería un nivel casi siempre vacío, y un selector
de granja sería complejidad sin uso (YAGNI). Si el cliente no tiene granja,
`/avicola` muestra el flujo de primera vez: crear la granja con solo el
nombre.

### 2. Experiencia móvil: AppLayout responsive general

El `AppLayout` pasa a ser responsive: en pantalla angosta la navegación se
vuelve menú hamburguesa con drawer; en escritorio queda igual. Las pantallas
avícolas se diseñan mobile-first (tarjetas en vez de tablas, botones
grandes, `inputMode="numeric"` en cantidades). Hoy no hay ni un breakpoint
en `web/src`, y el caso de uso principal es móvil: adaptar solo las páginas
nuevas dejaría al recolector navegando con una AppBar de escritorio.

### 3. Recogida y mortalidad en un mismo flujo visual

Formulario único "Registrar recogida" con los conteos de huevos (vendible y
descarte) y una sección plegable "¿Hubo bajas?" con la cantidad de muertas.
Si viene con cantidad > 0, al guardar se envían **dos requests** (producción
+ mortalidad), como anticipa el spec de SP6. Además, una acción aparte
"Registrar bajas" cubre la mortalidad sola (bajas de madrugada). Si el
segundo request falla tras éxito del primero, la UI muestra el error y
permite reintentar solo la mortalidad; nunca se reenvía la producción ya
creada.

### 4. Edición y desactivación durante el día

El detalle del galpón muestra las recogidas y bajas de hoy en una lista
cronológica unificada, cada una con "Editar" (el mismo formulario
precargado) y "Eliminar" (diálogo de confirmación; el backend desactiva,
nunca borra). Para fechas pasadas, la misma lista se muestra en **solo
lectura** con el aviso "Día sellado: no se puede corregir". La regla del
sellado se hace visible, no se esconde.

### 5. Representación de maples, sueltos y descarte

En el ingreso: campos numéricos "Maples" y "Unidades sueltas" con el total
en huevos calculado en vivo ("= 305 huevos") como verificación inmediata;
bloque de descarte visualmente diferenciado con el mismo par de campos. En
listas y resúmenes: "10 maples + 5 (= 305)". Nadie cuenta 305 huevos de a
uno: se cuentan maples, y la UI respeta ese conteo físico. La constante
`HUEVOS_POR_MAPLE = 30` se declara una sola vez en el frontend (paridad con
`Maple.HuevosPorMaple` del dominio).

### 6. Inventario y eficiencia

Cada tarjeta de galpón muestra número, gallinas actuales/capacidad y
eficiencia de hoy. El detalle repite inventario y eficiencia del día en el
encabezado. La vista `/avicola/galpones/:galponId/eficiencia` lista los días
del rango (por defecto, últimos 14 días): fecha, vendible, descarte,
gallinas vivas y % de eficiencia. El ajuste manual de inventario (endpoint
existente) es una acción del galpón, solo para el cliente. Sin gráficos ni
dashboard en este incremento.

### 7. Umbral del 70 %

Toda eficiencia bajo el 70 % se muestra en color de error con un chip
"Bajo umbral — considerar descarte", con el mismo criterio en la tarjeta del
galpón, el encabezado del día y la tabla de histórico. Color **y** texto (no
solo color, por accesibilidad); sin umbrales inventados (nada de semáforo
de tres niveles: el negocio solo definió el 70 %).

### 8. Permisos para Cliente y Trabajador

El backend ya autoriza (403 sin la funcionalidad), pero el frontend no podía
saber los permisos: ni el JWT ni `/identidad/me` incluían módulos ni
funcionalidades. **Cambio de backend autorizado por el usuario, acotado**:
`GET /identidad/me` pasa a devolver además `modulos: string[]` (del cliente)
y `funcionalidades: string[]` (del trabajador). Nada más se toca del
backend. En el frontend: `UsuarioActual` se amplía con esos campos, hook
`useFuncionalidad('ProduccionHuevos')` y guarda de ruta
`RequiereFuncionalidad` (par de `RequiereRol`). El cliente con el módulo
`GestionAvicola` ve todo; el trabajador solo las acciones de sus
funcionalidades asignadas. Ocultar UI no sustituye la autorización del
backend. Descartado: sondeo por 403 (mala UX) y gating solo por rol (rompe
el entitlement fino).

### 9. Estados vacíos, carga, errores y conectividad

Cada pantalla avícola define: estado vacío con acción ("Todavía no hay
galpones — Crear el primero"), carga (esqueleto/spinner), error con botón de
reintentar, y un banner global reutilizable "Sin conexión" (basado en
`navigator.onLine` y los eventos `online`/`offline`) que deshabilita el
envío con un mensaje claro. Errores mapeados como en `TrabajadoresPage`:
`ApiError.code`, `erroresValidacion` a campos del formulario, mensaje
genérico sin PII en el resto.

### 10. Online-first

Este incremento funciona con conexión; el banner de "sin conexión" bloquea
el envío. La **cola offline con IndexedDB + Background Sync queda como
subproyecto propio** con su brainstorming (tiene decisiones propias de
conflicto y UX de sincronización). El camino queda listo: el `api.ts` de
recogida y mortalidad ya genera la `IdempotencyKey` (`crypto.randomUUID()`)
en el cliente aunque el envío sea directo, y el backend ya es idempotente.

## Arquitectura frontend

- Feature nueva `web/src/features/avicola/` con el patrón del proyecto:
  `api.ts` (solo llama a `peticion` de `lib/http.ts`), páginas y
  componentes locales. Sin librerías nuevas (MUI 9 + RHF + zod + TanStack
  Query, según `web/AGENTS.md`).
- Tipos nuevos en `lib/tipos.ts`: `Granja`, `Galpon`, `RecogidaResumen`,
  `ProduccionDiaResumen`, `MortalidadResumen`, `EficienciaDia`, y la
  ampliación de `UsuarioActual`.
- `queryKey` por parámetros (`['avicola', 'galpones']`,
  `['avicola', 'produccion', galponId, fecha]`, etc.) e invalidación por
  prefijo tras cada mutación.
- Fechas `yyyy-MM-dd` y horas `HH:mm`: la fecha la fija el servidor; la
  hora real la manda el cliente.
- Rutas (lazy vía `paginasDiferidas.tsx`, con `ProtectedRoute` +
  `RequiereFuncionalidad`):
  - `/avicola` → primera vez (crear granja) o redirección a galpones.
  - `/avicola/galpones` → tarjetas de galpones con encabezado de granja.
  - `/avicola/galpones/:galponId` → día del galpón: resumen, lista
    cronológica, acciones.
  - `/avicola/galpones/:galponId/eficiencia` → histórico con rango.
- `ENLACES_POR_ROL` e `inicioSegunRol.ts`: Cliente y Trabajador aterrizan en
  `/avicola` (el trabajador, en su primera funcionalidad disponible).

## Cambio de backend (autorizado, acotado)

Único cambio: `GET /identidad/me` devuelve además `modulos` y
`funcionalidades`. Para el cliente, los módulos habilitados de su tenant;
para el trabajador, las funcionalidades asignadas; para el administrador,
listas vacías (su UI no las usa). Requiere consultar el módulo Clientes
desde el endpoint (la composición vive en el Host, como ya es patrón). Con
su test de integración. Si al implementarlo apareciera un bloqueo real
(acoplamiento indebido), se vuelve al usuario antes de ampliar nada.

## Pruebas

Vitest + Testing Library con las utilidades existentes (`fetchSimulado`,
`renderPagina`):

- Componentes por pantalla: lista de galpones, detalle del día, formulario
  de recogida (incluido el total en vivo y la sección de bajas), eficiencia
  con umbral, primera vez.
- Guarda `RequiereFuncionalidad` y hook `useFuncionalidad` (Cliente con
  módulo, Trabajador con y sin funcionalidad, 403).
- Integración del cliente HTTP: contratos de `api.ts` contra fetch simulado
  (rutas, bodies, `IdempotencyKey`, mapeo de `ApiError` y de errores de
  validación a campos).
- Estados: vacío, carga, error con reintento, banner de sin conexión.
- Responsive básico: drawer en viewport angosto (jsdom con
  `matchMedia` simulado).
- Backend: test de integración del `/me` ampliado.

## Fuera de alcance

Cola offline (IndexedDB, Background Sync, resolución de conflictos),
gráficos y dashboard de granja, alertas automáticas por umbral, asignación
de funcionalidades a trabajadores en UI (este incremento solo las lee),
multi-granja, vacunación, alimentación, despachos, precios y cualquier otro
cambio de backend distinto del `/me` acordado.
