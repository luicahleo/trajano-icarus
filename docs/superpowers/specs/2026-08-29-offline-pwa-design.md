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
- **Rearme al recuperar la red** (2026-09-01, diagnóstico SES-F83DFD4FD0ED):
  los disparadores 1 y 2 reariman el backoff antes de sincronizar
  (`rearmarPendientes` quita `proximoIntentoEn` conservando `intentos` y
  `estado`; las de estado `error` no se tocan). Sin rearme, una operación en
  backoff quedaba invisible hasta 2-5 min tras volver la red.
- **Sonda de conectividad real** (2026-09-01, mismo diagnóstico): los
  disparadores 1, 2 y el ciclo inicial primero sondean el API
  (`apiAccesible`, GET `/api/identidad/me` con timeout de 4 s; cualquier
  respuesta salvo 408/502/503/504 cuenta). `navigator.onLine` solo garantiza
  interfaz de red levantada, no backend vivo: sin sonda, el evento `online`
  quemaba reintentos en timeouts de 15 s contra una red caída. Si la sonda
  falla, el ciclo se pospone sin tocar la cola.
- **Reintento de sonda con pendientes** (2026-09-01, diagnóstico
  SES-3C45D68DB78A): si la sonda pospone habiendo operaciones en cola, se
  programa un reintento a los 15 s (un solo temporizador a la vez, cancelado
  al desmontar). Sin él, una recuperación del API sin evento `online` (WiFi
  viva, backend caído unos segundos) dejaba la cola esperando hasta el timer
  de respaldo y el usuario tenía que refrescar la página a mano. Sin
  pendientes no se reintentan sondas.
- **Actividad real del API adelanta la sync pospuesta** (2026-09-01,
  diagnóstico SES-C54A1A220B07): cualquier respuesta HTTP (aun un 401 o 500)
  prueba conectividad real, así que `peticion()` avisa
  (`avisarActividadApi`, `lib/offline/actividadApi.ts`) y el coordinador
  cancela el reintento programado y sincroniza de inmediato. Sin esto, el API
  podía volver entre sondas (la UI ya hacía GET 200) y la cola seguía
  esperando al timer ciego de 15 s.
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

### 5. Caché de lectura y precalentado tras login (solo trabajadores)

El modo offline está orientado al rol `Trabajador`: es quien opera en el campo.
IMGA, tras el login, descargaba y guardaba los datos del día (galpones,
registros, mortalidad) para trabajar sin red; la PWA replica ese comportamiento
con un **precalentado de caché tras el login del trabajador**.

La caché vive en IndexedDB (store `cache-lectura`, clave = ruta) y cubre solo
lo necesario para operar recogida/mortalidad sin red:

- `GET /granjas`, `GET /granjas/{id}/galpones`, `GET /galpones/{id}`,
- `GET /galpones/{id}/produccion` y `GET /galpones/{id}/mortalidad` (día actual,
  sin parámetro `fecha`).

**Precalentado**: cuando hay sesión con rol `Trabajador` y red, se descargan
granjas → galpones de cada granja → detalle, producción del día y mortalidad
del día de cada galpón, reutilizando las mismas funciones de `api.ts` (que ya
escriben en la caché). Así el trabajador puede perder la conexión y seguir
viendo los datos del día. Para otros roles no hay precalentado: la caché se
llena de forma perezosa con la navegación normal.

Cada respuesta exitosa actualiza la caché; ante fallo de red se sirve la caché.
Los datos cacheados se muestran tal cual (el banner ya avisa de la falta de
conexión). Eficiencia, vacunación y el resto de módulos quedan fuera: sin red
muestran el error actual.

**`networkMode: 'offlineFirst'`** (2026-09-01, diagnóstico
SES-8B501C010EBD): con el `networkMode` por defecto de TanStack Query
('online'), `navigator.onLine === false` **pausa las queries sin ejecutar el
queryFn**, así que `conCacheLectura` nunca consultaba la caché IndexedDB: tras
«Continuar sin conexión» la app quedaba sin datos (p. ej. `/avicola` mostraba
«La cuenta no tiene una granja configurada») y el trabajador rebotaba a
`/login`. El `QueryClient` de la app (`web/src/app/queryClient.ts`) se crea con
'offlineFirst': el primer intento corre aunque no haya red, falla rápido y cae
a la caché; en línea el comportamiento es idéntico a 'online'.

### 6. Sesión offline del trabajador (sin persistir el token)

IMGA persistía el refresh token en SecureStorage para abrir la app sin red. En
la PWA **no se persiste ningún token ni credencial**: la regla anti-PII del
proyecto (`AGENTS.md` raíz y `web/AGENTS.md`) es no negociable. Además no hace
falta:

- La cola y la caché no necesitan token: encolar y leer datos del día son
  operaciones locales.
- La sesión real la cubre la cookie HttpOnly de refresh, que ya es persistente
  (`Expires` = días de validez, `Icarus.Host/Endpoints/IdentidadEndpoints.cs`):
  al volver la red, `renovarSesion()` renueva el access token en memoria.

Lo que sí se persiste es un **snapshot mínimo de sesión** para que el
trabajador pueda abrir la PWA sin red y seguir trabajando:

- Se guarda en la caché local (clave `sesion-offline`) al obtener
  `/identidad/sesion/actual` con red, **solo si el rol es `Trabajador`**.
- Contiene: `usuarioId`, `rol`, `clienteId`, `trabajadorId`, `modulos`,
  `funcionalidades`. **Nunca** el correo (dato nominal) ni el token.
- **Caduca a las 12 horas** desde su guardado: el snapshot expirado se borra y
  no restaura. Así el trabajador se ve obligado a hacer login cada día, y ese
  login diario es también el que garantiza la sincronización diaria de la cola
  (al entrar con red, el ciclo inicial del motor la vacía).
- Si el login o la restauración devuelve otro rol, el snapshot se borra (el
  dispositivo quedó en manos de otro perfil).
- Al arrancar la app: si `renovarSesion()` falla **por red** (no por 401),
  `AuthContext` restaura desde el snapshot y la app queda operativa offline. Si
  el backend rechazó la sesión (401), no hay fallback: sesión anónima.
- Con sesión de snapshot activa, al dispararse `online` se revalida
  (`renovarSesion` + `/identidad/sesion/actual`) y se reemplaza el snapshot.
- `cerrarSesion()` **con red** borra el snapshot (dispositivo compartido).
  **Sin red** lo conserva: permite reincorporarse offline desde el login y
  caduca a las 12 h igualmente (diagnóstico SES-4AF9D4EF3BC1: un cierre de
  sesión offline dejaba al trabajador fuera hasta recuperar red). La cola NO
  se borra en ningún caso (podría perder datos del campo).
- En `/login` sin red y con snapshot vigente se ofrece «Continuar sin
  conexión»: restaura el snapshot como sesión de solo UI (sin llamar al API)
  y revalida al reconectar, igual que la restauración al arrancar.
- La autorización real sigue siendo el backend: el snapshot solo habilita la
  UI; al sincronizar, el backend valida tenant y permisos.

### 7. UI

- `BannerSinConexion`: el texto pasa a «Sin conexión: los registros se guardan
  en este dispositivo y se sincronizarán al volver la red», y muestra el
  contador de operaciones pendientes cuando lo hay. Su fuente, `useConexion`,
  re-sincroniza con `navigator.onLine` al suscribirse: los eventos
  online/offline que ocurren sin consumidores montados (p. ej. en `/login`)
  no tienen listener y dejaban el estado obsoleto, mostrando el banner
  habiendo red (diagnóstico SES-4AF9D4EF3BC1).
- Contador de pendientes accesible también con conexión (chip en la barra
  superior), con acciones manuales **Reintentar** (resetea intentos y dispara
  el motor) y **Descartar** (con confirmación) por operación en `error`.
- `RegistrarRecogidaDialog` y `RegistrarBajasDialog`: el botón Guardar deja de
  depender de `!online`; al encolar, cierra con snackbar «Guardado sin
  conexión».
- Los registros aún en cola se muestran en la tabla del día con un chip
  «Pendiente», sin sumar a los totales hasta sincronizarse. Al ser datos
  locales (nunca llegaron al servidor) se pueden **editar** (actualiza el
  cuerpo encolado conservando su `idempotencyKey`) y **eliminar** (descarta la
  operación de la cola, con confirmación) estén online u offline. La edición y
  eliminación de registros **ya sincronizados** siguen deshabilitadas sin
  conexión (conflicto de versiones, ver Fuera de alcance).

### 8. Pruebas

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

- Edición offline de registros **ya sincronizados** (IMGA tiene
  `PendienteEdicion`; aquí se difiere: el alta es el caso crítico y la edición
  offline de registros del servidor añade conflicto de versiones). Los
  pendientes en cola sí se editan y descartan localmente (decisión 7).
- Vacunación offline (completar/cancelar tareas).
- **Persistencia del token o de credenciales** (IMGA lo hacía en SecureStorage;
  aquí se descarta por la regla anti-PII — ver decisión 6).
- Precalentado y sesión offline para roles distintos de `Trabajador`.
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
- **Snapshot de sesión desactualizado**: si cambian las funcionalidades del
  trabajador mientras está offline, la UI seguirá mostrando las del snapshot
  hasta la revalidación al reconectar; el backend sigue siendo la autoridad, así
  que el impacto es solo de UI.
- **Dispositivo compartido**: el snapshot se borra al cerrar sesión con red y
  al entrar con otro rol; al cerrar sesión sin red se conserva (ver decisión
  6), así que cualquiera con el dispositivo podría reentrar offline dentro de
  las 12 h de validez: riesgo aceptado porque el snapshot no contiene
  credenciales y el backend sigue siendo la autoridad. Si dos trabajadores
  comparten dispositivo sin cerrar sesión, la cola mezcla operaciones y el
  backend resuelve por tenant.
