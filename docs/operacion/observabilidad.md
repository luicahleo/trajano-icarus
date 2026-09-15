# Observabilidad de incidentes frontend–backend

Guía operativa de la observabilidad de Icarus: cómo se registran, correlacionan
y buscan los errores técnicos sin exponer PII. Diseño y contrato:
`docs/superpowers/specs/2026-08-16-observabilidad-incidentes-frontend-backend-design.md`.

## Resumen

- **Diagnóstico manual** (`?debug=1`): descarga local de un JSON con eventos de
  la pestaña. Solo activo en desarrollo/testing o con
  `VITE_HABILITAR_DIAGNOSTICO_MANUAL=true`. Nunca es una función de soporte.
- **Reporte automático**: los errores técnicos relevantes del navegador se
  envían a `POST /api/diagnosticos/frontend` sin `debug=1` y quedan unidos a los
  breadcrumbs seguros de su `SessionId`.
- **Backend**: cada petición registra un `http.request.completed`; las
  excepciones no controladas generan `backend.error` con `ErrorId`.
- **Seq** es el almacén de búsqueda; la consola JSON es el fallback siempre
  disponible.

## Identificadores y eventos

| Identificador | Alcance | Formato | Búsqueda |
|---|---|---|---|
| `ErrorId` | un incidente | `ERR-` + 12 hex mayúsculas | `ErrorId = 'ERR-...'` |
| `SessionId` | una pestaña | `SES-` + 12 hex mayúsculas | `SessionId = 'SES-...'` |
| `CorrelationId` | una petición HTTP | UUID | `CorrelationId = '...'` |
| `DownstreamCorrelationId` | un envío MVC → API | UUID | `DownstreamCorrelationId = '...'` |
| `TraceId` | ejecución ASP.NET (W3C) | 32 hex minúsculas | `TraceId = '...'` |
| `Operation` | una operación del vuelo | vocabulario de dominio | `Operation = 'avicola.pedidos.crear'` |
| `ReasonCode` | motivo de una decisión | código técnico estable | `ReasonCode = 'identity_rejected'` |
| `Outcome` | resultado | `applied`/`rejected`/`failed`/`committed`/`rolled_back` | `Outcome = 'rejected'` |
| `Release` | despliegue | 1–40 ASCII seguros | `Release = 'v1.2.3'` |
| `Aplicacion` | proceso emisor | `Icarus` / `Trajano.GestorCaisy` | `Aplicacion = 'Icarus'` |

Eventos estables:

- `backend.error` — excepción no controlada; solo tipo de excepción y `ErrorId`,
  nunca el stack crudo;
- `backend.business_warning` — excepción esperada de dominio;
- `http.request.completed` — una sola vez por petición externa, con `Method`,
  `RoutePattern` (nunca el pathname), `StatusCode` y `DurationMs`, sin query ni
  cuerpos;
- `http.client.send` — cada envío real de GestorCaisy a la API, con
  `DownstreamCorrelationId`, método, ruta, estado y duración;
- `frontend.error` — reporte técnico del navegador;
- `frontend.flow` — breadcrumb adjunto.

Regla de oro: **nada nominal**. No hay mensajes de usuario, cuerpos, query,
tokens, credenciales, biometría, `UsuarioId` ni `TrabajadorId` en los logs
técnicos. La observabilidad no sustituye un sistema de auditoría.

## Flujo de un incidente

1. El backend lanza una excepción no controlada → genera `ErrorId`, escribe
   `backend.error` y devuelve un `ProblemDetails` genérico con `errorId`,
   `correlationId` y `traceId`.
2. El frontend conserva los IDs en el `ApiError`, adjunta los breadcrumbs de la
   pestaña y reporta `frontend.error` con el mismo `ErrorId`.
3. El usuario ve en pantalla la referencia `ERR-...` para comunicarla a soporte;
   el incidente ya está en Seq aunque no la comunique.
4. En Seq se reconstruye la pestaña completa por `SessionId` y la petición por
   `CorrelationId`/`TraceId`.

## Seq local (desarrollo)

`docker-compose.dev.yml` levanta Seq con la imagen fijada `datalust/seq:2026.1`:

- UI solo en `http://localhost:5341` (bind `127.0.0.1`, nunca en la LAN);
- sin autenticación por ser local;
- volumen persistente `seq-data` y healthcheck;
- la API apunta con las variables nombradas
  `Serilog__WriteTo__Seq__Name=Seq` y
  `Serilog__WriteTo__Seq__Args__serverUrl=http://seq:80` dentro de la red de
  compose; `Serilog__WriteTo__Seq__Args__apiKey` es opcional.

`iniciar-pc*.ps1` imprime la URL local al terminar y `estado-pc.ps1` muestra su
estado. Sin Seq, la API sigue operativa: los eventos quedan en la consola JSON.

## Seq central (plantilla VPS)

`docker-compose.seq.yml` es una plantilla de infraestructura **independiente**
de Icarus: un contenedor Seq central compartido por Icarus, Caserito y cualquier
aplicación autorizada. No es un despliegue productivo de Icarus.

Puntos del contrato:

- imagen fijada `datalust/seq:2026.1`, límite de memoria y `restart`;
- red externa compartida `trajano-shared-network` para la ingestión interna;
- volumen propio `seq-vps-data` fuera del ciclo de vida de las aplicaciones;
- UI solo en loopback de la VPS (`127.0.0.1:5341`): acceso por VPN o túnel SSH;
- autenticación obligatoria: hash salado del admin (`docker run --rm -i
  datalust/seq:2026.1 config hash`, la contraseña se pasa por STDIN) y
  `SEQ_FIRSTRUN_REQUIREAUTHENTICATIONFORHTTPINGESTION=true`;
- una API key de ingestión **distinta por aplicación** (se crean en la UI de
  Seq), nunca compartidas;
- propiedad `Aplicacion` obligatoria en los logs de cada consumidor para
  separar consultas, señales y alertas;
- retención inicial de **30 días** (se fija en *Settings → Retention* de la UI);
- secretos solo en el secret store / variables de la VPS, nunca en git.

Pasos de alta en la VPS (resumen; el detalle está en los comentarios del
compose):

```bash
docker network create trajano-shared-network
printf 'la-contrasena' | docker run --rm -i datalust/seq:2026.1 config hash
# guardar el hash como SEQ_ADMIN_PASSWORD_HASH en el secret store
docker compose -f docker-compose.seq.yml up -d
# túnel para la UI: ssh -L 5341:127.0.0.1:5341 usuario@vps  ->  http://localhost:5341
```

### Configurar una aplicación consumidora

Icarus ya lo hace vía configuración: los sinks se declaran en la sección
`Serilog` y las variables de despliegue usan entradas **nombradas** (`Name` +
`Args`) para crear el sink aunque no exista en el JSON base:

| Variable | Ejemplo | Uso |
|---|---|---|
| `Serilog__Properties__Release` | `1.0.0+a1b2c3d` | release en todos los logs (saneada a 1–40 ASCII seguros) |
| `ICARUS_RELEASE` | `1.0.0+a1b2c3d` | respaldo de la anterior si no se define la primera |
| `Serilog__WriteTo__Seq__Name` | `Seq` | nombre del sink; obligatorio al declararlo solo por variables |
| `Serilog__WriteTo__Seq__Args__serverUrl` | `http://seq:80` | URL de ingestión (red interna de la VPS) |
| `Serilog__WriteTo__Seq__Args__apiKey` | `<key propia de Icarus>` | API key exclusiva de Icarus |
| `Serilog__WriteTo__Seq__Args__batchPostingLimit` | `1000` | eventos por lote |
| `Serilog__WriteTo__Seq__Args__period` | `00:00:02` | intervalo de envío |
| `Serilog__WriteTo__Seq__Args__queueSizeLimit` | `10000` | cola acotada en memoria |

Cada consumidor debe añadir su propia propiedad `Aplicacion` en su Serilog
(`Icarus` ya lo hace) y usar una API key distinta. Si no se declara el sink
`Seq`, Icarus solo escribe a consola; una URL vacía no lo deshabilita por sí
sola.

## Consultas útiles en Seq

El buscador acepta la sintaxis `Propiedad = 'valor'`:

- por incidente: `ErrorId = 'ERR-0A1B2C3D4E5F'`
- por pestaña: `SessionId = 'SES-0A1B2C3D4E5F'`
- por petición: `CorrelationId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'`
- por salto MVC → API: `DownstreamCorrelationId = 'bbbbbbbb-cccc-dddd-eeee-ffffffffffff'`
- por ejecución: `TraceId = '0123456789abcdef0123456789abcdef'`
- por operación: `Operation = 'avicola.pedidos.crear'`
- por motivo: `ReasonCode = 'identity_rejected'`
- por resultado: `Outcome = 'rejected'`
- por despliegue: `Release = 'v1.2.3'`
- por aplicación: `Aplicacion = 'Icarus'`
- solo errores: `(@Level = 'Error') or EventName in ('backend.error','frontend.error')`
- por patrón de ruta: `RoutePattern = '/api/clientes/{id}'` o `RoutePattern like '/api/%'`
- ventana temporal: filtro de fecha en la barra de Seq.

> `RequestPath` se conserva sombreado con el mismo patrón seguro que
> `RoutePattern` para no heredar el pathname concreto del framework. La consulta
> de contrato es `RoutePattern`; `EventName` reemplaza al antiguo `EventType`.

## Alertas iniciales

Definir señales y umbrales iniciales (configuración operativa, fuera de este
incremento):

1. **Errores de aplicación**: aparición de `backend.error` o `frontend.error`
   (alerta inmediata).
2. **Repetición**: mismo `EventName` + `Release` + patrón de ruta repetido en la
   ventana (indica fallo sistemático).
3. **Status 5xx**: incremento del conteo de `http.request.completed` con
   `StatusCode >= 500`.
4. **Health caído**: monitor externo del endpoint de salud de la API.

## Entrega, retención y límites

- **Seq ya envía por lotes en segundo plano**: no se envuelve en
  `Serilog.Sinks.Async`. Los parámetros `batchPostingLimit`, `period` y
  `queueSizeLimit` se declaran en la configuración (`Serilog:WriteTo:Seq:Args`).
- **La consola JSON es un respaldo independiente**, envuelta en
  `Serilog.Sinks.Async` con cola acotada (`bufferSize`) y `blockWhenFull=false`
  para no bloquear el hilo de petición. La consola **no reenvía** a Seq: si Seq
  cae, los eventos solo quedan en `docker logs` y su retención depende del
  sistema de logs del contenedor.
- **No hay entrega exactamente una vez ni buffer durable**. Una cola llena, una
  caída prolongada de Seq o una terminación abrupta pueden perder eventos. Un
  buffer durable en volumen queda como mejora posterior y necesita límites de
  disco, permisos y retención propios.
- La retención de Seq central se fija en *Settings → Retention* (inicial: 30
  días); la del Seq local depende del volumen `seq-data`.
- **Diagnóstico seguro de desconexión**: se observa la ausencia de eventos en
  Seq y, del lado del proceso, `SelfLog`/contadores genéricos; nunca se redirige
  `SelfLog` al mismo logger ni se vuelca texto sin sanear. Un fallo de
  configuración (falta `Name`/`serverUrl`) se distingue de una caída temporal
  porque la aplicación no arranca o el sink no se crea en el primer caso.
- Un fallo del logger **no altera el resultado funcional**: la API y
  GestorCaisy siguen respondiendo aunque Seq no esté disponible.

## Operación sin Seq

Seq es opcional: si no responde, el backend escribe solo a consola JSON
(`docker logs`), la API sigue respondiendo y el endpoint de diagnósticos del
frontend no depende de él. Si no se declara el sink `Seq`, no hay intento de
ingestión; una URL vacía no lo deshabilita por sí sola.

## Reconstrucción de un registro de vuelo

Las mutaciones registrables narran su recorrido con eventos estructurados. La
clave primaria de reconstrucción es el `TraceId` existente; no se crea otro
identificador. El piloto es `clientes.alta_con_cuenta` y sus operaciones
internas incluyen `clientes.crear` y `clientes.suspender_alta_incompleta`.

Consultas reproducibles en Seq:

```text
Aplicacion = 'Icarus' and TraceId = '0123456789abcdef0123456789abcdef'
Aplicacion = 'Icarus' and TraceId = '0123456789abcdef0123456789abcdef' and Operation = 'clientes.alta_con_cuenta'
Aplicacion = 'Icarus' and TraceId = '0123456789abcdef0123456789abcdef' and EventName = 'operation.decision'
Aplicacion = 'Icarus' and TraceId = '0123456789abcdef0123456789abcdef' and Outcome = 'rejected'
Aplicacion = 'Icarus' and TraceId = '0123456789abcdef0123456789abcdef' and ReasonCode = 'identity_rejected'
Aplicacion = 'Icarus' and TraceId = '0123456789abcdef0123456789abcdef' and PersistenceContext = 'Clientes'
Aplicacion = 'Icarus' and TraceId = '0123456789abcdef0123456789abcdef' and Release = 'v1.2.3'
```

Para GestorCaisy, el vínculo de extremo a extremo es el `TraceId` compartido y
cada salto hacia la API tiene su `DownstreamCorrelationId`:

```text
Aplicacion = 'Trajano.GestorCaisy' and TraceId = '0123456789abcdef0123456789abcdef'
Aplicacion = 'Trajano.GestorCaisy' and DownstreamCorrelationId = 'bbbbbbbb-cccc-dddd-eeee-ffffffffffff'
Aplicacion = 'Icarus' and CorrelationId = 'bbbbbbbb-cccc-dddd-eeee-ffffffffffff'
```

### Ejemplo de recorrido correcto (sintético)

Un alta de pedido que se aplica se lee en orden cronológico como:

1. `operation.started` — `Operation = 'avicola.pedidos.crear'`, fase `start`.
2. `operation.decision` — `Outcome = 'applied'`, `Lineas = 2`; es la decisión
   tomada, no todavía una escritura confirmada.
3. `persistence.save_changes.completed` — `PersistenceContext = 'GestionAvicola'`,
   `RowsAffected = 1`.
4. `transaction.committed` — la transacción física de EF se confirmó.
5. `operation.completed` — fase `end`, `Outcome = 'succeeded'` con `DurationMs`.
6. `http.request.completed` — `RoutePattern = '/api/pedidos-alimento'`,
   `StatusCode = 201`.

### Ejemplo de recorrido rechazado (sintético)

Un alta de cliente que no puede completarse por identidad duplicada:

1. `operation.started` — `Operation = 'clientes.alta_con_cuenta'`.
2. `operation.decision` — `Operation = 'clientes.crear'`,
   `Outcome = 'rejected'`, `ReasonCode = 'identity_rejected'`; la decisión
   cambió el camino antes de escribir.
3. `operation.compensation.started` / `operation.compensation.completed` —
   `CompensationKind = 'logical'`; es compensación lógica, **no** un rollback
   físico.
4. `operation.rejected` — `Outcome = 'rejected'`, `ReasonCode` estable.
5. `http.request.completed` — `StatusCode = 409`.

El texto del evento indica operación, fase y resultado; `Outcome = 'applied'`
significa que la decisión se tomó y `transaction.committed` confirma la
persistencia. Nunca se infiere éxito definitivo de un evento anterior al
`SaveChanges`/commit.

Los eventos `transaction.committed` y `transaction.rolled_back` describen
transacciones físicas observadas por EF. `operation.compensation.*` describe
compensación lógica y nunca debe interpretarse como rollback físico. Los
eventos narrativos no contienen cuerpos, entidades, SQL, credenciales,
biometría ni datos nominales; `backend.error` conserva el tipo de excepción y
el `ErrorId`, pero no el stack crudo ni datos de la excepción.

## Release y source maps

- `Release` se inyecta por `Serilog__Properties__Release` (o su respaldo
  `ICARUS_RELEASE`) en el backend y por `VITE_RELEASE` en el frontend, con
  fallback `development`. El enriquecedor de metadatos seguros lo sanea a 1–40
  ASCII seguros.
- Los source maps de producción se generan ocultos solo bajo pedido
  (`npm run build:sourcemaps`) y se extraen a `sourcemaps/<release>/` (ignorado
  por git, fuera de `dist`) por `web/scripts/extraer-sourcemaps.mjs`. Nunca se
  publica un `.map` en la imagen o el servidor web.
