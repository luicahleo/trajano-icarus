# Frontend web

Aplican primero las reglas del `AGENTS.md` raíz y las de `docs/ai/`. Este
archivo define el estándar local de `web/`.

## Stack

- React 19 + TypeScript estricto (sin `any` ni aserciones inseguras), Vite 8.
- MUI 9 (`@mui/material`, `@mui/icons-material`) con Emotion; temas y tokens en
  `src/app/theme.ts`.
- TanStack Query 5 para estado remoto, React Hook Form + zod para formularios,
  React Router 7 para rutas.
- Vitest + Testing Library (jest-dom) para tests. No añadir otra librería de UI,
  formularios, estado remoto o iconos sin autorización.

## Organización

- `src/lib/`: transporte HTTP (`http.ts`), token en memoria (`session.ts`),
  correlation ID (`correlation.ts`) y contratos (`tipos.ts`). No importa de
  `features/` ni de `app/`.
- `src/features/<modulo>/`: páginas y APIs de negocio por feature. Una feature
  no monta páginas de otra; las rutas se declaran en `src/app/router.tsx`.
- `src/app/`: orquesta — router, `AppLayout`, providers, tema. Sin lógica de
  negocio.
- Imports relativos (sin alias `@/`), paridad con la referencia estructural de
  Caserito.

## Convenciones

- La sesión es estado de aplicación (`AuthContext`), no estado de servidor: no
  se usa react-query en `features/auth`.
- `queryKey` por parámetros relevantes; invalidar por prefijo coherente tras
  cada mutación.
- Formularios con RHF + zod; el esquema es la validación del cliente (UX), el
  backend sigue siendo la autoridad.
- Textos e identificadores en español correcto, UTF-8 sin BOM.
- Anti-PII: el access token vive solo en memoria (nunca localStorage). Los
  `ApiError` transportan `status`, `code` (title del ProblemDetails) y
  `correlationId`, nunca cuerpos, documentos, identificadores fiscales,
  credenciales ni tokens. No usar `console.log` con respuestas ni formularios.
- Guardas de ruta: `ProtectedRoute` (autenticado) y `RequiereRol` (rol); ocultar
  UI no sustituye la autorización del backend.

## Comandos

Ejecutar desde `web/`:

```bash
npm run lint
npm run build
npm run test
npm run format:check
```

Durante el cambio, correr primero la prueba dirigida; `build` al integrar rutas,
contrato, tema o configuración de Vite/PWA. La puerta completa del repo es
`./verify.sh` / `./verify.ps1` (Docker corriendo para los tests del backend).
