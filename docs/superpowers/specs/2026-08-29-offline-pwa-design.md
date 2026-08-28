# Modo offline de la PWA — Diseño

Fecha: 2026-08-29
Estado: propuesto en sesión de brainstorming (pendiente de revisión del usuario)

## Contexto

La PWA (`web/`) hoy es online-only para datos: el service worker
(`vite-plugin-pwa`, `generateSW` en `web/vite.config.ts`) solo precachea
estáticos, `useConexion` detecta la red y `BannerSinConexion` avisa que «no se
pueden guardar registros». En el campo (granjas con cobertura pobre) el
trabajador no puede registrar la recogida ni la mortalidad, que es justo el uso
crítico.

La app móvil IMGA (repo `dev/ICARUS_MOBILE`, análisis completo en
`docs/ai/HANDOFF.md` de 2026-08-29) ya resolvió este problema con SQLite local,
un motor de sync compartido (`OfflineSyncEngine`) y estados por registro. Este
diseño traslada ese modelo a la PWA, adaptado a sus restricciones (token en
memoria, sin SQLite, jsdom para tests).

Base ya existente que el diseño aprovecha:

- **Backend idempotente**: `RegistrarProduccionCommand` y
  `RegistrarMortalidadCommand` aceptan `IdempotencyKey` (Guid) con índice único
  filtrado; un reenvío con la misma clave devuelve el registro original sin
  duplicar. Es la pieza que hace segura la cola offline.
- `useConexion` (`navigator.onLine` + eventos) y `BannerSinConexion` ya
  existen; se extienden, no se reemplazan.
- El frontend ya genera `idempotencyKey` con `crypto.randomUUID()` en cada alta
  (`RegistrarRecogidaDialog`, `RegistrarBajasDialog`).

## Decisiones

### 1. Flujo de guardado: directo si hay red, cola solo al fallar

IMGA persiste siempre en local y sube después. La PWA hace lo contrario a
propósito:

- Si `navigator.onLine` es true, el alta va directo a la API como hoy. Los
  errores de validación del backend (4xx) siguen llegando síncronos al diálogo,
  que es donde el usuario los corrige.
- Si está offline, o si el `fetch` falla por red (online falso positivo,
  intermitencia), la operación se encola en IndexedDB con su `idempotencyKey`
  ya generada y el diálogo cierra con el aviso «Guardado sin conexión: se
  sincronizará al volver la red».
- Los errores 4xx/5xx de la API **nunca** encolan: son rechazos, no falta de
  red. El criterio es «encolar solo ante fallo de transporte».

Se descartó «encolar siempre» (modelo IMGA puro): convertiría los errores de
validación en asíncronos con el diálogo cerrado, peor UX en el 95 % del uso que
es online.

### 2. Cola en IndexedDB sin dependencias nuevas de runtime

Un solo object store (`operaciones`), sin `idb` ni `dexie`: la cola es CRUD
simple y no justifica dependencia (además `web/AGENTS.md` restringe librerías
nuevas). El módulo vive en `web/src/lib/offline/` (no importa de `features/`
ni de `app/`):

- `tipos.ts`: `OperacionPendiente` (ver modelo de datos) y
  `TipoOperacionOffline` = `'produccion.crear' | 'mortalidad.crear'`.
- `almacenCola.ts`: interfaz `AlmacenCola` (agregar, listar pendientes, marcar
  sincronizada, registrar intento fallido, descartar, contar) con dos
  implementaciones: IndexedDB (real) y memoria (tests; jsdom no tiene
  IndexedDB). Para probar la implementación real se añade `fake-indexeddb`
  **solo como devDependency**.
- `motorSincronizacion.ts`: motor genérico. Recibe el almacén, un dispatcher y
  la fuente de conectividad; no conoce la API avícola.

El dispatcher (mapea `TipoOperacionOffline` → llamada de `api.ts` +
invalidación de queries) vive en `web/src/features/avicola/offline.ts`, y el
arranque del motor en `web/src/app/` (providers), respetando la regla de que
`lib/` no importa de features.

### 3. Modelo de la operación pendiente

```ts
interface OperacionPendiente {
  id: string; // uuid local
  tipo: 'produccion.crear' | 'mortalidad.crear';
  galponId: string;
  cuerpo: DatosRecogida | DatosBajas; // incluye idempotencyKey
  estado: 'pendiente' | 'error';
  intentos: number;
  creadoEn: string; // ISO
  proximoIntentoEn: string | null; // ISO; null = ya
}
```

Solo datos de negocio. **Nunca** tokens ni credenciales en IndexedDB
(anti-PII). Los IDs son `string` (Guid serializado), paridad con el backend de
Trajano-Icarus; no se mezcla con los `int` de IMGA.

### 4. Motor de sincronización

Reglas (paridad con `OfflineSyncEngine` de IMGA, verificadas en su código):

- **No bloqueante**: si ya hay un ciclo en curso, retorna de inmediato (flag en
  memoria por pestaña).
- **Lote de 50** por ciclo; verifica conectividad antes de cada envío y corta
  el ciclo si se pierde.
- **Máximo 3 intentos** por operación; al agotarse pasa a estado `error`
  (terminal, requiere acción del usuario).
- **Backoff exponencial** 2^intentos minutos desde el último intento
  (`proximoIntentoEn`).
- Al sincronizar una operación se invalida el prefijo `['avicola']` de
  TanStack Query para refrescar la UI.

Disparadores del ciclo:

1. Evento `online` del navegador.
2. Timer de respaldo de 5 minutos (el evento no siempre dispara con red
   intermitente).
3. Inmediato tras encolar, si hay red (fire-and-forget).

**Token**: si el envío recibe 401 y `renovarSesion()` falla (cookie de refresh
expirada), el motor pausa el ciclo y las operaciones quedan pendientes hasta el
próximo login. Nada de esto persiste el token.

**Multi-pestaña**: no hay coordinación entre pestañas. Dos pestañas pueden
reenviar la misma operación; el backend la absorbe por `IdempotencyKey`. Se
documenta y no se resuelve (YAGNI).

### 5. Caché de lectura mínima

Sin caché, una recarga sin red deja la app vacía aunque el shell cargue desde
el service worker. Se cachea en IndexedDB (store `cacheLectura`, clave = ruta)
solo lo necesario para operar recogida/mortalidad sin red:

- `GET /granjas`, `GET /granjas/{id}/galpones`, `GET /galpones/{id}`.

Cada respuesta exitosa actualiza la caché; ante fallo de red se sirve la caché.
Los datos cacheados se muestran tal cual (el banner ya avisa de la falta de
conexión). Eficiencia, vacunación y el resto de módulos quedan fuera: sin red
muestran el error actual.

### 6. UI

- `BannerSinConexion`: el texto pasa a «Sin conexión: los registros se guardan
  en este dispositivo y se sincronizarán al volver la red», y muestra el
  contador de operaciones pendientes cuando lo hay.
- Contador de pendientes accesible también con conexión (chip en la barra
  superior), con acciones manuales **Reintentar** (resetea intentos y dispara
  el motor) y **Descartar** (con confirmación) por operación en `error`.
- `RegistrarRecogidaDialog` y `RegistrarBajasDialog`: el botón Guardar deja de
  depender de `!online`; al encolar, cierra con snackbar «Guardado sin
  conexión».
- Edición y eliminación de registros siguen deshabilitadas sin conexión (sin
  cambios).

### 7. Pruebas

- Vitest + Testing Library, como el resto de `web/`.
- El motor se prueba contra `AlmacenCola` en memoria: reintentos, backoff,
  estado `error` tras 3 intentos, corte al perder conectividad, pausa ante 401
  no renovable.
- La implementación IndexedDB se prueba con `fake-indexeddb` (devDependency
  nueva, solo tests).
- Diálogos: guardar offline encola y cierra; guardar online con fallo de red
  encola; 4xx no encola y muestra el error.
- TDD: cada test se ve en rojo antes de implementar.

## Fuera de alcance

- Edición offline de registros (IMGA tiene `PendienteEdicion`; aquí se difiere:
  el alta es el caso crítico y la edición offline añade conflicto de
  versiones).
- Vacunación offline (completar/cancelar tareas).
- Background Sync API del service worker (soporte incompleto fuera de Chromium).
- Caché de lectura de eficiencia, vacunación, clientes o identidad.
- Coordinación multi-pestaña.

## Riesgos y mitigaciones

- **Online falso positivo** (`navigator.onLine` true sin salida real): el fetch
  falla y encola; cubierto por el criterio «fallo de transporte → cola».
- **Duplicados por reintento o multi-pestaña**: absorbidos por el
  `IdempotencyKey` único del backend.
- **Cola huérfana tras logout/cambio de usuario**: las operaciones guardan
  `galponId` y el backend autoriza por tenant; al sincronizar con otra sesión,
  el backend rechaza lo que no corresponda y la operación pasa a `error` para
  revisión manual. No se borra la cola en logout (podría perder datos del
  campo); se documenta como comportamiento conocido.
- **Crecimiento de la cola**: acotada por los 3 reintentos y el estado `error`
  con descarte manual; no hay purga automática en esta iteración.
