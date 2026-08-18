# SP6 — Gestión avícola: Producción diaria y Mortalidad

Segundo incremento del bounded context `GestionAvicola`, sobre lo construido en
SP5 (agregados `Granja` y `Galpon`). Diseño validado en brainstorming con el
usuario el 2026-08-18.

## Contexto

El día a día de la granja: el trabajador recolector recoge huevos cuando puede
a lo largo del día (no hay turnos ni horario: la gallina no tiene horario de
producción), y registra las gallinas muertas cuando ocurren. La métrica central
del negocio es la **eficiencia diaria por galpón** = huevos vendibles del día ÷
gallinas vivas del galpón; si cae bajo el **70 %**, el lote se considera para
descarte (venta como carne). Los **huevos de descarte** (rajados, falto de
calcio) se registran aparte, con el mismo conteo que el huevo bueno (maples +
unidades sueltas): no entran a la eficiencia ni al vendible, y se venden en
otro mercado más barato. El legacy (`RegistroProduccionDiario`,
`RegistroMortalidad`) sirvió de referencia, con errores a no heredar: mortalidad
mezclada en la producción (doble fuente), numeración de turnos innecesaria,
eficiencia persistida por registro, y edición retroactiva permitida.

Reglas validadas con el usuario en este brainstorming:

1. **La mortalidad es un evento independiente** de la producción (sin causa
   probable ni observaciones: no hacen falta). En la futura PWA, la pantalla de
   recogida podrá enviar ambas cosas en un solo gesto (dos requests), y también
   mortalidad sola (bajas de madrugada).
2. **Ventana temporal**: solo se registra con fecha de **hoy**. Mientras sea
   hoy, el registro se puede corregir (editar o desactivar). Pasada la
   medianoche UTC, el día queda **sellado** para siempre: prohibido editar un
   registro pasado para agregar producción o mortalidad olvidada, porque
   distorsionaría la eficiencia histórica.
3. **Recogidas múltiples por galpón y día**, cada una con su hora. El total del
   día es la suma de las recogidas del día.
4. **Huevos de descarte**: se cuentan igual que el huevo bueno, en **maples y
   unidades sueltas**, dentro de la misma recogida. No entran al vendible ni a
   la eficiencia. Solo se registran; la venta del descarte queda para el módulo
   de despachos.
5. **Eficiencia derivada, nunca persistida**: se calcula al consultar, con la
   población **congelada por día** vía snapshot de gallinas vivas en cada
   evento.
6. **Idempotencia**: cada recogida/mortalidad acepta una `IdempotencyKey`
   (Guid generado por el cliente) para que los reintentos de la PWA offline no
   dupliquen. El legacy ya demostró la necesidad con la app móvil.

## Decisiones

### Alcance del subproyecto

Dentro del módulo `GestionAvicola` existente (mismos tres proyectos
`Icarus.GestionAvicola.*`, mismo schema `gestion_avicola`): dos agregados
nuevos (`RegistroProduccion`, `RegistroMortalidad`), sus operaciones, la
consulta de eficiencia, endpoints y tests. Fuera de alcance: vacunación,
alimentación, despachos, precios, alertas automáticas, la UI avícola de la PWA
y su sincronización offline (ver "Decisión para el frontend"), y la venta de
huevos de descarte.

### Constantes de dominio

- `Maple.HuevosPorMaple = 30` (clase estática del dominio; el glosario prohíbe
  repetir el 30 como número suelto).
- `EficienciaPostura.UmbralDescarte = 70` (%) y el cálculo
  `Calcular(totalVendible, gallinasVivas)` = porcentaje con dos decimales, 0 si
  no hay gallinas. Clase estática del dominio: la eficiencia es siempre
  derivada.

### Agregado `RegistroProduccion` (raíz propia)

Una **recogida**: el acto de recoger huevos en un momento del día.

- Tabla `registros_produccion`. Campos (todos `private set`):
  - `GalponId`, `ClienteId` (Guid, inmutables; ClienteId desnormalizado del
    galpón para el filtro de tenant sin join, patrón de SP5).
  - `Fecha` (`DateOnly`, inmutable). La fija el servidor (día de llegada del
    request), no el dispositivo.
  - `Hora` (`TimeOnly`, editable): la hora real de la recogida que manda el
    cliente; si no viene, la del servidor.
  - Huevo vendible: `CantidadMaples` (int ≥ 0), `UnidadesIncompletas`
    (int ≥ 0 y < 30).
  - Huevo de descarte: `MaplesDescarte` (int ≥ 0), `UnidadesDescarte`
    (int ≥ 0 y < 30). Mismo formato de conteo que el vendible; no entra a la
    eficiencia.
  - `GallinasVivas` (int ≥ 0, snapshot del inventario del galpón en el momento
    del registro; inmutable).
  - `IdempotencyKey` (Guid?, inmutable; índice único filtrado
    `WHERE IdempotencyKey IS NOT NULL`).
  - `EstaActivo` (soft delete).
- `TotalHuevosVendibles()` = `CantidadMaples * Maple.HuevosPorMaple +
  UnidadesIncompletas`. `TotalHuevosDescarte()` = `MaplesDescarte *
  Maple.HuevosPorMaple + UnidadesDescarte`. El descarte NO entra al vendible.
- Invariantes de dominio (eternas): fecha no futura, cantidades ≥ 0, sueltos
  < 30 (los dos pares). La ventana "solo hoy" es regla de **aplicación**
  (handler), no de dominio: así semillas y tests pueden construir histórico.
- Operaciones: crear (ctor), `Editar(cantidadMaples, unidadesIncompletas,
  maplesDescarte, unidadesDescarte, hora)` y `Desactivar()`. Editar y
  desactivar lanzan `ReglaNegocioException` ("El registro está sellado: solo se
  puede corregir el mismo día.") si `Fecha < hoy` — el sellado sí es
  invariante de dominio.

### Agregado `RegistroMortalidad` (raíz propia)

Un evento de bajas: fecha, hora y cuántas gallinas murieron. Sin causa ni
observaciones.

- Tabla `registros_mortalidad`. Campos: `GalponId`, `ClienteId`, `Fecha`
  (inmutable, la fija el servidor), `Hora`, `CantidadMuertas` (int > 0,
  editable el mismo día), `GallinasVivas` (snapshot **después** de descontar;
  se actualiza al editar la cantidad, porque el inventario ajustado también
  cambia), `IdempotencyKey`, `EstaActivo`.
- Invariantes: fecha no futura, `CantidadMuertas > 0`. Mismo sellado de dominio
  que producción al editar/desactivar.
- **Efecto sobre el inventario** (en los handlers, con el agregado `Galpon`):
  - Registrar: `galpon.AjustarInventarioGallinas(actuales − muertas)`; el
    snapshot del registro es el inventario resultante. La invariante de SP5
    (0 ≤ actuales ≤ capacidad) rechaza muertas > actuales.
  - Editar cantidad (mismo día): se repone la cantidad anterior y se descuenta
    la nueva (`actuales + anterior − nueva`); el snapshot se actualiza al
    inventario resultante.
  - Desactivar (mismo día): se reponen las muertas (`actuales + muertas`).
  - Cada efecto sobre el inventario se narra con el registro de vuelo
    (`Decidir`), con las cantidades como campos no-PII.

### Ventana temporal y sincronización offline

- La `Fecha` la asigna el servidor (hoy, UTC, patrón `DateOnly.FromDateTime(
  DateTime.UtcNow)`): no depende del reloj del dispositivo. Una recogida
  registrada offline que llega al día siguiente se graba en el día de llegada,
  conservando la `Hora` real que mandó el cliente. Es la única concesión a la
  sincronización offline; nunca se acepta una fecha pasada explícita.
- Editar y desactivar: solo si `Fecha == hoy` en el servidor al momento del
  request (y el dominio lo refuerza con el sellado).

### Application

Patrón de SP5: commands/queries `sealed record`, handlers `sealed class` con
`IUnidadTrabajoGestionAvicola`, FluentValidation, excepciones de dominio,
`IOperacionRegistrable` en mutaciones.

- Producción: `RegistrarProduccion`, `EditarProduccion`,
  `DesactivarProduccion`, `ListarProduccionPorDia` (devuelve las recogidas del
  día + totales agregados del día, incluido el descarte).
- Mortalidad: `RegistrarMortalidad`, `EditarMortalidad`,
  `DesactivarMortalidad`, `ListarMortalidadPorDia`.
- Eficiencia: `ObtenerEficienciaGalpon` (desde/hasta): por cada día con
  eventos, suma de vendibles ÷ `GallinasVivas` del **último evento activo del
  día** (recogida o mortalidad, la más reciente por hora); devuelve por día:
  fecha, maples, sueltos, vendible, maples y sueltos de descarte, total
  descarte, gallinas vivas, eficiencia (%) y `BajoUmbral` (eficiencia < 70). Un
  día sin eventos no aparece; el día de hoy sin eventos devuelve la población
  actual con eficiencia 0.
- **Idempotencia** en `RegistrarProduccion` y `RegistrarMortalidad`: si llega
  una `IdempotencyKey` ya existente (y activa) para el mismo galpón, el handler
  devuelve el registro existente sin crear nada (200, no 409).
- Registro de vuelo: `avicola.produccion.registrar`,
  `avicola.produccion.editar`, `avicola.produccion.desactivar`,
  `avicola.mortalidad.registrar`, `avicola.mortalidad.editar`,
  `avicola.mortalidad.desactivar`. Campos permitidos no-PII: `CantidadMaples`,
  `UnidadesIncompletas`, `MaplesDescarte`, `UnidadesDescarte`,
  `CantidadMuertas`, `GallinasVivas`. El ajuste de inventario se narra con
  `Decidir("...", "ajuste_inventario", "aplicada", { GallinasVivas })`.

### Infraestructura

- `GestionAvicolaDbContext`: dos `DbSet` nuevos con los mismos filtros
  globales (`EstaActivo && tenant`).
- Configuraciones EF: `Fecha` tipo `date`, `Hora` tipo `time`; índices
  `(GalponId, Fecha)` en ambas tablas; índice único filtrado por
  `IdempotencyKey`; checks `UnidadesIncompletas >= 0 AND < 30`,
  `UnidadesDescarte >= 0 AND < 30`, `CantidadMaples >= 0`,
  `MaplesDescarte >= 0` y `CantidadMuertas > 0`.
- Migración `ProduccionYMortalidad` con `dotnet ef` (mismo procedimiento que en
  SP5).
- Sin semilla nueva: las recogidas son datos del día; los tests de integración
  crean las suyas vía API.

### API (Host)

- `POST /galpones/{galponId}/produccion`, `GET
  /galpones/{galponId}/produccion?fecha=yyyy-MM-dd`, `PUT /produccion/{id}`,
  `DELETE /produccion/{id}` → política
  `PoliticasClientes.Para(Funcionalidades.ProduccionHuevos)`.
- `POST /galpones/{galponId}/mortalidad`, `GET
  /galpones/{galponId}/mortalidad?fecha=`, `PUT /mortalidad/{id}`, `DELETE
  /mortalidad/{id}` → `Funcionalidades.Mortalidad`.
- `GET /galpones/{galponId}/eficiencia?desde=&hasta=` →
  `Funcionalidades.ProduccionHuevos`.
- Pensados para el rol Trabajador (el recolector solo tendrá estas
  funcionalidades asignadas); el Cliente también puede usarlos.
- `DELETE` = desactivar (soft delete), nunca borrado físico (glosario).
- No hace falta tocar `Program.cs` (ensamblados ya registrados en SP5) ni las
  políticas (ya se generan para todos los valores de `Funcionalidades`).

### Tests

- Unitarios (TDD): invariantes y sellado de ambos agregados, constantes
  (`Maple`, `EficienciaPostura`), totales de vendible y descarte, handlers con
  NSubstitute (idempotencia, descuento/reposición de inventario, ventana de
  edición, anti-enumeración, cálculo de eficiencia con snapshots y con días
  sin eventos).
- Integración (Testcontainers): flujo completo recogida → mortalidad →
  eficiencia; 404 de otro tenant; 403 del trabajador sin la funcionalidad;
  edición permitida hoy; sellado de registros pasados (sembrados directo en
  BD con fecha de ayer); idempotencia con key repetida; descarte excluido de
  la eficiencia.
- Arquitectura: sin cambios (no hay ensamblados nuevos).

### Documentación

- Glosario: se corrige "turnos" (son recogidas a lo largo del día, sin
  horario) y se añaden recogida, ventana de edición del día, snapshot de
  gallinas vivas, idempotencia y el umbral operativo del 70 %.
- Spec de SP5: se corrige la mención a turnos en la sección de SP6.
- `AGENTS.md` se actualiza al cerrar la implementación (producción y
  mortalidad en el módulo) y se regeneran los adaptadores.

## Fuera de alcance

Vacunación, alimentación, despachos, precios, cuenta corriente, alertas
automáticas por umbral, venta de huevos de descarte, UI de la PWA y su
sincronización offline, migración de datos del legacy.

## Decisión para el frontend avícola (futura)

La PWA trabajará offline-first en la recogida: cola de salida en **IndexedDB**
(cada recogida/mortalidad pendiente con su `IdempotencyKey` generada en el
dispositivo) y sincronización al volver la red (evento `online` y Background
Sync API del service worker). El backend de este SP6 ya está preparado para
esa retransmisión: la idempotencia evita duplicados y la fecha la fija el
servidor a la llegada. La pantalla de recogida podrá mandar producción y
mortalidad en un solo gesto (dos requests).

## Orden orientativo de subproyectos avícolas

SP5 granjas + galpones (hecho) → **SP6 producción + mortalidad (este)** →
SP7 vacunación → SP8 alimentación → SP9 despachos → SP10 precios. Orientativo:
cada subproyecto confirma su alcance en su propio brainstorming.
