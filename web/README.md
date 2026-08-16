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

### Arranque en contenedores

El `docker-compose.dev.yml` de la raíz incluye un servicio `web` (Vite en un
contenedor Node 22 con bind mount y HMR) además de SQL Server y la API:

```bash
docker compose -f docker-compose.dev.yml up -d --build
```

Queda en `http://localhost:5173` proxying `/api` hacia el contenedor `api`.

## Probar desde un móvil (entorno PC)

Los scripts `iniciar-pc.ps1` (core) y `iniciar-pc1.ps1` / `iniciar-pc2.ps1` /
`iniciar-pc3.ps1` (wrappers) levantan todo el stack en contenedores y publican
HTTPS en la LAN con un gateway Caddy (`tls internal`, host `<ip>.sslip.io`):

```powershell
.\iniciar-pc1.ps1              # esta máquina es PC1
.\iniciar-pc1.ps1 -Logs        # con logs en vivo
.\iniciar-pc1.ps1 -RecrearDatos -ConfirmarBorradoDatos   # base y volúmenes desde cero
```

Al terminar imprime la URL (`https://<ip-lan>.sslip.io`), la ruta del certificado
de la CA para instalar en el móvil y un aviso de firewall. Los complementos
`estado-pc.ps1 -Perfil pc1` y `detener-pc.ps1 -Perfil pc1` consultan y detienen
el entorno. Detalle del esquema (paridad con Caserito): `docker-compose.pcX.yml`
e `infra/pcX/Caddyfile`.

### Cuentas semilla dev/test

Sistema cerrado (roles): `admin@icarus.test`, `soporte@icarus.test`,
`cliente@icarus.test`, `trabajador@icarus.test`. La contraseña es
`Semilla-Dev-1234` (configuración `Semilla:ContrasenaPrueba` de
`appsettings.Development.json`, la misma que usa el compose con
`ASPNETCORE_ENVIRONMENT=Development`).

El cliente semilla «Granja Demo S.A.C.» tiene el módulo `GestionAvicola` y un
trabajador demo (datos ficticios).

## Observabilidad

- `?debug=1` en una pestaña descarga un JSON con los eventos de esa sesión
  (solo en desarrollo/testing o con `VITE_HABILITAR_DIAGNOSTICO_MANUAL=true`).
- Los errores técnicos se reportan automáticamente a
  `POST /api/diagnosticos/frontend` con breadcrumbs seguros y sin PII.
- `VITE_RELEASE` identifica el despliegue en los reportes (fallback
  `development`).
- Para conservar los source maps como artefacto privado:
  `npm run build:sourcemaps` (los genera ocultos y los extrae a
  `sourcemaps/<release>/`, fuera de `dist`; nunca se sirven públicamente).
- Operación y consultas:
  [`docs/operacion/observabilidad.md`](../docs/operacion/observabilidad.md).

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
