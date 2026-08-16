# Observabilidad de incidentes frontend–backend — diseño

Fecha: 2026-08-16
Estado: aprobado en brainstorming el 2026-08-16

## Objetivo

Permitir detectar e investigar errores técnicos de Icarus aunque el cliente o
trabajador no los reporte. Un incidente debe poder reconstruirse desde el
navegador hasta el backend mediante identificadores opacos y logs estructurados,
sin registrar PII ni convertir la observabilidad en auditoría nominal.

El diseño conserva dos mecanismos independientes:

1. **Diagnóstico manual** para desarrollo y testing: `?debug=1` habilita la
   descarga local de un JSON que se entrega a un agente para analizar errores o
   comportamientos no deseados.
2. **Reporte automático** para todos los entornos: los errores técnicos
   relevantes se envían al backend y se registran aunque el usuario no contacte
   con soporte.

Seq es el almacén y buscador principal de logs de Icarus. La consola JSON sigue
siendo el fallback: el endpoint de diagnósticos no depende de que Seq esté
disponible.

## Estado de partida

Icarus ya tiene:

- `ILogger` sobre Serilog, consola en JSON compacto y sink de Seq;
- `CorrelationIdMiddleware` y `X-Correlation-ID`;
- `ExceptionHandlingMiddleware` con `ProblemDetails` genérico;
- Seq en `docker-compose.dev.yml`, con volumen `seq-data`;
- una implementación frontend aún sin commit de `SessionId`, breadcrumbs,
  exportación manual y `X-Session-Id`;
- pruebas frontend verdes para ese núcleo, pero la primera ejecución no llegó a
  observar un rojo funcional porque faltaban las dependencias npm.

Faltan el contrato automático frontend, `ErrorId`, `TraceId`, logs de petición
completada, consumo backend de `SessionId`, separación correcta entre sesión y
correlación, restricción ambiental de `debug=1`, operación segura de Seq y
alertas documentadas.

La referencia de Caserito aporta el contrato cerrado, el endpoint, el límite de
cuerpo, el rate limit y los eventos `frontend.error`/`frontend.flow`. Caserito no
usa Seq: escribe JSON a consola y depende de `docker logs`. Icarus adapta ese
patrón a su arquitectura y mantiene Seq como mejora propia.

## Decisiones

### 1. Identificadores con una responsabilidad cada uno

| Identificador | Alcance | Formato | Uso |
|---|---|---|---|
| `ErrorId` | un incidente | `ERR-` + 12 hex mayúsculas | referencia visible y búsqueda principal |
| `SessionId` | una pestaña | `SES-` + 12 hex mayúsculas | reconstruir navegación y llamadas relacionadas |
| `CorrelationId` | una petición HTTP | UUID | relacionar solicitud y respuesta |
| `TraceId` | ejecución backend | 32 hex minúsculas | seguir la actividad interna de ASP.NET |
| `Release` | despliegue | 1–40 ASCII seguros | identificar el código desplegado |

`SessionId` es estable en `sessionStorage`. `CorrelationId` deja de persistirse:
se genera para cada petición HTTP real; un refresh y un reintento reciben IDs
propios. El backend devuelve ambos `X-Correlation-ID` y `X-Trace-Id`.

### 2. El registro precede a la página de error

Ante una excepción backend no controlada:

1. el backend genera `ErrorId`;
2. escribe `backend.error` con excepción, `ErrorId`, IDs de correlación y
   contexto seguro;
3. devuelve `ProblemDetails` genérico con `errorId`, `correlationId` y `traceId`;
4. el frontend adjunta breadcrumbs mediante el endpoint automático;
5. la UI muestra el mismo `ErrorId` como referencia.

El usuario no activa el registro al comunicar el código: el incidente ya debe
existir. Si no lo reporta, Seq y las alertas siguen conservando la evidencia.

### 3. Contexto de identidad mínimo

El backend deriva del principal autenticado, nunca del cuerpo del navegador:

- `ClienteId` opaco, cuando exista;
- `Rol`;
- nunca `UsuarioId`, `TrabajadorId`, nombre, email, documento, biometría,
  credenciales, token, IP persistida ni registro nominal de acceso.

Los logs técnicos no sustituyen un sistema de auditoría.

### 4. Breadcrumbs seguros

El navegador conserva en memoria un buffer circular de 100 eventos:

- `flow.navigation`: pathname sanitizado;
- `flow.api_call`: método, pathname sanitizado, status, duración y IDs técnicos.

Se reemplazan segmentos numéricos y UUID por `:id`. Nunca se capturan query
strings, hash, cuerpos, valores de formularios, texto de usuario, headers,
tokens ni respuestas. Se adjuntan como máximo los últimos 30 eventos a un
reporte automático; el backend acepta como máximo 50.

El buffer se captura siempre porque aporta contexto a un error inesperado. Sin
error no sale del navegador, salvo exportación manual explícita.

### 5. Diagnóstico manual restringido

La exportación requiere simultáneamente:

- entorno de desarrollo, testing o una build con
  `VITE_HABILITAR_DIAGNOSTICO_MANUAL=true`;
- `?debug=1` en esa pestaña.

Producción queda deshabilitada por defecto. La marca solo persiste en
`sessionStorage` si el entorno permite el diagnóstico. El JSON es una
herramienta para desarrolladores/testing y no se presenta como función de
soporte al cliente.

### 6. Errores frontend automáticos

Se reportan:

- error de React Router o límite de error;
- `window.error`;
- `unhandledrejection`;
- fallo de red;
- HTTP `500–599`;
- fallo de carga de chunk/PWA.

No se reportan como incidentes técnicos los `400`, `401`, `403`, `404` o `409`
esperados. El reporte es best effort, usa `fetch(..., { keepalive: true })`, no
genera errores visibles ni se reporta a sí mismo. Se deduplica el mismo evento
durante una ventana corta y se limita también en servidor.

No se envía mensaje ni stack arbitrario. Para errores del navegador se permiten
solo ubicación saneada (`asset` del mismo origen, línea y columna) y nombres de
evento de una whitelist. Los source maps se generan como artefactos privados y
nunca se sirven públicamente.

### 7. Contrato del endpoint

El navegador llama `POST /api/diagnosticos/frontend`; el proxy de Icarus elimina
`/api`, por lo que el backend mapea `POST /diagnosticos/frontend`.

El endpoint es anónimo para cubrir login y sesiones vencidas, pero usa el
principal si llega un bearer válido. Requisitos:

- cuerpo máximo: 16 KiB;
- rate limit: 20 reportes/minuto por partición efímera, sin persistir la clave;
- propiedades desconocidas rechazadas;
- respuesta `202`, `400`, `413` o `429`;
- contrato cerrado y longitudes acotadas;
- sin eco del cuerpo inválido en respuesta o logs.

Campos de `ReporteDiagnosticoFrontend`:

- `errorId`, `eventName`, `category`, `source`;
- `sessionId`, `correlationId`, `traceId`, `statusCode`, `release` opcionales;
- `asset`, `lineNumber`, `columnNumber` opcionales y saneados;
- `flowEvents` opcional.

Whitelist inicial:

- eventos: `router.unexpected`, `window.unexpected`, `promise.unhandled`,
  `http.network_failed`, `http.server_failed`, `chunk.load_failed`;
- categorías: `unexpected`, `network`, `server`, `chunk`;
- fuentes: `router`, `window`, `promise`, `http`.

### 8. Observabilidad de peticiones backend

`RequestObservabilityMiddleware` registra exactamente un
`http.request.completed` por petición con:

- método;
- patrón de ruta, nunca valores concretos ni query;
- status code;
- duración;
- `ErrorId` si hubo excepción;
- `CorrelationId`, `TraceId`, `SessionId` válido, `ClienteId` opaco y `Rol`.

Los scopes se aplican también a logs emitidos durante la petición. Un
`X-Session-Id` inválido se ignora. El `TraceId` se toma de `Activity.Current` y
se devuelve en `X-Trace-Id`.

### 9. Eventos estructurados

Nombres estables:

- `backend.error`: excepción no controlada;
- `backend.business_warning`: excepción esperada de dominio;
- `http.request.completed`: petición completada;
- `frontend.error`: reporte técnico del navegador;
- `frontend.flow`: breadcrumb adjunto.

Las excepciones backend mantienen stack trace dentro de Seq/consola. Las
respuestas al cliente siguen siendo genéricas.

### 10. Release y source maps

- Backend: `AssemblyInformationalVersion` o SHA proporcionado por
  `ICARUS_RELEASE`; fallback `development`.
- Frontend: `VITE_RELEASE`; fallback `development`.
- Los source maps de producción se generan ocultos y se extraen del directorio
  público a un artefacto privado identificado por release.
- No se publica un `.map` en la imagen o servidor web.

Mientras no exista pipeline de despliegue productivo de Icarus, el repositorio
deja el mecanismo y la documentación preparados; la conservación del artefacto
se conecta al pipeline cuando este se cree.

### 11. Seq

Serilog escribe siempre JSON compacto a consola. Si `Seq:Url` está configurado,
añade el sink Seq y usa `Seq:ApiKey` cuando exista. Una caída de Seq no impide
responder peticiones.

Desarrollo:

- imagen oficial fijada a `datalust/seq:2026.1`;
- UI solo en `127.0.0.1:5341`;
- sin autenticación únicamente por ser local;
- volumen persistente `seq-data`;
- `iniciar-pc1/2/3.ps1` lo levanta mediante `docker-compose.dev.yml`.

Producción:

- una única instancia Seq central en un contenedor separado dentro de la VPS,
  compartida por Icarus, Caserito y otras aplicaciones autorizadas;
- despliegue y volumen independientes del ciclo de vida de ambas aplicaciones;
- una API key de ingestión diferente por aplicación consumidora;
- propiedad obligatoria `Aplicacion` para separar consultas,
  señales y alertas;
- red Docker externa compartida `trajano-shared-network` para ingestión interna;
- autenticación obligatoria y API key de ingestión;
- volumen persistente fuera del ciclo de vida de la aplicación;
- red interna para ingestión;
- UI no expuesta públicamente; acceso por VPN o túnel SSH;
- imagen fijada, límites de memoria y política inicial de retención de 30 días;
- secretos solo en variables/secret store de la VPS.

El despliegue productivo completo de Icarus aún no existe. Este incremento
documenta el contrato y añade una plantilla de compose del Seq central,
reutilizable por cualquier aplicación de la VPS, sin inventar el compose
productivo de Icarus.

### 12. Detección sin reporte humano

Seq debe permitir búsquedas por `ErrorId`, `SessionId`, `CorrelationId`,
`TraceId`, release, ruta y ventana temporal. Se documentan consultas y alertas
iniciales:

- aparición de `backend.error` o `frontend.error`;
- repetición del mismo evento/release/ruta;
- incremento de status `5xx`;
- health check caído.

El aprovisionamiento automático de señales de Seq queda fuera hasta contar con
el despliegue productivo; las consultas y umbrales quedan definidos para su
configuración operativa.

## Fallos y degradación

- Sin Seq: los logs siguen en consola/Docker.
- Sin backend o sin red: el navegador no puede reportar; no persiste ni reintenta
  indefinidamente. Health checks deben detectar la caída general.
- Endpoint de diagnóstico rechazado: no altera el flujo del usuario.
- Reportes repetidos: deduplicación cliente + rate limit servidor.
- Reloj del navegador incorrecto: el servidor conserva su timestamp como
  autoridad; timestamps de breadcrumbs son auxiliares.

## Fuera de alcance

- Sentry, ELK, Loki u OpenTelemetry en este incremento;
- auditoría de acciones de usuarios;
- grabación de pantalla, DOM, clicks genéricos o contenido de formularios;
- persistir incidentes en SQL;
- almacenar datos nominales para responder «quién»;
- desplegar toda la aplicación Icarus en producción;
- instrumentar acciones de negocio `flow.action` antes de definir una whitelist
  por módulo.

## Criterios de aceptación

1. Un error backend no controlado queda registrado con `ErrorId`, se devuelve de
   forma genérica y puede localizarse por el mismo código.
2. Un error frontend relevante se reporta automáticamente sin `debug=1` y queda
   unido a los breadcrumbs seguros de su `SessionId`.
3. Cada petición tiene `CorrelationId` propio y `TraceId`; el `SessionId` agrupa
   la pestaña completa.
4. `http.request.completed` usa patrones de ruta y no contiene query, IDs de
   recursos ni cuerpos.
5. `?debug=1` solo permite exportar cuando el entorno lo autoriza.
6. Ningún contrato o log contiene PII, credenciales, tokens, biometría o acceso
   nominal de trabajadores.
7. Seq local arranca con `iniciar-pc2.ps1`, persiste en volumen y solo expone su
   UI en localhost.
8. Sin Seq, la API sigue operativa y los eventos aparecen en consola.
9. Tests frontend, unitarios e integración cubren contrato, límites, propagación
   y captura; la puerta completa queda verde antes de commit/push.
