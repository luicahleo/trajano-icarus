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
| `TraceId` | ejecución ASP.NET | 32 hex minúsculas | `TraceId = '...'` |
| `Release` | despliegue | 1–40 ASCII seguros | `Release = 'v1.2.3'` |

Eventos estables:

- `backend.error` — excepción no controlada (stack dentro de Seq/consola);
- `backend.business_warning` — excepción esperada de dominio;
- `http.request.completed` — petición completada (patrón de ruta, status,
  duración, sin query ni cuerpos);
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
- la API apunta con `Seq__Url=http://seq:80` dentro de la red de compose;
  `Seq__ApiKey` opcional.

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

Icarus ya lo hace vía configuración (env vars para `ICARUS_RELEASE` y Seq):

| Variable | Ejemplo | Uso |
|---|---|---|
| `ICARUS_RELEASE` | `1.0.0+a1b2c3d` | release en todos los logs |
| `Seq__Url` | `http://seq:5341` | URL de ingestión (red interna de la VPS) |
| `Seq__ApiKey` | `<key propia de Icarus>` | API key exclusiva de Icarus |

Cada consumidor debe añadir su propia propiedad `Aplicacion` en su Serilog
(`Icarus` ya lo hace) y usar una API key distinta. Sin `Seq__Url`, Icarus solo
escribe a consola.

## Consultas útiles en Seq

El buscador acepta la sintaxis `Propiedad = 'valor'`:

- por incidente: `ErrorId = 'ERR-0A1B2C3D4E5F'`
- por pestaña: `SessionId = 'SES-0A1B2C3D4E5F'`
- por petición: `CorrelationId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'`
- por ejecución: `TraceId = '0123456789abcdef0123456789abcdef'`
- por despliegue: `Release = 'v1.2.3'`
- solo errores: `(@Level = 'Error') or EventType in ('backend.error','frontend.error')`
- por patrón de ruta: `RequestPath like '/api/clientes/%'`
- ventana temporal: filtro de fecha en la barra de Seq.

## Alertas iniciales

Definir señales y umbrales iniciales (configuración operativa, fuera de este
incremento):

1. **Errores de aplicación**: aparición de `backend.error` o `frontend.error`
   (alerta inmediata).
2. **Repetición**: mismo `EventType` + `Release` + patrón de ruta repetido en la
   ventana (indica fallo sistemático).
3. **Status 5xx**: incremento del conteo de `http.request.completed` con
   `StatusCode >= 500`.
4. **Health caído**: monitor externo del endpoint de salud de la API.

## Operación sin Seq

Seq es opcional: si no responde, el backend escribe solo a consola JSON
(`docker logs`), la API sigue respondiendo y el endpoint de diagnósticos del
frontend no depende de él. La retención y búsqueda de consola quedan a cargo del
sistema de logs del contenedor.

## Release y source maps

- `Release` se inyecta por `ICARUS_RELEASE` (backend) y `VITE_RELEASE`
  (frontend), con fallback `development`.
- Los source maps de producción se generan ocultos solo bajo pedido
  (`npm run build:sourcemaps`) y se extraen a `sourcemaps/<release>/` (ignorado
  por git, fuera de `dist`) por `web/scripts/extraer-sourcemaps.mjs`. Nunca se
  publica un `.map` en la imagen o el servidor web.
