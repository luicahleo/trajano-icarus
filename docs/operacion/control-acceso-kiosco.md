# Control de acceso — despliegue del kiosco

Contrato de despliegue del módulo `ControlAcceso`. La configuración productiva
la aplica el agenteVPS; este documento describe **qué** debe cumplirse, no un
archivo de proxy local. Diseño de referencia:
[spec](../superpowers/specs/2026-09-25-control-acceso-asistencia-design.md) y
[plan de fase 1](../superpowers/plans/2026-09-25-control-acceso-fase1.md).

## Alcance

- Un único kiosco web por cliente, offline prohibido: siempre online.
- La aplicación administrativa (PWA) y el kiosco comparten API pero **no** la
  sesión: el kiosco usa una cookie restringida, nunca un token administrativo.
- A0 (contrato real de ARGOS con prueba de vida) sigue pendiente. Mientras no
  se fije, el proveedor biométrico por defecto falla cerrado y el enrolamiento y
  el kiosco no son productivos.

## Dominio y origen

- `ControlAcceso:Kiosco:OrigenesPermitidos` enumera el origen exacto del kiosco
  (por ejemplo `https://<cliente>.kiosco.icarus...`). Un `Origin` distinto se
  rechaza con 403 aunque la cookie viaje.
- El shell del kiosco se publica como `kiosco.html` con su propio bundle
  (`kiosco-*.js`) y **no** entra en el precache del service worker de la PWA.
- El kiosco no registra service worker ni cola offline: no hay IndexedDB.

## Rutas permitidas

El proxy inverso solo debe exponer al origen del kiosco las rutas necesarias:

- `POST /api/control-acceso/kiosco/sesion` (activación).
- `GET /api/control-acceso/kiosco/sesion` (recuperación de sesión).
- `DELETE /api/control-acceso/kiosco/sesion` (salida con reautenticación).
- `POST /api/control-acceso/kiosco/marcaciones`
- `POST /api/control-acceso/kiosco/marcaciones/{propuestaId}/confirmar`

Cualquier otra ruta de administración queda fuera de ese origen.

## Cookies y antiforgery

- Cookie `icarus_kiosco`, **host-only** (sin `Domain`), `HttpOnly`, `Secure` y
  `SameSite=Strict`; el valor en claro solo viaja en la cookie y en la base solo
  se conserva su hash.
- Las mutaciones del kiosco exigen el encabezado `X-Icarus-Kiosco` y validan el
  `Origin` contra la allowlist. Sin encabezado o con origen hermano: 403.
- `no-store` en las respuestas del kiosco; la API de negocio no expone el token
  administrativo en ese origen.

## Sesión

- Activación con credenciales verificadas por Identity y elegibilidad del
  módulo; reemplaza transaccionalmente la sesión vigente del tenant.
- Expiración absoluta (`ControlAcceso:Kiosco:HorasVigencia`) y revocación local.
- La recuperación tras reiniciar el navegador/Android comprueba online la
  sesión; no reactiva sesiones revocadas o vencidas.
- La salida del modo kiosco exige reautenticación del mismo cliente.
- Rate limiting de activación por IP (`MaximoActivacionesPorMinuto`).

## ARGOS

- ARGOS se consume por la **red interna**; nunca es un servicio público del
  navegador. La API actúa como frontera: limita tamaño/formato, aplica timeout y
  traduce errores a códigos genéricos sin identidades ni puntuaciones.
- El nuevo flujo no consulta ICARUS legacy, no usa base de datos ni volumen
  biométrico en ARGOS y no conserva imágenes ni plantillas entre peticiones.
- Las plantillas cifradas se custodian en Trajano-Icarus. Las claves
  (`ControlAcceso:Plantillas:ClaveCifradoBase64`) se administran fuera de SQL y
  de git, con rotación, restauración y tratamiento de respaldos.

## Anti-PII

- Ningún log, traza, mensaje de error ni diagnóstico del frontend contiene
  imágenes, vectores, nombres, documentos, referencias faciales ni registros
  nominales de acceso. Se verifican centinelas sintéticos en el pipeline real
  (`PrivacidadAccesoTests`, `privacidadKiosco.test.tsx`).
- El resultado del kiosco muestra el nombre de forma efímera y lo limpia; no se
  escribe en el registro de idempotencia, caché ni diagnósticos.

## Verificación de regresión

Antes de publicar: login normal, refresh, roles, funcionalidades y offline
avícola siguen verdes con `./verify.ps1` (Docker para los tests de integración).

## Pendientes de operación

- A0: fijar PAD, formato y aceptación del contrato ARGOS antes de considerar el
  kiosco productivo.
- Plataforma Android dedicada (Android Enterprise/EMM) y arranque automático;
  Windows no es plataforma del kiosco en esta fase.
- Ensayo en hardware real (cámara, PAD, luz, latencia) y arranque tras reinicio
  con sesión vigente; usar entorno de prueba para mover el reloj lógico.
