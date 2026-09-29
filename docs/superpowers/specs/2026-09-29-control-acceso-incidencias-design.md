# Control de acceso — tres intentos e incidencias

Estado: aprobado funcionalmente, pendiente de implementación. Este documento
delimita el bloque 11A de la [fase 1](2026-09-25-control-acceso-fase1-design.md)
para un agente ejecutor. El [plan](../plans/2026-09-29-control-acceso-incidencias.md)
contiene la secuencia de trabajo. Prevalecen las reglas de `AGENTS.md`.

## Resultado esperado

En la única tablet Android del cliente, cada persona elige Entrada o Salida,
confirma y pulsa «Iniciar captura». Con la cámara lista ve 3–2–1 y se toma una
foto automáticamente. Si la identificación facial rechaza esa captura, puede
repetir hasta completar **tres intentos en el mismo flujo**. El kiosco muestra
cuántos quedan. El tercer rechazo crea una incidencia y libera la tablet para
la siguiente persona. Ningún rechazo crea una marcación.

El cliente recibe **una notificación interna** por esa incidencia y la abre
desde su módulo de Control de acceso. La incidencia nace sin trabajador
asignado: el reconocimiento no logró identificarlo. El cliente identifica a la
persona al revisarla y registra o corrige la marcación manualmente, con motivo,
o descarta la incidencia con motivo. No se pide nombre ni código en el kiosco.

## Límite de los intentos

Un flujo comienza tras confirmar Entrada/Salida. Conserva la acción elegida,
sesión de kiosco, identificador opaco y claves distintas para sus capturas. El
backend es la autoridad del contador; el navegador solo presenta el resultado.
Solo cuentan respuestas **definitivas de captura procesada sin identidad válida**:
rostro no detectado, varios rostros, prueba de vida fallida, coincidencia
ausente o ambigua. Un reenvío de la misma clave devuelve el resultado anterior
y no consume otro intento.

No cuentan cámara denegada/no disponible, muestra no obtenida, servicio ARGOS
indisponible, contrato/modelo incompatible ni respuesta incierta por red. La
respuesta incierta se reconcilia por su clave antes de permitir otra captura.
Un error de secuencia de negocio después de identificar al trabajador, como
Salida sin Entrada abierta, tampoco es un rechazo facial ni crea incidencia.
Cancelar antes del tercer rechazo termina el flujo sin incidencia. Un éxito
termina el flujo y conserva la semántica actual de marcación o propuesta de
Salida. La limitación es por flujo del kiosco, no por trabajador durante el día:
sin identidad no existe una forma fiable de imponer ese límite nominal.

El backend debe impedir que dos peticiones concurrentes para el mismo flujo
consuman más de tres intentos, creen dos incidencias o confirmen una marcación
después de agotar el flujo. El alcance de un flujo no se recupera de un nuevo
cliente o de otra sesión. La recuperación de operaciones tras recarga respeta
la idempotencia y la política online de la fase 1; no guarda evidencia en la
tablet.

## Incidencia, resolución y aviso

La incidencia es un registro funcional privado de ControlAcceso, distinto de
`JornadaAcceso` y `OperacionMarcacion`. Guarda cliente, sesión/flujo, acción
elegida, instantes UTC de primer y tercer rechazo (presentados en hora civil de
Bolivia), contador tres, estado y versión de concurrencia. No tiene
`TrabajadorId` al crearse. No guarda foto, embedding, plantilla, candidatos,
puntuaciones ni motivo biométrico detallado. Estados: Pendiente, Resuelta y
Descartada. El tenant se deriva de la sesión autorizada, nunca del cuerpo HTTP.

Pendiente puede resolverse solo por el Cliente del mismo tenant. Para resolver,
el cliente selecciona un trabajador y crea o vincula un registro/corrección
manual de Entrada o Salida con fecha/hora boliviana declarada y motivo. El
instante de la incidencia sirve de referencia, **no se convierte solo en hora
laboral**. La marcación manual sigue las reglas existentes de día, secuencia,
autor, idempotencia y auditoría. La resolución y su vínculo quedan trazables;
si el registro manual falla, la incidencia sigue pendiente. Para descartar, el
cliente registra un motivo y no se crea marcación. No se exige una segunda
aprobación. Resolver o descartar dos veces debe ser idempotente o devolver
conflicto, sin duplicar la marcación.

La notificación se persiste junto con la incidencia, con unicidad por
incidencia. Su destinatario es el Cliente del tenant y su enlace lleva a la
bandeja autorizada. El texto es genérico, por ejemplo «Hay una incidencia de
marcación por revisar». No incluye identidad supuesta, foto ni datos nominales.
Sigue la convención de notificaciones internas de la app, pero pertenece al
módulo ControlAcceso; no se reutiliza la entidad de pedidos avícolas. No hay
correo, SMS ni push en este bloque. Reintentos HTTP y recargas no generan más
avisos.

## Pantallas y API

El kiosco muestra «Intento 1 de 3», «Intento 2 de 3» y «Último intento» según
el estado del servidor. Repite el mismo botón y cuenta 3–2–1 para cada nueva
captura. Tras el tercero muestra una confirmación breve y genérica de que el
cliente revisará la incidencia, luego vuelve a listo; no muestra candidatos ni
historial. Si hay una operación incierta, muestra «Comprobando registro» y no
habilita otra captura hasta conocer su resultado.

La administración añade una bandeja de incidencias pendientes e históricas con
fecha, acción, estado y enlace desde la notificación interna. Solo el Cliente
puede ver, resolver o descartar; una sesión de kiosco, trabajador u otro tenant
no puede consultarla. La UI de resolución reutiliza los formularios de
marcación manual/corrección existentes y muestra explícitamente la hora de la
incidencia frente a la hora declarada por el cliente.

El contrato HTTP nuevo o extendido debe incluir identificador de flujo,
resultado durable por clave de captura y estado/contador autoritativo. Añadir
consulta de incidencias y resolución para Cliente. Mantener la cookie y el
antiforgery del kiosco, y las políticas del Cliente en la administración. Los
DTOs y errores son genéricos, sin eco de muestras ni detalles faciales.

## Límites y aceptación

- No modificar ARGOS, Caserito, despliegue VPS ni `master` en este bloque. El
  proveedor doble permite probar el flujo; la integración real A0/T6 y el
  piloto Android T13 siguen pendientes en el plan general.
- Sin almacenamiento de fotos, vectores o datos de trabajadores en logs,
  trazas, notificaciones, URLs ni almacenamiento del navegador. La incidencia
  funcional queda en SQL bajo tenant; no se convierte en telemetría.
- Tres rechazos definitivos crean exactamente una incidencia y un aviso; uno o
  dos permiten repetir y no crean ninguno.
- Respuesta perdida, doble toque, petición repetida, concurrencia y reinicio
  no incrementan indebidamente el contador ni duplican incidencia, aviso o
  marcación.
- Un éxito en el segundo o tercer intento registra la acción una sola vez y
  no crea incidencia. Un fallo técnico o de secuencia de negocio no consume
  intento facial.
- Cliente ajeno, trabajador y kiosco no pueden leer ni resolver incidencias.
  La corrección manual conserva la auditoría y no atribuye éxito facial.

La duración aceptada de 4–6 minutos más interacción para una fila de 20 era
una estimación **sin reintentos**; T13 medirá el efecto de los tres intentos.
