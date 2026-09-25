# Control de acceso y asistencia — diseño por fases

Diseño validado en brainstorming con el usuario el 2026-09-25, después de
revisar el proyecto MAUI IMCA, su documentación y el patrón modular de Gestión
Avícola. IMCA queda como fuente histórica: no se reutiliza su aplicación ni su
arquitectura offline.

Detalle de la fase 1: [brainstorming](2026-09-25-control-acceso-fase1-brainstorm.md),
[spec técnico propuesto](2026-09-25-control-acceso-fase1-design.md) y
[plan](../plans/2026-09-25-control-acceso-fase1.md). La sesión de preparación
es exclusivamente documental. Las propuestas técnicas y dependencias abiertas
se distinguen de las reglas funcionales aprobadas.

## Propósito

Crear `ControlAcceso` como un bounded context nuevo de Trajano-Icarus. Un único
kiosco web por cliente permitirá que sus trabajadores registren entradas y
salidas mediante reconocimiento facial. El cliente configurará quién puede
usar el kiosco y administrará posteriormente horarios, incidencias, reportes,
vacaciones y permisos.

El módulo mide asistencia y tiempo trabajado. No calcula salarios ni conserva
importes monetarios.

## Actores y autorización

### Cliente

El cliente autenticado necesita tener habilitado `Modulos.ControlAcceso`. Desde
la aplicación web puede:

- habilitar o deshabilitar trabajadores para el control de asistencia;
- registrar, sustituir o revocar su referencia facial en ARGOS;
- iniciar la sesión restringida del kiosco;
- consultar marcaciones, jornadas e incidencias de su tenant;
- corregir incidencias conservando trazabilidad;
- en fases posteriores, configurar horarios, consultar reportes y administrar
  vacaciones y permisos.

### Trabajador

El trabajador no inicia sesión ni navega por la administración del módulo. Solo
usa el kiosco: elige `Entrada` o `Salida`, confirma la acción, se identifica ante
la cámara y recibe el resultado. Su rostro solo autoriza esa marcación.

La habilitación para el kiosco no se modela como una funcionalidad general del
trabajador. `ControlAcceso` mantiene una `ConfiguracionAccesoTrabajador` propia,
porque se trata de elegibilidad operativa, no de permiso para navegar por la
aplicación.

## Alcance funcional acordado

- Siempre online. Sin cola offline, IndexedDB, caché de marcaciones ni
  sincronización posterior.
- Un único punto físico de acceso, bidireccional, por cliente. No existen zonas,
  catálogo de puntos de acceso ni inventario de dispositivos.
- Reconocimiento facial exclusivamente mediante ARGOS. No se porta ONNX ni la
  caché local de embeddings de IMCA.
- Sin huella digital.
- Varios pares `Entrada → Salida` por trabajador y día.
- Fecha civil y reglas diarias basadas en `America/La_Paz`.
- Administración de trabajadores por el cliente y operación cotidiana del
  kiosco por los trabajadores.
- Horarios, cálculo de horas, incidencias, reportes, vacaciones y permisos se
  incorporan en fases separadas.

## Flujo del kiosco

El cliente activa una sesión restringida, ligada al tenant y utilizable solo
en los endpoints del kiosco; nunca se expone un token administrativo en esa
pantalla. El detalle propuesto para la fase 1 utiliza un origen y arranque web
dedicados, con verificación de credenciales que emite solo sesión de kiosco.
Esto precisa la idea inicial de sustituir una sesión normal: el login normal
existente conserva un refresh token y no basta con ocultar la administración.
No se crea un catálogo de dispositivos. Salir del modo kiosco exige volver a
autenticarse como cliente.

La pantalla inactiva muestra dos botones grandes: `Entrada` y `Salida`.

1. El trabajador pulsa una acción.
2. El kiosco pregunta `¿Confirmas que deseas registrar tu entrada/salida?`.
3. Tras confirmar, solicita la cámara y captura una muestra temporal.
4. El backend envía la muestra a ARGOS por la red interna.
5. ARGOS exige prueba de vida e identifica a una persona dentro del tenant.
6. El backend comprueba que el trabajador sigue activo y habilitado, valida la
   secuencia diaria y registra la marcación.
7. La pantalla presenta éxito o un error genérico y vuelve automáticamente al
   estado inicial.

La aplicación web no llama directamente a ARGOS. La API actúa como frontera de
seguridad, limita tamaño y formato, aplica timeout y evita que ARGOS sea un
servicio público del navegador.

Si falta conexión, ARGOS no está disponible, no hay prueba de vida o la
identificación no supera el umbral, el sistema falla cerrado: no registra la
marcación y muestra un mensaje que no revela identidades, puntuaciones ni datos
biométricos.

## ARGOS y custodia biométrica

La copia local de ARGOS revisada genera embeddings ArcFace e identifica contra
plantillas recibidas o consultadas en ICARUS legacy. No se encontró una ruta
actual de registro en `ARGOS/views.py`, custodia independiente para el nuevo
módulo ni prueba de vida habilitada. La referencia de IMCA a registro no acredita
esa capacidad actual. Antes de que la fase 1 sea apta para uso real, ARGOS debe
ofrecer un contrato interno versionado que:

- registre o sustituya la referencia facial de un trabajador dentro de un
  tenant;
- revoque esa referencia;
- combine prueba de vida e identificación `1:N` restringida al tenant;
- devuelva una referencia opaca del trabajador y una decisión, sin devolver el
  embedding al navegador ni a `ControlAcceso`;
- no escriba imágenes, Base64, embeddings, nombres, documentos, puntuaciones
  asociadas a personas ni marcaciones nominales en logs;
- descarte las muestras capturadas después de procesarlas.

Trajano-Icarus conserva solamente la referencia opaca de ARGOS y el estado de
enrolamiento. Las fotografías y plantillas faciales no se duplican en su base
de datos. Los umbrales y el mecanismo de prueba de vida son configuración del
sistema, no parámetros editables por cada cliente.

La indisponibilidad de ARGOS no impide arrancar Trajano-Icarus ni utilizar otros
módulos, pero deja temporalmente inoperables el enrolamiento y el kiosco.

## Tiempo oficial y día operativo

El dispositivo del kiosco nunca decide la hora oficial. El backend obtiene el
instante mediante `TimeProvider.GetUtcNow()` y lo persiste como
`datetimeoffset` normalizado a UTC. La fecha y hora visibles y las reglas del
día se derivan explícitamente con la zona IANA `America/La_Paz`; no dependen de
la zona local del host, del contenedor o de SQL Server.

El agenteVPS verificó el 2026-09-25 que el host está en UTC y sincronizado por
NTP, la API resuelve `America/La_Paz` y SQL Server opera en UTC. No se necesita
un cambio de infraestructura.

Cada marcación pertenece únicamente al día civil boliviano en que el servidor
la recibe. No se admiten marcaciones retroactivas ni futuras desde el kiosco.

## Secuencia, pares e idempotencia

La primera marcación válida del día es `Entrada`; después se alternan
`Salida → Entrada → Salida`. Se permiten varios pares completos en el mismo
día.

- Una `Salida` sin entrada abierta se rechaza.
- Si existe una entrada abierta y el trabajador vuelve a elegir `Entrada`, el
  kiosco informa que ya tiene una entrada y ofrece registrar `Salida` con la
  hora actual, previa confirmación.
- Si la misma petición se reintenta, una clave de idempotencia evita duplicar la
  marcación.
- La alternancia y la idempotencia se protegen mediante transacción y control
  de concurrencia en el servidor.

Un par solo puede abrirse y cerrarse dentro del mismo día boliviano. Si llega la
medianoche con una entrada abierta, la jornada anterior queda `Incompleta`:

- no se inventa ni se infiere una hora de salida;
- no se bloquea la entrada del día siguiente;
- el cliente no está obligado a corregirla;
- los reportes distinguen y excluyen esa duración de los totales calculables;
- el cliente puede corregirla cuando necesite completar el informe.

## Correcciones e inmutabilidad

Las marcaciones originales son inmutables y nunca se eliminan físicamente. Una
corrección crea un ajuste vinculado a la marcación o jornada afectada con:

- valor corregido;
- motivo obligatorio;
- instante UTC de la corrección;
- referencia del usuario autenticado que la realizó.

Las consultas presentan el valor efectivo y permiten ver la historia. Los logs
y el registro de vuelo describen la operación y su resultado sin incluir
nombres, identificadores del trabajador, horas nominales ni datos biométricos.

## Modelo conceptual

`ControlAcceso` será un módulo hermano de Clientes y Gestión Avícola, con tres
proyectos `Domain`, `Application` e `Infrastructure`, esquema SQL
`control_acceso` y las mismas reglas de aislamiento por tenant.

Conceptos previstos para la fase 1:

- `ConfiguracionAccesoTrabajador`: `ClienteId`, `TrabajadorId`, estado
  habilitado, estado de enrolamiento y referencia opaca de ARGOS.
- `SesionKiosco`: credencial restringida y revocable del tenant. No representa
  un dispositivo ni un punto de acceso.
- `Marcacion`: evento inmutable `Entrada` o `Salida`, instante UTC, fecha
  boliviana, clave de idempotencia y origen kiosco.
- `JornadaDiaria`: proyección de los pares del trabajador en una fecha
  boliviana, completa o incompleta.
- `CorreccionJornada`: ajuste auditado efectuado por el cliente.

Los IDs de `Cliente` y `Trabajador` son referencias externas. El módulo no
copia nombres, documentos, cargos ni otros datos personales y no referencia
los ensamblados de Clientes o Identity. El Host compone las consultas necesarias
para comprobar tenant, módulo habilitado y estado activo.

## Frontend y modo kiosco

La solución será parte de la PWA React existente. No habrá aplicación MAUI.
`display: standalone` mejora la presentación, pero no impide abandonar la app.
Para un kiosco realmente bloqueado, el dispositivo Android deberá administrarse
como dispositivo dedicado mediante Android Enterprise/EMM y publicar la URL
como aplicación web en modo kiosco. Esa configuración operativa no crea una
entidad `Dispositivo` dentro del dominio.

La cámara web requiere HTTPS y permiso del navegador. El kiosco no conserva
capturas, resultados faciales ni credenciales administrativas en localStorage,
IndexedDB o Cache Storage.

## Evolución por fases

### Fase 1 — Marcación facial online

- proyectos y persistencia base de `ControlAcceso`;
- habilitación y enrolamiento facial de trabajadores por el cliente;
- contrato ARGOS con prueba de vida, alta, sustitución, revocación e
  identificación por tenant;
- sesión restringida y pantalla de kiosco;
- entradas, salidas, varios pares diarios, idempotencia e incompletos;
- historial diario y corrección auditada por el cliente;
- aislamiento por tenant, autorización, anti-enumeración y pruebas de
  arquitectura.

Esta fase no se considera lista para producción mientras ARGOS no proporcione
prueba de vida.

### Fase 2 — Horarios y cálculo de horas

- patrón semanal configurable y asignable a cada trabajador;
- días laborables, hora esperada de entrada y salida y descansos previstos;
- vigencia temporal de los horarios para no reinterpretar el pasado;
- cálculo reproducible de presencia, horas computables, retrasos, salidas
  anticipadas, ausencias y tiempo adicional;
- bandeja de incidencias y aprobación del cliente.

Las reglas exactas de redondeo, tolerancias, descansos y tiempo adicional se
definirán en el brainstorming y spec propios de esta fase. Hasta entonces la
fase 1 informa intervalos reales, no califica puntualidad.

### Fase 3 — Reportes

- filtros por trabajador y periodo;
- resumen de jornadas, horas e incidencias;
- detalle trazable desde el total hasta las marcaciones originales;
- exportación en formatos que se definan para esta fase.

Los pares incompletos se muestran como incidencias y no aportan una duración
inventada.

### Fase 4 — Vacaciones, permisos y feriados

- calendario de feriados aplicable al cliente;
- registro de vacaciones y permisos por el cliente;
- periodos, estados y trazabilidad;
- integración con la jornada esperada y los reportes de ausencias.

El flujo de solicitud, aprobación, saldos y reglas de acumulación necesita un
spec propio. No se presupone que el trabajador disponga de una pantalla fuera
del kiosco.

## Fuera de alcance global

- salarios, nómina, descuentos, bonos, importes o contabilidad;
- zonas y múltiples puntos de acceso;
- inventario, emparejamiento o administración remota de dispositivos;
- apertura física de puertas, tornos o cerraduras;
- huella digital;
- reconocimiento ONNX en el navegador o dispositivo;
- funcionamiento offline o sincronización diferida;
- migración automática de biometría o marcaciones desde IMCA;
- copia de fotografías o embeddings en Trajano-Icarus;
- marcaciones manuales del cliente presentadas como si fueran eventos del
  kiosco.

## Calidad y seguridad

- Pruebas unitarias de alternancia, cambio de día boliviano, varios pares,
  incompletos, correcciones e idempotencia usando `TimeProvider` controlado.
- Pruebas de integración con SQL Server para concurrencia, aislamiento por
  tenant, autorización y persistencia `datetimeoffset`.
- Pruebas de contrato del adaptador ARGOS sin imágenes reales ni fixtures
  biométricos personales.
- Pruebas de arquitectura para impedir dependencias desde `ControlAcceso` hacia
  Clientes e Identity.
- Ningún log, traza, métrica o mensaje de error contendrá datos biométricos,
  imágenes, nombres, documentos, referencias faciales ni registros nominales
  de acceso.
- La telemetría operativa se limita a disponibilidad, latencia, contadores
  agregados y códigos genéricos de resultado.

Cada fase posterior tendrá su propio spec y plan. El plan de fase 1 ya está
preparado por dependencias. Su preparación no implementa ni autoriza despliegue
de ninguna fase; A0 de ARGOS y la aceptación del equipo del kiosco siguen
pendientes.
