# Subproyecto 2 — Plan 4: Frontend PWA bajo `web/` — Diseño

Fecha: 2026-08-14
Estado: aprobado en brainstorming (sesión de la misma fecha)

## Contexto

El subproyecto 2 dejó el backend completo del plan 3 (módulo Clientes: agregados,
tenant, soft delete y entitlement) con CI verde en `develop`. Este plan 4 crea el
frontend PWA bajo `web/` (Vite + React + TypeScript) que consume esa API:
sesión con JWT + refresh en cookie, guardas de rol, navegación por rol y las
pantallas de gestión que la API ya expone (clientes, usuarios y trabajadores).

Referencia estructural, nunca copia de código: Caserito (`repos/dev_Caserito/web`).
Se estudiaron sus patrones reales (AuthContext con token en memoria, guardas de
ruta, cliente HTTP con refresh-on-401, Vite proxy, tema MUI) y se adaptan a los
contratos de Icarus.

## Decisiones tomadas en el brainstorming

1. **Alcance: andamiaje completo.** Shell PWA + sesión + guardas + cliente HTTP
   + pantallas de clientes (admin), usuarios (admin) y trabajadores (admin y
   cliente). Cubre todo lo que la API expone hoy. Quedan fuera los módulos de
   negocio que aún no tienen endpoints (Gestión Avícola, Control de Acceso).
2. **UI: MUI (Material UI) + Emotion, paridad con Caserito.** El usuario
   confirmó revisar Caserito; su frontend usa `@mui/material`, `@emotion/styled`
   e `@mui/icons-material` con tema propio. Icarus replica ese patrón con un
   tema de marca propio (verde avícola, tipografía Open Sans + Prompt) y sin
   componentes propietarios de Caserito. Con esto se descarta el CSS propio
   propuesto en el brainstorming.
3. **Validación de formularios: react-hook-form + zod** (patrón Caserito via
   `@hookform/resolvers`). El backend valida igualmente (FluentValidation); la
   validación del cliente es UX, no autoridad.
4. **Sin generación de cliente desde OpenAPI.** El backend NO publica documento
   OpenAPI (no hay `AddOpenApi()` en `Program.cs`). Agregarlo sería un cambio de
   backend fuera de alcance. El cliente HTTP es un wrapper `fetch` escrito a mano
   con tipos TypeScript explícitos de los contratos. Desviación documentada de
   Caserito (que usa `openapi-fetch` + `openapi-typescript`).
5. **Base de API bajo `/api` con proxy en dev.** El backend sirve bajo la raíz
   (`/identidad`, `/clientes`, `/health`), sin prefijo. El frontend usa base
   `/api` y el proxy de Vite reescribe `/api/*` → `http://localhost:8080/*` en
   desarrollo (la API del `docker-compose.dev.yml`). En producción se espera un
   reverse proxy que mapee `/api` al backend (despliegue fuera de alcance, pero
   la convención queda fijada).
6. **Sin CORS en el backend.** Con el proxy de Vite (dev) y el mismo origen
   (producción) no se toca el backend. La cookie de refresh es `SameSite=Strict`,
   compatible porque todas las llamadas son same-origin a través del proxy.
7. **Sesión sin persistencia del access token.** El access token vive solo en
   memoria (`lib/session.ts`, patrón Caserito); al recargar, la sesión se
   restaura llamando `POST /identidad/sesion/renovar` (cookie HttpOnly) y luego
   `GET /identidad/me`. Nunca se guarda el token en localStorage (anti-PII).
8. **Refresh reactivo single-flight.** Ante un 401 (salvo en rutas de sesión), el
   cliente renueva una sola vez y reintenta la petición original; si la
   renovación falla, cierra la sesión local. Se rechaza el refresh proactivo por
   temporizador: complejidad innecesaria para este incremento.
9. **Guardas por rol, no por permiso.** La autorización de Icarus es por rol
   (políticas `SoloAdministrador`/`GestionTrabajadores`), no hay claims de
   permiso. Caserito usa `RequierePermiso`; Icarus usa `ProtectedRoute`
   (autenticado) + `RequiereRol` (rol en lista), leyendo `GET /identidad/me`.
10. **Logout solo del lado del cliente (limitación conocida).** La API no expone
    `POST /identidad/sesion/logout` y la cookie es HttpOnly: el frontend limpia
    el token en memoria y redirige a `/login`, pero la cookie sigue viva hasta
    su vencimiento y la sesión se restauraría en la próxima carga. Se documenta
    como limitación y queda anotado para un plan futuro de Identity (endpoint de
    revocación). No se amplía el alcance.
11. **PWA con `generateSW`** (`vite-plugin-pwa`, `registerType: 'autoUpdate'`).
    Caserito usa `injectManifest` por sus notificaciones push (SignalR); Icarus
    no tiene push en alcance, así que el service worker generado basta (caché del
    shell de la app). Manifest en español, nombre «Icarus».
12. **Iconos PWA placeholder.** Sin diseñador: se generan iconos PNG simples
    (192/512/maskable) con un script de Node puro versionado en `web/scripts/` y
    se commitean. No bloquea el andamiaje; sustituibles más adelante.
13. **Navegación por rol.** `Administrador`: clientes, usuarios y trabajadores.
    `Cliente`: trabajadores de su empresa (`clienteId` viene de `/me`).
    `SoporteTecnico` y `Trabajador`: página de inicio «sin módulos habilitados»
    (la API no expone pantallas para ellos todavía).
14. **El frontend entra en la puerta de calidad desde el plan 4.** `verify.mjs`
    suma gates de frontend (`lint`, `build`, `vitest`) ejecutados en `web/`, y
    `ci.yml` añade `npm ci` y un job frontend. El `build` se añade como gate
    aunque el spec original solo listaba `npm run lint` y `vitest run`: el
    typecheck (`tsc -b`) es el análogo frontend del gate «Backend build».

## Arquitectura

Estructura bajo `web/` (base del spec del subproyecto 2, adaptada a los patrones
reales de Caserito):

```
web/
├── index.html                 # lang es, viewport, theme-color
├── package.json               # dependencias pineadas (paridad Caserito)
├── tsconfig.json              # proyecto base + alias @/
├── tsconfig.app.json          # src/ (DOM, jsx react-jsx)
├── tsconfig.node.json         # vite.config.ts, scripts
├── vite.config.ts             # react, VitePWA, proxy /api, vitest jsdom
├── eslint.config.js           # flat: js + typescript-eslint + react-hooks + prettier
├── .prettierrc.json
├── README.md                  # arranque local, credenciales semilla, puerta
├── AGENTS.md                  # convenciones del árbol web (complementa al raíz)
├── public/
│   └── pwa/                   # iconos 192/512/maskable
├── scripts/
│   └── generar-iconos.mjs     # genera los PNG placeholder (Node puro, zlib)
└── src/
    ├── main.tsx               # ThemeProvider MUI + CssBaseline + App
    ├── App.tsx                # AppProviders + RouterProvider
    ├── vite-env.d.ts
    ├── lib/
    │   ├── http.ts            # ApiError + petición con Bearer, correlation ID,
    │   │                      #   refresh-on-401 single-flight y reintento único
    │   ├── session.ts         # access token en memoria (nunca localStorage)
    │   ├── correlation.ts     # generar/propagar X-Correlation-ID
    │   └── tipos.ts           # contratos compartidos (Sesión, Me, Rol, Modulos…)
    ├── features/
    │   ├── auth/
    │   │   ├── AuthContext.tsx     # estado de sesión, restore al arrancar, login/logout
    │   │   ├── ProtectedRoute.tsx  # guard de autenticación
    │   │   ├── RequiereRol.tsx     # guard por rol
    │   │   ├── api.ts              # iniciarSesion, renovarSesion, obtenerMe, crearUsuario
    │   │   ├── LoginPage.tsx       # formulario email/contrasena (RHF + zod)
    │   │   └── LoginPage.test.tsx
    │   ├── admin/clientes/
    │   │   ├── api.ts              # listar/crear/suspender/reactivar/modulos
    │   │   ├── ClientesListaPage.tsx
    │   │   ├── ClienteNuevoPage.tsx
    │   │   ├── ClienteDetallePage.tsx   # estado + módulos (checkboxes)
    │   │   └── *.test.tsx
    │   ├── admin/usuarios/
    │   │   ├── api.ts              # alta de cuenta (POST /identidad/usuarios)
    │   │   ├── UsuarioNuevoPage.tsx
    │   │   └── UsuarioNuevoPage.test.tsx
    │   └── trabajadores/
    │       ├── api.ts              # listar/crear/cese/desactivar
    │       ├── TrabajadoresPage.tsx    # Administrador elige cliente; Cliente usa el suyo
    │       └── TrabajadoresPage.test.tsx
    ├── app/
    │   ├── providers.tsx       # QueryClient + AuthProvider
    │   ├── router.tsx          # createBrowserRouter + guardas + carga diferida
    │   ├── AppLayout.tsx       # shell MUI: AppBar + navegación por rol
    │   ├── InicioPage.tsx      # placeholder para SoporteTecnico/Trabajador
    │   ├── NotFoundPage.tsx
    │   └── theme.ts            # tema MUI de Icarus
    ├── pwa/
    │   └── registro.ts         # registerSW (virtual:pwa-register)
    └── test/
        ├── setup.ts           # @testing-library/jest-dom
        └── smoke.test.ts      # la app monta sin romper
```

Reglas de dependencia dentro del frontend:

- `lib/` no importa de `features/` ni de `app/`: transporte y tipos puros.
- `features/*` dependen de `lib/` y se referencian entre sí solo vía el router de
  `app/` (una feature no monta páginas de otra).
- `app/` orquesta: router, shell, providers y tema. No contiene lógica de negocio.
- No hay `react-query` en `features/auth`: la sesión es estado de aplicación
  (AuthContext), no estado de servidor. El resto de las features usan react-query
  para sus consultas.

## Contratos de la API consumidos (camelCase)

Sesión e identidad:

| Método/Ruta | Política | Entrada | Salida |
|---|---|---|---|
| `POST /identidad/sesion` | pública | `{email, contrasena}` | 200 `{accessToken, expiraEnSegundos}` + cookie `icarus_refresh` HttpOnly |
| `POST /identidad/sesion/renovar` | pública | cookie refresh | 200 `{accessToken, expiraEnSegundos}` |
| `GET /identidad/me` | autenticado | — | `{usuarioId, rol, clienteId}`; `rol` ∈ `Administrador\|SoporteTecnico\|Cliente\|Trabajador`; `clienteId` Guid o `null` |
| `POST /identidad/usuarios` | SoloAdministrador | `{email, contrasena, rol, clienteId, trabajadorId}` | 201 `{id}` |

Nota de contrato: el campo de credencial del login se llama `contrasena` (no
`password`), espejo de `IniciarSesionCommand`. `clienteId`/`trabajadorId` son
opcionales y solo cobran sentido según el rol (Cliente/Trabajador exigen
`clienteId`; Trabajador lleva también `trabajadorId`).

Clientes (solo Administrador):

| Método/Ruta | Entrada | Salida |
|---|---|---|
| `POST /clientes/` | `{razonSocial, identificadorFiscal}` | 201 `{id}` |
| `GET /clientes/` | — | `[{id, razonSocial, identificadorFiscal, estaActivo, modulos[]}]` |
| `POST /clientes/{id}/suspender` | — | 204 |
| `POST /clientes/{id}/reactivar` | — | 204 |
| `PUT /clientes/{id}/modulos` | `{modulos: string[]}` (`GestionAvicola`, `ControlAcceso`) | 204 |

Trabajadores (Administrador y Cliente de su empresa):

| Método/Ruta | Entrada | Salida |
|---|---|---|
| `POST /clientes/{clienteId}/trabajadores` | `{nombre, documentoIdentidad, cargo, fechaIngreso}` | 201 `{id}` |
| `GET /clientes/{clienteId}/trabajadores` | — | `[{id, nombre, documentoIdentidad, cargo, fechaIngreso, fechaCese}]` |
| `POST /clientes/trabajadores/{id}/cese` | `{fechaCese}` | 204 |
| `DELETE /clientes/trabajadores/{id}` | — | 204 (soft delete) |

Errores: ProblemDetails (RFC 7807) con `title` genérico (anti-PII) y header
`X-Correlation-ID` en la respuesta. El frontend muestra el mensaje genérico del
problema (no lo inventa) y, cuando existe, el correlation ID para soporte; nunca
muestra ni registra documentos, identificadores fiscales, credenciales ni tokens.

Roles semilla dev/test (sistema cerrado): `admin@icarus.test`,
`soporte@icarus.test`, `cliente@icarus.test`, `trabajador@icarus.test`. La
contraseña depende de cómo se levante la API (ver README de `web/`): por
`dotnet run` con `appsettings.Development.json` es `Semilla-Dev-1234`; por
docker-compose, `Solo-Desarrollo-123`. El cliente semilla «Granja Demo S.A.C.»
tiene habilitado el módulo `GestionAvicola` y un trabajador demo (datos
ficticios, anti-PII).

## Componentes

### Cliente HTTP (`lib/http.ts`)

Wrapper de `fetch` (sin librería) con:

- Base `/api` (resuelto contra `window.location.origin` en dev, proxy y prod).
- Inyección `Authorization: Bearer` desde el token en memoria.
- `credentials: 'include'` (cookie de refresh).
- `X-Correlation-ID`: se genera un UUID por petición si no existe uno; se
  propaga en las respuestas para trazar con el backend.
- Refresh-on-401 single-flight: un único renovar concurrente; reintento único de
  la petición original clonando el `Request` (el body es de un solo uso).
- `ApiError` tipado: `{ status, code (title de ProblemDetails), correlationId }`.
  Sin datos personales en ningún mensaje.

El login/renovar nunca pasa por el refresh-on-401 (evita bucles).

### Sesión (`features/auth/AuthContext.tsx`)

- Al arrancar: si `refrescar()` (cookie) tiene éxito, `obtenerMe()` para
  `{rol, clienteId}`; si no, sesión anónima. `cargando` cubre la espera.
- `iniciarSesion(cred)`: `POST /identidad/sesion`, guarda el access token en
  memoria y refresca `me`.
- `cerrarSesion()`: limpia el token y navega a `/login` (limitación: la cookie
  queda; ver decisión 10).
- Expone `rol`, `clienteId`, `estaAutenticado`, `cargando`, `tieneRol(...)`.

### Guardas

- `ProtectedRoute`: mientras `cargando` muestra un indicador; sin sesión,
  `Navigate` a `/login`.
- `RequiereRol`: exige `rol` en la lista; si no, `Navigate` al inicio del rol.

### Navegación y router (`app/router.tsx`)

Rutas (carga diferida por página):

| Ruta | Guarda | Acceso |
|---|---|---|
| `/login` | pública | todos |
| `/` | autenticado | redirige por rol: admin→`/admin/clientes`, cliente→`/trabajadores`, resto→`/inicio` |
| `/inicio` | autenticado | SoporteTecnico y Trabajador (placeholder) |
| `/admin/clientes` | `RequiereRol Administrador` | lista + acciones |
| `/admin/clientes/nuevo` | ídem | alta |
| `/admin/clientes/:id` | ídem | detalle + módulos |
| `/admin/usuarios/nuevo` | ídem | alta de cuenta |
| `/clientes/:clienteId/trabajadores` | `RequiereRol Administrador` | trabajadores de un cliente |
| `/trabajadores` | `RequiereRol Cliente` | trabajadores de la propia empresa |
| `*` | — | 404 |

El layout (`AppLayout`) muestra una AppBar con el título «Icarus», los enlaces
del menú según el rol y el cierre de sesión. Nada de la navegación depende de
`clienteId` del menú: solo los datos.

### PWA (`vite-plugin-pwa`)

- `registerType: 'autoUpdate'`, estrategia `generateSW` (sin service worker a
  mano), globs del shell (`js,css,html,svg,png,woff2`).
- Manifest: `name`/`short_name` «Icarus», `lang` `es`, `display: standalone`,
  `theme_color`/`background_color` del tema, iconos `pwa/`.
- `src/pwa/registro.ts` registra el SW (module `virtual:pwa-register`) con
  recarga en actualización. `web/src/vite-env.d.ts` declara el módulo virtual.

### Tema (`app/theme.ts`)

Tema MUI claro con paleta propia (verde pino como primario, terracota como
secundario, crema de fondo) y tipografía Open Sans (texto) + Prompt (títulos),
vía `@fontsource`. Botones sin elevación, inputs redondeados: es el tono visual
de la casa, no una copia de Caserito.

## Flujo de datos y manejo de errores

Página → hook react-query (o AuthContext) → `features/*/api.ts` → `lib/http.ts`
→ backend. `X-Correlation-ID` acompaña la petición y vuelve en la respuesta; un
`ApiError` 401 dispara el refresh single-flight y el reintento. Errores de
validación del backend (400 con errores por campo) se muestran en el formulario
donde aplican; los 404/409 genéricos se muestran como alerta con el `title` del
ProblemDetails; un 500 muestra un mensaje genérico y el correlation ID para
soporte. Nunca se loguea el cuerpo ni el token (anti-PII): el `ApiError` solo
transporta status, `code` no-PII y correlation ID.

## Testing y calidad

- Vitest 4 + Testing Library + jest-dom, jsdom, `globals: true`.
- Prioridades (spec del subproyecto 2): cliente HTTP (refresh-on-401
  single-flight, correlation ID, reintento único) y guardas de rol/ruta; luego
  sesión, login y las pantallas clave.
- Los tests de HTTP mockean `globalThis.fetch` (sin MSW; paridad de deps con
  Caserito).
- Puerta: `verify.mjs` suma `Frontend lint` (`eslint . && prettier --check .`),
  `Frontend build` (`tsc -b && vite build`) y `Frontend tests` (`vitest run`),
  ejecutados con `cwd: web/`. `ci.yml`: paso `npm ci` en `web/` para el job
  `calidad` (que corre la puerta entera) y un job `frontend` propio
  (`npm ci && lint && build && test`).

## Desviaciones conocidas respecto de Caserito

- Cliente HTTP escrito a mano con tipos explícitos; Caserito genera el cliente
  desde OpenAPI (el backend de Icarus no publica OpenAPI en este plan).
- Guardas por rol (`RequiereRol`); Caserito por permiso (`RequierePermiso`).
- PWA con `generateSW`; Caserito usa `injectManifest` (push/SignalR, fuera de
  alcance).
- Sin login social, sin registro público, sin SignalR, sin Capacitor/Android:
  la movilidad se resuelve con PWA (spec del subproyecto 2).
- Logout solo del lado del cliente hasta que Identity exponga revocación.

## Fuera de alcance

Pantallas de Gestión Avícola y Control de Acceso, sincronización offline de
datos, push/notificaciones, i18n (solo español), dark mode, alta de usuarios con
confirmación de correo (el backend no la usa), edición/borrado de clientes
distinto de suspender/reactivar, listado de usuarios (la API no lo expone),
endpoint de logout en el backend, y cualquier cambio de backend.

## Notas para el plan

- Nada de `web/` debe tocar el backend: sin CORS, sin prefijos nuevos, sin
  endpoints extra.
- Las versiones de las dependencias del `package.json` se toman de Caserito
  (paridad) y se verifican con un `npm install` real antes de escribir el plan.
- El `build` de la puerta exige Node 22 (CI ya lo usa) y `tsc -b` con
  `noUnusedLocals`/`noUnusedParameters` activos, como el backend trata los
  warnings como errores.
- El gate de mojibake y el de enlaces aplican a `web/` como al resto: textos en
  español correcto, UTF-8 sin BOM, enlaces relativos válidos en los `.md`.
