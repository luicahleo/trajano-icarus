# Subproyecto 2 — Plan 4: Frontend PWA bajo `web/`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Crear el frontend PWA bajo `web/` (Vite + React + TypeScript + MUI, paridad con Caserito) que consume la API del plan 3: cliente HTTP con correlation ID y refresh-on-401, sesión con token en memoria restaurada por cookie, guardas de rol, navegación por rol y las pantallas de clientes (admin), usuarios (admin) y trabajadores (admin y cliente). El frontend entra en la puerta de calidad (`verify.mjs`) y en CI desde este plan.

**Architecture:** SPA React bajo `web/` (spec: `docs/superpowers/specs/2026-08-14-sp2-4-frontend-pwa-design.md`). Estructura por features (`features/auth`, `features/admin/clientes`, `features/admin/usuarios`, `features/trabajadores`), transporte y tipos en `lib/`, orquestación en `app/` (router, shell, providers, tema) y `pwa/`. Referencia estructural de Caserito (`repos/dev_Caserito/web`), nunca copia de código. Sin cambios de backend: la API se consume bajo `/api` con proxy de Vite en dev; sin CORS.

**Tech Stack:** React 19.2, Vite 8.1, TypeScript 6.0, MUI 9.2 (@mui/material, @mui/icons-material, @emotion/react/styled), react-router-dom 7.18, @tanstack/react-query 5.101, react-hook-form 7.81 + @hookform/resolvers 5.4 + zod 4.4, vite-plugin-pwa 1.3, Vitest 4 + Testing Library, ESLint 10 (flat) + Prettier 3.9. Versiones tomadas de Caserito (paridad) y verificadas con un `npm install` real en el Task 1.

## Global Constraints

- Textos e identificadores en español correcto, UTF-8 sin BOM. Nunca mojibake. Los `.md` de `web/` también pasan el gate de enlaces y el de mojibake de la puerta.
- Anti-PII: el access token vive solo en memoria (nunca localStorage). Los `ApiError` transportan status, `code` (title del ProblemDetails) y correlation ID, nunca cuerpos, documentos, identificadores fiscales, credenciales ni tokens. El frontend no loguea nada de eso.
- Contrato de credencial: el login usa `{ email, contrasena }` (no `password`), espejo de `IniciarSesionCommand`.
- Un test que nunca se vio en rojo no prueba nada: cada test se corre primero en rojo por el motivo correcto (compilación o aserción).
- Cada commit corre `./verify.sh` (Docker corriendo, Testcontainers). Prohibido `--no-verify`. Hasta el Task 10 la puerta no incluye frontend: los tasks de `web/` corren además `npm run lint`, `npm run test` y `npm run build` desde `web/`.
- Commits en `develop`, directos, mensaje en español estilo conventional commits.
- No ampliar el alcance: nada de backend, nada de push/offline/i18n/dark mode. Lo fuera de alcance está en el spec.
- Las versiones de dependencias se toman de Caserito y se congelan en `package.json`; si un `npm install` real del Task 1 las rechaza, se ajusta la versión mínima compatible y se anota la desviación en el plan.
- El token de access expira en 15 min; el refresh (cookie) en 7 días (OpcionesJwt). Los tests usan estos hechos sin depender del reloj.

---

### Task 1: Andamiaje de `web/` (Vite + React + TS + MUI + tooling)

**Files:**
- Create: `web/` (scaffold oficial react-ts de Vite)
- Overwrite: `web/package.json` (versiones pineadas)
- Overwrite: `web/vite.config.ts` (react, proxy `/api`, vitest jsdom; sin PWA aún)
- Overwrite: `web/index.html` (lang es, título Icarus, theme-color)
- Overwrite: `web/src/main.tsx`, `web/src/App.tsx`
- Create: `web/src/app/theme.ts`, `web/src/app/providers.tsx`
- Create: `web/src/test/setup.ts`, `web/src/test/smoke.test.ts`
- Create: `web/.prettierrc.json`
- Delete: cruft del template (`src/App.css`, `src/assets/`, `public/vite.svg`, `src/index.css` si lo crea)

**Interfaces:**
- Consumes: nada.
- Produces: el proyecto base compilable con MUI y Vitest; lo usan todos los tasks siguientes. `app/providers.tsx` hoy solo envuelve en `QueryClientProvider` (el `AuthProvider` llega en el Task 3).

- [ ] **Step 1: Scaffold del template oficial**

Run (directorio raíz del repo, `web/` no debe existir):

```bash
npm create vite@latest web -- --template react-ts
```

Expected: estructura base react-ts de Vite 8 (tsconfig de proyecto/referencias, `eslint.config.js`, `.gitignore` propio con `dist`/`node_modules`). No debe instalar dependencias. Si el template cambió y omite `eslint.config.js`, crearlo según el de Caserito (`web/eslint.config.js` de `repos/dev_Caserito/web`).

- [ ] **Step 2: package.json con versiones pineadas (paridad Caserito)**

Reemplazo completo de `web/package.json`:

```json
{
  "name": "web",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "engines": { "node": ">=22" },
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "lint": "eslint .",
    "format": "prettier --write .",
    "format:check": "prettier --check .",
    "typecheck": "tsc -b --noEmit",
    "test": "vitest run"
  },
  "dependencies": {
    "@emotion/react": "^11.14.0",
    "@emotion/styled": "^11.14.1",
    "@fontsource/open-sans": "^5.3.0",
    "@fontsource/prompt": "^5.3.0",
    "@hookform/resolvers": "^5.4.0",
    "@mui/icons-material": "^9.2.0",
    "@mui/material": "^9.2.0",
    "@tanstack/react-query": "^5.101.2",
    "react": "^19.2.7",
    "react-dom": "^19.2.7",
    "react-hook-form": "^7.81.0",
    "react-router-dom": "^7.18.1",
    "zod": "^4.4.3"
  },
  "devDependencies": {
    "@eslint/js": "^10.0.1",
    "@testing-library/dom": "^10.4.1",
    "@testing-library/jest-dom": "^6.9.1",
    "@testing-library/react": "^16.3.2",
    "@testing-library/user-event": "^14.6.1",
    "@types/node": "^24.13.2",
    "@types/react": "^19.2.17",
    "@types/react-dom": "^19.2.3",
    "@vitejs/plugin-react": "^6.0.3",
    "eslint": "^10.7.0",
    "eslint-config-prettier": "^10.1.8",
    "eslint-plugin-react-hooks": "^7.1.1",
    "eslint-plugin-react-refresh": "^0.5.3",
    "globals": "^17.7.0",
    "jsdom": "^29.1.1",
    "prettier": "^3.9.5",
    "typescript": "~6.0.2",
    "typescript-eslint": "^8.64.0",
    "vite": "^8.1.1",
    "vite-plugin-pwa": "^1.3.0",
    "vitest": "^4.1.10"
  }
}
```

`.prettierrc.json`:

```json
{
  "printWidth": 100,
  "semi": true,
  "singleQuote": true,
  "trailingComma": "all"
}
```

- [ ] **Step 3: vite.config.ts, index.html y tree base**

`web/vite.config.ts`:

```ts
/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const apiTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8080';

export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    proxy: {
      // El backend sirve bajo la raíz sin prefijo (spec): el frontend usa la base
      // /api y el proxy reescribe a la API real (compose la publica en :8080).
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        rewrite: (p) => p.replace(/^\/api/, ''),
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    maxWorkers: 2,
  },
});
```

`web/index.html`:

```html
<!doctype html>
<html lang="es">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
    <meta name="theme-color" content="#1B5E20" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Icarus</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`web/src/main.tsx`:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { CssBaseline, ThemeProvider } from '@mui/material';
import '@fontsource/open-sans/latin-400.css';
import '@fontsource/open-sans/latin-600.css';
import '@fontsource/open-sans/latin-700.css';
import '@fontsource/prompt/latin-600.css';
import '@fontsource/prompt/latin-700.css';
import { theme } from './app/theme';
import App from './App';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <App />
    </ThemeProvider>
  </StrictMode>,
);
```

`web/src/app/theme.ts` (paleta propia de Icarus; verde pino primario, terracota secundario, crema de fondo):

```ts
import { createTheme } from '@mui/material/styles';

const colores = {
  pino: '#1B5E20',
  pinoOscuro: '#124316',
  pinoClaro: '#DCE8DC',
  terracota: '#D75A2D',
  terracotaOscura: '#AC3F1B',
  crema: '#F8F6F1',
  papel: '#FFFEFC',
  grafito: '#1D2924',
  salvia: '#5E6B64',
  borde: '#DEDCD5',
  blanco: '#FFFFFF',
} as const;

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: colores.pino,
      dark: colores.pinoOscuro,
      light: colores.pinoClaro,
      contrastText: colores.blanco,
    },
    secondary: {
      main: colores.terracota,
      dark: colores.terracotaOscura,
      contrastText: colores.blanco,
    },
    background: { default: colores.crema, paper: colores.papel },
    text: { primary: colores.grafito, secondary: colores.salvia },
    divider: colores.borde,
  },
  typography: {
    fontFamily: '"Open Sans", Arial, sans-serif',
    h1: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 700, letterSpacing: '-0.03em' },
    h2: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 700, letterSpacing: '-0.025em' },
    h3: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 700, letterSpacing: '-0.02em' },
    h4: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 600, letterSpacing: '-0.02em' },
    h5: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 600, letterSpacing: '-0.015em' },
    h6: { fontFamily: '"Prompt", "Open Sans", sans-serif', fontWeight: 600, letterSpacing: '-0.01em' },
    button: { fontWeight: 700, textTransform: 'none' },
  },
  shape: { borderRadius: 12 },
  components: {
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: {
        root: {
          borderRadius: '12px',
          minHeight: '40px',
          '&:active': { transform: 'translateY(1px)' },
          '@media (prefers-reduced-motion: reduce)': { transition: 'none' },
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: { root: { borderRadius: '12px', backgroundColor: colores.papel } },
    },
    MuiCard: { styleOverrides: { root: { borderRadius: '16px', backgroundImage: 'none' } } },
    MuiPaper: { styleOverrides: { root: { backgroundImage: 'none' } } },
  },
});
```

`web/src/app/providers.tsx`:

```tsx
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient();

export function AppProviders({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
```

`web/src/App.tsx` (placeholder hasta el Task 4):

```tsx
import { Box, Typography } from '@mui/material';
import { AppProviders } from './app/providers';

export default function App() {
  return (
    <AppProviders>
      <Box sx={{ p: 4 }}>
        <Typography variant="h4">Icarus</Typography>
      </Box>
    </AppProviders>
  );
}
```

`web/src/test/setup.ts`:

```ts
import '@testing-library/jest-dom';
```

`web/src/test/smoke.test.ts`:

```tsx
import { render, screen } from '@testing-library/react';
import App from '../App';

test('la app monta sin romper', () => {
  render(<App />);
  expect(screen.getByRole('heading', { name: 'Icarus' })).toBeInTheDocument();
});
```

- [ ] **Step 4: Instalación real y verificación**

Run:

```bash
npm install
npm run lint
npm run build
npm run test
```

Expected: `npm install` sin ERESOLVE; `lint` sin errores; `build` (`tsc -b && vite build`) sin errores de tipos; `test` con el smoke en verde. Si el template trae un `eslint.config.js` cuya config rompe algo, ajustar solo el mínimo para que pase.

- [ ] **Step 5: Verificar la puerta y commit**

```bash
./verify.sh
git add web
git commit -m "chore(web): andamiaje de la PWA (Vite + React + TS + MUI + Vitest)"
```

Nota: hasta el Task 10 la puerta no incluye frontend; el `./verify.sh` debe seguir verde porque el backend no cambió.

---

### Task 2: `lib/` — correlation ID, sesión en memoria, cliente HTTP y contratos de la API

**Files:**
- Create: `web/src/lib/correlation.ts`
- Create: `web/src/lib/session.ts`
- Create: `web/src/lib/http.ts`
- Create: `web/src/lib/tipos.ts`
- Create: `web/src/features/auth/api.ts`
- Create: `web/src/features/admin/clientes/api.ts`
- Create: `web/src/features/trabajadores/api.ts`
- Test: `web/src/lib/http.test.ts`
- Test: `web/src/lib/correlation.test.ts`

**Interfaces:**
- Consumes: nada de `features/` ni `app/`.
- Produces (los usan AuthContext y todas las páginas):

```ts
// lib/tipos.ts
export type Rol = 'Administrador' | 'SoporteTecnico' | 'Cliente' | 'Trabajador';
export type Modulo = 'GestionAvicola' | 'ControlAcceso';

export interface SesionInfo { accessToken: string; expiraEnSegundos: number; }
export interface UsuarioActual { usuarioId: string; rol: Rol; clienteId: string | null; }
export interface ClienteResumen {
  id: string; razonSocial: string; identificadorFiscal: string; estaActivo: boolean; modulos: Modulo[];
}
export interface TrabajadorResumen {
  id: string; nombre: string; documentoIdentidad: string; cargo: string;
  fechaIngreso: string; fechaCese: string | null;
}
```

`lib/session.ts` (token solo en memoria, patrón Caserito): `getAccessToken/setAccessToken/clearAccessToken`.

`lib/correlation.ts`: un correlation ID estable por pestaña en `sessionStorage`, regenerable en login:

```ts
const CLAVE = 'icarus-correlation-id';

export function obtenerCorrelationId(): string {
  const actual = sessionStorage.getItem(CLAVE);
  if (actual) return actual;
  const nuevo = crypto.randomUUID();
  sessionStorage.setItem(CLAVE, nuevo);
  return nuevo;
}

export function renovarCorrelationId(): string {
  const nuevo = crypto.randomUUID();
  sessionStorage.setItem(CLAVE, nuevo);
  return nuevo;
}
```

`lib/http.ts`: `ApiError { status, code, correlationId }` + `peticion<T>({ ruta, metodo, cuerpo })` con Bearer, correlation header, `credentials: 'include'`, 401→refresh single-flight con reintento único (las rutas `/identidad/sesion` y `/identidad/sesion/renovar` nunca reintentan) y parseo de 204 como `undefined`. El `ApiError` lee `code` del `title` del ProblemDetails (puede venir en el cuerpo `application/problem+json`) y el `correlationId` del header `X-Correlation-ID`. Exporta además `renovarSesion(): Promise<boolean>` para el arranque del AuthContext (Task 3). Snippet esencial:

```ts
// (helpers) URL completa contra window.location.origin; conHeaders(init, cuerpo)
//   -> Headers con X-Correlation-ID, Authorization Bearer si hay token,
//      Content-Type json si hay cuerpo; credentials: 'include'.
// (single-flight)
let renovacionEnCurso: Promise<boolean> | null = null;
async function renovarSesionInterna(): Promise<boolean> {
  renovacionEnCurso ??= (async () => {
    const r = await fetch(URL('/identidad/sesion/renovar'), {
      method: 'POST', credentials: 'include',
      headers: { 'X-Correlation-ID': obtenerCorrelationId() },
    });
    if (!r.ok) return false;
    try {
      const datos = (await r.json()) as SesionInfo;
      setAccessToken(datos.accessToken);
      return true;
    } catch { return false; }
  })().finally(() => { renovacionEnCurso = null; });
  return renovacionEnCurso;
}

export async function renovarSesion(): Promise<boolean> { return renovarSesionInterna(); }

export async function peticion<T>(o: { ruta: string; metodo?: 'GET' | 'POST' | 'PUT' | 'DELETE'; cuerpo?: unknown }): Promise<T> {
  const { ruta, metodo = 'GET', cuerpo } = o;
  const original = new Request(URL(ruta), conHeaders({ method: metodo }, cuerpo));
  const reintentable = !ES_RUTA_SESION(ruta);
  let respuesta = await fetch(original);

  if (respuesta.status === 401 && reintentable && (await renovarSesionInterna())) {
    respuesta = await fetch(original.clone());
  } else if (respuesta.status === 401 && reintentable) {
    clearAccessToken();
  }

  if (!respuesta.ok) throw await errorDesde(respuesta);
  if (respuesta.status === 204) return undefined as T;
  return (await respuesta.json()) as T;
}
```

`features/auth/api.ts` (contratos exactos): `iniciarSesion({ email, contrasena })` → llama `peticion<SesionInfo>` a `/identidad/sesion`, hace `renovarCorrelationId()` y `setAccessToken`; `obtenerMe()` → `peticion<UsuarioActual>` a `/identidad/me`; `crearUsuario(datos)` → `peticion<{ id: string }>` a `/identidad/usuarios` (POST).

`features/admin/clientes/api.ts`: `listarClientes()`, `crearCliente({ razonSocial, identificadorFiscal })` → `{ id }`, `suspenderCliente(id)`, `reactivarCliente(id)`, `definirModulos(id, modulos)` → `PUT /clientes/{id}/modulos` con cuerpo `{ modulos }`.

`features/trabajadores/api.ts`: `listarTrabajadores(clienteId)`, `crearTrabajador(clienteId, { nombre, documentoIdentidad, cargo, fechaIngreso })` → `{ id }`, `cesarTrabajador(id, fechaCese)` → `POST /clientes/trabajadores/{id}/cese` con `{ fechaCese }`, `desactivarTrabajador(id)` → `DELETE /clientes/trabajadores/{id}`.

- [ ] **Step 1: Escribir los tests en rojo**

`web/src/lib/correlation.test.ts`:

```ts
describe('correlation', () => {
  beforeEach(() => sessionStorage.clear());

  test('genera y reutiliza el mismo id dentro de la pestaña', () => {
    const a = obtenerCorrelationId();
    expect(obtenerCorrelationId()).toBe(a);
  });

  test('renovarCorrelationId cambia el id', () => {
    const a = obtenerCorrelationId();
    expect(renovarCorrelationId()).not.toBe(a);
  });
});
```

`web/src/lib/http.test.ts` (mock de `globalThis.fetch`; el esqueleto: cada test configura `fetch` con respuestas y llama `peticion`):

```ts
function respuesta(status: number, cuerpo?: unknown, headers: Record<string, string> = {}) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json', ...headers },
  });
}

describe('peticion', () => {
  beforeEach(() => { setAccessToken(null); vi.restoreAllMocks(); });

  test('inyecta correlation ID y Bearer en todas las peticiones', async () => {
    const fetchMock = vi.fn().mockResolvedValue(respuesta(200, { ok: true }));
    setAccessToken('tok');
    vi.stubGlobal('fetch', fetchMock);

    await peticion<{ ok: boolean }>({ ruta: '/clientes' });

    const [request] = fetchMock.mock.calls[0];
    expect(new Headers(request.headers).get('X-Correlation-ID')).toBe(obtenerCorrelationId());
    expect(new Headers(request.headers).get('Authorization')).toBe('Bearer tok');
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  test('401 en ruta de negocio renueva una vez y reintenta', async () => {
    setAccessToken('viejo');
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(respuesta(401, { title: 'No autorizado' }))          // original
      .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' }))            // renovar
      .mockResolvedValueOnce(respuesta(200, { id: 'c1' })));                      // reintento

    const datos = await peticion<{ id: string }>({ ruta: '/clientes', metodo: 'POST', cuerpo: {} });

    expect(datos.id).toBe('c1');
    expect(getAccessToken()).toBe('nuevo');
  });

  test('401 sin renovación posible limpia el token y lanza ApiError', async () => {
    setAccessToken('viejo');
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(respuesta(401, { title: 'No autorizado' }))
      .mockResolvedValueOnce(respuesta(401)));

    await expect(peticion({ ruta: '/clientes' })).rejects.toMatchObject({ status: 401 });
    expect(getAccessToken()).toBeNull();
  });

  test('las rutas de sesión nunca reintentan por 401', async () => {
    setAccessToken('viejo');
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(401, { title: 'No autorizado' })));

    await expect(peticion({ ruta: '/identidad/sesion', metodo: 'POST', cuerpo: {} }))
      .rejects.toMatchObject({ status: 401 });
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  test('204 devuelve undefined', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(respuesta(204)));
    await expect(peticion({ ruta: '/clientes/1/suspender', metodo: 'POST' })).resolves.toBeUndefined();
  });

  test('el error expone title del ProblemDetails y correlation ID del header', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      respuesta(409, { title: 'Conflicto con el estado actual' }, { 'X-Correlation-ID': 'abc-123' })));

    const error = await peticion({ ruta: '/clientes', metodo: 'POST', cuerpo: {} }).catch((e) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(409);
    expect(error.code).toBe('Conflicto con el estado actual');
    expect(error.correlationId).toBe('abc-123');
  });
});
```

Nota: la renovación single-flight en estos tests ocurre a través del `fetch` simulado; el 401 del original dispara `renovarSesionInterna` que usa el mismo `fetch`.

- [ ] **Step 2: Correr y verificar rojo**

Run: `npm run test`
Expected: FALLA de compilación (`http.ts`, `correlation.ts`, `tipos.ts` no existen) o aserciones en rojo si faltan.

- [ ] **Step 3: Implementar** los cinco módulos de `lib/` y los tres `api.ts` según las Interfaces.

- [ ] **Step 4: Correr y verificar verde**

```bash
npm run test
npm run lint
npm run build
```

Expected: PASS (correlation + http), lint y build sin errores.

- [ ] **Step 5: Verificar la puerta y commit**

```bash
./verify.sh
git add web
git commit -m "feat(web): cliente HTTP con refresh-on-401, correlation ID y contratos de la API"
```

---

### Task 3: Sesión — AuthContext, guardas de rol y arranque del provider

**Files:**
- Create: `web/src/features/auth/AuthContext.tsx`
- Create: `web/src/features/auth/ProtectedRoute.tsx`
- Create: `web/src/features/auth/RequiereRol.tsx`
- Modify: `web/src/app/providers.tsx` (agregar `AuthProvider`)
- Test: `web/src/features/auth/AuthContext.test.tsx`
- Test: `web/src/features/auth/ProtectedRoute.test.tsx`
- Test: `web/src/features/auth/RequiereRol.test.tsx`

**Interfaces:**
- Consumes: `lib/http.ts` (`renovarSesion`), `lib/session.ts`, `lib/tipos.ts`, `features/auth/api.ts` (`obtenerMe`, `iniciarSesion`).
- Produces:

```tsx
// AuthContext expone
interface EstadoAuth {
  usuario: UsuarioActual | null;
  estaAutenticado: boolean;
  cargando: boolean;
  rol: Rol | null;
  clienteId: string | null;
  tieneRol: (...roles: Rol[]) => boolean;
  iniciarSesion: (cred: Credenciales) => Promise<void>;
  cerrarSesion: () => void;
}
export function AuthProvider({ children }: { children: ReactNode }): JSX.Element;
export function useAuth(): EstadoAuth;
```

Al montar: si `renovarSesion()` da true, `obtenerMe()` y guarda el usuario; `cargando` baja al final (incluso si la restauración falla). `cerrarSesion` limpia el token en memoria y el estado (limitación conocida del spec: la cookie HttpOnly queda hasta su vencimiento).

`ProtectedRoute`: mientras `cargando` muestra `CircularProgress` centrado; sin `estaAutenticado` → `Navigate to="/login" replace`; con sesión → `children`.

`RequiereRol`: `{ roles, children }`; si `!tieneRol(...roles)` → `Navigate to="/" replace` (la raíz redirige según rol); si no → `children`.

- [ ] **Step 1: Escribir los tests en rojo**

`AuthContext.test.tsx` (mock de fetch): restauración con renovar 200 + me 200 → `estaAutenticado` true y `rol` correcto; renovar 401 → anónimo; `cargando` pasa de true a false en ambos casos. `ProtectedRoute.test.tsx`: sin sesión → `Navigate` a `/login`; con sesión → muestra el hijo; mientras carga → `CircularProgress` y no redirige. `RequiereRol.test.tsx`: rol permitido → hijo; rol ajeno → `Navigate` a `/`.

Esqueleto (se completa al implementar):

```tsx
// ProtectedRoute.test.tsx — con AuthContext real y fetch simulado
const estado = {
  usuario: null, estaAutenticado: false, cargando: true, rol: null, clienteId: null,
  tieneRol: () => false, iniciarSesion: async () => {}, cerrarSesion: () => {},
};
```

Nota: para testear el contexto sin red, los tests envuelven el componente bajo un `AuthProvider` real con `fetch` simulado (reusar el patrón de `http.test.ts`), o inyectan un `AuthContext` de prueba vía un wrapper. Preferir el `AuthProvider` real con fetch simulado: cubre el ciclo completo de restauración.

- [ ] **Step 2: Correr y verificar rojo**

Run: `npm run test`
Expected: FALLA de compilación o aserción (AuthContext no existe).

- [ ] **Step 3: Implementar** AuthContext, guardas y `providers.tsx` con `AuthProvider`.

- [ ] **Step 4: Correr y verificar verde**

```bash
npm run test
npm run lint
npm run build
```

- [ ] **Step 5: Verificar la puerta y commit**

```bash
./verify.sh
git add web
git commit -m "feat(web): sesión con AuthContext, restauración por cookie y guardas de rol"
```

---

### Task 4: Tema, shell, router por rol y páginas base

**Files:**
- Create: `web/src/app/inicioSegunRol.ts`
- Create: `web/src/app/paginasDiferidas.tsx`
- Create: `web/src/app/router.tsx`
- Create: `web/src/app/AppLayout.tsx`
- Create: `web/src/app/InicioPage.tsx`
- Create: `web/src/app/NotFoundPage.tsx`
- Modify: `web/src/App.tsx` (RouterProvider)
- Test: `web/src/app/inicioSegunRol.test.ts`
- Test: `web/src/app/AppLayout.test.tsx`

**Interfaces:**
- Consumes: AuthContext, guardas, theme.
- Produces: rutas por rol con carga diferida. Las páginas de negocio (Tasks 5-8) se montan como placeholders `«Próximamente»` y se reemplazan por las reales en sus tasks.

`app/inicioSegunRol.ts`:

```ts
import type { Rol } from '../lib/tipos';

// Destino de inicio según rol: Administrador ve clientes; Cliente ve sus
// trabajadores; SoporteTecnico y Trabajador caen en el placeholder.
export function inicioSegunRol(rol: Rol): string {
  switch (rol) {
    case 'Administrador': return '/admin/clientes';
    case 'Cliente': return '/trabajadores';
    default: return '/inicio';
  }
}
```

`app/router.tsx` (createBrowserRouter; rutas del spec):

| Ruta | Elemento |
|---|---|
| `/login` | `LoginPage` (placeholder hasta Task 5) |
| `/` | `ProtectedRoute` + componente que redirige con `inicioSegunRol` |
| `/inicio` | `ProtectedRoute` + `InicioPage` |
| `/admin/clientes` | `ProtectedRoute` + `RequiereRol ['Administrador']` + placeholder |
| `/admin/clientes/nuevo` | ídem |
| `/admin/clientes/:id` | ídem |
| `/admin/usuarios/nuevo` | ídem |
| `/clientes/:clienteId/trabajadores` | `ProtectedRoute` + `RequiereRol ['Administrador']` + placeholder |
| `/trabajadores` | `ProtectedRoute` + `RequiereRol ['Cliente']` + placeholder |
| `*` | `NotFoundPage` |

`AppLayout`: `AppBar` con título «Icarus», menú según rol (Administrador: Clientes, Usuarios, Trabajadores; Cliente: Trabajadores; SoporteTecnico/Trabajador: sin enlaces), botón de cerrar sesión y `Outlet`. Carga diferida vía `paginasDiferidas.tsx` (patrón Caserito).

- [ ] **Step 1: Escribir los tests en rojo**

`inicioSegunRol.test.ts`: administrador→`/admin/clientes`; cliente→`/trabajadores`; soporte→`/inicio`; trabajador→`/inicio`.

`AppLayout.test.tsx` (con `AuthProvider` real + fetch simulado para rol): Administrador ve «Clientes», «Usuarios» y «Trabajadores»; Cliente solo «Trabajadores»; el botón de cierre llama `cerrarSesion` (verifica que navega a `/login` y deja anónimo).

- [ ] **Step 2: Correr y verificar rojo**

Run: `npm run test`
Expected: FALLA de compilación (los módulos no existen).

- [ ] **Step 3: Implementar** tema ya presente (Task 1), `inicioSegunRol`, router con placeholders, `AppLayout`, `InicioPage`, `NotFoundPage`, `paginasDiferidas` y `App.tsx` con `RouterProvider`.

- [ ] **Step 4: Correr y verificar verde**

```bash
npm run test
npm run lint
npm run build
```

Expected: router y layout verdes; `build` con las páginas diferidas lazy ok.

- [ ] **Step 5: Verificar la puerta y commit**

```bash
./verify.sh
git add web
git commit -m "feat(web): shell MUI, router por rol y páginas base"
```

---

### Task 5: Pantalla de inicio de sesión

**Files:**
- Modify: `web/src/features/auth/LoginPage.tsx` (reemplaza el placeholder del router)
- Test: `web/src/features/auth/LoginPage.test.tsx`

**Interfaces:**
- Consumes: `iniciarSesion`, `obtenerMe`, `inicioSegunRol`, `useAuth`.
- Produces: formulario `email` + `contrasena` con react-hook-form + zod; en éxito navega a `inicioSegunRol(rol)`; en `ApiError` muestra el `title` genérico del ProblemDetails y, si existe, el correlation ID; mensajes de campo en español.

Esquema zod:

```ts
const esquema = z.object({
  email: z.string().min(1, 'El correo es obligatorio.').email('Correo inválido.'),
  contrasena: z.string().min(1, 'La contraseña es obligatoria.'),
});
```

- [ ] **Step 1: Escribir los tests en rojo**

`LoginPage.test.tsx` (con `AuthProvider` real + fetch simulado):
1. Render: título y los dos campos, botón «Iniciar sesión».
2. Envío válido: llama al endpoint de sesión con `{ email, contrasena }`, restaura `me` y navega a `/admin/clientes` para rol Administrador.
3. Envío vacío: muestra «El correo es obligatorio.» y «La contraseña es obligatoria.» sin llamar a la API.
4. `ApiError` 401 con `title` «No autorizado»: muestra el mensaje genérico y el correlation ID del header (no credenciales).

- [ ] **Step 2: Correr y verificar rojo**

Run: `npm run test`
Expected: FALLA (LoginPage placeholder no tiene el formulario).

- [ ] **Step 3: Implementar** `LoginPage.tsx` con MUI (`TextField`, `Alert`, `Button`), `useForm` + `zodResolver`, manejo de `ApiError` con `snackbar`/`Alert` y `navigate(inicioSegunRol(usuario.rol))`.

- [ ] **Step 4: Correr y verificar verde**

```bash
npm run test
npm run lint
npm run build
```

- [ ] **Step 5: Verificar la puerta y commit**

```bash
./verify.sh
git add web
git commit -m "feat(web): pantalla de inicio de sesión con validación y errores anti-PII"
```

---

### Task 6: Gestión de clientes (Administrador)

**Files:**
- Create: `web/src/features/admin/clientes/ClientesListaPage.tsx`
- Create: `web/src/features/admin/clientes/ClienteNuevoPage.tsx`
- Create: `web/src/features/admin/clientes/ClienteDetallePage.tsx`
- Modify: `web/src/app/router.tsx` (reemplaza placeholders por las páginas reales)
- Test: `ClientesListaPage.test.tsx`, `ClienteNuevoPage.test.tsx`, `ClienteDetallePage.test.tsx`

**Interfaces:**
- Consumes: `features/admin/clientes/api.ts`, react-query (`useQuery`, `useMutation`, invalidación).
- Comportamiento:
  - `ClientesListaPage` (`/admin/clientes`): tabla con razón social, identificador fiscal, estado (activo/suspendido), chips de módulos y acciones: suspender/reactivar (con confirmación), ir al detalle. Botón «Nuevo cliente» → `/admin/clientes/nuevo`.
  - `ClienteNuevoPage` (`/admin/clientes/nuevo`): formulario `razonSocial` (obligatorio, máx 200) e `identificadorFiscal` (obligatorio, máx 32); éxito → navega a la lista; 409 → muestra el `title` genérico.
  - `ClienteDetallePage` (`/admin/clientes/:id`): datos, estado, `Checkbox` por módulo (`GestionAvicola`, `ControlAcceso`) que guardan con `definirModulos` al cambiar, y botones suspender/reactivar.

- [ ] **Step 1: Escribir los tests en rojo** (fetch simulado por endpoint; los contratos de `api.ts` ya existen)

1. Lista: `GET /clientes` con dos clientes → renderiza ambas filas con su estado y módulos; el botón suspender llama `POST /clientes/{id}/suspender` y refresca la lista; «Nuevo cliente» navega.
2. Nuevo: campos vacíos → errores «La razón social es obligatoria.» / «El identificador fiscal es obligatorio.» sin llamar a la API; envío válido → `POST /clientes` con `{ razonSocial, identificadorFiscal }` y navega a la lista; `ApiError` 409 → muestra el `title`.
3. Detalle: `GET /clientes` (para encontrar el registro por `:id`) muestra los datos; alternar un módulo llama `PUT /clientes/{id}/modulos` con la lista completa nueva; suspender/reactivar llaman a su endpoint.

- [ ] **Step 2: Correr y verificar rojo**

Run: `npm run test`
Expected: FALLA (páginas no existen o placeholder).

- [ ] **Step 3: Implementar** las tres páginas con MUI (`Table`, `Chip`, `Dialog` de confirmación, `Checkbox`, `TextField`) y react-query (`queryKey` por lista, invalidación tras mutación).

- [ ] **Step 4: Correr y verificar verde**

```bash
npm run test
npm run lint
npm run build
```

- [ ] **Step 5: Verificar la puerta y commit**

```bash
./verify.sh
git add web
git commit -m "feat(web): gestión de clientes para administradores"
```

---

### Task 7: Alta de usuarios (Administrador)

**Files:**
- Create: `web/src/features/admin/usuarios/api.ts` (envuelve `POST /identidad/usuarios` con los datos del formulario)
- Create: `web/src/features/admin/usuarios/UsuarioNuevoPage.tsx`
- Modify: `web/src/app/router.tsx` (página real)
- Test: `web/src/features/admin/usuarios/UsuarioNuevoPage.test.tsx`

**Interfaces:**
- Consumes: `features/admin/clientes/api.ts` (`listarClientes`) y `features/trabajadores/api.ts` (`listarTrabajadores`) para los selectores dependientes.
- Formulario: `email` (formato correo), `contrasena` (mínimo 12, la exige el backend), `rol` (uno de los cuatro). Si rol ∈ `Cliente|Trabajador` → selector de cliente (`listarClientes`). Si rol `Trabajador` → selector de trabajador del cliente elegido (`listarTrabajadores`). Éxito → mensaje de éxito y reset; `ApiError` 409 → `title` genérico. Nunca se muestran ni loguean credenciales.

- [ ] **Step 1: Escribir los tests en rojo**

`UsuarioNuevoPage.test.tsx` (fetch simulado):
1. Validación: sin rol/email/contrasena corta → errores en español sin llamar a la API.
2. Rol `Administrador` → no muestra selectores de cliente/trabajador.
3. Rol `Cliente` → aparece el selector de cliente (de `GET /clientes`) y no el de trabajador.
4. Rol `Trabajador` → seleccionar cliente carga `GET /clientes/{id}/trabajadores` y permite elegir trabajador.
5. Envío válido → `POST /identidad/usuarios` con `{ email, contrasena, rol, clienteId, trabajadorId }` (nulls según rol) y muestra éxito.
6. `ApiError` 409 → muestra el `title` genérico.

- [ ] **Step 2: Correr y verificar rojo**

Run: `npm run test`
Expected: FALLA (página no existe).

- [ ] **Step 3: Implementar** `UsuarioNuevoPage.tsx` y `api.ts` con react-hook-form + zod (esquema condicional por rol), MUI (`Select`, `MenuItem`, `TextField`), react-query para los selectores.

- [ ] **Step 4: Correr y verificar verde**

```bash
npm run test
npm run lint
npm run build
```

- [ ] **Step 5: Verificar la puerta y commit**

```bash
./verify.sh
git add web
git commit -m "feat(web): alta de cuentas de usuario para administradores"
```

---

### Task 8: Gestión de trabajadores (Administrador y Cliente)

**Files:**
- Create: `web/src/features/trabajadores/TrabajadoresPage.tsx`
- Modify: `web/src/app/router.tsx` (página real en `/trabajadores` y `/clientes/:clienteId/trabajadores`)
- Test: `web/src/features/trabajadores/TrabajadoresPage.test.tsx`

**Interfaces:**
- Consumes: `features/trabajadores/api.ts`, `features/admin/clientes/api.ts` (selector del Administrador), `useAuth` (`clienteId`).
- Comportamiento:
  - Rol `Cliente`: el `clienteId` es el propio (`/me`); lista de trabajadores de su empresa.
  - Rol `Administrador`: selector de cliente (`GET /clientes`) y luego su lista.
  - Tabla: nombre, documento de identidad, cargo, fecha de ingreso, fecha de cese (si hay) y acciones: cesar (dialogo con `fechaCese`, validación no futura), desactivar (confirmación; soft delete). Alta con formulario (dialogo o página) `{ nombre, documentoIdentidad, cargo, fechaIngreso }`.

- [ ] **Step 1: Escribir los tests en rojo**

`TrabajadoresPage.test.tsx` (fetch simulado):
1. Rol `Cliente` con `clienteId` propio: `GET /clientes/{id}/trabajadores` y renderiza filas.
2. Rol `Administrador`: primero `GET /clientes`; al elegir un cliente se carga su lista.
3. Alta: formulario válido → `POST /clientes/{clienteId}/trabajadores` y refresca.
4. Cesar: dialog con fecha inválida (futura) → error «La fecha de cese no puede ser futura.»; válida → `POST /clientes/trabajadores/{id}/cese`.
5. Desactivar: confirmación → `DELETE /clientes/trabajadores/{id}`.
6. `ApiError` 409 (documento duplicado) → muestra el `title` genérico sin revelar el documento.

- [ ] **Step 2: Correr y verificar rojo**

Run: `npm run test`
Expected: FALLA (página no existe o placeholder).

- [ ] **Step 3: Implementar** `TrabajadoresPage.tsx` con MUI y react-query; validación de `fechaCese` no futura en el cliente (el dominio también la valida).

- [ ] **Step 4: Correr y verificar verde**

```bash
npm run test
npm run lint
npm run build
```

- [ ] **Step 5: Verificar la puerta y commit**

```bash
./verify.sh
git add web
git commit -m "feat(web): gestión de trabajadores para administradores y clientes"
```

---

### Task 9: PWA — manifest, service worker, iconos y documentación del árbol

**Files:**
- Create: `web/scripts/generar-iconos.mjs`
- Create: `web/public/pwa/pwa-192x192.png`, `pwa-512x512.png`, `pwa-maskable-192x192.png`, `pwa-maskable-512x512.png`
- Modify: `web/vite.config.ts` (plugin `VitePWA` con `generateSW` y manifest en español)
- Create: `web/src/pwa/registro.ts`
- Modify: `web/src/vite-env.d.ts` (referencia `vite-plugin-pwa/client`)
- Modify: `web/src/main.tsx` (importar `registro`)
- Create: `web/README.md`
- Create: `web/AGENTS.md`

**Interfaces:**
- Consumes: nada nuevo del backend.
- Produces: PWA instalable (`generateSW`, `registerType: 'autoUpdate'`, manifest español «Icarus», iconos placeholder), y la documentación local de `web/`.

- [ ] **Step 1: Generar los iconos placeholder (Node puro)**

`web/scripts/generar-iconos.mjs`: escribe PNG válidos (firma + IHDR + IDAT con zlib + IEND, CRC-32 implementado a mano) de un cuadrado sólido del color primario (`#1B5E20`) en 192 y 512, más sus variantes `maskable` (el mismo cuadrado a sangre completa). No usa dependencias. Run:

```bash
node scripts/generar-iconos.mjs
```

Expected: cuatro PNG en `web/public/pwa/` (verificables con `file public/pwa/pwa-192x192.png`).

- [ ] **Step 2: Configurar VitePWA y el registro**

En `vite.config.ts`, agregar el plugin:

```ts
import { VitePWA } from 'vite-plugin-pwa';
// ...
VitePWA({
  registerType: 'autoUpdate',
  strategies: 'generateSW',
  manifest: {
    name: 'Icarus',
    short_name: 'Icarus',
    description: 'Gestión de clientes y trabajadores',
    lang: 'es',
    theme_color: '#1B5E20',
    background_color: '#F8F6F1',
    display: 'standalone',
    start_url: '/',
    icons: [
      { src: 'pwa/pwa-192x192.png', sizes: '192x192', type: 'image/png' },
      { src: 'pwa/pwa-512x512.png', sizes: '512x512', type: 'image/png' },
      { src: 'pwa/pwa-maskable-192x192.png', sizes: '192x192', type: 'image/png', purpose: 'maskable' },
      { src: 'pwa/pwa-maskable-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
    ],
  },
  workbox: { globPatterns: ['**/*.{js,css,html,svg,png,woff2}'] },
})
```

`web/src/pwa/registro.ts`:

```ts
import { registerSW } from 'virtual:pwa-register';

export function instalarServiceWorker(): void {
  registerSW({ immediate: true });
}
```

`web/src/vite-env.d.ts`: agregar `/// <reference types="vite-plugin-pwa/client" />`. `main.tsx`: `instalarServiceWorker()`.

- [ ] **Step 3: Verificar el build PWA**

```bash
npm run build
```

Expected: en `web/dist/` aparecen `manifest.webmanifest` y `sw.js` (y `registerSW` compilado). Ejecutar además `npm run lint` y `npm run test`.

- [ ] **Step 4: README y AGENTS.md de `web/`**

`web/README.md`: cómo levantar el entorno (API: `docker compose -f docker-compose.dev.yml up -d` o `dotnet run` desde `Icarus/`), `npm install`, `npm run dev` (proxy `/api` → `http://localhost:8080`), cuentas semilla dev/test (admin/soporte/cliente/trabajador@icarus.test; contraseña según cómo se levante la API: `Semilla-Dev-1234` por `dotnet run` con `appsettings.Development.json`, `Solo-Desarrollo-123` por compose) y los comandos de la puerta.

`web/AGENTS.md`: convenciones del árbol (alias `@/`, MUI + Emotion, react-query, react-hook-form + zod, `lib/` sin dependencias de `features/`/`app/`, anti-PII: token solo en memoria y errores sin datos personales, comandos `npm run lint/test/build`, puerta `./verify.sh`).

- [ ] **Step 5: Verificar la puerta y commit**

```bash
./verify.sh
git add web
git commit -m "feat(web): PWA con manifest, service worker e iconos"
```

---

### Task 10: Frontend en la puerta de calidad y en CI

**Files:**
- Modify: `quality/verify.mjs` (gates de frontend con `cwd: web/` y shell para npm)
- Modify: `quality/__tests__/verify.test.mjs` (comando esperado por gate: npm para Frontend)
- Modify: `docs/ai/PUERTA_CALIDAD.md` (tabla de gates)
- Modify: `.github/workflows/ci.yml` (`npm ci` en `web/` + job `frontend`)
- Modify: `web/README.md` (mención de los gates) si hace falta

**Interfaces:**
- Consumes: `web/` completo (node_modules presente desde el Task 1).
- Produces: la puerta verifica el frontend (lint, build, tests) igual que el backend; CI los corre en `calidad` (vía verify) y en un job `frontend` propio.

- [ ] **Step 1: Agregar los gates en rojo**

En `quality/verify.mjs`, la lista `GATES` pasa a incluir (tras «Enlaces», antes de «Backend build»):

```js
  { nombre: 'Frontend lint', comando: npm, args: ['run', 'lint'], cwd: web, shell: true },
  { nombre: 'Frontend build', comando: npm, args: ['run', 'build'], cwd: web, shell: true },
  { nombre: 'Frontend tests', comando: npm, args: ['run', 'test'], cwd: web, shell: true },
```

Con los ajustes de cabecera:

```js
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
const raiz = fileURLToPath(new URL('..', import.meta.url));
const web = resolve(raiz, 'web');
// npm en Windows es npm.cmd y necesita shell; dotnet/git no (se mantiene sinShell).
const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';
```

Y el ejecutor del CLI respeta `cwd`/`shell` por gate:

```js
const { cwd = raiz, shell = false } = gate;
const { codigo, duracionMs } = ejecutar(gate.comando, gate.args, { cwd, sinShell: shell ? false : true });
```

Run: `node --test quality/__tests__/verify.test.mjs`
Expected: el test «cada gate se invoca con el comando que le corresponde» FALLA (espera `node` para los nuevos gates `npm`). Es la prueba roja por el motivo correcto.

- [ ] **Step 2: Actualizar el test de la puerta**

En `quality/__tests__/verify.test.mjs`, el test del comando pasa a:

```js
test('cada gate se invoca con el comando que le corresponde', () => {
  for (const gate of GATES) {
    const esperado = gate.nombre.startsWith('Backend')
      ? 'dotnet'
      : gate.nombre.startsWith('Frontend')
        ? 'npm'
        : 'node';
    assert.equal(gate.comando, esperado);
    assert.ok(Array.isArray(gate.args) && gate.args.length > 0);
  }
});
```

Run: `node --test quality/__tests__/verify.test.mjs`
Expected: PASS.

- [ ] **Step 3: Verificar la puerta completa**

```bash
./verify.sh
```

Expected: los tres gates de frontend en verde (lint/build/tests desde `web/`) y el resto sin cambios. Docker corriendo para los tests de integración.

- [ ] **Step 4: Documentar la puerta y el CI**

`docs/ai/PUERTA_CALIDAD.md`: agregar a la tabla las filas `Frontend lint` (`eslint . && prettier --check .`), `Frontend build` (`tsc -b && vite build`) y `Frontend tests` (`vitest run`), y a la sección de ejecución la nota de que los gates de frontend necesitan `npm install` previo en `web/` (Node 22).

`.github/workflows/ci.yml`:
- En el job `calidad`, tras `Setup Node`, agregar:

```yaml
      - name: Instalar dependencias del frontend
        run: npm ci
        working-directory: web
```

- Agregar un job `frontend`:

```yaml
  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7
      - name: Setup Node
        uses: actions/setup-node@820762786026740c76f36085b0efc47a31fe5020 # v7
        with:
          node-version: 22
      - name: Instalar dependencias
        run: npm ci
        working-directory: web
      - name: Lint
        run: npm run lint
        working-directory: web
      - name: Build
        run: npm run build
        working-directory: web
      - name: Tests
        run: npm run test
        working-directory: web
```

- [ ] **Step 5: Commit**

```bash
./verify.sh
git add quality docs/ai/PUERTA_CALIDAD.md .github/workflows/ci.yml web/README.md
git commit -m "build(quality): gates de frontend en la puerta y en CI"
```

---

## Registro de ejecución

- [ ] Todos los tests vistos en rojo por el motivo correcto antes de su verde (Tasks 2-8, 10-Step 1).
- [ ] `./verify.sh` verde antes de cada commit (Docker corriendo).
- [ ] Versiones del `package.json` resueltas por el `npm install` real del Task 1 (sin ERESOLVE); si hubo ajustes, anotarlos aquí:
- [ ] Desviaciones detectadas al ejecutar respecto de este plan (actualizar este documento, nunca maquillar el resultado):
