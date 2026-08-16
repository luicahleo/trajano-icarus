# Observabilidad de incidentes frontend–backend

**Goal:** Detectar e investigar errores técnicos de Icarus aunque el usuario no
los reporte, correlando frontend, backend y Seq sin PII, y conservar `?debug=1`
como herramienta manual exclusiva de desarrollo/testing.

**Architecture:** Spec:
`docs/superpowers/specs/2026-08-16-observabilidad-incidentes-frontend-backend-design.md`.
El navegador mantiene breadcrumbs seguros y reporta incidentes a un endpoint
cerrado. El backend genera `ErrorId`, `TraceId`, logs de petición y eventos
estructurados. Serilog escribe siempre a consola y opcionalmente a Seq. Seq se
levanta localmente por compose y se entrega una plantilla segura para VPS.

**Tech stack:** React 19, TypeScript 6, Vite 8, Vitest; .NET 10 Minimal APIs,
Serilog, rate limiting ASP.NET; Seq 2026.1 en Docker.

## Restricciones globales

- Anti-PII estricta: nunca bodies, query strings, texto de usuario, nombres,
  emails, documentos, biometría, credenciales, tokens, `UsuarioId`,
  `TrabajadorId` ni actividad nominal.
- TDD real en cada cambio nuevo: test dirigido, rojo causal, implementación
  mínima, verde. No fingir rojo para el código parcial ya existente; registrar
  la limitación histórica y añadir rojo para los requisitos aún ausentes.
- No copiar Caserito literalmente: adaptar `/api` al proxy de Icarus y conservar
  Serilog/Seq.
- Un bloque por sesión si se cambia de modelo. Actualizar este plan y
  `docs/ai/HANDOFF.md` al cortar.
- `./verify.ps1` antes de cada commit y push; nunca `--no-verify`.
- Commits directos en `develop`, uno por bloque coherente.

---

## Task 1: cerrar el diagnóstico manual y separar sesión/correlación

**Files:**

- Modify: `web/src/lib/sesionDiagnostico.ts`
- Modify: `web/src/lib/sesionDiagnostico.test.ts`
- Modify: `web/src/lib/correlation.ts`
- Modify: `web/src/lib/correlation.test.ts`
- Modify: `web/src/lib/http.ts`
- Modify: `web/src/lib/http.test.ts`
- Create: `web/src/app/RaizAplicacion.tsx`
- Modify: `web/src/app/router.tsx`
- Modify: `web/src/app/AppLayout.tsx`
- Modify: `web/src/app/BotonDiagnostico.test.tsx`
- Modify: `web/src/vite-env.d.ts`

- [ ] Añadir tests que demuestren en rojo que `debug=1` no activa la descarga
  cuando la build no lo autoriza y sí lo hace en desarrollo/testing.
- [ ] Añadir test rojo: dos peticiones reales obtienen `CorrelationId` distintos
  y comparten `SessionId`; refresh y reintento son peticiones independientes.
- [ ] Implementar `diagnosticoManualPermitido()` con
  `import.meta.env.DEV || VITE_HABILITAR_DIAGNOSTICO_MANUAL === 'true'`.
- [ ] Dejar `SessionId` estable por pestaña y generar UUID de correlación por
  petición, sin persistirlo.
- [ ] Montar captura y botón en una raíz común del router para incluir login;
  retirar esos componentes de `AppLayout`.
- [ ] Verificar que no se registra query/hash y que el buffer sigue limitado a
  100 eventos.

**Red/green:**

```powershell
cd web
npm run test -- src/lib/sesionDiagnostico.test.ts src/lib/correlation.test.ts src/lib/http.test.ts src/app/BotonDiagnostico.test.tsx
```

**Commit previsto:** `feat(web): separa diagnóstico manual y correlación por petición`

---

## Task 2: IDs y contexto de observabilidad backend

**Files:**

- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/DiagnosticIds.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/DiagnosticContext.cs`
- Create: `Icarus/src/Host/Icarus.Host/Observability/RequestObservabilityMiddleware.cs`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/CorrelationIdMiddleware.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs`
- Create: `Icarus/tests/Icarus.UnitTests/Observability/DiagnosticIdsTests.cs`
- Create: `Icarus/tests/Icarus.UnitTests/Observability/RequestObservabilityMiddlewareTests.cs`
- Modify: `Icarus/tests/Icarus.IntegrationTests/CorrelationIdIntegrationTests.cs`

- [ ] Test rojo para formatos y unicidad de `ErrorId`/`SessionId`.
- [ ] Test rojo para `X-Trace-Id`, validación de `X-Session-Id` y
  `http.request.completed` con patrón de ruta, status y duración.
- [ ] Implementar `DiagnosticContext` sobre `HttpContext.Items`.
- [ ] Implementar middleware después de autenticación y antes de autorización;
  enriquecer con `ClienteId`/`Rol`, nunca actor nominal.
- [ ] Responder siempre con `X-Correlation-ID` y `X-Trace-Id`.

**Red/green:**

```powershell
dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter "FullyQualifiedName~Observability"
dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj --filter "FullyQualifiedName~CorrelationId"
```

**Commit previsto:** `feat(observabilidad): correlaciona peticiones con sesión y trace`

---

## Task 3: ErrorId en excepciones backend

**Files:**

- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ExceptionHandlingMiddleware.cs`
- Modify: `Icarus/tests/Icarus.UnitTests/Observability/ExceptionHandlingMiddlewareTests.cs`
- Create: `Icarus/tests/Icarus.IntegrationTests/Observability/ErrorIdIntegrationTests.cs`

- [ ] Test rojo: excepción inesperada produce `ErrorId`, lo incluye en
  `ProblemDetails`, lo deja en `DiagnosticContext` y emite `backend.error`.
- [ ] Test rojo: errores esperados de negocio no reciben referencia de incidente
  técnico y conservan respuesta genérica.
- [ ] Añadir `correlationId` y `traceId` sin exponer excepción ni mensajes
  internos.
- [ ] Verificar que `http.request.completed` comparte el mismo `ErrorId`.

**Red/green:**

```powershell
dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter "FullyQualifiedName~ExceptionHandling"
dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj --filter "FullyQualifiedName~ErrorId"
```

**Commit previsto:** `feat(observabilidad): asigna referencias a errores backend`

---

## Task 4: endpoint seguro de diagnósticos frontend

**Files:**

- Create: `Icarus/src/Host/Icarus.Host/Observability/ReporteDiagnosticoFrontend.cs`
- Create: `Icarus/src/Host/Icarus.Host/Observability/ClientDiagnosticsBodyLimitMiddleware.cs`
- Create: `Icarus/src/Host/Icarus.Host/Endpoints/DiagnosticosEndpoints.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs`
- Create: `Icarus/tests/Icarus.UnitTests/Observability/ReporteDiagnosticoFrontendTests.cs`
- Create: `Icarus/tests/Icarus.IntegrationTests/Observability/DiagnosticosEndpointTests.cs`

- [ ] Test rojo del contrato exacto y whitelist; rechazar propiedades extra,
  mensajes, rutas con query, IDs inválidos, más de 50 eventos y campos largos.
- [ ] Test rojo de `202`, `400`, `413` y `429`.
- [ ] Configurar rate limit `diagnosticos-frontend` a 20/minuto y límite 16 KiB.
- [ ] Mapear backend `/diagnosticos/frontend` y documentar que el navegador usa
  `/api/diagnosticos/frontend`.
- [ ] Emitir `frontend.error` y `frontend.flow` con scopes seguros y contexto de
  tenant/rol derivado del principal.

**Red/green:**

```powershell
dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter "FullyQualifiedName~ReporteDiagnostico"
dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj --filter "FullyQualifiedName~Diagnosticos"
```

**Commit previsto:** `feat(observabilidad): recibe diagnósticos seguros del frontend`

---

## Task 5: reporte automático y página de error frontend

**Files:**

- Create: `web/src/lib/diagnosticos.ts`
- Create: `web/src/lib/diagnosticos.test.ts`
- Modify: `web/src/lib/http.ts`
- Modify: `web/src/lib/http.test.ts`
- Create: `web/src/app/ErrorDiagnosticoPage.tsx`
- Create: `web/src/app/ErrorDiagnosticoPage.test.tsx`
- Create: `web/src/app/CapturaErroresGlobales.tsx`
- Create: `web/src/app/CapturaErroresGlobales.test.tsx`
- Modify: `web/src/app/router.tsx`
- Modify: `web/src/main.tsx`

- [ ] Test rojo: reporte incluye `ErrorId`, IDs técnicos, release y últimos 30
  breadcrumbs, pero no mensaje, stack, query, body ni token.
- [ ] Test rojo: red/5xx se reportan; 4xx esperados no; el endpoint nunca se
  reporta a sí mismo.
- [ ] Test rojo: `window.error`, `unhandledrejection` y error de router se
  clasifican y muestran referencia.
- [ ] Implementar envío best effort con `keepalive`, deduplicación local y
  bearer opcional sin persistirlo.
- [ ] Leer `errorId`, `correlationId` y `traceId` de `ProblemDetails`/headers y
  conservarlos en `ApiError`.

**Red/green:**

```powershell
cd web
npm run test -- src/lib/diagnosticos.test.ts src/lib/http.test.ts src/app/ErrorDiagnosticoPage.test.tsx src/app/CapturaErroresGlobales.test.tsx
```

**Commit previsto:** `feat(web): reporta errores técnicos con contexto seguro`

---

## Task 6: release y source maps privados

**Files:**

- Modify: `web/vite.config.ts`
- Modify: `web/src/vite-env.d.ts`
- Modify: `web/package.json`
- Create: `web/scripts/extraer-sourcemaps.mjs`
- Create: `web/scripts/extraer-sourcemaps.test.mjs`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ObservabilityExtensions.cs`
- Modify: `Icarus/src/Host/Icarus.Host/appsettings.json`
- Modify: `Icarus/src/Host/Icarus.Host/appsettings.Development.json`

- [ ] Test rojo del script: extrae `.map` a artefacto privado por release y no
  deja source maps en `dist`.
- [ ] Inyectar `VITE_RELEASE` y `ICARUS_RELEASE`, con fallback `development`.
- [ ] Generar sourcemaps ocultos solo cuando se solicite y extraerlos antes de
  publicar el directorio web.
- [ ] Enriquecer todos los logs backend con `Release`.

**Red/green:**

```powershell
cd web
node --test scripts/extraer-sourcemaps.test.mjs
npm run build
```

**Commit previsto:** `feat(observabilidad): identifica releases y protege source maps`

---

## Task 7: Seq local seguro y plantilla de VPS

**Files:**

- Modify: `docker-compose.dev.yml`
- Create: `docker-compose.seq.yml`
- Modify: `.env.example`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ObservabilityExtensions.cs`
- Modify: `Icarus/src/Host/Icarus.Host/appsettings.Development.json`
- Create: `docs/operacion/observabilidad.md`
- Modify: `web/README.md`
- Modify: `iniciar-pc.ps1`
- Modify: `estado-pc.ps1`

- [ ] Fijar `datalust/seq:2026.1`, bind local `127.0.0.1:5341:80`, volumen y
  healthcheck en desarrollo.
- [ ] Cambiar la API a `Seq__Url=http://seq:80`; usar `Seq__ApiKey` opcional.
- [ ] Hacer que los scripts impriman la URL local de Seq y muestren su estado.
- [ ] Añadir plantilla independiente de VPS para un contenedor Seq central,
  compartido por cualquier aplicación autorizada: password hash, API keys
  distintas por aplicación, propiedad `Aplicacion`, volumen propio, red externa
  `trajano-shared-network`, UI local/túnel y límites; no crear un despliegue
  productivo ficticio de Icarus.
- [ ] Documentar consultas por `ErrorId`, `SessionId`, `CorrelationId`,
  `TraceId`, operación sin Seq, retención 30 días y alertas iniciales.
- [ ] Validar compose renderizado sin exponer secretos.

**Verificación:**

```powershell
docker compose -f docker-compose.dev.yml config --quiet
docker compose -f docker-compose.seq.yml config --quiet
```

**Commit previsto:** `ops(observabilidad): asegura Seq local y prepara VPS`

---

## Task 8: integración y cierre

**Files:**

- Modify: este plan, marcando tareas completadas y desviaciones reales.
- Delete: `docs/ai/HANDOFF.md` al cerrar.

- [ ] Ejecutar pruebas dirigidas de todos los bloques.
- [ ] Ejecutar `./verify.ps1` con Docker disponible.
- [ ] Revisar `git diff --check`, `git diff --stat` y los diffs propios.
- [ ] Confirmar manualmente que Seq abre en `http://localhost:5341`, recibe un
  `http.request.completed` y permite buscar una referencia de prueba.
- [ ] Confirmar que `?debug=1` se exporta en desarrollo y queda inerte en build
  productiva sin opt-in.
- [ ] Commit final de documentación si el plan necesitó correcciones, push a
  `develop` y borrar handoff.

**Puerta:**

```powershell
./verify.ps1
```

**Resultado esperado:** todos los gates verdes; ningún secreto o PII en diffs o
logs de prueba.
