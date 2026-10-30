# Gestor veterinario: publica los programas de vacunación

Cambio sobre el catálogo global de vacunación de SP7
([spec SP7](2026-08-19-sp7-vacunacion-design.md)). Tarea
`2026-10-30-gestor-veterinario-vacunacion`; las decisiones de negocio del
usuario están registradas en su bitácora.

## Contexto

Hoy el Administrador de plataforma crea, edita, importa (Excel) y desactiva los
programas de vacunación desde `/admin/vacunacion` del PWA, y todo programa
activo es visible y asignable por los clientes desde que se crea, aunque aún no
tenga cronograma. El catálogo es responsabilidad sanitaria de CAISY, no de la
plataforma: lo gestionará una cuenta de oficina de CAISY con una funcionalidad
nueva, desde la aplicación MVC Trajano.GestorCaisy, con un ciclo de borrador y
publicación como el de los precios de alimento.

## Decisiones del usuario

1. Funcionalidad nueva `FuncionalidadesCaisy.GestorVeterinario = 4`.
2. UI en Trajano.GestorCaisy (rol `GestorCaisy` + funcionalidad). Desaparece
   `/admin/vacunacion` del PWA.
3. El Administrador pierde toda gestión del catálogo: crear, editar, importar
   Excel y desactivar.
4. Ciclo como precios de alimento: borrador editable y eliminable; publicado sin
   versionado. El cliente solo ve publicados.
5. Sin vigencia: solo publicado y desactivar.
6. Desactivar mantiene el efecto actual: desactiva las tareas pendientes del
   programa en todos los galpones.
7. Cliente y trabajador sin cambios (el trabajador solo usa la bandeja de
   tareas). El gestor ve el catálogo completo (borradores, publicados e
   inactivos) pero **nunca** asignaciones por galpón ni tareas.
8. Cuentas del gestor: se crean con scripts análogos a los de
   `GestorPedidoAlimento` y `GestorRecepcionHuevos`.
9. Los programas existentes son datos de prueba: migrar como publicados o
   eliminar, a criterio del plan.
10. El formato del Excel no cambia.

## Decisiones de diseño (planificación)

- **Nombre del estado.** `EstadoProgramaVacunacion { Borrador = 0, Publicado = 1 }`:
  concuerda en género con «programa». Se persiste como entero.
- **Inactivo no es un estado.** Igual que en precios, el borrado lógico es una
  dimensión aparte (`EstaActivo`). Combinaciones válidas:

  | Estado | EstaActivo | Significado | Lo ve |
  |---|---|---|---|
  | Borrador | true | Borrador en edición | Gestor |
  | Borrador | false | Borrador eliminado | Nadie |
  | Publicado | true | Publicado, asignable | Gestor, cliente, trabajador |
  | Publicado | false | Desactivado | Gestor (como «Inactivo») |

- **Publicado es inmutable.** Como en precios: no se editan los datos básicos ni
  se reemplaza el cronograma. Para corregir un plan se desactiva y se crea otro.
- **Publicar exige cronograma.** Al menos un ítem activo; un borrador sin Excel
  no se publica.
- **Eliminar** solo un borrador (borrado lógico, regla transversal 1 del
  glosario). **Desactivar** solo un publicado. Las dos operaciones son
  idempotentes sobre un objeto que ya está en el estado final.
- **Unicidad del nombre.** Sin cambios: incluye inactivos, así que un borrador
  eliminado o un programa desactivado conservan su nombre reservado (criterio
  de SP7). Al crear un plan corregido se usa otro nombre.
- **Separación de rutas.** El consumo del tenant y la gestión del gestor usan
  grupos de endpoints, queries y DTOs distintos. Ningún handler decide por el
  rol: decide la política del endpoint.
- **El Administrador pierde también la lectura.** La política
  `CatalogoVacunacion` deja de aceptar el rol de plataforma: sin la pantalla de
  administración no tiene uso, y menos privilegio es más seguro.
- **Producción.** El bootstrap de cuentas de producción
  (`SeedCuentasInicialesProduccion`) no cambia: no se agrega ningún secreto
  nuevo al despliegue. La cuenta se crea con el script nuevo; en Development la
  semilla crea `gve@icarus.test`.
- **Inicio de sesión del MVC.** Tras acceder, la aplicación redirige a la
  primera sección que la cuenta puede usar (alimento, huevo o vacunación). Sin
  esto una cuenta solo veterinaria aterrizaría en «Acceso restringido».

## Dominio

`ProgramaVacunacion` agrega `Estado` (inicia en `Borrador`) y:

- `ActualizarDatos`, `ReemplazarCronograma` y `EstablecerFechaEmision` exigen
  `Borrador` («Un programa publicado ya no es editable.»).
- `AsegurarEditable()` público, para que el handler de importación falle antes
  de parsear el Excel.
- `Publicar()`: exige `Borrador` activo y al menos un ítem activo.
- `EliminarBorrador()`: exige `Borrador`; deja `EstaActivo = false`.
- `Desactivar()`: exige `Publicado`; deja `EstaActivo = false`.
- `EsAsignable`: `EstaActivo && Estado == Publicado`.

`TareaVacunacion`, `ItemPlanVacunacion` y la asignación no cambian de forma: las
tareas siguen guardando el snapshot del ítem.

## Application

| Operación | Tipo | Notas |
|---|---|---|
| `CrearProgramaVacunacionCommand` | existente | Crea en Borrador. |
| `ActualizarProgramaVacunacionCommand` | existente | Solo Borrador. |
| `ImportarCronogramaExcelCommand` | existente | Solo Borrador; falla antes de parsear. |
| `PublicarProgramaVacunacionCommand` | nuevo | Registro de vuelo `avicola.vacunacion.programas.publicar` → `{ ItemsPublicados: Entero }`. |
| `EliminarBorradorProgramaVacunacionCommand` | nuevo | Registro de vuelo `avicola.vacunacion.programas.eliminar-borrador` → `{}`. |
| `DesactivarProgramaVacunacionCommand` | existente | Solo Publicado; mismo efecto sobre pendientes. |
| `ListarProgramasVacunacionQuery()` | cambia | Consumidor: solo publicados activos; sin parámetro `IncluirInactivos`. |
| `ObtenerProgramaVacunacionQuery` | cambia | Consumidor: 404 si no está publicado y activo. |
| `ListarProgramasVacunacionGestionQuery` | nuevo | Gestor: todo salvo borradores eliminados. |
| `ObtenerProgramaVacunacionGestionQuery` | nuevo | Gestor: 404 para borradores eliminados. |

`AsignarPlanVacunacionHandler` carga el programa con
`ObtenerPublicadoPorIdAsync` y además verifica `EsAsignable` (defensa en
profundidad): un borrador o un desactivado responden 404 genérico.

DTOs del gestor (`ProgramaVacunacionGestionResumen` y
`ProgramaVacunacionGestionDetalle`) llevan `Estado` como texto, `EstaActivo` y
la cantidad de ítems, y **ningún** dato de galpones, clientes ni tareas. Una
prueba de arquitectura lo vuelve ejecutable.

Anti-PII: los registros de vuelo nuevos solo llevan conteos; nunca nombre del
programa, vacunas ni observaciones.

## API

Consumidor del tenant (sin cambios de forma):

| Método | Ruta | Acceso |
|---|---|---|
| GET | `/vacunacion/programas` | `Funcionalidad:Vacunacion` (vía `CatalogoVacunacion`) |
| GET | `/vacunacion/programas/{id}` | igual |

Se retiran `POST /vacunacion/programas`, `PUT` y `DELETE /vacunacion/programas/{id}`
y `POST /vacunacion/programas/{id}/cronograma-excel`.

Gestor veterinario, grupo `/programas-vacunacion-caisy`, política
`FuncionalidadCaisy:GestorVeterinario` (se autorregistra por el bucle sobre el
enum):

| Método | Ruta | Respuesta |
|---|---|---|
| GET | `/` | Lista de gestión. |
| GET | `/{id}` | Detalle de gestión con ítems activos. |
| POST | `/` | 201 con id (borrador). |
| PUT | `/{id}` | 204; 400 si no es borrador. |
| POST | `/{id}/cronograma-excel` | 200 `{ itemsImportados }`; 400 con `errors.Cronograma` por fila; 413 sobre 5 MB. |
| POST | `/{id}/publicar` | 204; 400 sin ítems o ya publicado. |
| DELETE | `/{id}` | 204 elimina el borrador; 400 si está publicado. |
| POST | `/{id}/desactivar` | 204; 400 si es borrador. |

Matriz efectiva:

| Operación | Administrador | Gestor veterinario | Cliente | Trabajador con `Vacunacion` |
|---|---|---|---|---|
| Gestionar catálogo | 403 | Sí | 403 | 403 |
| Ver catálogo completo | 403 | Sí | 403 | 403 |
| Ver publicados | 403 | 403 (usa su vista) | Sí | Sí |
| Asignar/quitar plan | 403 | 403 | Sí | 403 |
| Tareas y notificación | 403 | 403 | Sí | Sí |

El 403 del gestor en las rutas del tenant es crítico: el filtro EF de tenant es
permisivo para cuentas sin `ClienteId`, así que la única barrera es la política.

## Persistencia y datos existentes

Migración `EstadoProgramaVacunacion`:

1. `AddColumn Estado int NOT NULL DEFAULT 0` en `gestion_avicola.programas_vacunacion`.
2. `UPDATE` a `Estado = 1` de los programas que tienen al menos un ítem activo.
   Los que no tienen cronograma quedan en Borrador (activos: visibles solo para
   el gestor; inactivos: equivalen a borradores eliminados). Así se mantiene el
   invariante «todo publicado tiene cronograma» y las tareas ya materializadas
   siguen apuntando a programas publicados.
3. `Down` elimina la columna.

## Trajano.GestorCaisy

- `ConstantesAutorizacion`: `BitGestorVeterinario = 4`,
  `PoliticaGestorVeterinario`; `ReclamosCaisy.TieneGestorVeterinario` y
  `ReclamosCaisy.SeccionInicial`.
- `ProgramasVacunacionController` (`/ProgramasVacunacion`): lista, nuevo,
  detalle, editar datos, importar cronograma (errores por fila en el resumen de
  validación), confirmar publicación, confirmar eliminación, confirmar
  desactivación (advierte del efecto sobre tareas pendientes sin mostrar
  cifras ni galpones).
- Menú lateral: grupo «Sanidad» con «Programas de vacunación». Sin campana ni
  banner (no hay notificaciones para este rol).
- `IApiIcarusClient` con ocho métodos nuevos y plantillas de ruta propias.
- Sin service worker, IndexedDB ni acceso a datos (la prueba de arquitectura
  existente lo cubre).

## PWA

Se retiran la ruta, la página, su prueba, el enlace de navegación y las
funciones de API de escritura del catálogo. El cliente conserva
`listarProgramasVacunacion()` (selector de asignación) y
`obtenerProgramaVacunacion`.

## Cuentas

- `crear-usuario-gestor-veterinario.ps1` en la raíz, copia del script de
  recepción de huevo con `-Funcionalidades GestorVeterinario`.
- `SemillaIdentidad.EmailGestorVeterinario = "gve@icarus.test"` solo en
  Development.

## Pruebas

- Unitarias: dominio (transiciones y guardas), handlers (gestión y consumo),
  asignación de borrador, política `CatalogoVacunacion` sin Administrador,
  `FuncionalidadesCaisy` con el bit nuevo, DTOs del gestor sin datos del
  tenant.
- Integración (Testcontainers): ciclo del gestor, visibilidad por actor,
  403 del Administrador y del gestor en rutas del tenant, importación con fila
  inválida, flujo completo gestor → cliente → trabajador.
- GestorCaisy: controlador, cliente API, flujo con la API falsa, menú, inicio
  de sesión por funcionalidad, contratos sin datos del tenant.
- Frontend: navegación del Administrador sin «Vacunación», API sin escritura del
  catálogo.

## Fuera de alcance

Versionado o vigencia de programas, reactivación de un programa desactivado,
liberar el nombre de un programa eliminado, notificaciones al gestor,
integración automática con CAISY, cambios en el formato del Excel, cambios en
tareas y en la bandeja del trabajador, alta de la cuenta en el bootstrap de
producción.
