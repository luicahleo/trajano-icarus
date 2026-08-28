# Plan — plantilla maestra aqua

1. [x] Escribir pruebas rojas del contrato de navegación, título contextual, accesibilidad y sesión.
2. [x] Sustituir los tokens principales del tema por la paleta aqua accesible.
3. [x] Extraer la configuración de navegación y construir la navegación responsive reutilizable.
4. [x] Integrar navegación, cabecera contextual, banner y contenido en `AppLayout`.
5. [x] Ejecutar pruebas dirigidas, suite web y puerta de calidad completa.
6. [x] Revisar el diff, cerrar este plan y realizar commit/push a `develop` si todo está verde.

## Archivos previstos

- `web/src/app/theme.ts`
- `web/src/app/theme.test.ts`
- `web/src/app/navegacion.tsx`
- `web/src/app/navegacion.test.tsx`
- `web/src/app/NavegacionPrincipal.tsx`
- `web/src/app/AppLayout.tsx`
- `web/src/app/AppLayout.test.tsx`

## Verificación

```powershell
Set-Location web
npm run test -- src/app/navegacion.test.tsx src/app/AppLayout.test.tsx
npm run test
npm run lint
npm run build
npm run format:check
Set-Location ..
./verify.ps1
```
