# Serilog y Seq: plan de mejora verificable

Fecha: 2026-09-15.
Estado: **implementado** en `develop` (commits `555772f`, `6f6a932`, `4e651a1`,
`300b55a`, `70fcc66` y el cierre documental). Evidencia y correcciones en
[Estado de implementación](#estado-de-implementación).
Base: `develop`, `8ad9664`.
Diseño: [spec de Serilog y Seq](../specs/2026-09-15-serilog-seq-configuracion-design.md).

## Objetivo

Poder leer una ejecución en Seq con su entrada, decisiones, persistencia y
resultado, sin ruido innecesario ni datos personales. El cambio se realizará
en una sesión posterior: el usuario pidió expresamente solo revisión, spec y
plan. Este documento no constituye autorización de implementación.

## Condiciones de ejecución futura

- Comprobar git y cambios ajenos; trabajar en `develop`, sin nuevas ramas ni PR.
- Leer instrucciones locales cuando existan. No introducir dependencias de
  backend en `Trajano.GestorCaisy`.
- Mantener el contrato anti-PII y los nombres usados en consultas existentes.
- Prueba dirigida roja por el defecto correcto antes de corregirlo. No basta
  con comprobar que un archivo contiene una cadena o que una cabecera existe.
- Capturar `LogEvent` de Serilog real y su JSON; los capturadores de `ILogger`
  solos no detectan propiedades heredadas, filtros ni configuración del sink.
- Ejecutar `./verify.ps1` antes de cada commit y push de implementación. Los
  comandos dirigidos de abajo sirven durante desarrollo y no sustituyen ese gate.
- Docker y .NET 10 para integración; Seq de pruebas aislado. No detener, borrar
  ni reconfigurar contenedores/volúmenes actuales para simular fallos.

## Tarea 1 — Configuración efectiva de ambos hosts

Dependencias: ninguna.

Rutas existentes:

- `Icarus/Directory.Packages.props`.
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/Icarus.BuildingBlocks.Observability.csproj`.
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ObservabilityExtensions.cs`.
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ReleaseDiagnostico.cs`.
- `Icarus/src/Host/Icarus.Host/appsettings.json` y `appsettings.Development.json`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Trajano.GestorCaisy.csproj`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Program.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/appsettings.json` y `appsettings.Development.json`.
- `docker-compose.dev.yml`, `docker-compose.prodlocal.yml`.
- `quality/__tests__/compose-restart.test.mjs`.
- `docs/operacion/observabilidad.md` (variables y propiedades; completar guía en tarea 6).

Rutas nuevas previstas:

- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/MetadatosSegurosEnricher.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/MetadatosSegurosEnricher.cs`.
- `Icarus/tests/Icarus.UnitTests/Observability/ConfiguracionSerilogTests.cs`.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Observabilidad/ConfiguracionSerilogTests.cs`.

- [ ] Probar composición real base + Development/Testing/Production + variables.
  Rojo esperado: override Microsoft ausente en API; Information suprimido en MVC
  Development; ausencia de máquina/hilo y falta de Seq MVC de desarrollo.
- [ ] Añadir Environment, Thread y Async al CPM y consumidores, sin actualizar
  por rutina paquetes actuales. Comprobar versión transitiva de Configuration
  antes de hacerla explícita.
- [ ] Declarar sinks/enrichers/niveles en JSON. Mantener consola JSON con Async
  acotado y no bloqueante. Resolver `Entorno` real y `Release` saneada mediante
  extensión declarada desde configuración; preservar pruebas existentes de Release.
- [ ] Migrar variables Seq en compose, tests y documentación en el mismo cambio.
  Para Production con Seq opcional, configurar `Name` además de `serverUrl`.
  Sin configuración remota, consola sigue disponible. No inventar que una URL
  vacía deshabilita automáticamente un sink declarado.
- [ ] Verificar número y tipo de sinks mediante entrega a destinos de prueba,
  sin duplicados; propiedades globales con los mismos nombres en ambos hosts.
  Verificar que cambiar un valor en configuración modifica el comportamiento
  observado y que ningún secreto aparece en las salidas de las pruebas.

Comandos dirigidos:

```powershell
dotnet restore Icarus/Icarus.sln
dotnet test Icarus/tests/Icarus.UnitTests --filter 'FullyQualifiedName~ConfiguracionSerilog|FullyQualifiedName~ReleaseDiagnostico'
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter 'FullyQualifiedName~ConfiguracionSerilog'
node --test quality/__tests__/compose-restart.test.mjs
```

Commit previsto tras gate: `refactor(observabilidad): declarar configuración Serilog en ambos hosts`.

## Tarea 2 — Resumen HTTP y privacidad de los eventos finales

Dependencias: tarea 1.

Rutas existentes:

- `Icarus/src/Host/Icarus.Host/Program.cs`.
- `Icarus/src/Host/Icarus.Host/Observability/RequestObservabilityMiddleware.cs`.
- `Icarus/src/Host/Icarus.Host/Middleware/ClienteActivoMiddleware.cs` (referencia para pruebas).
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs`.
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/CorrelationIdMiddleware.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Program.cs`.
- Los JSON de Serilog de la tarea 1 para los filtros necesarios.
- `Icarus/tests/Icarus.IntegrationTests/CorrelationIdIntegrationTests.cs`.
- `Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs`.

Rutas nuevas previstas:

- `Icarus/src/Host/Icarus.Host/Observability/RegistroHttpSeguro.cs`.
- `Icarus/src/Host/Icarus.Host/Observability/ContextoTrazaMiddleware.cs`.
- `Icarus/src/Host/Icarus.Host/Observability/ContextoIdentidadObservabilidadMiddleware.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/RegistroHttpSeguro.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/ContextoPeticionMiddleware.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/ExcepcionesSegurasMiddleware.cs`.
- `Icarus/tests/Icarus.IntegrationTests/Observability/RegistroHttpSeguroTests.cs`.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Observabilidad/RegistroHttpSeguroTests.cs`.

- [ ] Capturar eventos completos para 2xx, 400, 401 por cliente inactivo, 403,
  404, 409, 429, 500 y errores MVC reejecutados. Rojo esperado: resumen ausente
  en rechazo temprano, `RequestPath` presente y duplicación MVC según escenario.
- [ ] Añadir canarios sintéticos en ruta desconocida, query, headers, excepción
  y parámetros de datos ficticios. Verificar ausencia en JSON, mensaje renderizado,
  excepción y scopes. No enviar canarios ni excepciones inducidas al stack real.
- [ ] Separar scopes de contexto del resumen; situar el manejador seguro de
  errores antes de autenticación/cliente activo, con el resumen por fuera.
  Capturar el endpoint original antes de reejecuciones y el estado final después.
- [ ] Sustituir exclusivamente la emisión manual final por el resumen Serilog
  adaptado. Reemplazar propiedades por defecto con `GetMessageTemplateProperties`;
  conservar nombres actuales y enriquecer desde contexto guardado, incluyendo
  IDs, ErrorId y tenant/rol después de autenticación.
- [ ] Eliminar rutas concretas importadas por scopes en eventos propios.
  Identificar y excluir emisiones crudas de hosting/EF/HttpClient con filtros
  declarativos y diagnóstico propio seguro. No marcar esta tarea completada
  solo porque el resumen principal pase: repetir los canarios en el pipeline real.
- [ ] No añadir IP/UserAgent ni Host sin lista confiable. Validar Release y rol;
  nunca copiar claims completos ni cuerpos.
- [ ] Probar cancelación, respuesta ya iniciada y fallo temprano. No generar un
  segundo resumen ni convertir una cancelación en un supuesto error de negocio.

Comandos dirigidos:

```powershell
dotnet test Icarus/tests/Icarus.IntegrationTests --filter 'FullyQualifiedName~RegistroHttpSeguro|FullyQualifiedName~CorrelationId'
dotnet test Icarus/tests/Icarus.UnitTests --filter 'FullyQualifiedName~ExceptionHandlingMiddleware|FullyQualifiedName~CorrelationId'
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter 'FullyQualifiedName~RegistroHttpSeguro'
```

Commit previsto tras gate: `fix(observabilidad): cubrir peticiones y sanear eventos HTTP`.

## Tarea 3 — Correlación MVC → API y reintentos

Dependencias: tarea 2.

Rutas existentes:

- `Icarus/src/Apps/Trajano.GestorCaisy/Program.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/ContextoPeticionMiddleware.cs` (tarea 2).

Rutas nuevas previstas:

- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/CorrelacionApiHandler.cs`.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Observabilidad/CorrelacionApiTests.cs`.

- [ ] Registrar una llamada MVC, sus envíos HTTP y la respuesta API en hosts
  de prueba. Rojo esperado: no hay UUID propio enviado explícitamente por MVC.
- [ ] Implementar handler de envío que genere un UUID por intento, también en
  multipart y refresh, sin serializar contenido ni autenticación.
- [ ] Mantener el ID de entrada en el scope MVC y registrar el saliente como
  `DownstreamCorrelationId`; no sobrescribir el contexto del padre.
- [ ] Verificar `TraceId` compartido y spans distintos con instrumentación real
  de ASP.NET/HttpClient; los tests con handler falso solos no prueban propagación.
- [ ] Cubrir 401 → refresh → reintento y fallo de renovación: IDs diferentes
  por envío, misma traza, resultado final inequívoco y sin tokens en eventos.

Comandos dirigidos:

```powershell
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter 'FullyQualifiedName~CorrelacionApi'
dotnet test Icarus/tests/Icarus.ArchitectureTests
```

Commit previsto tras gate: `feat(observabilidad): correlacionar llamadas de GestorCaisy a la API`.

## Tarea 4 — Campos perdidos y narración del recorrido

Dependencias: tarea 1; integrar después de tarea 3 para validar contexto completo.

Rutas existentes:

- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/Observability/IRegistroVuelo.cs`.
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/Observability/DescriptorOperacionRegistroVuelo.cs`.
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/RegistroVuelo.cs`.
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/RegistroVueloBehavior.cs`.
- `Icarus/tests/Icarus.UnitTests/Observability/RegistroVueloTests.cs`.
- `Icarus/tests/Icarus.UnitTests/Observability/RegistroVueloBehaviorTests.cs`.
- `Icarus/tests/Icarus.IntegrationTests/Observability/RegistroVueloAltaClienteIntegrationTests.cs`.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/ComandosPedidosAlimento.cs`.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Mortalidad/Commands.cs`.

- [ ] Rojo dirigido: `Decidir` público debe conservar `Lineas` permitido y
  rechazar campos desconocidos. Compensación debe conservar
  `CompensationKind = logical`. Verificar ambas cosas en Serilog serializado.
- [ ] Exigir descriptor explícito en decisiones independientes. Usar búsqueda
  por `Decidir(` para enumerar consumidores y dobles antes de cambiar la firma;
  registrar ese inventario en el plan al ejecutarlo. Migrarlos sin ampliar las
  listas permitidas ni añadir registros nominales.
- [ ] Declarar `CompensationKind` como campo permitido interno de compensación.
  No eliminar la condición de descriptor nulo que protege los datos.
- [ ] Usar frases estables en español con operación/decisión/resultado. Mantener
  `EventName`, fases y nombres de operación. Normalizar resultados al contrato.
- [ ] Añadir una decisión segura al retorno idempotente representativo, para
  distinguir reutilización de creación sin registrar una identidad del trabajador.
- [ ] Comprobar secuencia de alta con compensación y un pedido: inicio,
  decisión, persistencia/transacción, resultado y resumen HTTP. Probar error
  después de una decisión y evitar que el texto afirme una escritura confirmada.

Comandos dirigidos:

```powershell
dotnet test Icarus/tests/Icarus.UnitTests --filter 'FullyQualifiedName~RegistroVuelo'
dotnet test Icarus/tests/Icarus.IntegrationTests --filter 'FullyQualifiedName~RegistroVuelo|FullyQualifiedName~PersistenciaRegistro'
```

Los tests representativos nuevos de pedido/idempotencia se añaden en
`Icarus/tests/Icarus.IntegrationTests/Observability/RegistroVueloFlujosTests.cs`
y se incluyen en el filtro `RegistroVuelo`.

Commit previsto tras gate: `fix(observabilidad): conservar campos seguros y explicar decisiones del flujo`.

## Tarea 5 — Entrega a Seq y fallos de transporte

Dependencias: tareas 1–4.

Rutas nuevas previstas:

- `Icarus/tests/Icarus.IntegrationTests/Observability/IngestionSeqTests.cs`.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Observabilidad/IngestionSeqTests.cs`.

Rutas a completar: los adaptadores de observabilidad de ambos hosts y
`docs/operacion/observabilidad.md`.

- [ ] Crear fixtures aisladas con destino controlable y contenedor Seq de
  pruebas; datos sintéticos, puertos/volúmenes propios y limpieza solo de recursos
  creados por la fixture.
- [ ] Probar ausencia de URL/configuración remota, URL inaccesible, rechazo de
  API key con valor sintético, recuperación y apagado ordenado. Distinguir fallo
  de configuración de caída temporal del destino.
- [ ] Probar que latencia de transporte no se incorpora a la petición mediante
  un destino bloqueado controlado; evitar assertions con tiempos frágiles.
- [ ] Verificar eventos estructurados de las dos aplicaciones, cola acotada,
  comportamiento de desbordamiento y señal genérica independiente de fallo.
  No envolver Seq con Async ni reproducir eventos de consola como si hubiera
  un mecanismo de reenvío automático.
- [ ] Afirmar únicamente garantías observadas: no hay entrega exactamente una
  vez ni supervivencia ante terminación abrupta sin almacenamiento durable.

Comandos dirigidos:

```powershell
dotnet test Icarus/tests/Icarus.IntegrationTests --filter 'FullyQualifiedName~IngestionSeq'
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter 'FullyQualifiedName~IngestionSeq'
```

Commit previsto tras gate: `test(observabilidad): verificar ingestión y fallos de Seq`.

## Tarea 6 — Guía de lectura y cierre

Dependencias: tareas 1–5.

Rutas: `docs/operacion/observabilidad.md`, esta spec y este plan.

- [ ] Corregir consultas a `EventName` y `RoutePattern`; mantener `Aplicacion`.
- [ ] Documentar búsqueda por `TraceId`, `CorrelationId`,
  `DownstreamCorrelationId`, `Operation`, `ReasonCode`, `Outcome` y `Release`.
- [ ] Añadir un ejemplo sintético de recorrido correcto y otro rechazado/fallido:
  qué decisión cambió el camino, si hubo persistencia y cuál fue el resultado.
- [ ] Explicar retención, consola como respaldo independiente, pérdidas posibles,
  configuración de ingestión y diagnóstico seguro de desconexión.
- [ ] Ejecutar puerta completa y leer diff propio; anotar salidas reales y
  pruebas no ejecutadas. Solo entonces marcar tareas completas y hacer commit/push
  en develop según las reglas del repositorio.

```powershell
./verify.ps1
git diff --stat
git diff --check
```

Commit previsto: `docs(observabilidad): documentar diagnóstico de flujos y límites de entrega`.

## Verificación de esta sesión documental

Realizado: inspección de código/configuración, consulta de documentación primaria
y sondeo de salud API confirmado en Seq local. Detalle y límites en la spec.
No se ejecutaron tareas de implementación, tests .NET ni pruebas de caída:
el usuario pidió solo análisis y documentos. La puerta completa de código no
corresponde a este cambio exclusivamente documental.

Verificación documental ejecutada el 2026-09-15:

- `node quality/check-mojibake.mjs`: sin hallazgos en archivos versionados.
- `node quality/check-enlaces.mjs`: 102 Markdown versionados sin enlaces rotos.
- `git diff --check`: sin errores; no cubre los dos archivos aún no versionados.
- Comprobación explícita de ambos borradores con las funciones de esos gates:
  UTF-8 válido, sin BOM, mojibake, enlaces rotos ni espacios al final de línea;
  los dos bloques JSON de la spec son sintácticamente válidos.

La validación sintáctica del JSON no prueba el arranque de la propuesta: contiene
adaptadores futuros. Los documentos quedan como borradores locales, sin commit.

## Estado de implementación

Implementado el 2026-09-15 en `develop`, sin ramas ni PR, con `./verify.ps1`
verde antes de cada commit (Docker y .NET 10).

### Tareas

1. **Configuración declarativa (commit `555772f`)**. `Serilog` gobierna sinks,
   niveles, filtros y enrichers en ambos hosts; `AddObservabilidad` solo compone
   `ReadFrom.Configuration` + `ReadFrom.Services`. Se añadieron
   `Serilog.Enrichers.Environment`, `Serilog.Enrichers.Thread`,
   `Serilog.Sinks.Async` y `Serilog.Settings.Configuration` explícito al CPM.
   `WithMetadatosSeguros` resuelve `Entorno` real y sanea `Release` (verificado
   que gana sobre `Properties` porque `PropertyEnricher` usa
   `AddPropertyIfAbsent`). Variables de Seq migradas a entradas nombradas en
   ambos compose, `compose-restart.test.mjs` y la guía.
2. **Resumen HTTP y privacidad (commit `6f6a932`)**. Orden nuevo:
   correlación → traza → `UseSerilogRequestLogging(RegistroHttpSeguro)` →
   excepciones → routing → auth → contexto de identidad → cliente activo.
   `ContextoIdentidadObservabilidadMiddleware` guarda tenant/rol en `Items` para
   el resumen externo. `GetMessageTemplateProperties` emite `EventName`,
   `Method`, `RoutePattern`, `StatusCode`, `DurationMs`, `ErrorId` y **sombra
   `RequestPath` con el patrón**; el manejador de excepciones dejó de registrar
   `ExceptionStackTrace` y sombra la ruta. Canarios sintéticos verificados en el
   JSON serializado real (API y MVC). GestorCaisy usa `ContextoPeticion`,
   `ContextoRuta`, `ExcepcionesSeguras` y `UseExceptionHandler` siempre activo
   para consumir el error antes del resumen.
3. **Correlación MVC → API (commit `4e651a1`)**. `CorrelacionApiHandler` genera
   un UUID por envío real (multipart, refresh y reintento), lo envía en
   `X-Correlation-ID` y lo registra como `DownstreamCorrelationId` sin tocar el
   `CorrelationId` del padre ni exponer token/cuerpo. Se verificó el `TraceId`
   W3C entrante en el resumen de la API.
4. **Campos y narración (commit `300b55a`)**. `IRegistroVuelo` expone
   `Decidir(string, string, string)` (sin campos) y
   `Decidir(DescriptorOperacionRegistroVuelo, string, string, campos)` con lista
   cerrada; se migraron todos los llamadores que aportan campos en Vacunación,
   Precios, Pedidos, Despachos, Balance, Mortalidad y Granjas. Las
   compensaciones declaran `CompensationKind`. Frase estable en español; las
   decisiones `aplicada` se normalizan a `Outcome = 'applied'`. Se añadió la
   decisión `idempotencia`/`reutilizada` en mortalidad y una prueba de secuencia
   completa del alta con cuenta.
5. **Ingestión y fallos de Seq (commit `70fcc66`)**. Pruebas con contenedor Seq
   aislado (Testcontainers, puertos propios): evento estructurado consultable,
   ausencia de configuración remota, destino inaccesible que no bloquea, rechazo
   401 simulado y apagado ordenado. GestorCaisy verifica la ingestión de su
   resumen real. No se envuelve Seq con Async ni se reproduce consola hacia Seq.
6. **Documentación y cierre**. Guía corregida a `EventName`/`RoutePattern` y
   `Aplicacion`, con `DownstreamCorrelationId`, ejemplos de recorrido correcto y
   rechazado, y límites de entrega/retención.

### Correcciones al diseño

- **`Decidir` no exige descriptor en todas las llamadas.** Se mantiene una
  sobrecarga de narración sin campos y una sobrecarga con descriptor para las
  que sí aportan datos. Así la ausencia de descriptor no autoriza campos libres
  y no se reescriben los ~35 llamadores de narración pura.
- **El resumen de `UseSerilogRequestLogging` usa el logger del host (DI), no el
  estático `Log`.** El middleware crea el `LogEvent` y lo escribe por el logger
  configurado; dejarlo en el estático permitía que otro host o un logger en
  apagado silenciara el resumen (y volvía inestable la captura en pruebas).
- **`RequestPath` se conserva sombreado con el patrón seguro.** El scope del
  framework lo hereda con el pathname concreto; se emite el mismo valor seguro
  que `RoutePattern` en el resumen y en el manejador de excepciones.
- **`ExcepcionesSegurasMiddleware` registra en un método fuera del `catch`**
  para no pasar la excepción cruda al logger (regla S6667) sin perder la señal.
- **Testcontainers pasa a ser dependencia de pruebas de `Trajano.GestorCaisy.Tests`**
  (no del backend que consume).
- **`UseExceptionHandler` está activo en todos los entornos de GestorCaisy**, no
  solo en Development: el diagnóstico del framework con la excepción cruda podía
  alcanzar cualquier entorno. La afirmación previa de que era «una limitación de
  desarrollo, no de producción» quedó **corregida** por el
  [cierre correctivo](../specs/2026-09-15-serilog-seq-cierre-correctivo-design.md),
  que suprime ese diagnóstico y cubre los tres entornos con canarios sintéticos.

### Verificación observada

`./verify.ps1` verde tras cada tarea: build sin warnings; `ArchitectureTests`
6/6; `UnitTests` 545/545; `GestorCaisy.Tests` 213/213; `IntegrationTests`
177/177 (incluye Testcontainers de SQL Server y de Seq); frontend build, lint y
303 pruebas. Las pruebas de ingestión de Seq levantaron contenedores aislados
con datos sintéticos.

### Limitaciones

- No se probó desbordamiento de cola acotada ni supervivencia a terminación
  abrupta (no hay buffer durable); la guía documenta la pérdida posible.
- El `TraceId` W3C de extremo a extremo se verificó de forma entrante en la API
  y por la instrumentación real de `HttpClient`; no se levantó un `Activity` de
  punta a punta MVC→API en la suite.
- El rechazo de API key se simuló con un destino 401 controlado, no con un Seq
  con autenticación real.
