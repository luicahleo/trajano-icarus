# Frontend de Icarus

PWA (Vite + React + TypeScript + MUI) que consume la API del backend del
subproyecto 2 (módulos Identity y Clientes). El plan de construcción está en
[`docs/superpowers/plans/2026-08-14-sp2-4-frontend-pwa.md`](../docs/superpowers/plans/2026-08-14-sp2-4-frontend-pwa.md)
y su diseño en
[`docs/superpowers/specs/2026-08-14-sp2-4-frontend-pwa-design.md`](../docs/superpowers/specs/2026-08-14-sp2-4-frontend-pwa-design.md).

## Entorno

- Node >= 22.
- API de Icarus corriendo en el puerto `8080` (dev). Dos formas de levantarla:
  - `docker compose -f docker-compose.dev.yml up -d` desde la raíz del repo, o
  - `dotnet run` desde `Icarus/src/Host/Icarus.Host`.
- Docker corriendo para los tests de integración del backend (puerta).

## Arranque local

```bash
npm install
npm run dev
```

El servidor de Vite reescribe `/api/*` hacia la API real
(`http://localhost:8080/*`, configurable con `VITE_API_PROXY_TARGET`), así que
el frontend siempre llama bajo `/api` y no hay CORS.

### Cuentas semilla dev/test

Sistema cerrado (roles): `admin@icarus.test`, `soporte@icarus.test`,
`cliente@icarus.test`, `trabajador@icarus.test`. La contraseña depende de cómo
se levante la API:

| Forma de levantar | Contraseña |
|---|---|
| `dotnet run` con `appsettings.Development.json` | `Semilla-Dev-1234` |
| `docker compose -f docker-compose.dev.yml up -d` | `Solo-Desarrollo-123` |

El cliente semilla «Granja Demo S.A.C.» tiene el módulo `GestionAvicola` y un
trabajador demo (datos ficticios).

## Comandos de calidad

```bash
npm run lint        # eslint (flat config)
npm run build       # tsc -b && vite build (incluye typecheck)
npm run test        # vitest run
npm run format:check
```

La puerta de calidad del repo (`./verify.sh` / `./verify.ps1`) corre estos tres
comandos como gates de frontend además de los del backend.

## Iconos PWA

Los iconos placeholder de `public/pwa/` se generan con Node puro:

```bash
node scripts/generar-iconos.mjs
```
