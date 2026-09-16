# Serilog y Seq: cierre correctivo de privacidad y trazabilidad

Fecha: 2026-09-15.
Estado: implementado y verificado (2026-09-16); las tareas y sus commits están
en el [plan](../plans/2026-09-15-serilog-seq-cierre-correctivo.md).
Base verificada: `develop`, `f0faf94`, árbol limpio al iniciar.
Plan: [tareas del cierre correctivo](../plans/2026-09-15-serilog-seq-cierre-correctivo.md).

## Objetivo

Cerrar los defectos encontrados al revisar la implementación anterior:
excepciones crudas de MVC, rutas concretas en eventos salientes e internos,
pérdida de contexto de tenant/rol y ausencia de prueba MVC → API con transporte
real. Completar además la prueba de desbordamiento de la consola asíncrona.

Este documento complementa la
[spec original](2026-09-15-serilog-seq-configuracion-design.md) y su
[plan implementado](../plans/2026-09-15-serilog-seq-configuracion.md).
En los puntos corregidos prevalece este cierre. Se conserva la configuración
declarativa, los eventos existentes, los campos seguros recuperados, la
consola como respaldo y el envío asíncrono nativo de Seq.

## Diagnóstico de partida

Rutas relativas a la raíz del repositorio:

| Hallazgo | Evidencia actual | Efecto |
|---|---|---|
| Excepción original vuelve al framework | `Icarus/src/Apps/Trajano.GestorCaisy/Observabilidad/ExcepcionesSegurasMiddleware.cs` registra y relanza; `Program.cs` la entrega a `UseExceptionHandler("/Sesion/Error")` sin suprimir sus diagnósticos. | El evento propio seguro no evita otro evento con excepción cruda. Warning permite los errores del framework. |
| Ruta concreta bajo nombre de patrón | `Observabilidad/CorrelacionApiHandler.cs`, método `Ruta`, devuelve `RequestUri.AbsolutePath`. | IDs y segmentos arbitrarios quedan en `RoutePattern` y en el mensaje. |
| Identidad solo en el resumen | `Icarus/src/Host/Icarus.Host/Observability/ContextoIdentidadObservabilidadMiddleware.cs` guarda `ClienteId` y `Rol` en `Items`, sin scope. | `operation.*`, persistencia y transacciones no heredan el contexto que antes recibían. |
| Saneamiento parcial de rutas | Los adaptadores `RegistroHttpSeguro` y el manejador de errores sombrean `RequestPath` únicamente al escribir sus eventos. | Los demás eventos pueden heredar el pathname de los scopes de ASP.NET. |
| Cobertura de traza insuficiente | `CorrelacionApiTests.cs` usa `FakeManejadorHttp`; la integración API solo prueba `traceparent` entrante. | No demuestra el vínculo entre dos servidores ni los spans de renovación/reintento. |
| Cola sin prueba de saturación | Hay Async 2.1.0 con buffer acotado y `blockWhenFull=false`, pero no evidencia de desbordamiento. | No se ha comprobado el descarte, su observación ni el desbloqueo/recuperación. |

Precisiones respecto del cierre anterior:

- `UseExceptionHandler` está activo en todos los entornos de MVC. El riesgo de
  su evento de excepción no se limita a Development. Una página de desarrollo
  exterior no implica por sí misma que una excepción consumida alcance el resumen.
- En ASP.NET Core 10, el manejo por ruta de error no obtiene la supresión
  predeterminada que sí recibe un `IExceptionHandler` que informa éxito.
  Hay además caminos específicos cuando la respuesta ya empezó o falla la
  propia página de error. Referencia: [implementación de .NET 10](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Middleware/Diagnostics/src/ExceptionHandler/ExceptionHandlerMiddlewareImpl.cs).
- Las suites verdes reportadas son evidencia histórica. No se ejecutaron de
  nuevo durante esta preparación y no sustituyen las regresiones de este cierre.

## Decisiones

### 1. Privacidad de todos los eventos del escenario

La unidad de comprobación es el conjunto completo de eventos serializados de
los hosts de prueba, incluyendo mensaje renderizado, propiedades, excepción,
scopes y fuentes del framework. No basta el evento `http.request.completed`.
No filtrar primero por `CorrelationId`: eso excluiría emisiones anteriores al
middleware y podría ocultar el defecto. Capturar por instancia de host/escenario
aislado y usar los IDs después para comprobar relaciones.

Usar canarios sintéticos distintos en pathname, query, headers, cookie, token,
cuerpo, mensaje de excepción e inner exception. Ninguno debe salir por los
sinks configurados. Tampoco registrar IP, UserAgent, identidad de trabajadores,
claims completos, SQL o payloads. Un ID técnico permitido por un descriptor
puede aparecer en su propiedad; eso no autoriza a registrar toda la ruta.

Mantener `backend.error` con referencia técnica, tipo de error y contexto
seguro. No sustituir el diagnóstico por silencio ni elevar globalmente los
niveles hasta que los tests dejen de encontrar eventos.

### 2. Excepciones MVC consumidas y diagnóstico seguro

Conservar la página `/Sesion/Error`, el estado HTTP y un resumen por petición
externa. Primer ajuste previsto: configurar explícitamente las opciones de
`UseExceptionHandler`, con `SuppressDiagnosticsCallback` para el manejo que
ya tiene diagnóstico propio seguro. Suprimir ese duplicado es política de
instrumentación HTTP, no configuración de sinks o niveles.

La supresión por callback no basta para todos los caminos. Probar respuesta
iniciada, fallo de la página de error y cancelación. En esos caminos el
framework o `UseSerilogRequestLogging` pueden recibir la excepción original.
Completar el manejo con un fallback terminal seguro y, donde resulte
necesario, filtros declarativos específicos por fuente/evento. Documentar las
categorías concretas y el evento seguro que conserva el diagnóstico.

No relanzar una excepción con datos hacia un logger no protegido ni cambiarla
por una excepción genérica que mantenga la original como `InnerException`.
No eliminar todos los eventos con nivel Error. No usar un enriquecedor que solo
borre una propiedad llamada `Exception`: `LogEvent.Exception` es otro campo.

Para respuestas completas, comprobar un `backend.error` y un resumen 500 con
el mismo `ErrorId`. Ante un fallo secundario al representar la página, reutilizar
la referencia del incidente; no sustituirla silenciosamente por otra.
Si la respuesta ya empezó, preservar la semántica de aborto y evitar escribir
otro cuerpo/estado ficticio. No tratar una desconexión del cliente como fallo
de negocio ni afirmar que recibió una respuesta completa.

Aplicar los mismos invariantes en Development, Testing y Production; las
fixtures no deben modificar la política de privacidad para hacer pasar un entorno.

### 3. Rutas salientes declaradas en el cliente HTTP

El cliente que construye una llamada conoce su plantilla. Añadir un contrato
local pequeño en `Trajano.GestorCaisy`, transportado por
`HttpRequestMessage.Options`, con el patrón técnico permitido. El handler de
correlación lo consume; no deriva patrones desde `AbsolutePath`.

- Pasar la plantilla al construir solicitudes JSON, multipart y renovación.
- Las plantillas provienen de constantes/código de la operación, nunca de
  datos del formulario, respuesta API o cabeceras entrantes.
- Ejemplo: `/api/pedidos-alimento/{pedidoId}`; un valor dinámico concreto se
  inserta solo en la URI de transporte, no en el metadato de observabilidad.
- Sin metadato seguro, usar `unmatched`. No aplicar como fallback una regex que
  solo sustituya UUID/números: los segmentos de texto también pueden contener datos.
- Conservar `DownstreamCorrelationId`, método, estado y duración. No añadir
  otro identificador global ni cambiar el comportamiento funcional del cliente.

Cubrir las emisiones automáticas de `System.Net.Http.HttpClient.*`: sus URI y
excepciones no se vuelven seguras por arreglar el evento propio. Si se excluyen
esas fuentes, mantener la señal segura de envío/fallo del handler. Los filtros
se declaran en JSON; añadir `Serilog.Expressions` solo si se usa esa vía.

### 4. Scopes de identidad y de ruta durante la ejecución

Mantener `Items` para el resumen externo y restaurar un scope alrededor de
`await siguiente` con `ClienteId` opaco y rol validado después de autenticación.
Disponerlo al salir, también ante excepciones. No confiar en que el scope siga
abierto cuando el middleware exterior registra el resumen.

Establecer el patrón de ruta seguro después de routing y antes de los eventos
de autenticación/negocio que se quieran contextualizar. En API y MVC, el scope
de ejecución debe sombrear `RequestPath` con el mismo valor seguro usado en
`RoutePattern`. En MVC, conservar la ruta original durante las reejecuciones.
Sin endpoint, usar un valor cerrado; nunca un pathname arbitrario.

El scope posterior a routing no puede sanear eventos que ya emitió routing o
hosting, ni sustituir una URI incrustada en otro mensaje. Revisar fuentes de
framework con canarios y filtrar las emisiones inseguras identificadas. No dar
por resuelto todo el pipeline porque la propiedad del resumen sea segura.

Comprobar en un flujo de tenant real que eventos de operación, decisión,
persistencia/transacción y resumen comparten contexto. Dos peticiones
concurrentes de tenants diferentes no pueden intercambiar scopes; una petición
anónima posterior no debe heredar tenant/rol. No añadir esos campos cuando no
existan, por ejemplo en una operación de cuenta global.

### 5. Dos hosts y transporte real para la prueba de traza

Crear la prueba en `Icarus.IntegrationTests`, referenciando el ensamblado MVC
solo desde ese proyecto de pruebas, con alias para evitar la colisión de los
dos tipos `Program`. No añadir ninguna referencia del backend al proyecto MVC
productivo ni un servicio compartido nuevo.

Levantar ambos entrypoints con `WebApplicationFactory` y Kestrel en puertos
dinámicos de loopback. En .NET 10 existe `UseKestrel(0)`, que debe invocarse
antes de iniciar la factory. Ver [fuente oficial](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Mvc/Mvc.Testing/src/WebApplicationFactory.cs).
Usar el SQL efímero de las fixtures y una base aislada para este escenario.

El cliente tipado MVC, `CorrelacionApiHandler` y el transporte HttpClient son
los productivos. No reemplazar `IApiIcarusClient` por `ApiIcarusFalsa`, ni
conectar los hosts con un handler en memoria. Los loggers son por host, con
captura completa y `preserveStaticLogger` cuando corresponda.

Verificar:

1. La petición MVC mantiene un `TraceId` durante toda la ejecución.
2. Cada envío API conserva ese TraceId con spans distintos de servidor/cliente
   y relación padre-hijo coherente; los reintentos no tienen que ser hijos unos
   de otros, pero sí pertenecer a la misma ejecución MVC.
3. Cada petición HTTP física tiene su UUID propio; el
   `DownstreamCorrelationId` del envío coincide con el `CorrelationId` API.
4. Un 401 → renovación → reintento exitoso tiene tres IDs salientes diferentes,
   misma traza y un resultado MVC único. Cubrir también renovación fallida y multipart.
5. Los IDs nativos de Activity/Serilog y la propiedad `TraceId` no discrepan.

Se permite provocar un único 401 mediante infraestructura exclusiva de tests,
pero el transporte y la propagación deben ser reales. No asignar manualmente
el mismo TraceId a ambos hosts ni inyectar `traceparent` en cada salto desde la
prueba: eso comprobaría el montaje del test, no la aplicación. Puede iniciarse
una actividad raíz o enviarse un traceparent solo en la petición externa.

No añadir OpenTelemetry por rutina: primero comprobar el comportamiento nativo.
Si un listener de prueba habilita Activities, documentar su efecto y verificar
también la ruta con la configuración productiva sin instrumentación extra.

### 6. Desbordamiento observable de consola, sin bloquear

Conservar Async con cola acotada y `blockWhenFull=false`. La prueba carga la
configuración real y sustituye únicamente el destino consola por un sink
controlable, con la cola reducida mediante override de prueba.

Bloquear el consumidor con una señal, llenar la cola, provocar descartes,
comprobar que el productor termina antes de liberar la señal y observar el
contador mediante `IAsyncLogEventSinkMonitor`/`IAsyncLogEventSinkInspector`.
Liberar el consumidor en `finally`, comprobar que se drena y acepta un evento
posterior. Evitar esperas basadas solo en milisegundos y sleeps arbitrarios.

Exponer el conteo de descartes de forma genérica e independiente de esa cola;
una métrica estándar de .NET es suficiente, sin endpoint público, dashboard
ni servicio remoto nuevo. No emitir el aviso por la misma cola saturada ni
volcar `SelfLog` crudo. Referencia: [monitorización de Async](https://github.com/serilog/serilog-sinks-async).

Probar que el sink Seq sigue siendo independiente del consumidor de consola
bloqueado. Esta prueba no demuestra la capacidad ni el descarte de la cola
interna de Seq: documentar esa diferencia. No se modifica su batching ni se
envuelve Seq en Async.

## Criterios de aceptación

| ID | Evidencia requerida para cerrar |
|---|---|
| C1 | Error MVC en los tres entornos: evento seguro y resumen correlacionados, sin excepción original en ningún sink/fuente. Caminos de fallo secundario y respuesta iniciada cubiertos. |
| C2 | JSON, multipart, refresh, reintento y fallo de red registran plantillas o `unmatched`; ausencia de canarios en toda la salida, incluidos HttpClient/hosting. |
| C3 | Tenant/rol correcto en eventos internos y resumen, sin fuga entre peticiones concurrentes o anónimas. |
| C4 | Ningún evento del escenario conserva pathname concreto; rutas de resumen, ejecución y reejecución son seguras. |
| C5 | Dos hosts reales: traza común, spans relacionados, UUID por envío, refresh/reintento/multipart comprobados y sin cliente falso en el recorrido. |
| C6 | Consola saturada: cola acotada, productor no bloqueado, descartes observables, recuperación y Seq independiente. |
| C7 | Documentación coherente con resultados, puerta completa verde y estado de cada tarea respaldado por evidencia. |

## Fuera de alcance

Cambiar reglas de negocio, roles, autenticación funcional o UI; añadir auditoría
nominal; instrumentar todos los handlers; relajar gates; modificar el stack
local o producción; incorporar almacenamiento durable, OpenTelemetry, alertas
externas o dashboards. No se promete supervivencia de eventos ante terminación
abrupta ni entrega exactamente una vez. Seq con autenticación real puede quedar
como prueba posterior: el rechazo 401 simulado se describe expresamente como tal.

## Entrega a la siguiente sesión

El [plan](../plans/2026-09-15-serilog-seq-cierre-correctivo.md) contiene rutas,
dependencias, pruebas y texto de arranque para otro agente. El handoff general
existente corresponde a otro bloque anterior: verificar su fecha y usar este
par de documentos como referencia específica, sin borrar trabajo ajeno.
