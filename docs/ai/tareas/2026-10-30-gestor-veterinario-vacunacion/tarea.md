# Gestor veterinario: publica los programas de vacunación (reemplaza al Administrador)

## Pedido

Trasladar la gestión del catálogo global de programas de vacunación del
Administrador de plataforma a una cuenta de oficina de CAISY con la
funcionalidad nueva `GestorVeterinario`, en Trajano.GestorCaisy, con un ciclo
Borrador → Publicado como el de los precios de alimento. Las diez decisiones del
usuario están en la bitácora (evento `decision`).

- Spec: [`docs/superpowers/specs/2026-10-30-gestor-veterinario-vacunacion-design.md`](../../../superpowers/specs/2026-10-30-gestor-veterinario-vacunacion-design.md)
- Plan: [`docs/superpowers/plans/2026-10-30-gestor-veterinario-vacunacion.md`](../../../superpowers/plans/2026-10-30-gestor-veterinario-vacunacion.md)

## Alcance

- `FuncionalidadesCaisy.GestorVeterinario = 4` y su política.
- Dominio: `EstadoProgramaVacunacion` (Borrador, Publicado); publicar,
  eliminar borrador (borrado lógico), desactivar solo publicados; publicado
  inmutable.
- Application y API: rutas de gestión `/programas-vacunacion-caisy` para el
  gestor; `/vacunacion/programas` queda de solo lectura y solo publicados para
  cliente y trabajador; el Administrador pierde gestión y lectura del catálogo.
- Migración EF: columna `Estado`; los programas existentes con cronograma pasan
  a Publicado y el resto queda en Borrador.
- Cuentas: script `crear-usuario-gestor-veterinario.ps1` y semilla de
  desarrollo `gve@icarus.test`.
- Trajano.GestorCaisy: sección «Programas de vacunación» completa, incluida la
  subida del Excel con errores por fila; aterrizaje tras iniciar sesión según
  funcionalidad.
- PWA: se retira `/admin/vacunacion`.
- Documentación: glosario, `gestion-avicola.md`, `maquinas-estados.md`,
  `api.md`, `gestorcaisy.md`, `identidad-clientes-permisos.md`, `datos.md`,
  `frontend-pwa.md`, `web/README.md`, `AGENTS.md` y adaptadores.

## Fuera de alcance

- Versionado, vigencia o reactivación de programas.
- Liberar el nombre de un programa eliminado o desactivado.
- Cambios en tareas, asignación, bandeja del trabajador y formato del Excel.
- Notificaciones para el gestor veterinario.
- Alta de la cuenta en el bootstrap de producción (`SeedCuentasInicialesProduccion`).

## Criterios de aceptación

1. Una cuenta CAISY con `GestorVeterinario` inicia sesión en GestorCaisy y
   aterriza en «Programas de vacunación»; el menú lateral muestra el grupo
   «Sanidad» y no muestra campana.
2. El gestor crea un programa: queda como «Borrador» y no aparece en el selector
   «Asignar plan de vacunación» de un cliente.
3. En el borrador, el gestor edita nombre, cantidad de aves y observaciones, y
   sube el Excel del cronograma; el detalle muestra los ítems y la fecha de
   emisión tomada de la primera fecha del Excel.
4. Al subir un Excel con una fila inválida (por ejemplo, edad «abc» en la fila
   2), la pantalla muestra «Fila 2: La edad debe ser un número entero mayor que
   cero.» y el cronograma anterior no cambia.
5. Un borrador sin cronograma no ofrece «Publicar».
6. El gestor publica el borrador tras una confirmación; el programa pasa a
   «Publicado», ya no ofrece editar, importar ni eliminar, y aparece en el
   selector de asignación del cliente.
7. El gestor elimina otro borrador tras una confirmación; desaparece de su
   lista y no aparece para ningún cliente.
8. El cliente asigna el programa publicado a un galpón; el trabajador con
   `Vacunacion` ve sus tareas en la bandeja como antes.
9. El gestor desactiva el programa publicado tras una confirmación que advierte
   que se retiran sus tareas pendientes; el programa se ve «Inactivo» en su
   lista, deja de estar en el selector del cliente y las tareas pendientes del
   galpón desaparecen; las completadas siguen en el historial.
10. En ninguna pantalla de GestorCaisy aparecen galpones, clientes ni tareas.
11. El Administrador ya no ve «Vacunación» en el menú del PWA y
    `/admin/vacunacion` muestra la página de no encontrado.
12. En Development existe la cuenta `gve@icarus.test` con la contraseña de las
    cuentas semilla.

## Plan de pruebas

- Unitarias (`Icarus.UnitTests`): transiciones y guardas de `ProgramaVacunacion`;
  handlers de gestión y de consumo; asignación de borrador y de programa
  publicado; política `CatalogoVacunacion` sin Administrador; bit nuevo de
  `FuncionalidadesCaisy`; DTOs del gestor sin datos del tenant.
- Integración (`Icarus.IntegrationTests`, Docker): ciclo completo del gestor,
  visibilidad por actor, 403 del Administrador y del gestor en rutas del
  tenant, importación con fila inválida, flujo gestor → cliente → trabajador.
- GestorCaisy (`Trajano.GestorCaisy.Tests`): `ReclamosCaisy`, controlador,
  cliente API, flujo con API falsa (errores por fila en el HTML), menú lateral,
  aterrizaje tras sesión, contratos sin datos del tenant; la prueba de
  arquitectura existente sigue verde.
- Frontend (`web`, Vitest): navegación del Administrador sin «Vacunación»; API
  sin funciones de escritura del catálogo; `lint` y `typecheck`.
- Puerta: `./verify.ps1` completo.
- Prueba manual: criterios 1 a 12 en el entorno levantado por operaciones.

Riesgos:

- El filtro EF de tenant es permisivo para cuentas sin `ClienteId`: la única
  barrera del gestor ante tareas y galpones es la política de cada endpoint. Lo
  cubren pruebas de integración explícitas.
- La migración convierte datos de prueba existentes; un programa sin ítems
  activos queda como borrador y deja de ser visible para los clientes.
- El nombre de un borrador eliminado sigue reservado (regla de SP7).

## Ampliación del 2026-11-01: campana de tareas del trabajador

Alcance nuevo, hallado en la prueba manual y aprobado por el usuario (evento
`decision` del 2026-11-01). Solo afecta al PWA; la API no cambia.

- Spec: [`docs/superpowers/specs/2026-11-01-campana-tareas-vacunacion-trabajador-design.md`](../../../superpowers/specs/2026-11-01-campana-tareas-vacunacion-trabajador-design.md)
- Plan: [`docs/superpowers/plans/2026-11-01-campana-tareas-vacunacion-trabajador.md`](../../../superpowers/plans/2026-11-01-campana-tareas-vacunacion-trabajador.md)

### Alcance

Revisado el 2026-11-01: una sola campana general en lugar de dos (cambio
aprobado por el usuario). Las decisiones aprobadas y las pendientes de
confirmación están en la tabla «Decisiones» de la spec.

- Una campana general de notificaciones en la cabecera, siempre visible para
  cualquier usuario con sesión. Su insignia suma los pendientes de todas las
  fuentes; el menú se agrupa por secciones y cada fuente solo se consulta si el
  usuario puede verla, con su propia clave de React Query. Se muda de
  `features/control-acceso/` a `web/src/app/notificaciones/` con un contrato de
  fuente extensible.
- Fuente «Vacunación»: solo el Trabajador con `Vacunacion`. Una entrada-resumen
  («3 tareas pendientes (1 vencida)») que lleva a `/avicola/tareas`; cuenta las
  pendientes con fecha programada anterior o igual a hoy (fecha local); no se
  marca como leída. Se refresca cada 60 s, al recuperar el foco y al completar.
- Fuente «Control de acceso»: solo con el módulo `ControlAcceso`; conserva el
  comportamiento actual (incidencias, marcar leída y navegar a
  `/control-acceso/incidencias`).
- La página nueva `/avicola/tareas` muestra las pendientes vencidas y de hoy
  agrupadas por galpón, con Completar y textos largos desplegables.
- En la página del galpón, el trabajador ya no ve la sección «Vacunación». Queda
  un aviso con enlace.
- En la lista de galpones, el trabajador ya no ve `VacunacionNotificacion`.
- Se actualizan `gestion-avicola.md` §4 y, en `frontend-pwa.md`, la tabla de
  rutas y la sección de features.

### Fuera de alcance

- Cambios en la API, el DTO o el cálculo de «hoy» en el servidor.
- Push, notificaciones del navegador, estado «leído» o descartar para la
  vacunación.
- Campana de tareas o página de tareas para el Cliente; su vista de vacunación
  no cambia.
- Migrar a la campana las notificaciones internas de pedidos de alimento y de
  despachos.
- La campana de Trajano.GestorCaisy (MVC).
- Caché offline de las tareas de vacunación.

### Criterios de aceptación de la ampliación

13. Cualquier usuario con sesión (Administrador, Cliente o Trabajador, con o sin
    módulos) ve la campana en la cabecera. Sin pendientes no tiene insignia, y al
    abrirla dice «No hay notificaciones».
14. Un trabajador con `Vacunacion` y tareas pendientes vencidas o de hoy ve en
    la campana una insignia con cuántas son; las de fechas futuras no cuentan. Al
    abrirla ve la sección «Vacunación» con una sola entrada, por ejemplo «3
    tareas pendientes (1 vencida)», y al elegirla llega a «Tareas de
    vacunación».
15. La entrada de vacunación no se marca como leída: la insignia no baja al
    abrir el menú, al abrir la página de tareas ni al recargar. Baja al instante
    cuando el trabajador completa una tarea. Si el Cliente cancela una tarea o
    quita el plan, baja en menos de un minuto o al volver a la pestaña.
16. En un tenant con `ControlAcceso`, la sección «Control de acceso» funciona
    como antes: lista las incidencias con su hora; al elegir una, se marca como
    leída, el número baja y se abre la página de incidencias. Sin ese módulo, la
    sección no aparece.
17. Un trabajador que tiene ambas fuentes ve en la insignia la suma de las dos y,
    en el menú, la sección «Vacunación» seguida de «Control de acceso». Si una de
    las dos no responde (por ejemplo, bloqueando su petición en las
    herramientas del navegador), la otra sigue funcionando y la que falla
    muestra «No se pudieron cargar estas notificaciones.».
18. La página «Tareas de vacunación» agrupa las tareas por galpón con títulos
    como «Galpón 1 · 2 pendientes», y no aparecen galpones sin pendientes.
    Primero van las vencidas, con la etiqueta «Vencida», y luego las de hoy, con
    «Hoy». Cada tarea muestra la vacuna o el manejo, el día, la fecha programada
    y el botón «Completar». No aparece ninguna tarea futura ni el botón
    «Cancelar».
19. Un texto de vacuna o de aplicación largo se ve recortado a dos líneas con
    «Ver más», que lo despliega completo y cambia a «Ver menos».
20. Al completar una tarea desde esa página, la tarea desaparece, y bajan el
    contador de su grupo y la insignia. Si era la última del galpón, el grupo
    desaparece; si era la última de todas, la sección «Vacunación» deja de
    aparecer en la campana.
21. En la página de un galpón, el trabajador ya no ve la sección «Vacunación»
    ni su tabla. Si ese galpón tiene pendientes vencidas o de hoy, ve el aviso
    «N tareas pendientes en este galpón», que lleva a la página de tareas; sin
    pendientes no hay aviso. En la lista de galpones ya no ve las tablas de
    vacunación ni «Próximas (7 días)».
22. Ni un trabajador sin `Vacunacion` ni el Cliente ven la sección «Vacunación»
    en la campana ni pueden abrir la página de tareas. El Cliente conserva en la
    lista de galpones las tablas de hoy, vencidas y próximas, y en el galpón la
    sección completa: plan, historial, asignar, quitar plan y cancelar.
23. En un móvil, la página de tareas y el menú de la campana se leen sin
    desplazamiento horizontal. Sin conexión, la campana conserva el último
    número, «Guardar» queda deshabilitado en el diálogo de completar, y la
    recogida y la mortalidad offline siguen funcionando como antes.

### Plan de pruebas de la ampliación

- Vitest dirigido: `tareasPendientes.test.ts` (filtro por fecha local,
  agrupación, orden y textos, incluido el resumen de la campana),
  `fuentes.test.ts` (suma de fuentes visibles y nombre accesible),
  `CampanaNotificaciones.test.tsx` en `app/notificaciones/` (las dos pruebas
  existentes de Control de Acceso conservadas, campana vacía, suma y orden de
  secciones, fallo aislado de una fuente, Cliente sin sección de vacunación),
  `AppLayout.test.tsx` (campana siempre visible y navegación del trabajador),
  `TareasVacunacionPage.test.tsx` (grupos, orden, etiquetas, «Ver más»,
  «Completar», vacío y error), `GalponPage.test.tsx` (aviso del trabajador, sin
  consulta del historial y Cliente sin cambios) y
  `VacunacionNotificacion.test.tsx` (el trabajador no consulta).
- Suite completa de `web`, `lint` y `typecheck`. Incluye las pruebas
  offline-first existentes, que deben seguir verdes sin modificarse.
- Puerta: `./verify.ps1` completo.
- Prueba manual: criterios 13 a 23.

Riesgos de la ampliación:

- El servidor calcula «hoy» en UTC; el PWA filtra con la fecha local para no
  mostrar las tareas de mañana desde las 20:00 de Bolivia.
- La campana se monta ahora para todo usuario con sesión: todas las pruebas que
  rendericen `AppLayout` necesitan `QueryClientProvider` y un `fetch` por ruta.
- La campana del trabajador abre un menú en lugar de navegar directo: un clic
  más a cambio de una sola campana.
- Las fuentes son hooks llamados en orden fijo; una fuente nunca se monta de
  forma condicional (su visibilidad va en `visible` y `enabled`).
- Las decisiones 5, 6, 8, 9, 13, 14 y 15 de la spec están pendientes de
  confirmación del usuario antes de pasar a `DESARROLLO`.
