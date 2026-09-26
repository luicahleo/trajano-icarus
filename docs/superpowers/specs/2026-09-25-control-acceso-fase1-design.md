# Control de acceso — fase 1: marcación facial online

Creado: 2026-09-25. Actualizado: 2026-09-26 con decisiones del brainstorming.
Estado: reglas funcionales confirmadas; propuestas técnicas sujetas a revisión.
No hay implementación ni autorización de despliegue en esta sesión.

Revisión de ARGOS compartido: [evidencia local y VPS](2026-09-26-control-acceso-argos-evaluacion.md).
La custodia de plantillas se reabre como decisión técnica pendiente: no fue
aprobada por el usuario. El contrato de perfiles de la sección 5 describe la
alternativa inicial con almacén en ARGOS, no una capacidad existente ni una
obligación ya elegida. A0 debe resolverlo antes de ejecutar persistencia
biométrica, adaptador real o enrolamiento.

Referencias: [brainstorming](2026-09-25-control-acceso-fase1-brainstorm.md),
[diseño general](2026-09-25-control-acceso-asistencia-design.md) y
[plan ejecutable por dependencias](../plans/2026-09-25-control-acceso-fase1.md).

## 1. Resultado de la fase

El cliente con ControlAcceso habilitado selecciona sus trabajadores, enrola
sus rostros y activa un kiosco web restringido. Cada trabajador confirma
Entrada o Salida y se identifica ante la cámara. El backend registra eventos
con hora oficial de Bolivia y el cliente consulta el historial y corrige
excepciones cuando lo necesita.

No entran horarios, cálculo de horas computables, reportes/exportaciones,
vacaciones, salarios, zonas, catálogo de accesos, gestión de dispositivos,
puertas físicas, huella ni offline. La fase no migra biometría del legacy.

## 2. Fronteras y elegibilidad

Se crean tres proyectos en `Icarus/src/ControlAcceso/`:

- `Icarus.ControlAcceso.Domain`: referencia BuildingBlocks.Domain.
- `Icarus.ControlAcceso.Application`: Domain y BuildingBlocks.Application.
- `Icarus.ControlAcceso.Infrastructure`: Application, EF Core SqlServer y
  BuildingBlocks.Observability.

El Host compone políticas y adaptadores; el nuevo módulo no referencia
Clientes ni Identity. Usa `IUnidadTrabajoControlAcceso` propia, sin reemplazar
el `IUnitOfWork` global de otros módulos. Pruebas de arquitectura blindan esto.

Administración: rol Cliente, tenant del principal, cliente activo y flag
ControlAcceso vigente. No basta con un claim antiguo ni con ocultar el menú.
Trabajador, Administrador y GestorCaisy no reciben administración del módulo.
El Administrador conserva su función existente de asignar módulos al cliente.

Marcación: sesión de kiosco válida, cliente activo y módulo habilitado,
trabajador del mismo tenant, activo, sin `FechaCese`, habilitado para acceso y
con enrolamiento vigente. Un cese bloquea nuevas marcaciones al registrarse,
incluso si `EstaActivo` sigue a true. Un par abierto queda incompleto si no se
puede cerrar; la corrección del cliente sigue disponible.

El Host implementa `IConsultaElegibilidadAcceso` mediante consultas estrechas
de Clientes. Comprobar pertenencia por `ClienteId` y `TrabajadorId` explícitos:
jamás inferir tenant del cuerpo del navegador o aceptar tenant nulo como acceso
global. Aplicar filtros EF que fallen cerrados ante ausencia de tenant.

El alta de trabajador es común, en Clientes, e incluye siempre correo y
contraseña. ControlAcceso puede contratarse sin Gestión Avícola y reutiliza esa
misma alta. No se crea otra persona ni cuenta al enrolar. La cuenta no se usa
para marcar y no concede por sí sola acceso a módulos: se exigen contratación
por el cliente y funcionalidades asignadas. Si posteriormente contrata Gestión
Avícola, asigna las funcionalidades sobre la cuenta existente.

La pantalla común Trabajadores ofrece las opciones correspondientes a los
módulos contratados. Mantener datos generales/cuenta disponibles para clientes
que solo tienen ControlAcceso; no exigir ni mostrar como utilizables las
funcionalidades avícolas sin su módulo. Las comprobaciones también son backend.

## 3. Pantallas y acciones

### Administración

Dentro del módulo de la aplicación existente, desde el teléfono, tablet o PC
del cliente. El Android dedicado es exclusivamente para marcar:

- Trabajadores: listado por nombre resuelto desde Clientes, estado de
  habilitación y enrolamiento; habilitar/deshabilitar, registrar, sustituir y
  revocar rostro. Sin copia de documento, nombre o foto en ControlAcceso.
- Enrolamiento presencial: el cliente elige trabajador, revisa su identidad y
  captura su rostro en cámara. No se importan fotos de galería. El operador ve
  la previsualización temporal; se libera al guardar, cancelar o salir.
  Al confirmar un enrolamiento correcto queda habilitado automáticamente para
  marcar. Un fallo o resultado pendiente no habilita. Sustituir el rostro
  conserva la cuenta y todo el historial; el cliente puede deshabilitarlo después.
- Kiosco: estado de sesión, revocar y abrir la dirección dedicada. Sin lista
  de dispositivos ni configuración de puntos de acceso.
- Historial: fecha boliviana (hoy por defecto), trabajador opcional y estado;
  paginación del servidor (25 por defecto, máximo 100), intervalos y trazabilidad.
- Corrección: valores efectivos de la jornada, motivo obligatorio y comparación
  con la revisión anterior. Acción explícita, sin tarea diaria obligatoria.
- Registro manual ante fallo de reconocimiento: seleccionar trabajador, fecha
  y hora bolivianas y Entrada/Salida, con motivo. Puede iniciar una jornada que
  no exista, incluso de un día anterior. Se valida al guardar, sin aprobación
  posterior, mostrando su origen manual.

Solo el cliente consulta listados de identidad/historial. El kiosco muestra
únicamente el nombre de la persona de la marcación exitosa durante el resultado;
no permite buscar personas ni consultar sus jornadas. Los datos de presentación
no se escriben en telemetría ni almacenamiento del navegador.

### Kiosco

Estados: desactivado, listo, confirmación, cámara, verificando, confirmación de
cambio de acción, resultado y sin conexión. Una sola operación en curso en UI.

1. Elegir Entrada/Salida y confirmar. Cancelar no crea marcación.
2. Capturar un único rostro; enviar evidencia temporal al Host.
3. Host verifica sesión/tenant y solicita identificación y prueba de vida.
4. Si la secuencia admite la acción, guarda y responde con acción y hora BO.
5. Si se eligió Entrada y existe entrada abierta hoy, devuelve una propuesta
   opaca de Salida. Mostrar «Ya tienes una entrada. ¿Registrar salida ahora?».
6. Confirmarla registra Salida con hora nueva del servidor; no convierte una
   Entrada en Salida silenciosamente. Cancelar no registra nada.
7. Tras éxito se muestra el nombre del trabajador, «Entrada/Salida registrada»
   y la hora boliviana durante 5 segundos (duración técnica propuesta). Después
   se limpia todo el resultado, se libera la captura y se vuelve a listo.
   Sin documento ni fotografía. El Host resuelve el nombre desde Clientes
   para esta respuesta efímera, sin copiarlo al dominio o registro de operaciones.
   También se limpia al cancelar, navegar, expirar sesión o iniciar otro intento.

La propuesta dura 30 segundos, pertenece a sesión/tenant/persona/acción y solo
se consume una vez. El backend conserva temporalmente la referencia validada,
no la imagen; al confirmar revalida elegibilidad y secuencia. Expiración o cambio
de día exige comenzar de nuevo con cámara.

Salir o revocar kiosco no devuelve una sesión administrativa. La salida local
requiere autenticar de nuevo al cliente correspondiente. Desde su propia app,
el cliente también puede revocar a distancia la sesión actual.

## 4. Sesión del kiosco y aislamiento del navegador

Propuesta: origen HTTPS dedicado (nombre pendiente de agenteVPS) y entrada
`web/kiosco.html`, compilada con el frontend existente y su tema MUI. Este
origen sirve únicamente el shell de kiosco y sus endpoints a través del proxy.
No sirve la administración ni expone `/api/identidad/sesion` o su renovación.

`web/src/kiosco/main.tsx` no monta AuthProvider, coordinador offline, caché de
consultas administrativas ni registro de service worker. No instala PWA offline.
El shell utiliza `Cache-Control: no-store`; el proxy no lo almacena. La
administración avícola conserva su comportamiento actual en su propio origen.

Activar: el cliente introduce sus credenciales en el formulario del kiosco.
Un servicio del Host reutiliza la verificación de Identity, comprueba rol y
entitlement y emite exclusivamente una sesión opaca de kiosco. No llama al
handler de login normal, no emite access/refresh tokens administrativos ni
guarda credenciales. El navegador dedicado comienza en este origen; no se usa
para administrar trabajadores.

Cookie host-only `__Host-icarus_kiosco`, Secure, HttpOnly, SameSite=Strict,
Path=/; el servidor almacena solo su hash. Esquema de autenticación explícito
`Kiosco`, sin rol Cliente y no admitido por las políticas Bearer habituales.
La credencial de kiosco nunca viaja en URL ni es legible desde JavaScript.

La cookie persistente y el estado del servidor permiten recuperar una sesión
vigente al reiniciar el Android. El arranque administrado abre directamente el
kiosco y comprueba online su validez; no exige credenciales por el solo reinicio.
No restaura una sesión administrativa ni reactiva una revocada o vencida.
Sin conectividad muestra indisponibilidad y no encola marcaciones.

Las mutaciones usan antiforgery y validación exacta de Origin (incluidos
hermanos bajo el mismo dominio); sin CORS permisivo. Proxy y backend validan
hosts autorizados. El contexto no se determina por un header de tenant del
cliente. La activación tiene limitación de intentos y errores genéricos.

Valores operativos propuestos: una sesión activa por tenant, vencimiento
absoluto de 30 días y revocación inmediata. Activar otra avisa al cliente y
reemplaza la anterior atómicamente. No caduca a medianoche ni requiere apertura
diaria. No se renueva indefinidamente en segundo plano; tras vencer el cliente
la activa de nuevo. Se comprueban revocación y entitlement en cada petición.

Estos valores son decisiones técnicas propuestas, no requisitos previos del
usuario. Solo hay una sesión lógica, sin registro de hardware ni emparejamiento.

## 5. Contrato ARGOS: dependencia externa A0

ARGOS ya es el servicio facial compartido con Caserito; `/api/verify` compara
dos imágenes y su funcionamiento está acreditado por la batería VPS del
2026-08-06 (doc 34). Existe además `/api/identify`, con candidatos aportados
en la petición o consultados en ICARUS legacy, pero exige adaptación para GUID,
privacidad y el nuevo flujo. No activa PAD ni ofrece custodia independiente.

A0 decidirá entre mantener ARGOS como motor con plantillas cifradas custodiadas
por Trajano-Icarus o añadir custodia en ARGOS. La segunda es la propuesta
inicial desarrollada a continuación. Si se elige la primera, actualizar
contratos/persistencia/enrolamiento antes de ejecutarlos; no cambiar nunca la
prohibición de biometría en logs o navegador. No se trata de crear otro motor
facial ni de reutilizar el catálogo KYC de Caserito.

Proponer API interna separada y versionada `/api/v2/control-acceso`, con
autenticación entre servicios, namespace de aplicación y tenant obligatorios:

| Operación propuesta | Contrato mínimo |
|---|---|
| `GET /capacidades` | Versión, modelos y capacidades de custodia/PAD; sin datos personales. |
| `PUT /perfiles/{referencia}` | Alta/sustitución idempotente de referencia opaca, namespace, tenant y evidencia; devuelve versión de perfil. |
| `DELETE /perfiles/{referencia}` | Revocación/borrado biométrico idempotente y comprobable. |
| `GET /operaciones/{id}` | Reconciliar resultado incierto de enrolamiento/revocación, sin recuperar imágenes. |
| `POST /identificaciones` | Tenant, namespace, desafío/operación y evidencia; decisión única, referencia y versión. |

Los identificadores son cadenas opacas, no conversiones a entero. El Host
deriva el tenant de la sesión y envía la referencia de trabajador solo como
valor opaco. El servicio autentica qué namespaces permite al consumidor.
No devolver embeddings, candidatos alternativos ni puntuaciones al frontend.

Prueba de vida: se propone evaluación pasiva de una captura JPEG/PNG, máximo
2 MiB y 1920×1920 tras decodificar, una sola cara y límites de recursos tanto
en Host como ARGOS. El formato final depende de A0. Si la prueba adecuada exige
vídeo o reto activo, actualizar spec y plan antes de implementar la captura.

A0 debe entregar versión fijada del motor y modelos, licencia compatible,
umbral de identificación y margen ante candidatos ambiguos, criterio PAD,
ensayos contra fotografías y pantallas, tasa de rechazo y latencia medidas en
el equipo previsto. No copiar umbrales ONNX/ArcFace de IMCA. No afirmar que
un booleano `is_real` prueba identidad o garantiza ausencia de suplantaciones.

La configuración no es editable por tenant. El Host exige contrato compatible,
prueba de vida aprobada, coincidencia no ambigua y perfil vigente. Si faltan
campos, la respuesta es negativa o el servicio no está disponible, no marca.
La disponibilidad de ARGOS no condiciona el arranque de los otros módulos.

Bajo la alternativa de custodia en ARGOS, este guardaría plantillas cifradas
y versionadas, con aislamiento por
namespace/tenant, restauración probada e invalidación inmediata de cachés al
revocar. No retiene capturas de intentos. A0 documentará claves, volumen,
retención de respaldos y eliminación; no se da por hecho que eso exista hoy.

En esa alternativa, Trajano-Icarus conserva referencia/versión y estado, jamás
foto o plantilla. Esto sigue sujeto a la decisión de custodia en A0.
Estados de enrolamiento: SinEnrolar, Pendiente, Vigente, RevocacionPendiente,
Revocado. Ante resultado incierto, conservar operación durable sin biometría
y reconciliar. Nunca activar un perfil con solo un timeout como evidencia.
El éxito confirmado actualiza perfil vigente y habilitación conjuntamente,
respetando la versión de configuración y la elegibilidad actual. Una respuesta
tardía no revierte una deshabilitación o revocación realizada mientras esperaba.
Al sustituir, se bloquea la marcación hasta confirmar la nueva versión. Revocar
bloquea localmente de inmediato aunque ARGOS tarde en completar el borrado.
Deshabilitar es reversible y no borra el perfil; revocar sí lo elimina.

Cambios de ARGOS se ejecutarán en su propio repositorio y según su AGENTS.md.
No romper `/api/verify`, usado por CaseritoApp, ni alterar sus consumidores.
Conservar contrato y semántica, incluyendo errores sin rostro; no trasladar
umbrales de aprobación KYC al kiosco ni activar PAD sobre documentos de KYC.
Ensayar carga compartida y regresión: ambos flujos usan el mismo servicio.
Esta sesión solo documenta el requisito; no modifica ni despliega ese servicio.

## 6. Modelo temporal, secuencia y concurrencia

Hora: `TimeProvider.GetUtcNow()` en backend; almacenamiento `datetimeoffset(7)`
UTC; fecha operativa derivada mediante `America/La_Paz`. El reloj del kiosco
no es una fuente de datos del dominio. No usar `DateTime.Now` ni la fecha UTC
como fecha civil boliviana.

El instante candidato se fija al recibir por primera vez la petición de
verificación autenticada con evidencia. Si cambia el día boliviano antes de
confirmar la transacción, el intento vence y debe repetirse. No se introduce
un evento del día anterior por haber empezado la cámara antes de medianoche.
Un cambio de acción confirmado usa su propio instante de recepción.

En cada fecha: Entrada, Salida, Entrada, Salida. Salida sin Entrada y Salida
repetida se rechazan. No se infieren descansos, puntualidad ni presencia física.

La jornada se considera Abierta mientras hay entrada sin salida en el día
actual, Completa cuando los pares están cerrados e Incompleta si quedan
entradas abiertas en un día pasado. Ese estado puede derivarse al consultar:
no se necesita un job de medianoche para desbloquear el día siguiente.
Los pares completos previos del mismo día conservan su validez.

Agregado de escritura `JornadaAcceso`, único por cliente/trabajador/fecha, con
eventos `Marcacion` append-only, revisiones y `rowversion`. Tabla de operaciones
con unicidad cliente/sesión/clave de idempotencia. Crear la primera jornada debe
resolver también la carrera de inserción mediante restricción única.

`OperacionMarcacion` vincula clave, sesión, acción, instante y estado
(Procesando, Confirmada, Rechazada, RequiereConfirmacion, Expirada).
La captura nunca se persiste. Recuperar por clave devuelve el resultado previo,
no vuelve a reconocer ni genera otro evento. Una clave con acción distinta
produce conflicto. Las propuestas de cambio de acción tienen otro comando
idempotente ligado a la operación original.

No mantener una transacción SQL abierta durante la llamada a ARGOS. Reservar
operación, verificar fuera de transacción y confirmar bajo bloqueo/concurrencia
con relectura de sesión, trabajador, habilitación y estado diario. Marcación,
versión de jornada y resultado durable se confirman juntos. Un intento que
pierde la carrera de secuencia devuelve conflicto; nunca convierte el tipo.

Si el proceso cae durante la verificación, una operación sin evidencia
recuperable caduca; no se reconstruye la captura ni se registra al reiniciar.
Si ya confirmó y se perdió la respuesta, la consulta por clave devuelve éxito.
Al recargar el kiosco no se recupera una cola local. Una operación reciente del
servidor puede consultarse desde la sesión, sin exponer identidad; no se crea
otra marcación automáticamente. El detalle de resultado individual en kiosco
caduca a los 2 minutos; después solo se consulta en el historial del cliente.

Valores propuestos de espera: ARGOS 10 s, Host 15 s, cliente 20 s; reservar
30 s para terminar/expirar una operación antes de iniciar otra en la sesión.
No reintentar POST de captura automáticamente con otra clave.

## 7. Registros manuales, correcciones y persistencia funcional

Cuando falla el reconocimiento, el cliente registra manualmente Entrada o
Salida para un trabajador de su tenant, desde su administración online. Puede
declarar hora/fecha de hoy o de días anteriores, sin futuro. Requiere motivo
(1–500 caracteres) y guarda autor e instante real de creación del servidor,
además del instante declarado convertido de Bolivia a UTC.

Queda válido al guardar: sin segunda aprobación, firma de otro usuario ni
tarea posterior obligatoria. No requiere validación positiva de ARGOS ni una
marcación facial previa. Puede crear la jornada del día afectado. La pertenencia
al tenant y el permiso actual del cliente se verifican siempre; el ajuste
histórico no se bloquea simplemente porque el trabajador haya cesado después.

El evento append-only tiene origen ManualCliente; un evento facial tiene
origen Kiosco. No inventar una evidencia biométrica para registros manuales.
Se mantienen alternancia, pares del mismo día y no solapamiento. Una Entrada
manual puede quedar abierta; completar una Salida olvidada usa la misma regla.
Si insertar un evento intermedio rompe la secuencia existente, usar la revisión
de jornada para presentar todos los valores efectivos coherentes.

El comando manual usa clave idempotente y versión de jornada (o ausencia
esperada al crearla), con la misma protección de concurrencia del kiosco. La
repetición del envío no duplica el evento ni altera fecha de creación o motivo.

El cliente puede completar una salida olvidada, rectificar una hora o anular
una marcación errónea. Cada corrección crea una revisión efectiva completa de
la jornada; conserva originales y revisiones anteriores y requiere versión
esperada, motivo (1–500 caracteres), autor e instante oficial.

Cada revisión indica `HastaSecuenciaOriginal`: qué eventos originales abarca.
La vista efectiva aplica la última revisión y después los eventos originales
posteriores a ese límite. Así, una marcación nueva no desaparece detrás de una
corrección anterior. La transacción y rowversion mantienen ese orden; no basta
con seleccionar siempre el último snapshot de corrección.

Validar pares ordenados, sin solapamiento, salida posterior a entrada, todos
en la misma fecha boliviana, nunca futuros. Puede quedar una última entrada
abierta: no se obliga a inventar el dato que falta. Una revisión puede quedar
vacía para anular una jornada equivocada, sin borrar su historia.

Corregir actúa sobre jornadas existentes de fechas iguales o anteriores a hoy;
el registro manual sí puede iniciar una jornada sin eventos previos. Las
correcciones del día actual también participan en el control de concurrencia
con el kiosco. Un conflicto exige releer y revisar; no reintentar una corrección
silenciosamente.

La UI distingue Kiosco, ManualCliente y AjusteCliente; una hora añadida por el
cliente no se presenta como validada facialmente. La secuencia siguiente se evalúa
sobre la última revisión efectiva. Los futuros cálculos deberán respetar esta
distinción y las versiones de jornada.

Historial privado en SQL: referencias de trabajador, eventos, autor del ajuste
y motivo. Logs/Seq/trazas/diagnósticos: sin identidad ni horas nominales. La
auditoría funcional no se implementa como texto en el registro de vuelo.
No borrar historia al deshabilitar un trabajador o revocar su rostro.

## 8. API propuesta y políticas

Prefijo del Host `/api`; las rutas siguientes son relativas. Cuerpos y DTOs
en español, errores genéricos, sin eco de credenciales o muestras.

| Ruta | Autorización y propósito |
|---|---|
| `GET /control-acceso/trabajadores` | Cliente: estados del módulo, paginados. |
| `PUT /control-acceso/trabajadores/{id}/habilitacion` | Cliente: habilitar/deshabilitar con versión. |
| `PUT /control-acceso/trabajadores/{id}/rostro` | Cliente: captura temporal, operación idempotente de alta/sustitución. |
| `DELETE /control-acceso/trabajadores/{id}/rostro` | Cliente: revocación idempotente. |
| `GET /control-acceso/enrolamientos/{operacionId}` | Cliente: estado de reconciliación. |
| `GET /control-acceso/jornadas` | Cliente: fecha, trabajador, estado y paginación. |
| `GET /control-acceso/jornadas/{id}` | Cliente: revisión efectiva e historia. |
| `POST /control-acceso/marcaciones-manuales` | Cliente: trabajador, tipo, fecha/hora BO, motivo, versión y clave idempotente; válida al guardar. |
| `POST /control-acceso/jornadas/{id}/correcciones` | Cliente: nueva revisión con versión esperada. |
| `GET /control-acceso/sesion-kiosco` | Cliente: estado sin credencial. |
| `DELETE /control-acceso/sesion-kiosco` | Cliente: revocar sesión actual. |
| `POST /kiosco/activacion` | Credenciales frescas de Cliente, limitación de intentos y origen autorizado. |
| `GET /kiosco/sesion` | Esquema Kiosco: estado, conectividad y antiforgery. |
| `POST /kiosco/marcaciones` | Kiosco: acción, clave y evidencia temporal. |
| `GET /kiosco/operaciones/{clave}` | Kiosco: resultado de su operación reciente. |
| `GET /kiosco/operacion-actual` | Kiosco: reconciliación tras recargar. |
| `POST /kiosco/confirmaciones/{id}` | Kiosco: confirmar propuesta de Salida vigente. |
| `POST /kiosco/salida` | Credenciales frescas del Cliente correspondiente; revoca y borra cookie. |

Estados HTTP: 401 sesión inválida; 403 permiso/módulo; 404 para recurso ajeno o
inexistente indistinguibles; 409 secuencia/versión; 413 tamaño; 429 límite;
503 proveedor no disponible. Resultado facial negativo usa un código de
negocio genérico, nunca los candidatos devueltos por ARGOS.

## 9. Límites de seguridad y operación

No se registra cuerpo de peticiones/respuestas, multipart, tokens, cookie,
antiforgery, imagen, embedding, identificador de trabajador, referencia facial,
motivo de corrección ni eventos nominales. Usar plantillas de ruta, no URLs
con IDs. Revisar logs de proxy, HTTP client, excepciones, EF y buffer frontend;
no basta con omitir un `console.log` en la nueva página.

Memoria de cámara y blobs solo durante el intento; detener pistas y liberar
URLs temporales al acabar. Respuestas `no-store`, sin IndexedDB, localStorage,
Cache Storage ni fallback offline. Una caída de conexión después del envío
muestra «Comprobando registro» hasta conocer resultado; no afirmar fallo si
la transacción pudo completarse.

La plataforma del kiosco es Android dedicado, exclusivamente para registro.
La credencial web restringida no bloquea Android. El cierre operativo requiere
cámara autorizada en HTTPS, bloqueo administrado, arranque automático tras
reinicio y salida protegida por el sistema operativo. El modelo del equipo y
la solución de administración siguen pendientes; no se compran licencias ni se
configuran equipos como parte de este trabajo documental.

Fuentes técnicas consultadas el 2026-09-25:

- [Android: políticas para dispositivos dedicados](https://developers.google.com/android/management/policies/dedicated-devices).
- [DeepFace: implementación de detección y anti-spoofing](https://github.com/serengil/deepface/blob/master/deepface/modules/detection.py).

La documentación de DeepFace muestra una capacidad candidata; no demuestra
que la imagen de ARGOS desplegada la incluya ni valida su eficacia en el kiosco.

## 10. Criterios de aceptación

1. Cliente sin módulo y trabajador autenticado no administran acceso.
2. Recurso de otro tenant devuelve lo mismo que recurso inexistente.
3. Cookie de kiosco no autentica endpoints de administración ni otro tenant.
4. Suspensión, cese, deshabilitación, revocación y vencimiento se aplican en la
   siguiente petición; no dependen del caché facial.
5. Dos pares en un día se aceptan; Salida inicial y acciones repetidas no crean
   eventos indebidos. Cambiar a Salida requiere la segunda confirmación.
6. A las 03:59 UTC y 04:00 UTC se distinguen los días BO correspondientes.
   Pruebas fijan instantes concretos con reloj simulado, sin esperar medianoche.
7. Una Entrada incompleta ayer no bloquea hoy ni se cierra automáticamente.
8. Carreras, doble clic, reintentos, reinicio del proceso y respuesta perdida
   no duplican eventos; idempotencia conserva el resultado ya confirmado.
9. Correcciones mantienen originales, validan intervalos y autor, y rechazan
   una versión obsoleta incluso si compite una marcación del kiosco.
10. ARGOS negativo, ambiguo, sin PAD, incompatible o caído nunca produce éxito.
11. Enrolamiento/revocación con resultado incierto se reconcilia sin imagen
    persistida; no puede marcar un perfil pendiente o revocado.
12. Recargar, navegar atrás, desconectar y reiniciar equipo no recupera sesión
    administrativa ni almacena biometría local.
13. Telemetría del flujo completo supera pruebas de privacidad con centinelas
    sintéticos y los logs del proxy se revisan antes del piloto.
14. El piloto registra resultados de cámara, bloqueo, reinicio, latencia y PAD.
    Dobles de ARGOS y tests verdes no sustituyen esta aceptación.
15. Cliente solo con ControlAcceso crea trabajadores con correo/contraseña,
    enrola y marca sin acceso a Gestión Avícola. Al contratarla después se
    reutiliza la cuenta; sus funcionalidades no se conceden automáticamente.
16. Enrolamiento por cámara desde teléfono/tablet/PC, sin galería, habilita al
    éxito; sustituir rostro conserva cuenta e historial y no habilita tras fallo.
17. Registro manual actual o pasado, incluso en jornada nueva, válido sin
    segunda aprobación, distingue instante declarado/creación y origen. Futuro,
    tenant ajeno, falta de motivo y reintento duplicado se rechazan o deduplican.
18. Android reiniciado abre el kiosco y restaura su sesión vigente online. El
    nombre solo aparece en resultado exitoso y desaparece al volver a inicio;
    nunca llega a logs, caché, mensajes de error ni listados públicos.

## 11. Estado y siguientes decisiones

No se ha ejecutado ninguno de esos criterios contra una implementación nueva.
La fase tiene un plan por dependencias: dominio e integración con dobles pueden
avanzar después de aprobar el diseño; integración real y producción dependen
de A0, del origen HTTPS y del equipo del kiosco. La autorización de esta sesión
termina en brainstorming, spec y plan.
