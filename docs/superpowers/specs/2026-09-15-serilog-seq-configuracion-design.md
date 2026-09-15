# Serilog y Seq: diagnóstico y propuesta de mejora

Fecha: 2026-09-15.
Estado: **implementado** en `develop`; ver
[Estado de implementación del plan](../plans/2026-09-15-serilog-seq-configuracion.md#estado-de-implementación).
Base inspeccionada: `develop`, commit `8ad9664`.

## Objetivo y alcance de esta sesión

Entender si los logs permiten reconstruir una ejecución y explicar resultados
incorrectos. Entregables: diagnóstico, comparación con el prompt, configuración
de referencia y [plan de implementación](../plans/2026-09-15-serilog-seq-configuracion.md).
Este documento se conserva como diseño de referencia; la implementación y sus
correcciones se registran en el plan.

Se conservan los contratos del
[registro de vuelo](2026-08-17-registro-vuelo-seq-design.md) y de
[incidentes](2026-08-16-observabilidad-incidentes-frontend-backend-design.md).
Cambiar sinks y enrichers por sí solo no aporta las decisiones de negocio que
faltan en una narración.

## Evidencia y diferencias frente al prompt

Rutas relativas a la raíz del repositorio:

| Área | Implementación verificada | Mejora propuesta |
|---|---|---|
| Configuración API | `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ObservabilityExtensions.cs`: usa `ReadFrom.Configuration`, pero añade consola, Seq y propiedades en C#. La URL y clave ya son externas (`Seq:Url`, `Seq:ApiKey`); no están hardcodeadas. | Declarar sinks, filtros, niveles y enrichers en `Serilog`; migrar también las variables de despliegue. |
| Niveles API | `Icarus/src/Host/Icarus.Host/appsettings.json` solo contiene `Logging`, sin sección `Serilog`. | Overrides efectivos dentro de `Serilog:MinimumLevel`, manteniendo eventos propios en Information. |
| GestorCaisy | `Icarus/src/Apps/Trajano.GestorCaisy/Program.cs` duplica la configuración. Su JSON de desarrollo eleva el nivel global a Warning y no configura Seq. | Information para los flujos propios en todos los entornos y Seq local declarativo. |
| Contexto global | Ya hay `FromLogContext`, `Aplicacion`, `Entorno`; la API también tiene `Release` saneada. Faltan máquina e hilo. | Conservar propiedades existentes; añadir `EnvironmentName`, `MachineName` y `ThreadId`. No renombrar `Aplicacion` a `Application` sin necesidad. |
| HTTP API | `RequestObservabilityMiddleware` emite un resumen propio con patrón de ruta. Está después de autenticación y `ClienteActivoMiddleware`. | Cubrir también los rechazos tempranos y errores anteriores al endpoint; un solo resumen final seguro. |
| HTTP MVC | `UseSerilogRequestLogging()` con valores predeterminados, dentro de los middlewares que reejecutan la página de error. | Evitar ruta concreta, excepción cruda y resúmenes duplicados por reejecución. |
| Correlación API | UUID validado, generado si falta, devuelto y propagado por `LogContext`. PWA genera un ID por llamada. | Preservar el contrato y probar las propiedades del evento real, además de las cabeceras. |
| Correlación MVC → API | `Servicios/ApiIcarusClient.cs` no añade `X-Correlation-ID`; MVC no instala middleware equivalente. | ID por salto HTTP y `TraceId` W3C compartido para toda la ejecución, incluidos renovación y reintento. |
| Envío | Seq 9.0.0, sin buffer durable configurado. Consola síncrona en ambos procesos. | Mantener batching asíncrono de Seq; envolver solo consola con Async y cola acotada. |
| Narración | Behavior MediatR, decisiones e interceptores EF en Identity, Clientes y Gestión Avícola. | Corregir pérdida de campos y comprobar un recorrido completo; no instrumentar todo el dominio indiscriminadamente. |

### Prueba real de ingestión

El 2026-09-15 los contenedores locales de API, MVC y Seq estaban saludables.
Se hizo únicamente un `GET http://localhost:8085/api/health`, sin credenciales
ni datos de negocio, con un UUID sintético:

- HTTP 200; la respuesta conservó el `CorrelationId`.
- Consulta de lectura a `/api/events` de Seq: cinco eventos con esa correlación.
- Uno era `http.request.completed`; cuatro procedían de routing y del resultado
  HTTP de ASP.NET, todos en Information.
- El evento propio tenía `TraceId` coincidente con `X-Trace-Id` y también
  `RequestPath`, heredado del scope del framework.

Identificadores reproducibles dentro de la retención local:
`CorrelationId = '80072378-1411-4c76-a54c-7cb2916c8004'` y
`TraceId = '13ce117c81e6165186e02a2aa808eb3d'`.

Conclusión acotada: la API local sí entrega eventos a Seq, pero hay ruido y
propiedades adicionales a las declaradas por el middleware. Este sondeo no
demuestra la ingestión de MVC, el funcionamiento de una VPS, la ausencia de
PII en todo el sistema ni el comportamiento ante caída de Seq. Tampoco verifica
que la imagen local corresponda exactamente al commit del árbol inspeccionado.

### Hallazgos prioritarios

1. **Los overrides de la API no gobiernan Serilog.** La sección `Logging` no
   sustituye `Serilog:MinimumLevel`. El sondeo confirma ruido real del framework.
2. **Hay peticiones sin el resumen propio.** `ClienteActivoMiddleware` devuelve
   401 antes de entrar en `RequestObservabilityMiddleware`; sus fallos tampoco
   pasan por el manejador seguro de excepciones situado después.
3. **El patrón de ruta no garantiza privacidad por sí solo.** `FromLogContext`
   importa scopes con `RequestPath`. MVC registra la ruta concreta por defecto.
   Bajar Microsoft a Warning tampoco elimina posibles SQL, URI o excepciones
   con datos en eventos de EF/HttpClient. La prueba anti-PII debe inspeccionar el
   evento serializado completo, incluidos mensaje, excepción y scopes.
4. **Las decisiones pierden campos útiles.** En `RegistroVuelo.cs`, el método
   público `Decidir(string, ...)` llama a `Escribir` sin descriptor. Este omite
   todos los campos cuando el descriptor es nulo. Por ejemplo, `Lineas` en
   `CrearPedidoAlimentoHandler` y `GallinasVivas` en mortalidad se descartan.
   La variante `IOperacionVuelo.Decidir` sí usa descriptor. Las compensaciones
   también pasan `CompensationKind` sin descriptor y pierden esa propiedad.
5. **La narración no siempre explica el resultado.** El formatter del vuelo
   devuelve solo `EventName`; los rechazos suelen tener códigos generales.
   Hay retornos por idempotencia sin decisión específica. Un HTTP 200 no permite
   distinguir por sí solo una escritura nueva de un reintento ya aplicado.
6. **La guía tiene consultas desalineadas.** Usa `EventType` en errores y
   `RequestPath` para patrones; el contrato propio es `EventName` y `RoutePattern`.

## Diseño recomendado

### 1. Configuración declarativa y compatibilidad

Toda selección de sinks, endpoints, claves, niveles, filtros y enrichers vive
en `appsettings*.json` y sus overrides de configuración. Los secretos se
inyectan por entorno/secret store, nunca se guardan en JSON versionado.
La lógica dinámica de privacidad y correlación continúa en C#: la exigencia
de configuración declarativa no convierte el procesamiento de una petición
en configuración estática.

Mantener `Aplicacion`, `Entorno` y `Release`: ya son contrato de consulta.
Añadir los enrichers solicitados; `ThreadId` describe el hilo del evento, no
identifica una petición, porque un `await` puede continuar en otro hilo.
Conservar el saneamiento de `ReleaseDiagnostico`; migrar su entrada a
`Serilog:Properties:Release` sin perder el contrato de 1–40 caracteres seguros.
El enriquecedor de metadatos seguro debe resolver `Entorno` desde el entorno
real del host y normalizar `Release`, y declararse también desde JSON.

El siguiente es el **archivo base completo propuesto para la API**, preservando
las opciones funcionales actuales. Es una referencia de diseño: requiere los
paquetes y el enriquecedor `WithMetadatosSeguros` previstos en el plan; no se ha
instalado ni probado como configuración de la aplicación.

```json
{
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console",
      "Serilog.Sinks.Async",
      "Serilog.Sinks.Seq",
      "Serilog.Enrichers.Environment",
      "Serilog.Enrichers.Thread",
      "Icarus.BuildingBlocks.Observability"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    },
    "Enrich": [
      "FromLogContext",
      "WithEnvironmentName",
      "WithMachineName",
      "WithThreadId",
      "WithMetadatosSeguros"
    ],
    "Properties": {
      "Aplicacion": "Icarus",
      "Release": "development"
    },
    "WriteTo": {
      "Consola": {
        "Name": "Async",
        "Args": {
          "bufferSize": 10000,
          "blockWhenFull": false,
          "configure": [
            {
              "Name": "Console",
              "Args": {
                "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
              }
            }
          ]
        }
      }
    }
  },
  "AllowedHosts": "*",
  "PedidosAlimento": {
    "MaximoPorSemana": 3
  },
  "AlmacenDocumentosPedido": {
    "MaxTamanoBytes": 524288,
    "MaxDimensionesPixeles": 8000
  }
}
```

El endurecimiento de propiedades heredadas y filtros de categorías con datos
crudos se completa en la tarea HTTP/anti-PII antes de considerar esta base apta
para producción. No se presenta este JSON como solución suficiente de privacidad.

MVC usa la misma sección con `Aplicacion = Trajano.GestorCaisy`, la extensión
local equivalente en `Using` y conserva `ApiIcarus:BaseUrl`. No debe referenciar
el building block actual: arrastra Application, Domain y EF del backend.
Dos adaptadores pequeños locales son preferibles a ampliar la arquitectura
para compartir unas pocas líneas.

En desarrollo se añade esta sección al JSON específico de cada aplicación,
conservando las demás opciones de ese archivo:

```json
{
  "Serilog": {
    "MinimumLevel": { "Default": "Information" },
    "WriteTo": {
      "Seq": {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341",
          "batchPostingLimit": 1000,
          "period": "00:00:02",
          "queueSizeLimit": 10000
        }
      }
    }
  }
}
```

Para contenedores, también en Production, usar una entrada nombrada completa:

```text
Serilog__WriteTo__Seq__Name=Seq
Serilog__WriteTo__Seq__Args__serverUrl=http://seq:80
Serilog__WriteTo__Seq__Args__apiKey=<secreto de la aplicación>
Serilog__WriteTo__Seq__Args__batchPostingLimit=1000
Serilog__WriteTo__Seq__Args__period=00:00:02
Serilog__WriteTo__Seq__Args__queueSizeLimit=10000
Serilog__Properties__Release=<versión del despliegue>
```

`Name` es necesario si el sink no existe en el JSON base; una URL aislada no
debe asumirse suficiente. Las entradas nombradas evitan depender de índices.
Verificar la composición base + entorno + variables: los arrays/objetos de
configuración se combinan por claves; una colección vacía no borra por sí sola
sinks heredados. Migrar `Seq__*` en ambos compose, sus tests y la guía juntos.

### 2. Un resumen HTTP seguro, con cobertura completa

Adoptar `UseSerilogRequestLogging` en ambos hosts solo con adaptación y pruebas
del contrato actual. La API ya tiene una función equivalente: instalar el
middleware estándar además del resumen propio produciría duplicados.

- Separar el establecimiento del contexto del evento de finalización.
- Orden previsto: forwarded headers confiables → correlación/trace → resumen
  HTTP → manejo seguro de excepciones → routing → autenticación → scope de
  tenant/rol → cliente activo → límites/autorización → endpoint.
- En MVC el resumen debe envolver las reejecuciones de error para contar una
  vez la petición externa y conservar el endpoint original.
- Guardar `TraceId`/`CorrelationId` durante toda la petición; capturar tenant/rol
  solo después de autenticar y desde claims validados. No capturarlos antes de
  que existan ni confiar en que un scope interno sobreviva al resumen externo.
- Conservar `EventName`, `Method`, `RoutePattern`, `StatusCode`, `DurationMs`,
  `ErrorId` y los IDs existentes. Ruta no resuelta: `unmatched`; estáticos: un
  valor cerrado. Nunca copiar el pathname recibido como fallback.
- Usar `GetMessageTemplateProperties` para reemplazar las propiedades
  predeterminadas y `EnrichDiagnosticContext` para añadir contexto seguro.
  Cambiar solo `MessageTemplate` no elimina `RequestPath`.
- Eliminar también rutas concretas heredadas de scopes. Revisar categorías
  EF/HttpClient/hosting que escriben SQL, URI y excepciones crudas, sin asumir
  que Warning equivale a saneamiento. Suprimir las emisiones inseguras y
  conservar diagnóstico propio genérico equivalente.
- El manejador de excepciones debe consumir el error antes del resumen: el
  middleware estándar puede adjuntar la excepción original si escapa. En MVC
  se necesita diagnóstico seguro equivalente, sin mensajes arbitrarios.
- Resumen en Information para respuestas esperadas, Error para 5xx; evitar
  registrar cada 404 o sondeo como incidente de negocio. Los niveles de eventos
  de la taxonomía son semántica de instrumentación, no niveles globales en C#.

No registrar `ClientIP` ni `UserAgent` crudos. `Host` tampoco es confiable con
`AllowedHosts = *` y forwarded headers abiertos: omitirlo inicialmente. Solo
añadir un host normalizado de una lista de despliegue si aporta diagnóstico.
Estos datos no son necesarios para reconstruir el flujo con IDs técnicos.

Bloque objetivo de arranque de Serilog, común a ambos adaptadores:

```csharp
builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));
```

Esquema del tramo HTTP de la API, **pseudocódigo de integración**, no un
`Program.cs` sustituto listo para copiar (los adaptadores nombrados son futuros):

```csharp
app.UseForwardedHeaders(); // Conservar y revisar las opciones reales del proxy.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ContextoTrazaMiddleware>();
app.UseSerilogRequestLogging(RegistroHttpSeguro.Configurar);
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<ContextoIdentidadObservabilidadMiddleware>();
app.UseMiddleware<ClienteActivoMiddleware>();
// Conservar después los límites de cuerpo, rate limiter, autorización y endpoints.
```

### 3. Correlación entre aplicaciones

Preservar el significado vigente: `CorrelationId` identifica una petición
HTTP física. Un header entrante válido se acepta; valores arbitrarios se
sustituyen. No es una identidad autenticada ni una garantía de unicidad global.

MVC genera/conserva su ID de entrada. Cada envío real de `HttpClient`, incluido
refresh y reintento, crea un UUID propio en un `DelegatingHandler`. Un evento
seguro del envío, bajo el contexto MVC, guarda ese valor como
`DownstreamCorrelationId`, método, operación/ruta patrón, estado y duración.
No registrar URL completa, query, cookie, token ni cuerpo.

El `TraceId` W3C es el vínculo de extremo a extremo; verificar la propagación
automática ASP.NET → HttpClient → API y los spans en pruebas. No afirmar que
está ausente solo porque no haya código manual de `traceparent`. No introducir
otro identificador global ni reutilizar el mismo UUID para todos los reintentos.

### 4. Recuperar información útil del vuelo

Corregir el contrato de decisiones públicas para exigir un descriptor explícito
tipado, o utilizar el ámbito existente con su descriptor. Recomendación:
descriptor explícito para llamadas independientes; evita un registro global
mutable y ambigüedad en operaciones anidadas. Migrar sus consumidores y dobles.
La ausencia de descriptor nunca autoriza campos libres.

Probar que sobreviven los contadores permitidos y `CompensationKind`, mientras
siguen excluidos campos desconocidos, de tipo incorrecto y personales.
No ampliar listas permitidas para admitir solicitudes/entidades completas.

Mantener nombres de eventos y agregar frases estables en español que incluyan
operación, decisión y resultado. Alinear resultados de decisiones actualmente
como `aplicada` con el contrato documentado; distinguir decisión tomada de
persistencia confirmada. No inferir éxito definitivo de un evento anterior al
`SaveChanges`/commit.

Validación representativa: alta con compensación existente y un flujo de
pedidos de alimento. Para idempotencia, demostrar con un caso de recogida o
mortalidad si hubo escritura nueva o respuesta reutilizada. Ampliaciones a más
reglas se priorizan después; esta spec no autoriza logging masivo.

### 5. Rendimiento y límites de entrega

Seq ya envía lotes en segundo plano. No envolverlo en `Serilog.Sinks.Async`.
Conservar consola JSON como respaldo del contrato vigente, envuelta en Async
con cola acotada y `blockWhenFull = false`; supervisar descartes. Esto mantiene
la opción del prompt de evitar I/O de consola en el hilo de petición.

No prometer entrega garantizada: una cola llena, caída prolongada o terminación
abrupta pueden perder eventos. El respaldo de consola no se reenvía por sí solo
a Seq. Probar recuperación tras indisponibilidad y vaciado en cierre ordenado,
y comprobar que fallos del logger no alteren el resultado funcional.

Diagnosticar fallos de ingestión mediante contadores/estado genéricos de un
canal independiente; nunca redirigir `SelfLog` al mismo logger ni volcar sin
saneamiento texto que pudiera contener endpoints con credenciales o payloads.
Buffer durable en volumen queda como mejora posterior si se exige recuperar
eventos tras reiniciar; necesita límites de disco, permisos y retención propios.

## Paquetes

Versiones actuales del CPM: Serilog 4.3.0, AspNetCore 10.0.0, Seq 9.0.0,
Console 6.1.1 y Formatting.Compact 3.0.0. No se propone actualizarlos por rutina.

- Añadir `Serilog.Enrichers.Environment` y `Serilog.Enrichers.Thread`.
- Añadir `Serilog.Sinks.Async` para consola.
- `Serilog.Settings.Configuration` ya llega transitivamente por AspNetCore;
  considerar referencia directa para hacer explícito el contrato utilizado.
- Si se eligen filtros por expresiones JSON, añadir `Serilog.Expressions`;
  no incorporarlo antes de concretar las categorías y expresiones necesarias.
- Fijar versiones compatibles con .NET 10 en `Icarus/Directory.Packages.props`
  durante la implementación y verificar restauración; no usar versiones flotantes.

## Criterios de aceptación de la futura implementación

1. Configuración real de ambos hosts compuesta por entorno: sin sinks duplicados,
   sin secretos versionados, Information propio y overrides efectivos.
2. Un resumen por petición que termina en respuesta HTTP: 2xx, 400, 401 por
   cliente inactivo, 403, 404, 409, 429 y 500; MVC sin duplicar por reejecución.
   Desconexión/cancelación tiene prueba específica; no inventar un estado HTTP
   recibido por un cliente que ya se desconectó.
3. Captura Serilog real serializada sin canarios sintéticos de PII, URL/query,
   cuerpos o excepciones crudas, incluyendo scopes y proveedores del framework.
4. Correlación comprobada en API/PWA y MVC → API, incluidos refresh/reintento,
   sin pérdida de tenant/rol permitido en el resumen externo.
5. Campos permitidos y compensación visibles; campos prohibidos siguen excluidos.
   Lectura cronológica distingue decisión, persistencia y resultado final.
6. Evidencia de ingestión de ambas aplicaciones en Seq aislado y de continuidad
   funcional ante caída; límites y pérdidas posibles documentados.
7. Guía operativa usa `EventName`/`RoutePattern` y búsquedas por TraceId,
   CorrelationId, SessionId, operación, motivo y versión; puerta completa verde.

## Fuera de alcance

Implementar en esta sesión; modificar reglas de negocio, datos existentes,
autenticación o permisos; desplegar/reiniciar el stack actual; consultar
registros nominales; añadir OpenTelemetry, dashboards, alertas o almacenamiento
durable sin una tarea posterior concreta. No evaluar cada handler del dominio.

## Referencias primarias consultadas

- [Configuración JSON de Serilog](https://github.com/serilog/serilog-settings-configuration): sección Serilog, composición y entradas nombradas.
- [Integración ASP.NET Core](https://github.com/serilog/serilog-aspnetcore): resumen HTTP y orden del middleware.
- [Implementación del middleware 10.0.0](https://github.com/serilog/serilog-aspnetcore/blob/v10.0.0/src/Serilog.AspNetCore/AspNetCore/RequestLoggingMiddleware.cs): propiedades predeterminadas, excepción y diagnóstico.
- [Sink de Seq](https://github.com/datalust/serilog-sinks-seq): configuración y opciones de transporte.
- [Sink Async](https://github.com/serilog/serilog-sinks-async): consola fuera del hilo de petición, cola y descartes; Seq ya tiene batching.
- [Enrichers de entorno](https://github.com/serilog/serilog-enrichers-environment) y [de hilo](https://github.com/serilog/serilog-enrichers-thread).
