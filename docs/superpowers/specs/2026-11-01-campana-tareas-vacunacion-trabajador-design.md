# Campana general de notificaciones y tareas de vacunación del trabajador

Ampliación de alcance de la tarea `2026-10-30-gestor-veterinario-vacunacion`,
hallada en la prueba manual y aprobada por el usuario (evento `decision` del
2026-11-01 en la bitácora). Cambio solo del PWA (`web/`); la API no cambia.
Antecedentes: [spec SP7](2026-08-19-sp7-vacunacion-design.md) y
[gestión avícola, §4](../../proyecto/gestion-avicola.md).

**Revisión del 2026-11-01 (campana única).** La primera versión de esta spec
proponía una `CampanaVacunacion` nueva junto a la `CampanaNotificaciones` de
Control de Acceso, que `AppLayout` monta solo si el tenant tiene el módulo
`ControlAcceso`. El usuario aprobó sustituirlas por **una campana general de
notificaciones**, siempre visible y con secciones por fuente. Desaparece el
riesgo de dos campanas. El resto del alcance se conserva.

## Contexto

Hoy el trabajador con `Vacunacion` ve las tareas en dos lugares incómodos:

- En la lista de galpones (`GalponesPage`), el componente
  `VacunacionNotificacion` muestra dos tablas anchas: «hoy o vencidas» y
  «Próximas (7 días)».
- En la página de un galpón (`GalponPage`), tras la tabla de registros del día,
  la sección «Vacunación» lista el historial completo del galpón: pasadas,
  canceladas, de hoy y futuras.

En el móvil esto obliga a recorrer tablas con columnas que no caben y muestra
tareas futuras que el trabajador todavía no puede ejecutar.

La cabecera ya tiene una campana, `features/control-acceso/CampanaNotificaciones`:
solo existe en tenants con `ControlAcceso`, lista las incidencias de acceso con
su hora, marca cada una como leída al elegirla y navega a
`/control-acceso/incidencias`. Su contador es el `contador` de sin leer que
devuelve `GET /api/control-acceso/notificaciones`.

## Hallazgo sobre la API

No hace falta endpoint nuevo. `GET /api/vacunacion/tareas`
(`ListarNotificacionVacunacionQuery`, política `Funcionalidad:Vacunacion`)
devuelve las pendientes de **todo el tenant**: `vencidasYHoy`
(`FechaProgramada <= hoy`) y `proximas` (hasta `hoy + 7`). El trabajador con
`Vacunacion` ya está autorizado, y completar usa
`POST /api/vacunacion/tareas/{id}/completar` con la misma política.

El servidor calcula «hoy» con la fecha UTC. Bolivia está en UTC−4: desde las
20:00 locales, `vencidasYHoy` ya incluye las tareas de mañana. El PWA filtra
además con la fecha local (`fechaProgramada <= hoyIso()`), y así cumple la regla
«las futuras no se muestran» sin tocar el backend.

El DTO trae `galponId`, no el número de galpón. El número se obtiene con
`listarGranjas` y `listarGalpones`, que ya usa la lista de galpones y tienen
caché de lectura offline.

## Decisiones

La numeración es propia de esta spec. «Aprobada» significa que el usuario la
confirmó de forma explícita; «Pendiente» significa que la propone el
planificador y el usuario debe confirmarla antes de pasar a `DESARROLLO`.

| # | Decisión | Estado |
|---|---|---|
| 1 | Sin endpoint nuevo: se reutiliza `GET /api/vacunacion/tareas`. | Aprobada |
| 2 | El PWA filtra por la fecha local; las futuras nunca se muestran al trabajador. | Aprobada |
| 3 | Página `/avicola/tareas` con las pendientes vencidas y de hoy por galpón y la acción Completar. | Aprobada |
| 4 | En el galpón, el trabajador pierde la sección «Vacunación» (plan y tabla) y ve solo un aviso con enlace. | Aprobada |
| 5 | En la lista de galpones, el trabajador deja de ver `VacunacionNotificacion`, incluidas las «Próximas (7 días)». | Pendiente |
| 6 | Refresco de cada fuente cada 60 s y al recuperar el foco; sin red se conserva el último valor sin mostrar error. | Pendiente |
| 7 | El Cliente no cambia: conserva sus tablas, su historial y sus acciones. | Aprobada |
| 8 | Recorte a dos líneas con «Ver más» para textos de más de 80 caracteres. | Pendiente |
| 9 | Orden de los grupos: el de la tarea más antigua primero; empate por número de galpón (orden numérico); galpón desconocido como «Galpón —». | Pendiente |
| 10 | Una sola campana general, siempre visible con sesión, con secciones por fuente y fuentes declarativas. Se mueve a `web/src/app/notificaciones/`. | Aprobada |
| 11 | Vacunación: una sola entrada-resumen que lleva a `/avicola/tareas`; no se marca como leída. | Aprobada |
| 12 | Control de acceso conserva su comportamiento: incidencias con hora, marcar leída y navegar a `/control-acceso/incidencias`. | Aprobada |
| 13 | Una sección se muestra solo si su fuente es visible y tiene entradas o un error; si ninguna se muestra, el menú dice «No hay notificaciones». | Pendiente |
| 14 | Una fuente que falla sin datos previos muestra en su sección «No se pudieron cargar estas notificaciones.» y aporta 0 a la insignia; con datos previos conserva el último valor. | Pendiente |
| 15 | Nombre accesible de la campana: «Notificaciones: N pendientes» (singular con una) y «Notificaciones sin pendientes». Orden de secciones: Vacunación, luego Control de acceso. | Pendiente |

## Diseño

### Regla de visibilidad de la vacunación

- Una tarea **notifica** al trabajador si está `Pendiente` y su
  `FechaProgramada` es anterior o igual a la fecha local de hoy.
- Las futuras no notifican ni se muestran al trabajador en ninguna pantalla.
- La entrada de vacunación no tiene estado «leído» ni se descarta. Solo baja
  cuando la tarea se completa, o cuando el Cliente la cancela o quita el plan.
- La consulta es la fuente de verdad. No hay jobs, push ni almacenamiento local
  nuevo.

### Campana general (`app/notificaciones/CampanaNotificaciones`)

- `AppLayout` la monta siempre que haya sesión (`estaAutenticado`), con
  independencia del rol, de los módulos del tenant y de que haya o no
  notificaciones. Cubre también la sesión offline.
- Es un botón con el ícono de campana que abre un menú. Si la suma de pendientes
  es mayor que cero, muestra una insignia con el número; con cero no muestra
  insignia.
- Nombre accesible: «Notificaciones: N pendientes», en singular con una, y
  «Notificaciones sin pendientes» con cero (decisión 15).
- La insignia es la suma de los `conteo` de las fuentes visibles.
- El menú se agrupa por secciones, con un subtítulo (`ListSubheader`) por
  fuente. Una sección aparece solo si su fuente es visible para el usuario y
  tiene entradas o un error (decisión 13). Si no aparece ninguna, el menú
  muestra «No hay notificaciones».
- Elegir una entrada ejecuta su acción previa, si la tiene (por ejemplo, marcar
  como leída), cierra el menú y navega a su destino. Si la acción falla, no se
  navega, igual que hoy.
- La campana no conoce ninguna fuente concreta: recibe la lista de
  `useFuentesNotificaciones`.

### Contrato de fuente

Cada fuente es un hook que devuelve:

| Campo | Significado |
|---|---|
| `id` | Identificador estable de la fuente. |
| `seccion` | Título de la sección en el menú. |
| `visible` | Si el usuario puede ver la sección. Con `false`, la consulta de la fuente no se ejecuta (`enabled: false`). |
| `conteo` | Lo que la fuente suma a la insignia. |
| `entradas` | Lista de `{ id, texto, detalle?, icono?, destino, alSeleccionar? }`. |
| `error` | La consulta falló y no hay datos previos. |

`useFuentesNotificaciones` llama a cada hook de forma incondicional y en orden
fijo, y devuelve la lista. Añadir una fuente futura consiste en crear su hook
junto a su feature y añadir una línea en `useFuentesNotificaciones`, sin tocar
la campana. Cada fuente usa su propia clave de React Query, así el fallo de una
no afecta a las demás (decisión 14).

### Fuente «Vacunación»

- Visible solo si `rol === 'Trabajador'` y el usuario tiene `Vacunacion`. El
  Cliente, aunque tenga `Vacunacion`, no la ve ni la consulta.
- Comparte la clave de React Query `['vacunacion', 'notificacion']`
  (`CLAVE_NOTIFICACION_VACUNACION`) con el resto de la vacunación. Así,
  completar, cancelar y asignar o quitar un plan, que ya invalidan esa clave,
  actualizan la insignia al instante.
- `conteo`: pendientes vencidas y de hoy según la fecha local.
- Una sola entrada-resumen, sin icono ni acción previa:
  «3 tareas pendientes (1 vencida)», «2 tareas pendientes (2 vencidas)»,
  «1 tarea pendiente». Sin vencidas no lleva paréntesis. Destino:
  `/avicola/tareas`. Sin pendientes no hay entrada y la sección no aparece.
- Refresco: `refetchInterval` de 60 s, más el refetch al recuperar el foco que
  React Query trae por defecto (decisión 6). Sin red, React Query pausa la
  consulta y la insignia conserva el último valor; no se muestra error.

### Fuente «Control de acceso»

- Visible solo si el tenant tiene el módulo `ControlAcceso`, sin mirar el rol,
  como hoy.
- Conserva exactamente el comportamiento actual: clave
  `['control-acceso', 'notificaciones']`, refresco de 60 s, `conteo` igual al
  `contador` de sin leer del servidor, una entrada por notificación con el texto
  «Hay una incidencia de marcación por revisar» (o «Nueva notificación»), la
  hora en `America/La_Paz` como detalle y el icono actual. Elegirla llama a
  `marcar-leida`, invalida la clave y navega a `/control-acceso/incidencias`.
- `textoNotificacion` y `horaResumida` se mudan del componente al hook de la
  fuente sin cambios.

### Página `/avicola/tareas` (`TareasVacunacionPage`)

- Ruta protegida con `RequiereRol(['Trabajador'])` y
  `RequiereFuncionalidad(['Vacunacion'])`, cargada en diferido como el resto.
  El título de la barra queda «Gestión Avícola» por prefijo de ruta.
- Encabezado «Tareas de vacunación».
- Agrupa por galpón. Cada grupo tiene el título «Galpón 1 · 2 pendientes»
  (singular: «1 pendiente»). Un galpón sin pendientes no aparece. Si el galpón
  no figura en la lista, el título usa «Galpón —» (decisión 9).
- Orden de los grupos: primero el que tiene la tarea más antigua, de modo que los
  galpones con vencidas quedan arriba. Empate por número de galpón, con orden
  numérico (decisión 9).
- Orden dentro del grupo: por `fechaProgramada` ascendente, así las vencidas van
  primero. Cada tarea lleva una etiqueta «Vencida» (color de error) u «Hoy»
  (color de advertencia), con `clasificarTarea`.
- Cada tarea es una tarjeta apilada, sin tabla, pensada para móvil:
  - texto de la vacuna o del manejo (`vacuna`);
  - «Día N · Programada AAAA-MM-DD»;
  - aplicación, si existe (`modoAplicacion ?? observacionesProgramadas`);
  - botón «Completar», que abre `CompletarTareaDialog` sin cambios. El diálogo
    ya se deshabilita sin conexión e invalida las claves de vacunación.
- Los textos de más de 80 caracteres se recortan a dos líneas
  (`-webkit-line-clamp: 2`) con un botón «Ver más» / «Ver menos»
  (`aria-expanded`). El umbral fijo de caracteres hace la regla comprobable en
  jsdom, donde no se pueden medir líneas (decisión 8).
- Sin pendientes: «No hay tareas de vacunación pendientes.»
- Error de carga: alerta «No se pudieron cargar las tareas de vacunación.» con
  «Reintentar». Si hay datos previos y falla un refresco, se siguen mostrando
  los datos.
- Sin botón «Cancelar». Cancelar es exclusivo del Cliente.

### Página del galpón para el trabajador

- Se retira la sección «Vacunación» completa: título, plan asignado y tabla.
  `GalponPage` deja de pedir `GET /galpones/{id}/vacunacion/tareas` cuando el
  rol es Trabajador.
- Si el galpón tiene pendientes que notifican, aparece bajo la cabecera del
  galpón un aviso pequeño (`Alert` de advertencia) con un enlace
  «N tareas pendientes en este galpón» (singular: «1 tarea pendiente en este
  galpón») hacia `/avicola/tareas`. Sin pendientes, no hay aviso.
- El conteo sale de la misma consulta de la fuente «Vacunación», filtrada por
  `galponId`.

### Lista de galpones para el trabajador

- `VacunacionNotificacion` no se muestra ni consulta para el rol Trabajador: lo
  reemplaza la campana, y sus «Próximas (7 días)» violarían la regla de futuras
  (decisión 5).

### Cliente

Sin cambios en la vacunación. Conserva en la lista de galpones
`VacunacionNotificacion` (hoy y vencidas, próximas, Completar y Cancelar) y en
el galpón la sección completa: plan, historial, asignar, quitar plan y cancelar.
No ve la sección «Vacunación» de la campana ni la ruta `/avicola/tareas`. Sí ve
la campana general, como todo usuario con sesión, con la sección «Control de
acceso» si su tenant tiene el módulo.

## Código nuevo y tocado

| Archivo | Cambio |
|---|---|
| `web/src/app/notificaciones/fuentes.ts` | Nuevo. Contrato de fuente, `totalPendientes`, `etiquetaCampana` y el mensaje de error de sección. |
| `web/src/app/notificaciones/useFuentesNotificaciones.ts` | Nuevo. Registro ordenado de fuentes. |
| `web/src/app/notificaciones/CampanaNotificaciones.tsx` | Nuevo. Campana general; sustituye a la de Control de Acceso. |
| `web/src/features/control-acceso/useFuenteNotificacionesAcceso.tsx` | Nuevo. Fuente «Control de acceso» con la lógica que hoy tiene su campana. |
| `web/src/features/control-acceso/CampanaNotificaciones.tsx` y `.test.tsx` | Se eliminan; la prueba se muda a `app/notificaciones/` y se amplía. |
| `web/src/features/avicola/tareasPendientes.ts` | Nuevo. Funciones puras `pendientesHastaHoy`, `agruparPorGalpon`, `textoPendientesGalpon`, `textoTituloGrupo` y `textoResumenVacunacion`. |
| `web/src/features/avicola/useTareasPendientesVacunacion.ts` | Nuevo. Hook sobre la consulta compartida, con refresco de 60 s y filtro local. |
| `web/src/features/avicola/useFuenteNotificacionesVacunacion.ts` | Nuevo. Fuente «Vacunación». |
| `web/src/features/avicola/TextoRecortado.tsx` | Nuevo. Recorte a dos líneas con «Ver más». |
| `web/src/features/avicola/TareasVacunacionPage.tsx` | Nuevo. Página agrupada. |
| `web/src/app/AppLayout.tsx` | Monta la campana general con sesión, en lugar de la de Control de Acceso condicionada al módulo. |
| `web/src/app/router.tsx`, `paginasDiferidas.tsx` | Ruta `/avicola/tareas`. |
| `web/src/features/avicola/GalponPage.tsx` | Aviso para el Trabajador; sección e historial solo para los demás roles. |
| `web/src/features/avicola/VacunacionNotificacion.tsx` | No consulta ni pinta para el Trabajador. |
| `docs/proyecto/gestion-avicola.md`, `docs/proyecto/frontend-pwa.md` | Documentan la campana general, sus fuentes, la ruta y la regla de visibilidad. |

## Restricciones

- Offline-first intacto: la vacunación sigue siendo online. No se añade nada a
  la cola IndexedDB ni al precalentado. Las pruebas de `offline.test.ts`,
  `GalponPage.test.tsx` (pendientes en cola) y `PendientesOffline` siguen
  verdes.
- Anti-PII: la campana, sus fuentes y la página no muestran ni registran datos
  del trabajador. Los mensajes de error son genéricos. No se escriben eventos de
  diagnóstico con nombres de vacunas ni observaciones.
- Las pruebas existentes de la campana de Control de Acceso y de `AppLayout` se
  adaptan sin perder ninguna comprobación: contador de sin leer, apertura del
  menú, `marcar-leida` y navegación a incidencias; marca, navegación, rutas
  activas, correo y cierre de sesión.
- Español neutro, sin voseo, en los textos de UI nuevos.

## Riesgos

- La campana se monta ahora para todos los usuarios con sesión. Todas las
  pruebas que rendericen `AppLayout` necesitan `QueryClientProvider` y un
  `fetch` simulado por ruta en lugar del secuencial actual.
- La campana deja de navegar directamente: abre un menú también para la
  vacunación. Es un clic más para el trabajador, a cambio de una sola campana.
- Las fuentes se llaman como hooks en orden fijo. Una fuente nunca debe
  montarse de forma condicional: su visibilidad va en `visible` y en `enabled`.
- El aviso y la entrada de vacunación dependen de la fecha local del
  dispositivo. Un reloj mal configurado adelanta o atrasa el conteo, igual que
  ya ocurre con `clasificarTarea`.
- Un tenant con `ControlAcceso` sin pendientes de vacunación verá el mismo menú
  que hoy, pero con el subtítulo «Control de acceso».

## Fuera de alcance

- Cambios en la API, el DTO o el cálculo de «hoy» en el servidor.
- Notificaciones push, del navegador o persistentes nuevas.
- Campana de tareas o página de tareas para el Cliente.
- Migrar a la campana las notificaciones internas de pedidos de alimento y de
  despachos de huevo.
- La campana de Trajano.GestorCaisy (MVC).
- Caché offline de las tareas de vacunación.
