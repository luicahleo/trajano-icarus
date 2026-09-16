# Serilog y Seq: plan del cierre correctivo

Fecha: 2026-09-15.
Estado: implementado y verificado (2026-09-16), sobre `develop`.
Base inspeccionada: `develop`, `f0faf94`.
Spec: [privacidad y trazabilidad](../specs/2026-09-15-serilog-seq-cierre-correctivo-design.md).

## Resultado de la implementación (2026-09-16)

Puerta completa (`./verify.ps1`) verde tras cada tarea. Commits en `develop`:

| Tarea | Commit | Rojo/control observado | Prueba dirigida |
|---|---|---|---|
| 1 | `43ee93e` | el canario aparecía en `ExceptionHandlerMiddleware` en Development, Testing y Production | `PrivacidadExcepcionesTests` (3 entornos) + `RegistroHttpSeguroTests` |
| 2 | `b016229` | segmento sintético literal en `RoutePattern`; y el fallo de red del cliente tipado filtraba la URI por el scope de `IHttpClientFactory` | `CorrelacionApiTests` + host real en `RegistroHttpSeguroTests` |
| 3 | `7b38943` | faltaban `ClienteId`/`Rol` en eventos internos y `RequestPath` conservaba el pathname concreto | `ContextoFlujoTests`, `PrivacidadPipelineTests` |
| 4 | `8ab6383` | n/a (prueba nueva); halló y corrigió que el enriquecedor de ruta de la tarea 3 sobrescribía el `RoutePattern` saliente | `CorrelacionExtremoAExtremoTests` + `Icarus.ArchitectureTests` |
| 5 | `570f4d3` | sin monitor conectado, la capacidad no era observable en el host | `ColaConsolaTests` (UnitTests y MVC) + `IngestionSeqTests` |
| 6 | este commit | — | `./verify.ps1` |

Hallazgo real de la tarea 4: el enriquecedor de `ContextoRutaMiddleware` (tarea 3)
usaba `AddOrUpdateProperty` para `RoutePattern`, de modo que sobrescribía la
plantilla de `http.client.send` con el patrón de la ruta MVC entrante. Se corrigió
a `AddPropertyIfAbsent` para `RoutePattern` y se mantuvo `AddOrUpdateProperty`
solo para `RequestPath`.

Límites declarados: no hay buffer durable; la prueba de 401 usa una inyección
exclusiva de tests sobre el servidor API y no equivale a Seq con autenticación
real. La tarea 4 cubre petición normal y ciclo 401 → renovación → reintento; el
multipart y la renovación fallida de extremo a extremo quedan como ampliación.

## Preparación y límites

Esta sesión solo escribe documentos. La siguiente implementa las correcciones
sin repetir la refactorización ya incorporada en `555772f`..`f0faf94`.

1. Leer AGENTS.md, comenzar con `git status --short --branch` y
   `git log -5 --oneline`. Consultar el handoff y comprobar fecha/base: el
   existente al redactar trataba un bloque anterior, no este cierre.
2. Leer esta spec y plan completos, más las instrucciones locales aplicables.
   Verificar defectos contra el árbol actual y preservar cambios ajenos.
3. Trabajar en develop, sin ramas nuevas ni PR. No usar subagentes salvo
   autorización explícita aplicable. No tocar master.
4. Usar .NET 10 y Docker para los tests de integración, con recursos efímeros
   propios. Ninguna prueba de error/parada se ejecuta contra el stack del usuario.
5. Observar rojo dirigido por el defecto real antes del cambio. Si una prueba
   de una garantía existente pasa inicialmente, registrar ese hecho y demostrar
   sensibilidad con un control negativo aislado; no afirmar una regresión roja
   que no ocurrió ni fabricar un defecto en el producto para justificarla.
6. Antes de cada commit/push de implementación: `./verify.ps1` completo,
   `git diff --stat`, diff relevante y `git diff --check`. Sin `--no-verify`,
   exclusiones de calidad nuevas ni umbrales relajados.

Secuencia: tarea 1 → tarea 2 → tarea 3 → tarea 4 → tarea 5 → tarea 6.
La captura que se prepare en tarea 1 se reutiliza en las posteriores.

## Tarea 1 — Cerrar la salida de excepciones crudas en MVC

Criterio: C1; comienza la cobertura transversal de C4.

Modificar:

- `Icarus/src/Apps/Trajano.GestorCaisy/Program.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/ExcepcionesSegurasMiddleware.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/RegistroHttpSeguro.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/appsettings.json` y `appsettings.Development.json`, si requieren filtros.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/AplicacionDePruebas.cs`.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Observabilidad/RegistroHttpSeguroTests.cs`.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Observabilidad/ColectorSerilog.cs`.

Crear si se necesita separar escenarios:

- `Icarus/tests/Trajano.GestorCaisy.Tests/Observabilidad/PrivacidadExcepcionesTests.cs`.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/ControladorErroresDePrueba.cs`.

- [x] Capturar el logger real por instancia de escenario y serializar todos
  sus eventos, sin seleccionar primero por CorrelationId ni descartar fuentes
  Microsoft. Evitar que un colector acotado descarte precisamente la evidencia.
- [x] Incorporar un endpoint/controlador solo de tests que lance una excepción
  con canario e inner exception. Ejecutarlo en Development, Testing y Production,
  aislando la URL Seq de desarrollo mediante override de prueba.
- [x] Rojo esperado: el canario aparece en la emisión de
  `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware` aunque el
  `backend.error` propio sea seguro. Guardar test y causa, sin volcar payloads.
- [x] Configurar supresión explícita del diagnóstico redundante del manejador
  y completar fallback seguro. Conservar página, estado, un ErrorId y un resumen.
- [x] Probar fallo al renderizar la página, respuesta ya iniciada y cancelación.
  Completar filtros por fuente/evento si el callback no cubre esos caminos.
  Verificar también `LogEvent.Exception` del resumen Serilog. Si no puede
  emitirse una respuesta completa, comprobar aborto sin fabricar otra respuesta.
- [x] Confirmar que siguen existiendo eventos seguros Error: un filtro que
  elimina todo Error no satisface el contrato.

Si hacen falta expresiones declarativas, las únicas rutas adicionales de
dependencias son `Icarus/Directory.Packages.props` y
`Icarus/src/Apps/Trajano.GestorCaisy/Trajano.GestorCaisy.csproj`. Justificar el
paquete y mantener configuración de sinks/niveles en JSON.

```powershell
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter 'FullyQualifiedName~PrivacidadExcepciones|FullyQualifiedName~RegistroHttpSeguro'
```

Commit previsto: `fix(observabilidad): impedir excepciones crudas en los logs MVC`.

## Tarea 2 — Plantillas seguras para cada envío a la API

Dependencias: tarea 1. Criterio: C2.

Modificar:

- `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/CorrelacionApiHandler.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/appsettings.json`, si necesita filtros de HttpClient.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Observabilidad/CorrelacionApiTests.cs`.

Crear:

- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/MetadatosPeticionApi.cs`.

- [x] Enumerar mediante búsqueda dirigida los constructores de peticiones:
  JSON, multipart, refresh y otros `new HttpRequestMessage`. No limitarse a
  `ListarNotificacionesAsync`, cuya ruta no contiene parámetros dinámicos.
- [x] Rojo esperado: una ruta con segmento sintético llega literalmente a
  RoutePattern. Probar también URI desconocida, query y excepción de red.
- [x] Transportar la plantilla por `HttpRequestMessage.Options` desde el lugar
  que conoce la operación; actualizar todas las llamadas afectadas. El handler
  usa ese metadato o `unmatched`, sin inspeccionar AbsolutePath como fallback.
- [x] Cubrir JSON, multipart, renovación y reintentos. Verificar que la URI y
  el contenido enviados siguen siendo funcionalmente los mismos.
- [x] Revisar los logs automáticos de IHttpClientFactory en el host real y
  excluir emisiones inseguras concretas conservando `http.client.send` seguro.
  Esta exclusión es política de logging, no una exclusión del gate de calidad.
- [x] Confirmar UUID diferente por envío, conservación del padre y ningún
  canario en mensaje, excepción o propiedades del conjunto completo.

```powershell
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter 'FullyQualifiedName~CorrelacionApi|FullyQualifiedName~RegistroHttpSeguro'
```

Commit previsto: `fix(observabilidad): registrar plantillas en las llamadas a la API`.

## Tarea 3 — Restaurar contexto interno y sanear rutas del pipeline

Dependencias: tareas 1–2. Criterios: C3 y C4.

Modificar:

- `Icarus/src/Host/Icarus.Host/Program.cs`.
- `Icarus/src/Host/Icarus.Host/Observability/ContextoIdentidadObservabilidadMiddleware.cs`.
- `Icarus/src/Host/Icarus.Host/Observability/RegistroHttpSeguro.cs`.
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs`, si requiere contexto recuperado tras desenrollar scopes.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/ContextoRutaMiddleware.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Program.cs`, si cambia ubicación del scope.
- Los `appsettings.json` de ambos hosts para filtros concretos necesarios.
- `Icarus/tests/Icarus.IntegrationTests/Observability/RegistroHttpSeguroTests.cs`.
- `Icarus/tests/Icarus.IntegrationTests/Observability/RegistroVueloAltaClienteIntegrationTests.cs`.

Crear:

- `Icarus/src/Host/Icarus.Host/Observability/ContextoRutaMiddleware.cs`.
- `Icarus/tests/Icarus.IntegrationTests/Observability/ContextoFlujoTests.cs`.
- `Icarus/tests/Icarus.IntegrationTests/Observability/PrivacidadPipelineTests.cs`.

- [x] Usar una operación autenticada de tenant con decisión y persistencia
  reales; no usar únicamente el alta de cliente por un administrador global,
  que legítimamente puede no tener ClienteId.
- [x] Rojo esperado: faltan ClienteId/Rol en eventos internos y se conserva
  RequestPath concreto fuera del resumen. Comprobar cada clase de evento.
- [x] Mantener Items y abrir scope de identidad con datos permitidos después
  de autenticar. Abrir scope de ruta segura después de routing y antes de
  emisiones de la ejecución. Disponer ambos incluso ante error.
- [x] Probar peticiones concurrentes de dos tenants y una anónima posterior:
  contexto correcto, sin contaminación ni scopes estáticos/globales compartidos.
- [x] Revisar ruta desconocida, autenticación, operación, SaveChanges fallido,
  reejecución MVC y eventos de hosting. Inspeccionar JSON completo por escenario,
  no solo las propiedades del evento propio ni un conjunto filtrado por IDs.
- [x] Para fuentes que incrustan datos en mensajes/excepciones, aplicar
  supresión específica y conservar un diagnóstico propio seguro. No pretender
  sanearlas solo sombreando una propiedad de scope.
- [x] Mantener el contexto del resumen y los logs de error exterior tras
  cerrarse los scopes internos. No registrar claims/usuarios/trabajadores.

```powershell
dotnet test Icarus/tests/Icarus.IntegrationTests --filter 'FullyQualifiedName~ContextoFlujo|FullyQualifiedName~PrivacidadPipeline|FullyQualifiedName~RegistroHttpSeguro|FullyQualifiedName~RegistroVueloAltaCliente'
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter 'FullyQualifiedName~RegistroHttpSeguro|FullyQualifiedName~PrivacidadExcepciones|FullyQualifiedName~CorrelacionApi'
```

Commit previsto: `fix(observabilidad): conservar contexto seguro durante todo el flujo`.

## Tarea 4 — Demostrar correlación con dos servidores reales

Dependencias: tareas 1–3. Criterio: C5.

Modificar:

- `Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj`: referencia
  de pruebas a `Trajano.GestorCaisy.csproj` con alias, no una dependencia productiva.
- `Icarus/tests/Icarus.IntegrationTests/IdentityFactory.cs`, solo para exponer
  puntos de extensión necesarios sin cambiar la semántica del resto de la suite.
- `Icarus/tests/Icarus.IntegrationTests/Observability/ColectorSerilog.cs`, si
  necesita captura por instancia para no mezclar hosts.

Crear:

- `Icarus/tests/Icarus.IntegrationTests/Observability/CorrelacionExtremoAExtremoTests.cs`.
- `Icarus/tests/Icarus.IntegrationTests/Observability/HostsObservabilidadFixture.cs`.

- [x] Levantar MVC y API con sus entrypoints reales y `UseKestrel(0)` antes
  de iniciar las factories. Configurar content roots separados: ambos hosts
  tienen appsettings con los mismos nombres, que no deben confundirse en tests.
- [x] Compartir el contenedor SQL de integración con una base exclusiva del
  escenario; no cambiar a Kestrel la factory ya iniciada de toda la colección.
- [x] Resolver la colisión `Program` con alias de ProjectReference y
  `extern alias`. No agregar dependencias backend al csproj MVC ni copiar los
  Program.cs a hosts de juguete. Ejecutar las pruebas de arquitectura.
- [x] Configurar URL API hacia el puerto efímero y conservar el cliente HTTP
  real. Preparar sesión sintética mediante el flujo de autenticación de tests.
- [x] Comprobar mismo TraceId, relaciones de spans, UUID propio por salto y
  correspondencia DownstreamCorrelationId ↔ CorrelationId API. Capturar Activity
  y JSON de ambos hosts; no comparar solo una cabecera enviada manualmente.
- [x] Cubrir petición normal, multipart, 401 → refresh → reintento y renovación
  fallida. Para el 401 se permite inyección exclusiva de tests sobre el servidor,
  sin sustituir HttpClient ni asignar IDs de traza a cada salto.
- [x] Si la propagación nativa ya pasa, registrarlo y añadir control negativo
  que rompa únicamente la propagación en el montaje aislado para verificar que
  las assertions lo detectan. No introducir código productivo innecesario.
- [x] Si se observa un fallo real, corregir solo los adaptadores de contexto,
  cliente o handler implicados; añadir sus rutas y causa al plan antes del commit.
- [x] Verificar que la captura/listener del test no sea quien haga funcionar una
  traza ausente en la configuración productiva. Disponer listeners y hosts al salir.

```powershell
dotnet test Icarus/tests/Icarus.IntegrationTests --filter 'FullyQualifiedName~CorrelacionExtremoAExtremo'
dotnet test Icarus/tests/Icarus.ArchitectureTests
```

La segunda orden incluye `ReglasDeModulosTests.GestorCaisyNoDependeDelBackend`,
que comprueba la separación del desplegable MVC. No buscar esa prueba en la
suite MVC ni interpretar una salida de cero tests como evidencia de arquitectura.

Commit previsto: `test(observabilidad): verificar correlación MVC API de extremo a extremo`.

## Tarea 5 — Saturación y recuperación de la cola de consola

Dependencias: tarea 1; ejecutar después de la tarea 4 para el cierre. Criterio: C6.

Crear:

- `Icarus/tests/Icarus.UnitTests/Observability/ColaConsolaTests.cs`.
- `Icarus/tests/Trajano.GestorCaisy.Tests/Observabilidad/ColaConsolaTests.cs`.
- `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/MonitorColaConsola.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/MonitorColaConsola.cs`.

Modificar:

- Configuración/registro de observabilidad y JSON de ambos hosts para conectar
  el monitor de Async sin cambiar su capacidad productiva ni blockWhenFull.
- `Icarus/tests/Icarus.IntegrationTests/Observability/IngestionSeqTests.cs`,
  para independencia de Seq respecto de la consola bloqueada.

- [x] Cargar la configuración efectiva y reemplazar solo el consumidor final
  por un sink de prueba bloqueable. Usar capacidad pequeña por override del test.
- [x] Esperar señal de consumidor ocupado; llenar la cola y emitir excedentes.
  El productor debe terminar mientras la señal sigue bloqueada. Usar timeout
  de seguridad, no un umbral de rendimiento frágil.
- [x] Observar capacidad, ocupación y descartes con el inspector de Async.
  Añadir monitor mínimo por host y contador genérico independiente, por ejemplo
  `System.Diagnostics.Metrics`. No loguear el aviso en la cola saturada.
- [x] Rojo esperado para la observación productiva: no hay monitor/contador
  conectado. Si el no bloqueo ya funciona, documentar que era una garantía
  existente comprobada; un control negativo con consumidor bloqueado y política
  bloqueante solo en el montaje debe demostrar la sensibilidad del test.
- [x] Liberar consumidor en finally antes de Dispose; comprobar drenaje,
  aceptación de un evento posterior y observación del contador. Ningún payload
  perdido se imprime como diagnóstico de la prueba.
- [x] Con Seq aislado, comprobar que recibe un evento mientras consola está
  bloqueada. No atribuir a esta prueba garantías sobre la cola interna de Seq.

```powershell
dotnet test Icarus/tests/Icarus.UnitTests --filter 'FullyQualifiedName~ColaConsola'
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter 'FullyQualifiedName~ColaConsola'
dotnet test Icarus/tests/Icarus.IntegrationTests --filter 'FullyQualifiedName~IngestionSeq'
```

Commit previsto: `test(observabilidad): comprobar descarte y recuperación de consola`.

## Tarea 6 — Documentación consistente, puerta y entrega

Dependencias: tareas 1–5. Criterio: C7.

Modificar:

- `docs/operacion/observabilidad.md`.
- `docs/superpowers/specs/2026-09-15-serilog-seq-configuracion-design.md`.
- `docs/superpowers/plans/2026-09-15-serilog-seq-configuracion.md`.
- Esta spec y este plan.

- [x] Corregir la afirmación anterior sobre Development y el cierre de todos
  los criterios. Mantener evidencia histórica diferenciada de las nuevas pruebas.
- [x] Explicar consultas con plantillas seguras, identidad disponible en eventos
  internos, relaciones TraceId/SpanId/CorrelationId/DownstreamCorrelationId y
  comportamiento de saturación. Documentar qué fuentes se suprimen y por qué.
- [x] Marcar cada tarea completada con test rojo/control negativo observado,
  comando dirigido, resultado final y commit. No dejar todos los checkboxes
  pendientes junto a un párrafo que diga «implementado».
- [x] Ejecutar puerta completa. Revisar la captura de canarios antes de declararla
  segura y verificar que los tests nuevos están incluidos en la solución/gate.
- [x] Commit y push directos a develop tras verificar. Mantener master intacto.
  Reportar límites: sin buffer durable; la prueba 401 no equivale a Seq con auth real.

```powershell
./verify.ps1
git diff --stat
git diff --check
```

Commit documental previsto: `docs(observabilidad): cerrar privacidad y trazabilidad verificadas`.

## Texto para iniciar la siguiente sesión

```text
Trabaja en C:\Users\lrcahuana\source\repos\Trajano-Icarus.
Implementa el plan docs/superpowers/plans/2026-09-15-serilog-seq-cierre-correctivo.md
y su spec docs/superpowers/specs/2026-09-15-serilog-seq-cierre-correctivo-design.md.

Autorizo la implementación de las seis tareas en esta nueva sesión. Las notas
«solo documentos» describen la sesión de preparación, no restringen esta orden.
Es un cierre correctivo sobre f0faf94, no una repetición de la refactorización.

Sigue AGENTS.md; empieza con git status --short --branch y git log -5 --oneline.
Lee spec y plan, verifica el código actual y preserva cambios ajenos. Resuelve
autónomamente los detalles normales dentro del alcance. No uses subagentes
salvo autorización explícita aplicable.

Prioridades: excepciones MVC seguras en todos los entornos; plantillas salientes
sin rutas concretas; scopes de tenant/rol y ruta seguros en todos los eventos;
correlación con MVC y API reales; saturación observable de consola sin bloquear.
No basta inspeccionar el resumen ni filtrar primero los eventos por CorrelationId.
No uses un cliente falso como prueba de propagación de extremo a extremo.

TDD y pruebas dirigidas con evidencia real. Docker/Seq/SQL aislados: no toques
el stack actual ni datos reales. Mantén configuración declarativa y anti-PII;
no añadas buffer durable ni cambies reglas de negocio o dependencias productivas
entre MVC y backend.

Trabaja en develop, sin ramas nuevas ni PR. Ejecuta ./verify.ps1 antes de cada
commit/push, sin --no-verify ni relajar gates. Haz commit y push a develop de
los cambios verificados; no modifiques master. Actualiza documentación y
checkboxes con resultados reales. Termina con pruebas, commits y limitaciones.
```

## Verificación de la sesión de preparación

Solo se crean esta spec y este plan; ninguna tarea de código está ejecutada.
Se contrastaron rutas y decisiones con `f0faf94` y fuentes primarias de .NET y
Serilog. Verificación documental realizada el 2026-09-15:

- `node quality/check-mojibake.mjs`: sin hallazgos en archivos versionados.
- `node quality/check-enlaces.mjs`: 104 Markdown versionados sin enlaces rotos.
- `git diff --check`: sin errores en cambios versionados.
- Comprobación explícita de ambos documentos nuevos con las funciones de esos
  gates: UTF-8 válido, sin BOM, mojibake, espacios finales ni enlaces rotos;
  bloques de código correctamente cerrados.

Los documentos quedan locales, sin commit. No se ejecutaron tests .NET ni la
puerta completa porque esta entrega solo contiene documentación; las pruebas
de implementación y criterios C1–C7 continúan pendientes.
