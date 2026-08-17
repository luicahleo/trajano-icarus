# SP5 — Gestión avícola: Granjas y Galpones

Primer bounded context de negocio avícola de Trajano-Icarus. Diseño validado en
brainstorming con el usuario el 2026-08-17, tras analizar el módulo avícola del
ICARUS legacy.

## Contexto

Hoy existen Identity, Clientes, la observabilidad transversal y el frontend PWA.
No hay ningún módulo avícola en `Icarus/src`. El subproyecto 5 crea el contexto
de Gestión avícola con sus dos agregados base, sobre los que colgarán todo lo
demás (producción, mortalidad, vacunación, alimentación, despachos, precios).

Dominio real, validado con el usuario contra el legacy:

- El cliente (tenant) es un granjero afiliado a la cooperativa **CAISY**
  (Cooperativa Agropecuaria San Juan de Yapacaní). El código legacy menciona
  "CAICI": es un error del legacy; el nombre correcto es CAISY.
- **Un cliente tiene una sola granja.** Las granjas reales son muy grandes y
  agrupan todos los galpones; ningún cliente actual tiene más de una. El legacy
  admite varias filas de `GestorAvicola` por cliente, pero no ocurre en la
  práctica y el modelo nuevo lo prohíbe.
- Cada galpón alberga **un lote de gallinas ponedoras** (sin lotes mezclados).
  Su `FechaNacimiento` es la fecha en que se pobló el galpón con el lote.
- El legacy modela la granja como `GestorAvicola`, contaminada con contadores
  (`ContadorHuevos`, `TotalGallinas`, `BajasGallinas`) y estadísticas derivadas.
  Es un error de naming y de diseño: la entidad se llama `Granja` y es limpia.

## Decisiones

### Alcance del subproyecto

SP5 = **Granjas + Galpones**, únicamente. Cortes evaluados:

- Solo Granjas: descartado; el agregado queda vacío (un nombre) y sin galpones
  no hay dónde colgar nada después.
- Granjas + Galpones: elegido. Slice vertical completo, pequeño, que desbloquea
  todos los subproyectos siguientes.
- Añadir producción y mortalidad: descartado para SP5 por tamaño; va en SP6 con
  sus reglas ya registradas al final de este documento.

### Bounded context y proyectos

Tres proyectos nuevos replicando el patrón del módulo Clientes:

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain` → referencia solo
  `Icarus.BuildingBlocks.Domain`.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application` → Domain +
  `Icarus.BuildingBlocks.Application`.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure` →
  Application + `Icarus.BuildingBlocks.Observability`, EF Core SqlServer.

Schema de base de datos `gestion_avicola`; tablas `granjas` y `galpones` en
minúsculas. El Host referencia solo el Infrastructure del módulo, igual que con
Clientes e Identity. El módulo **no referencia Clientes ni Identity**: la
composición (políticas de entitlement, orquestación) vive en el Host.

### Agregado `Granja` (raíz)

- `sealed class Granja : AggregateRoot`, `Id` Guid, ctor privado para EF y ctor
  público que valida invariantes lanzando `ReglaNegocioException` (mensajes en
  español, genéricos, sin PII), igual que `Cliente`/`Trabajador`.
- Atributos (todos con `private set`):
  - `ClienteId` (Guid, obligatorio, inmutable): el tenant.
  - `Nombre` (string, requerido, `Trim()`, máx 200).
  - `EstaActivo` (bool, soft delete; empieza en `true`).
  - Nada más. YAGNI: el legacy no tenía dirección ni otros datos propios.
- Regla de negocio: **un cliente tiene a lo sumo una granja activa**. Al crear,
  se rechaza con `ConflictException` genérico si el cliente ya tiene una granja
  activa. Una granja desactivada no bloquea crear otra.
- Unicidad de nombre: por cliente, **incluyendo inactivas** (mismo criterio que
  el documento en Clientes: el soft delete no libera el nombre).
- **Sin contadores desnormalizados.** `ContadorHuevos`, `TotalGallinas` y
  `BajasGallinas` del legacy NO se portan: son datos derivados de galpones y de
  registros diarios. Cuando exista SP6 se calcularán por consulta.
- Operaciones de dominio: crear (ctor), `Renombrar(nombre)`, `Desactivar()`.

### Agregado `Galpon` (raíz propia)

`Galpon` NO es hijo del agregado `Granja`: es su propia raíz. Motivo: los
registros diarios de producción y mortalidad (SP6) lo actualizarán con alta
frecuencia y por turnos; arrastrar a la granja en cada mutación sería
acoplamiento y contención innecesarios.

- Atributos (todos con `private set`):
  - `GranjaId` (Guid, obligatorio, inmutable).
  - `ClienteId` (Guid, obligatorio, inmutable, **desnormalizado** de la granja):
    permite aplicar el filtro global de tenant sin join. Mismo patrón del legacy
    en producción y despachos.
  - `Numero` (string, requerido, `Trim()`, máx 10): identificador libre dentro
    de la granja ("1", "A", "Norte").
  - `CapacidadMaxima` (int, > 0).
  - `GallinasActuales` (int, ≥ 0).
  - `FechaNacimientoLote` (`DateOnly`, obligatoria, nunca futura).
  - `Descripcion` (string?, opcional, máx 500).
  - `EstaActivo` (bool, soft delete).
- Invariantes de dominio (también reflejadas como check constraints en EF):
  - `0 ≤ GallinasActuales ≤ CapacidadMaxima`, siempre: al crear, al ajustar
    inventario y al cambiar la capacidad (capacidad nueva ≥ gallinas actuales).
  - `FechaNacimientoLote` no futura (glosario: ninguna fecha del dominio admite
    futuro; validación con `DateOnly.FromDateTime(DateTime.UtcNow)`, patrón de
    Clientes).
- Unicidad: `(GranjaId, Numero)` único **incluyendo inactivos**.
- Operaciones de dominio: crear (ctor), `ActualizarDatos(numero, descripcion,
  capacidadMaxima)`, `AjustarInventarioGallinas(nuevoTotal)` (total absoluto, no
  delta), `Desactivar()`.
- Reglas de consistencia con la granja (en los handlers de Application):
  - Crear un galpón exige que la granja exista, esté activa y sea del mismo
    tenant; si no, `NotFoundException` genérico (anti-enumeración).
  - **Desactivar una granja desactiva sus galpones activos** en la misma unidad
    de trabajo; la decisión se narra con el registro de vuelo
    (`operacion.Decidir`). Una granja inactiva no admite galpones activos ni
    altas de galpones.

### Reglas transversales (desde el inicio)

- **Soft delete** `EstaActivo` en ambas entidades; nunca borrado físico.
- **Fechas sin futuro**, validadas en dominio (no en interfaz).
- **Aislamiento por tenant**: `GestionAvicolaDbContext` recibe `ICurrentUser` y
  aplica filtros globales
  `e.EstaActivo && (_clienteIdActual == null || e.ClienteId == _clienteIdActual)`
  en ambas entidades (Galpón sobre su `ClienteId` desnormalizado). El rol de
  plataforma (`ClienteId` null) ve todos los tenants. Mismo patrón y misma
  trampa del nullable ya documentada en `ClientesDbContext`: no usar `.Value`.
- **Anti-enumeración**: un id inexistente y un id de otro tenant devuelven lo
  mismo (`NotFoundException` → 404). Los métodos de repositorio documentan cuál
  respeta filtros globales y cuál usa `IgnoreQueryFilters()`.
- **Anti-PII**: errores genéricos; nunca nombres de granja en logs ni mensajes.
- **Registro de vuelo**: todos los commands de mutación implementan
  `IOperacionRegistrable` (aquí el payload no es PII, a diferencia de Clientes).
  Nombres y campos permitidos (solo no-PII; la lista `NombresProhibidos` filtra
  "Nombre", así que el nombre de la granja nunca se registra):
  - `avicola.granjas.crear` → `{}`
  - `avicola.granjas.renombrar` → `{}`
  - `avicola.granjas.desactivar` → `{ GalponesDesactivados: Entero }`
  - `avicola.galpones.crear` → `{ Numero: Texto, CapacidadMaxima: Entero,
    GallinasActuales: Entero }`
  - `avicola.galpones.actualizar` → `{ Numero: Texto, CapacidadMaxima: Entero }`
  - `avicola.galpones.ajustar-inventario` → `{ GallinasActuales: Entero }`
  - `avicola.galpones.desactivar` → `{}`
- Interceptores de registro de vuelo enchufados en el DbContext con
  `DescriptorContextoPersistencia("GestionAvicola")`, igual que en Clientes.

### Application

Patrón idéntico a Clientes: un archivo por pieza agrupado por carpeta de
agregado (`Granjas/`, `Galpones/`); commands/queries como `sealed record :
IRequest<...>`; handlers `sealed class` que cargan el agregado
(`?? throw new NotFoundException(...)`), verifican unicidad
(`ConflictException` genérico), invocan el método de dominio y
`SaveChangesAsync` vía `IUnitOfWork`. Validación con FluentValidation
(`AbstractValidator<TCommand>`) ejecutada por el `ValidationBehavior`
existente. Interfaces de repositorio en Application
(`IRepositorioGranjas`, `IRepositorioGalpones`) con DTOs de lectura
(`GranjaResumen`, `GalponResumen`) como `sealed record` en el mismo archivo.

Operaciones:

- Granjas: `CrearGranja`, `RenombrarGranja`, `DesactivarGranja`,
  `ObtenerGranja`, `ListarGranjas` (del tenant actual; como mucho una activa).
- Galpones: `CrearGalpon`, `ActualizarGalpon`, `AjustarInventarioGalpon`,
  `DesactivarGalpon`, `ObtenerGalpon`, `ListarGalponesPorGranja`.

### API

Minimal APIs en el Host, `Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs`,
siguiendo `ClientesEndpoints.cs`:

- `POST /granjas`, `GET /granjas`, `GET /granjas/{id}`, `PUT /granjas/{id}`
  (renombrar), `DELETE /granjas/{id}` (desactivar) → política
  `PoliticasClientes.Para(Funcionalidades.Granjas)`.
- `POST /granjas/{granjaId}/galpones`, `GET /granjas/{granjaId}/galpones`,
  `GET /galpones/{id}`, `PUT /galpones/{id}`, `PUT /galpones/{id}/inventario`,
  `DELETE /galpones/{id}` → política
  `PoliticasClientes.Para(Funcionalidades.Galpones)`.
- Bodies como `private sealed record` anidados en el endpoint. Errores mapeados
  por el `ExceptionHandlingMiddleware` existente (400/404/409).

El entitlement ya está preparado: `Funcionalidades.Granjas = 1` y
`Galpones = 2` existen y mapean a `Modulos.GestionAvicola`; las políticas
`"Funcionalidad:X"` ya se generan en `AddClientesInfraestructura`. El Host las
usa sin que GestionAvicola referencie Clientes.

### Infraestructura y composición

- `GestionAvicolaDbContext : DbContext, IUnitOfWork`, schema por defecto
  `gestion_avicola`, `ApplyConfigurationsFromAssembly`, filtros globales de
  tenant + activos.
- `ConfiguracionGranja` / `ConfiguracionGalpon` (`IEntityTypeConfiguration<T>`):
  longitudes, índices únicos `(ClienteId, Nombre)` y `(GranjaId, Numero)`
  incluyendo inactivos, índice único filtrado de una granja activa por cliente
  (`ClienteId` WHERE `EstaActivo = 1`), check constraints de las invariantes.
- Migración EF `InicialGestionAvicola` + `DesignTimeGestionAvicolaDbContextFactory`
  (cadena ficticia + `ICurrentUser` nulo, patrón de Clientes).
- `DependencyInjection.AddGestionAvicolaInfraestructura(...)`: DbContext,
  interceptores de registro de vuelo, repositorios, `IUnitOfWork`.
- `Program.cs`: registrar ensamblados de MediatR/validadores, llamar
  `AddGestionAvicolaInfraestructura`, `MapGestionAvicola()`, y aplicar
  migración + semilla en Development/Testing.
- `SemillaGestionAvicola`: datos demo solo Dev/Testing (una granja con dos
  galpones para el cliente demo), ids fijos pasados desde el Host, anti-PII.

### Tests

- **Unitarios** (`tests/Icarus.UnitTests/GestionAvicola/`): invariantes de
  `Granja` y `Galpon` (nombre vacío, fecha futura, capacidad ≤ 0, inventario
  fuera de rango, trims), handlers con NSubstitute (una granja activa por
  cliente → `ConflictException`; nombre duplicado → `ConflictException`; número
  duplicado → `ConflictException`; id ajeno → `NotFoundException`; cascada al
  desactivar granja). Nombres de test en español estilo frase. TDD: cada test
  se ve en rojo antes de implementar.
- **Integración** (`tests/Icarus.IntegrationTests/`): endpoints con
  Testcontainers.MsSql (Docker corriendo): CRUD completo, 404 para id de otro
  tenant, 403 sin la funcionalidad, 400 con fecha futura o capacidad inválida,
  rechazo de segunda granja activa.
- **Arquitectura** (`tests/Icarus.ArchitectureTests/`): extender
  `ReglasDeCapasTests` y `ReglasDeModulosTests` con los tres ensamblados nuevos:
  GestionAvicola no depende de `Icarus.Clientes`, `Icarus.Identity`, EF/AspNetCore
  en Domain, ni Infrastructure en Application.

### Documentación

- `docs/dominio/glosario-avicola.md` se amplía **en el mismo commit del spec**
  con: CAISY, Granja, Galpón, una granja por cliente, lote y fecha de
  poblado, huevo de descarte, eficiencia de postura con umbral del 70 % y
  mortalidad no retroactiva.
- `AGENTS.md` (sección Proyecto) se actualiza al cerrar la implementación del
  módulo, y se regeneran los adaptadores con
  `node quality/generar-adaptadores.mjs`.

## Fuera de alcance

Producción de huevos, mortalidad, vacunación, alimentación (cronogramas y
pedidos), despachos, precios, cuenta corriente, estadísticas y contadores de
granja, programas de vacunación, migración de datos del legacy, frontend. Nada
de eso se crea, ni siquiera como esqueleto.

## Decisiones registradas para SP6 (producción + mortalidad)

Validadas con el usuario durante el brainstorming. NO se implementan en SP5; se
registran aquí y en el glosario para que el spec de SP6 las tome como base. Su
única influencia en SP5 es que `Galpon` sea raíz propia.

1. **Eficiencia diaria por galpón** = huevos producidos del día ÷ gallinas
   vivas del galpón. La recogida la hacen los trabajadores en distintos turnos:
   varios registros por galpón y día.
2. **Umbral de descarte de lote: 70 %.** Si la eficiencia cae bajo ese umbral,
   el lote se considera para descarte y venta como carne. Es métrica derivada,
   no estado persistido.
3. **Huevos de descarte** (rajados o con falta de calcio): se registran aparte,
   NO cuentan para la eficiencia y se venden en otro mercado más barato. El
   legacy no los registra: es funcionalidad nueva, no migración fiel.
4. **Mortalidad no retroactiva**: prohibido editar un registro de producción
   pasado para agregar mortalidad olvidada; distorsionaría la eficiencia
   histórica. La mortalidad se registra en su momento o no se registra.
5. Unidades: maple = 30 huevos (ya en el glosario). Amarra = 180 huevos =
   6 maples (la usan los despachos, subproyecto posterior).

## Orden orientativo de subproyectos avícolas

SP5 granjas + galpones → SP6 producción + mortalidad → SP7 vacunación →
SP8 alimentación → SP9 despachos → SP10 precios. Es orientativo: cada
subproyecto confirma su alcance en su propio brainstorming y spec.
