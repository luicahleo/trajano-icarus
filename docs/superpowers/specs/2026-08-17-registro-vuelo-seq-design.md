# Registro de vuelo narrativo en Seq — diseño

Fecha: 2026-08-17
Estado: aprobado el 2026-08-17

## Objetivo

Permitir reconstruir en Seq el recorrido técnico de una operación de Icarus:
entrada, validaciones relevantes, decisiones que cambian el flujo, escrituras,
transacciones, compensaciones, errores y resultado. La lectura cronológica por
`TraceId` debe contar una historia comprensible sin volver al volumen, la
duplicación entre capas ni la exposición de datos sensibles del ICARUS
anterior.

Este diseño extiende
`2026-08-16-observabilidad-incidentes-frontend-backend-design.md`. Conserva sus
identificadores, eventos de incidente, reglas anti-PII, consola JSON y Seq; no
los sustituye ni redefine.

## Estado de partida verificado

Icarus ya tiene:

- `ILogger` respaldado por Serilog, consola en JSON compacto y sink opcional de
  Seq;
- propiedades globales `Aplicacion`, `Entorno` y `Release`;
- un `TraceId` por ejecución backend, además de `CorrelationId`, `SessionId` y
  contexto seguro de la petición;
- un único `http.request.completed` por petición;
- `backend.error` y `backend.business_warning` en el manejador global de
  excepciones;
- algunos rechazos estructurados de Identity;
- dos contextos EF Core que implementan `IUnitOfWork`, pero no instrumentación
  transversal de `SaveChanges` ni de transacciones;
- un único behavior transversal de MediatR para validación;
- una alta de cliente con cuenta que comprueba disponibilidad, escribe en
  Clientes, intenta escribir en Identity y suspende lógicamente al cliente si
  falla la segunda escritura.

El navegador envía `X-Correlation-ID` y `X-Session-Id`, y consume
`X-Trace-Id`; no envía `traceparent`. Por tanto, en esta versión el `TraceId` lo
origina ASP.NET y reúne toda la ejecución backend de una petición.

El ICARUS anterior sirve solo como referencia narrativa. Su configuración raíz
usa nivel `DEBUG` y el código contiene miles de llamadas de logging, mensajes
interpolados con identificadores y repetición de pasos internos. No se copiarán
su API, su granularidad ni sus datos.

## Principios

1. **Una operación, una narración.** Los límites comunes se automatizan y el
   código de negocio solo registra decisiones que modifican el recorrido.
2. **Estructura para buscar, texto para leer.** Cada evento tiene propiedades
   estables y una frase breve en español que explica qué ocurrió.
3. **Correlación existente.** `TraceId` es la clave primaria para reconstruir
   una petición; no se introduce otro identificador de vuelo en esta versión.
4. **Fallo cerrado para datos.** Solo se aceptan propiedades declaradas por la
   operación. Una propiedad desconocida o prohibida se omite sin afectar el
   flujo funcional.
5. **Una responsabilidad por evento.** El registro de operación describe el
   recorrido; `backend.error` conserva el diagnóstico de la excepción;
   `http.request.completed` resume el transporte HTTP.
6. **El mecanismo es transversal.** Clientes/Trabajadores es el piloto, no el
   propietario de la taxonomía ni de la API.

## Alcance de la primera versión

La primera versión construye el mecanismo reutilizable y lo demuestra en el
alta de cliente con cuenta. Incluye:

- contrato y taxonomía compartidos;
- ciclo automático de operaciones de escritura en MediatR;
- API tipada para decisiones y compensaciones explícitas;
- instrumentación común de `SaveChanges` y transacciones EF Core;
- eventos narrativos del alta de cliente con cuenta, incluida su compensación;
- pruebas de estructura, correlación, secuencia y ausencia de datos prohibidos;
- consultas operativas de Seq para reconstruir el vuelo.

Las demás operaciones existentes se incorporarán de forma incremental. Crear
la infraestructura no autoriza a llenar todos los handlers de logs.

## Modelo del vuelo

### Operación registrable

Una operación registrable declara un nombre estable de dominio técnico, por
ejemplo `clientes.alta_con_cuenta` o `clientes.crear`. El nombre no se deriva
del nombre de clase para que sobreviva a refactors.

En la primera versión son registrables las mutaciones seleccionadas. Las
queries no se registran automáticamente: su inicio y fin duplicaría
`http.request.completed` sin aportar una decisión de negocio. Una query futura
podrá optar explícitamente por el contrato si existe un caso operativo real.

El behavior de MediatR envuelve validación y handler, de modo que puede narrar:

1. `operation.started` antes de validar;
2. `operation.completed` con resultado `succeeded` al finalizar;
3. `operation.rejected` con un código seguro ante una excepción esperada;
4. `operation.failed` ante un error inesperado, y vuelve a propagar la
   excepción.

La validación sigue siendo la autoridad. El registro nunca captura el request,
la response ni los mensajes de validación. Solo puede emitir un código general,
como `validation_failed`, y el número de errores.

El manejador global mantiene la propiedad de `backend.error`, stack trace y
`ErrorId`. `operation.failed` no vuelve a registrar la excepción ni el stack:
indica qué operación quedó incompleta. Son eventos relacionados, no dos copias
del mismo diagnóstico.

### Operación compuesta

Una orquestación que atraviesa módulos, como el alta con cuenta, abre una
operación raíz explícita. Las operaciones MediatR internas conservan sus
propios nombres y comparten el mismo `TraceId`. La cronología permite ver la
operación raíz y sus pasos sin introducir acoplamiento entre Clientes e
Identity.

La finalización del ámbito debe ser explícita: si el código sale sin marcar un
resultado, el registro lo trata como fallo. Esto evita vuelos aparentemente
exitosos por olvidar un evento final.

## Contrato estructurado

Todos los eventos del registro de vuelo incluyen, cuando corresponda:

| Propiedad | Significado |
|---|---|
| `EventName` | Nombre estable del tipo de evento |
| `Operation` | Nombre estable de la operación |
| `Phase` | Fase normalizada: `start`, `decision`, `persistence`, `transaction`, `compensation` o `end` |
| `Outcome` | Resultado cerrado: `succeeded`, `rejected`, `failed`, `committed`, `rolled_back` o `compensated` |
| `ReasonCode` | Código estable y no personal que explica una decisión o rechazo |
| `DurationMs` | Duración del límite automático o paso técnico |
| `PersistenceContext` | Contexto lógico, inicialmente `Clientes` o `Identity` |
| `RowsAffected` | Filas informadas por `SaveChanges` |
| `CompensationKind` | Tipo cerrado, inicialmente `logical` |

`TraceId`, `CorrelationId`, `SessionId`, `ClienteId`, `Rol`, `Aplicacion`,
`Entorno` y `Release` llegan por los scopes y enrichers existentes; no se
vuelven a pasar manualmente en cada llamada.

Los nombres de propiedades se mantienen en inglés donde ya forman parte del
contrato de observabilidad. Los nombres de operaciones, códigos y mensajes son
estables, breves y no dependen de textos de excepción.

## Taxonomía de eventos

| `EventName` | Nivel | Responsabilidad |
|---|---|---|
| `operation.started` | Information | Entró una operación registrable |
| `operation.decision` | Information | Una decisión material eligió el siguiente recorrido |
| `operation.completed` | Information | La operación terminó correctamente |
| `operation.rejected` | Warning | Una regla esperada impidió completar la operación |
| `operation.failed` | Error | La operación terminó por un error inesperado, sin duplicar excepción |
| `persistence.save_changes.completed` | Information | `SaveChanges` terminó e informa duración y filas |
| `persistence.save_changes.failed` | Error | `SaveChanges` falló, sin registrar entidades ni valores |
| `transaction.committed` | Information | Una transacción física confirmó sus cambios |
| `transaction.rolled_back` | Warning | Una transacción física revirtió sus cambios |
| `operation.compensation.started` | Warning | Comenzó una compensación lógica |
| `operation.compensation.completed` | Warning | La compensación lógica terminó |
| `operation.compensation.failed` | Error | La compensación lógica no pudo completarse |

No se crea un evento para cada método, constructor, consulta de repositorio o
cambio de estado interno. Los mensajes narrativos son plantillas fijas; por
ejemplo: «Alta de cliente con cuenta: Identity rechazó la cuenta; se inicia la
compensación lógica». Los valores variables viven en propiedades estructuradas,
nunca interpolados dentro de texto libre.

## API y responsabilidades

El contrato de aplicación ofrece tipos cerrados para:

- declarar una operación registrable y su nombre estable;
- registrar una decisión mediante `ReasonCode`, resultado y contexto permitido;
- abrir y finalizar una operación compuesta;
- abrir y finalizar una compensación lógica.

La implementación usa `ILogger`; Serilog continúa siendo el proveedor y Seq el
sink. El código de aplicación no depende directamente de la API estática de
Serilog ni conoce la URL de Seq.

El building block de observabilidad es responsable de:

- implementar la escritura narrativa estructurada;
- ejecutar el behavior de MediatR;
- aplicar la política de campos permitidos;
- instrumentar EF Core de forma reutilizable;
- enriquecer todos los eventos con el scope actual.

Cada módulo declara nombres de operación, códigos de decisión y campos seguros
propios, pero no redefine eventos, niveles ni formato. El Host compone los
registros de todos los módulos.

## Campos permitidos y anti-PII

Cada operación tiene una lista explícita de propiedades adicionales. La lista
define nombre y tipo; no basta con que el llamador entregue un diccionario. La
política transversal también mantiene nombres prohibidos que ningún módulo
puede habilitar.

Se permiten, cuando la operación los declara:

- booleanos de presencia o disponibilidad;
- cantidades, duraciones y filas afectadas;
- categorías, estados, roles y códigos de error cerrados;
- identificadores técnicos u opacos que el spec base ya permite;
- valores de negocio no personales expresamente revisados.

Se prohíben siempre:

- contraseñas, hashes de contraseña, tokens, cookies, secretos y credenciales;
- biometría o derivados biométricos;
- correo, NIT, documentos, nombres, teléfonos y direcciones;
- `TrabajadorId` o cualquier dato que permita reconstruir actividad nominal de
  acceso;
- cuerpos de request/response, entidades serializadas y valores de formularios;
- mensajes de excepción o validación que puedan contener entradas del usuario.

Cuando haga falta correlacionar un valor protegido entre eventos, se usará una
categoría, presencia, longitud o código. Un fingerprint solo será admisible si
lo genera un servicio central con un digest con clave y propósito específico;
un hash directo de datos de baja entropía no se considera irreversible.

Una propiedad fuera de la lista se descarta y produce, como máximo, un aviso
técnico con el nombre de la propiedad y la operación, nunca con su valor. El
fallo del propio registro tampoco interrumpe la operación funcional.

## Persistencia, transacciones y compensaciones

Un interceptor común de EF Core registra todos los `SaveChanges` de los
contextos incorporados:

- al completar: contexto lógico, filas, duración y resultado;
- al fallar: contexto lógico y duración, sin estado del change tracker,
  sentencias SQL, entidades ni valores.

La instrumentación de transacciones registra confirmación y rollback físicos.
No interpreta un rollback como compensación. Una compensación es otra operación
de negocio que revierte o neutraliza un efecto ya confirmado y se registra con
los eventos `operation.compensation.*` y `CompensationKind=logical`.

Las migraciones y semillas ejecutadas al arrancar no forman parte del vuelo de
una petición. Pueden conservar logs técnicos propios, pero no inventan un
`TraceId` de negocio ni nombres de operación.

## Piloto: alta de cliente con cuenta

El vuelo esperado de éxito es:

1. comienza `clientes.alta_con_cuenta`;
2. se registra la decisión `account_identifier_available` sin correo ni valor
   consultado;
3. `clientes.crear` valida la unicidad fiscal sin registrar NIT;
4. Clientes completa `SaveChanges` y, si EF abrió una transacción física,
   registra su confirmación;
5. Identity crea la cuenta sin registrar correo, contraseña, usuario ni IDs
   personales;
6. Identity completa `SaveChanges` y, si EF abrió una transacción física,
   registra su confirmación;
7. finaliza `clientes.alta_con_cuenta` con `succeeded`.

Si Identity rechaza la cuenta después de crear el cliente:

1. se registra la decisión con un `ReasonCode` seguro de la lista permitida;
2. comienza la compensación lógica `clientes.suspender_alta_incompleta`;
3. la suspensión completa su `SaveChanges` y registra la transacción física si
   EF necesitó abrirla;
4. la compensación termina con `compensated`;
5. la operación raíz termina como `rejected`.

Si la compensación falla, se registra `operation.compensation.failed`; la
operación raíz queda `failed` y el manejador global conserva la excepción como
`backend.error`. No se oculta el fallo original ni se afirma que hubo rollback:
la primera escritura ya había sido confirmada.

El piloto no registra el `clienteId` recién creado, correo, NIT, razón social,
contraseña, `usuarioId` ni contenido de errores de Identity. Solo admite códigos
de Identity previamente revisados; un código desconocido se normaliza como
`identity_rejected`.

## Consulta en Seq

La reconstrucción principal filtra por `Aplicacion = 'Icarus'` y `TraceId`, y
ordena por timestamp ascendente. A partir de ahí se puede restringir por:

- `Operation` para seguir la raíz o una operación interna;
- `EventName` para ver decisiones, persistencia, transacciones o
  compensaciones;
- `Outcome` y `ReasonCode` para agrupar rechazos y fallos;
- `PersistenceContext` para aislar Clientes o Identity;
- `Release` para comparar despliegues.

Las consultas operativas se documentarán como texto reproducible. Crear
dashboards, señales o alertas automáticas de Seq continúa sujeto al despliegue
productivo definido en el spec base.

## Fallos y degradación

- Sin Seq, los eventos siguen disponibles en la consola JSON.
- Una caída o rechazo del sink no altera el resultado de negocio.
- Sin `Activity.Current`, el middleware existente mantiene su `TraceId`
  generado para la petición.
- Fuera de una petición, un proceso técnico puede registrar eventos, pero no
  simula correlación HTTP; deberá declarar su propio límite cuando se diseñen
  workers.
- Una propiedad no permitida se omite; nunca se serializa primero para luego
  sanearla.
- Si un `SaveChanges` falla, no se informa un rollback salvo que EF observe una
  transacción físicamente revertida.

## Fuera de alcance

- OpenTelemetry, exportadores OTLP y propagación `traceparent` desde el
  frontend;
- spans distribuidos y jerarquías de trazas;
- auditoría de usuarios o acciones nominales;
- registrar lecturas rutinarias, cada método o todas las operaciones actuales;
- capturar SQL, parámetros, change tracker, requests, responses o entidades;
- cambiar el contrato de incidentes frontend del spec base;
- desplegar o administrar la instancia productiva de Seq;
- crear el frontend o una conexión directa del navegador a Seq.

OpenTelemetry queda como evolución compatible: los nombres de operación y la
taxonomía podrán convertirse en actividades o eventos si aparecen servicios
externos, procesos asíncronos o necesidad real de jerarquía distribuida.

## Criterios de aceptación

1. Un único `TraceId` permite leer cronológicamente la entrada HTTP, la
   operación raíz, sus decisiones, escrituras, transacciones, resultado y la
   petición completada.
2. El ciclo común de una mutación registrable se genera sin repetir llamadas de
   inicio y fin en cada handler.
3. La validación rechazada produce un resultado seguro sin serializar el
   request ni mensajes con valores.
4. Cada `SaveChanges` incorporado informa contexto, filas y duración; commit y
   rollback físicos se distinguen de compensación lógica.
5. El alta de cliente con cuenta narra tanto el camino exitoso como el rechazo
   de Identity y la compensación.
6. Un error inesperado produce `operation.failed` y un único `backend.error`
   con la excepción; el evento de operación no duplica stack ni mensaje.
7. Una propiedad desconocida o prohibida nunca llega a los eventos capturados,
   y su rechazo no rompe la operación.
8. Ningún evento del piloto contiene correo, NIT, razón social, contraseña,
   token, documento, biometría, `TrabajadorId`, cuerpos ni actividad nominal.
9. Sin Seq, la operación conserva su comportamiento y la narración aparece en
   consola JSON.
10. Las pruebas dirigidas observan primero el fallo esperado y la puerta de
    calidad completa queda verde antes de cualquier commit o push.
