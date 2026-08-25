# SP7 — Gestión avícola: Vacunación

Tercer incremento del bounded context `GestionAvicola`, sobre lo construido en
SP5 (agregados `Granja` y `Galpon`) y SP6 (`RegistroProduccion`,
`RegistroMortalidad`). Diseño validado en brainstorming con el usuario el
2026-08-19, tras investigar el módulo de vacunación del ICARUS legacy.

## Contexto

La cooperativa **CAISY** emite planes de vacunación en papel (ejemplo real:
"PROGRAMA DE VACUNACION PARA 1000 AVES", con columnas FECHA, EDAD DIA, VACUNA
y MODO DE APLICACION). El plan es una **plantilla por edad del lote**: cada
ítem indica "a los N días de edad, aplicar X". La columna FECHA del papel es
derivada: `FECHA = fecha de entrada del lote + EdadDia` (verificado con el
ejemplo: entrada 06-oct-2023 → día 3 = 09-oct, día 10 = 16-oct, día 245 =
07-jun-2024). El documento real no es solo vacunas: incluye filas de **manejo**
(paracetamol al ingreso, recorte de pico, desparasitaciones, antibioterapia,
traslado al galpón de producción) y tablas de iluminación y alimentación al
final.

Hoy no existe integración Trajano-Icarus ↔ CAISY: el **Administrador de
plataforma** sube el plan (vía Excel) para que cada **cliente** lo asigne a sus
galpones. El valor central de la feature es la **notificación**: indicar al
trabajador qué toca vacunar hoy y cuál es la próxima vacunación de cada
galpón.

El legacy (`ProgramaVacunacion`, `CronogramaVacunacion`, `GalponTareaVacunacion`)
sirvió de referencia, con errores a no heredar:

- Reasignar un plan **borraba físicamente** las tareas anteriores: se perdía el
  historial sanitario del lote.
- Doble fuente de verdad del estado de completado (cronograma y tarea por
  galpón), con un handler de completado muerto.
- Ítems con todo opcional (fecha, edad, vacuna): las tareas sin `EdadDia`
  **nunca aparecían** en las notificaciones aunque tuvieran fecha.
- Dos controladores web paralelos para lo mismo, con autorización
  inconsistente (admin-only en uno, cualquier autenticado en otro).
- `CantidadAves <= 0` corregida silenciosamente a 1; fecha fin calculada y
  nunca persistida; DTO con nombres distintos de la entidad; mezcla de
  `DateTime.Now` y `UtcNow`.

Decisiones de negocio validadas en el brainstorming:

1. **Catálogo global de planes.** Los planes los crea CAISY; hoy los sube el
   Administrador (canal manual) y cada cliente los asigna a sus galpones. La
   futura integración con CAISY queda fuera de SP7, pero el diseño no la
   estorba: el catálogo ya es global y su escritura ya está aislada en un
   rol.
2. **Historial preservado.** Al asignar un plan nuevo a un galpón, las tareas
   pendientes del plan anterior se desactivan (soft delete); las completadas
   y canceladas quedan como historial sanitario. Nada se borra físicamente.
3. **Aplicación con dato mínimo útil.** Al completar se registra fecha real de
   aplicación, quién la registró, observaciones y **cantidad de aves
   vacunadas** (permite detectar aplicaciones parciales).
4. **Vacuna como texto libre.** No hay catálogo de vacunas: el dato legacy no
   está en condiciones de catalogarse. `Vacuna` contiene la vacuna *o el
   manejo* ("BIO COCCIVET R", "Desparasitación con Niclosamida..."), igual que
   la columna VACUNA del papel de CAISY.
5. **`Vacunacion` es funcionalidad asignable al trabajador.** La notificación
   es para él: ve las tareas del día y las próximas, y marca aplicaciones.
   Nunca asigna planes ni administra estructura.
6. **Fecha de aplicación informada por el usuario.** Por defecto hoy, admite
   pasado, nunca futura. A diferencia de SP6 (donde el servidor fija la
   fecha), aquí la tarea ya existe desde la asignación y completarla es
   *cerrar* algo que pudo ocurrir ayer. Se conservan `FechaProgramada` (lo
   que decía el plan) y `FechaAplicacion` (lo que pasó), para detectar
   aplicaciones a destiempo.
7. **Tareas con tres destinos: pendiente → completada o cancelada.** Cancelar
   es decisión del cliente (motivo opcional; queda en el historial). Sin
   reprogramación individual: la `FechaProgramada` no se edita; si el plan
   cambia, se corrige el plan o se reasigna el galpón.
8. **El Administrador sube el Excel del plan.** Alta del programa en dos
   pasos: datos básicos por formulario y cronograma por Excel.
9. **Todas las filas del cronograma son tareas notificables**, vacunas y
   manejos por igual: para el trabajador es "hoy toca hacer X en el galpón
   Y".

## Decisiones

### Alcance del subproyecto

Dentro del módulo `GestionAvicola` existente (mismos tres proyectos
`Icarus.GestionAvicola.*`, mismo schema `gestion_avicola`): el agregado
`ProgramaVacunacion` (catálogo global), el agregado `TareaVacunacion`
(tenant), asignación de planes, notificación de tareas, completar/cancelar,
importación Excel, endpoints, permisos, frontend y tests.

Enfoque de modelado elegido (descartados: cálculo al vuelo sin tareas
persistidas —historial mutable ante ediciones del plan—, y tareas como hijas
de `Galpon` —contención con producción/mortalidad—): **tareas materializadas
al asignar, con snapshot**.

### Agregado `ProgramaVacunacion` (raíz, catálogo global)

- **Sin `ClienteId`**: es un catálogo de plataforma. El filtro global de
  tenant NO aplica a esta tabla; sí aplica el filtro `EstaActivo` (el rol de
  plataforma puede ver inactivos vía repositorio con `IgnoreQueryFilters`).
- Atributos (todos con `private set`):
  - `Id` (Guid).
  - `Nombre` (string, requerido, `Trim()`, máx 200). Unicidad **incluyendo
    inactivos** (criterio del proyecto: el soft delete no libera el nombre).
  - `FechaEmision` (`DateOnly`, requerida, no futura).
  - `CantidadAves` (int > 0, informativa: el "PARA 1000 AVES" del encabezado).
  - `Observaciones` (string?, máx 1000).
  - `EstaActivo` (bool, soft delete).
- Ítems del cronograma como **entidades hijas del agregado** (`ItemPlanVacunacion`,
  tabla `programas_vacunacion_items`): `EdadDia` (int > 0, obligatorio),
  `Vacuna` (string, requerido, `Trim()`, máx 200), `ModoAplicacion` (string?,
  máx 500), `Observaciones` (string?, máx 1000), `EstaActivo`.
  - Invariante: **no puede haber dos ítems activos con la misma `EdadDia`**
    en el mismo programa (el papel de CAISY agrupa varias vacunas del mismo
    día en una sola fila; si el Excel trae dos filas con la misma edad, la
    importación la rechaza y el admin las combina).
- Operaciones de dominio: crear (ctor con datos básicos), `ActualizarDatos`,
  `ReemplazarCronograma(items)` (desactiva los ítems actuales y agrega los
  nuevos; **no afecta** a las tareas ya materializadas en galpones, que tienen
  snapshot), `Desactivar()`.
- Un programa desactivado **no es asignable**; los galpones que ya lo tenían
  conservan sus tareas.

### Agregado `TareaVacunacion` (raíz propia, del tenant)

Tabla `tareas_vacunacion`. Campos (todos `private set`):

- `GalponId`, `ClienteId` (Guid, inmutables; `ClienteId` desnormalizado del
  galpón para el filtro de tenant sin join, patrón de SP5/SP6).
- `ProgramaVacunacionId` e `ItemPlanVacunacionId` (Guid, informativos; sin FK
  dura al catálogo global: el catálogo puede cambiar y el historial no).
- Snapshot: `EdadDia` (int), `Vacuna` (string), `ModoAplicacion` (string?),
  `ObservacionesProgramadas` (string?).
- `FechaProgramada` (`DateOnly`, inmutable; calculada al asignar:
  `galpon.FechaNacimientoLote + EdadDia` días).
- `Estado` (enum `EstadoTareaVacunacion`: `Pendiente`, `Completada`,
  `Cancelada`; empieza en `Pendiente`).
- Ejecución (nullables hasta completar): `FechaAplicacion` (`DateOnly?`,
  informada por el usuario; por defecto hoy en el handler), `AvesVacunadas`
  (int? > 0; si se omite se asume el lote completo), `CompletadaPor` (Guid?
  —id del usuario, no nombre: anti-PII—), `ObservacionesAplicacion` (string?,
  máx 1000), `MotivoCancelacion` (string?, máx 500).
- `EstaActivo` (bool, soft delete).
- Invariantes de dominio:
  - `FechaAplicacion` nunca futura (glosario: ninguna fecha del dominio admite
    futuro; validación con `DateOnly.FromDateTime(DateTime.UtcNow)`).
  - `AvesVacunadas` > 0 cuando se informa.
  - **Sellado por estado**: `Completar` y `Cancelar` solo desde `Pendiente`;
    una tarea completada o cancelada no admite cambios
    (`ReglaNegocioException`).

### Ciclo de vida y reglas de negocio (handlers de Application)

**Asignar plan a galpón** (`AsignarPlanVacunacion`, solo cliente):

1. El galpón debe existir, estar activo y ser del tenant (`NotFoundException`
   genérico si no — anti-enumeración). El programa debe existir y estar
   activo (si no, `NotFoundException`/`ConflictException` genéricos).
2. Las tareas **pendientes** del plan anterior del galpón se desactivan
   (soft delete; historial "reemplazadas"). Las completadas y canceladas no
   se tocan.
3. Se crea una `TareaVacunacion` por ítem activo del programa, con
   `FechaProgramada = galpon.FechaNacimientoLote + EdadDia` y snapshot de
   vacuna/modo/edad/observaciones.
4. Un galpón tiene a lo sumo un plan vigente: la asignación anterior queda
   sin pendientes tras el paso 2, así que no hace falta un campo "plan
   actual" en `Galpon`; el plan vigente se deriva de las tareas pendientes.

**Quitar plan** (`QuitarPlanVacunacion`, solo cliente): desactiva las tareas
pendientes del galpón. Conserva completadas y canceladas.

**Notificación** (`ListarTareasVacunacion`, query del tenant): pendientes con
`FechaProgramada <= hoy + 7 días`, de todos los galpones del cliente,
ordenadas por fecha y agrupadas por galpón en la UI. El backend devuelve dos
bloques: `VencidasYHoy` (`FechaProgramada <= hoy`) y `Proximas`
(`hoy < FechaProgramada <= hoy + 7`). Las vencidas no desaparecen: permanecen
en el primer bloque hasta completarse o cancelarse. Query por galpón
(`ListarTareasPorGalpon`): todas las tareas activas del galpón con su estado,
para el historial.

**Completar** (`CompletarTareaVacunacion`, cliente o trabajador con
`Vacunacion`): la tarea debe estar `Pendiente`. Body: `fechaAplicacion?`
(default hoy, nunca futura — el dominio valida), `avesVacunadas?`,
`observaciones?`. Una segunda llamada sobre la misma tarea es 409 por estado
(no hace falta `IdempotencyKey`: el estado hace la operación naturalmente
idempotente).

**Cancelar** (`CancelarTareaVacunacion`, **solo cliente**): la tarea debe
estar `Pendiente`. Body: `motivo?`. El trabajador recibe 403: cancelar es
decisión de gestión, no de operación.

### Importación Excel

- Endpoint `POST /vacunacion/programas/{id}/cronograma-excel`
  (`multipart/form-data`, solo Administrador). Reemplaza el cronograma
  completo del programa (`ReemplazarCronograma`).
- Formato (el del papel de CAISY): columnas `FECHA`, `EDAD`, `VACUNA`,
  `MODO DE APLICACION`, `OBSERVACIONES`; nombres de columna tolerantes
  (mayúsculas/minúsculas, tildes, espacios). **La columna FECHA se ignora**:
  la fuente de verdad es `EDAD`.
- Reglas por fila: `EDAD` obligatoria, entera, > 0, no repetida en el archivo;
  `VACUNA` obligatoria. **Todo-o-nada**: una fila inválida rechaza la
  importación completa con la lista de errores por número de fila, sin guardar
  nada.
- Librería: **ClosedXML** (la del legacy). El parseo vive en Infrastructure
  detrás de la interfaz de Application `IImportadorCronogramaVacunacion`
  (devuelve ítems o errores por fila), para no acoplar Application a la
  librería. Se verificará al planear que las reglas de arquitectura permiten
  la dependencia en Infrastructure.

### Registro de vuelo y anti-PII

Operaciones registrables (`IOperacionRegistrable`), campos permitidos solo
no-PII:

- `avicola.vacunacion.programas.crear` → `{ CantidadAves: Entero }`
- `avicola.vacunacion.programas.actualizar` → `{ CantidadAves: Entero }`
- `avicola.vacunacion.programas.importar-cronograma` → `{ ItemsImportados: Entero }`
- `avicola.vacunacion.programas.desactivar` → `{}`
- `avicola.vacunacion.asignar` → `{ TareasCreadas: Entero, TareasPendientesDesactivadas: Entero }`
- `avicola.vacunacion.quitar-plan` → `{ TareasPendientesDesactivadas: Entero }`
- `avicola.vacunacion.completar` → `{ AvesVacunadas: Entero }` (si se informa)
- `avicola.vacunacion.cancelar` → `{}`

Nunca se registran nombres de vacuna, motivos ni observaciones (texto libre:
podría contener PII). `CompletadaPor` guarda el id del usuario, no el nombre.

### Application

Patrón de SP5/SP6: commands/queries `sealed record : IRequest<...>`, handlers
`sealed class` con `IUnidadTrabajoGestionAvicola`, FluentValidation,
excepciones de dominio, carpetas `Vacunacion/`. Interfaces de repositorio en
Application: `IRepositorioProgramasVacunacion` (catálogo global; documenta
qué métodos ignoran el filtro de activos para el rol de plataforma) e
`IRepositorioTareasVacunacion` (`Agregar`, `ObtenerPorIdAsync`,
`ListarPendientesPorGalponAsync`, `ListarPorGalponAsync`,
`ListarNotificacionAsync(clienteId, hoy, hasta)`, `DesactivarPendientesDeGalponAsync`).
DTOs de lectura `sealed record` en el mismo archivo.

### API (Host)

Endpoints nuevos en `Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs`
(convenciones actuales: bodies `private sealed record`, `Results.Created` en
altas, `Results.NoContent` en mutaciones sin contenido, errores por el
middleware global):

| Ruta | Autorización |
|---|---|
| `POST /vacunacion/programas` | Rol Administrador |
| `POST /vacunacion/programas/{id}/cronograma-excel` | Rol Administrador |
| `PUT /vacunacion/programas/{id}` | Rol Administrador |
| `DELETE /vacunacion/programas/{id}` (desactivar) | Rol Administrador |
| `GET /vacunacion/programas`, `GET /vacunacion/programas/{id}` | `Funcionalidad:Vacunacion` (la cumple el cliente con el módulo y el trabajador con la funcionalidad) o Administrador |
| `POST /galpones/{galponId}/plan-vacunacion` | `Funcionalidad:Galpones` (solo cliente: es decisión estructural) |
| `DELETE /galpones/{galponId}/plan-vacunacion` | `Funcionalidad:Galpones` |
| `GET /galpones/{galponId}/vacunacion/tareas` | `Funcionalidad:Vacunacion` |
| `GET /vacunacion/tareas` (notificación del tenant) | `Funcionalidad:Vacunacion` |
| `POST /vacunacion/tareas/{id}/completar` | `Funcionalidad:Vacunacion` |
| `POST /vacunacion/tareas/{id}/cancelar` | Solo cliente (rol Cliente + módulo) |

### Entitlement y permisos

- `FuncionalidadesTrabajador.Asignables` pasa a
  `ProduccionHuevos | Mortalidad | Vacunacion` (backend), y
  `FuncionalidadOperativaTrabajador` en `web/src/lib/tipos.ts` añade
  `'Vacunacion'` (frontend). La pantalla de asignación de permisos muestra el
  nuevo checkbox.
- La política `GestionAvicolaEstructura` (lectura estructural implícita de
  granja/galpones) amplía su OR con `Vacunacion`.
- La política `Funcionalidad:Vacunacion` ya se autorregistra (bucle sobre el
  enum en `AddClientesInfraestructura`); sin cambios de registro.
- Compatibilidad: los valores numéricos del enum no cambian; no hay migración
  destructiva de datos.

Matriz efectiva:

| Operación | Administrador | Cliente | Trabajador con `Vacunacion` |
|---|---|---|---|
| Gestionar catálogo de planes | Sí | 403 | 403 |
| Ver catálogo de planes | Sí | Sí | Sí |
| Asignar/quitar plan a galpón | 403 | Sí | 403 |
| Ver tareas y notificación | No aplica (sin tenant) | Sí | Sí |
| Completar tarea | No aplica | Sí | Sí |
| Cancelar tarea | No aplica | Sí | 403 |

### Infraestructura

- `GestionAvicolaDbContext`: `DbSet` para `programas_vacunacion` (filtro solo
  `EstaActivo`, sin tenant — tabla global), `programas_vacunacion_items`
  (mismo criterio) y `tareas_vacunacion` (filtro `EstaActivo && tenant`,
  patrón actual).
- Configuraciones EF: longitudes, índice único `(Nombre)` en programas
  incluyendo inactivos, índice `(ClienteId, FechaProgramada)` y
  `(GalponId, Estado)` en tareas, checks (`EdadDia > 0`,
  `AvesVacunadas IS NULL OR AvesVacunadas > 0`, coherencia
  `Estado`/`FechaAplicacion`), `FechaProgramada`/`FechaAplicacion` tipo `date`.
- Migración EF `Vacunacion` con el procedimiento habitual
  (`DesignTimeGestionAvicolaDbContextFactory`).
- Semilla: un programa demo con cronograma (estilo del ejemplo real de CAISY)
  solo en Development/Testing, ids fijos desde el Host.

### Frontend (`web/src/features/avicola/`)

Patrón existente: TanStack Query + MUI, online-first, páginas diferidas,
`RequiereFuncionalidad`.

- **Admin**: sección `/admin/vacunacion`: lista de programas, alta/edición de
  datos básicos, subida de Excel con reporte de errores por fila,
  activar/desactivar.
- **Cliente**: acción "Asignar plan de vacunación" en la pantalla del galpón
  (selector del catálogo; confirmación que advierte cuántas pendientes del
  plan anterior se desactivarán); sección "Vacunación" del galpón con el
  historial de tareas; vista de vacunación en `/avicola` con `VencidasYHoy` y
  `Proximas` agrupadas por galpón; completar (fecha editable con default hoy,
  aves vacunadas, observaciones) y cancelar (motivo).
- **Trabajador con `Vacunacion`**: la vista de tareas (sin asignar ni
  cancelar) y completar. La sección se consulta solo si tiene la
  funcionalidad (no pedir lo no autorizado). El enlace de navegación aparece
  con `ProduccionHuevos | Mortalidad | Vacunacion`.

### Tests

- **Unitarios** (`tests/Icarus.UnitTests/GestionAvicola/`, TDD, NSubstitute,
  nombres en español estilo frase): invariantes de `ProgramaVacunacion`
  (nombre vacío, fecha futura, ítem sin edad o sin vacuna, edad duplicada) y
  de `TareaVacunacion` (fecha futura, aves ≤ 0, sellado por estado);
  handlers: asignación (fechas calculadas `FechaNacimientoLote + EdadDia`,
  desactivación de pendientes preservando completadas/canceladas,
  anti-enumeración, programa inactivo), completar (default hoy, 409 por
  estado), cancelar (solo pendiente), notificación (bloques y ventana de 7
  días).
- **Integración** (`tests/Icarus.IntegrationTests/`, Testcontainers, patrón
  `IdentityFactory`): flujo completo admin→cliente→trabajador; 403 del
  trabajador sin `Vacunacion`, del trabajador cancelando/asignando, del
  cliente gestionando el catálogo; 404 de tarea de otro tenant; importación
  Excel válida y todo-o-nada con fila inválida.
- **Arquitectura**: sin ensamblados nuevos; verificar que ClosedXML en
  Infrastructure no viola `ReglasDeModulosTests` (ajustar la regla solo si la
  dependencia es legítima y documentarlo).

### Documentación

- `docs/dominio/glosario-avicola.md` se amplía **en el mismo commit del
  spec**: programa de vacunación (catálogo global de CAISY), ítem por edad en
  días, asignación (día 0 = `FechaNacimientoLote` del galpón), tarea de
  vacunación y sus estados, cancelación, regla de historial preservado.
- `AGENTS.md` (sección Proyecto) se actualiza al cerrar la implementación y
  se regeneran los adaptadores con `node quality/generar-adaptadores.mjs`.

## Fuera de alcance

Iluminación; alimentación (SP8); revacunación recurrente automática (el
"cada 2 a 3 meses" del papel se carga como filas concretas); catálogo de
vacunas; integración CAISY → Trajano-Icarus; campañas de fecha fija para toda
la granja; recordatorios push o jobs; reprogramación individual de tareas;
migración de datos del legacy; cola offline en IndexedDB (el frontend es
online-first y el completado es idempotente por estado); despachos y precios
(SP9, SP10).

## Orden orientativo de subproyectos avícolas

SP5 granjas + galpones (hecho) → SP6 producción + mortalidad (hecho) →
**SP7 vacunación (este)** → SP8 alimentación → SP9 despachos → SP10 precios.
