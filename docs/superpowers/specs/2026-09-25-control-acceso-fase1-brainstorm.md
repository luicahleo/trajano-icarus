# Control de acceso — brainstorming de la fase 1

Creado: 2026-09-25. Actualizado con respuestas del usuario: 2026-09-26.
Alcance de esta sesión: documentación exclusivamente.
El usuario solicita brainstorming, spec y plan; no autoriza implementar.

Referencia: [diseño general](2026-09-25-control-acceso-asistencia-design.md).
Resultado técnico: [spec de fase 1](2026-09-25-control-acceso-fase1-design.md)
y [plan](../plans/2026-09-25-control-acceso-fase1.md).

Revisión del servicio compartido con Caserito:
[ARGOS local y respuestas VPS](2026-09-26-control-acceso-argos-evaluacion.md).
Custodia confirmada después de revisar el servicio: plantillas cifradas en
Trajano-Icarus, ARGOS solo procesa. La propuesta inicial de almacén en ARGOS
queda descartada.

## Decisiones del usuario que se conservan

- Nuevo módulo de Trajano-Icarus, sin MAUI ni funcionamiento offline.
- Un punto físico bidireccional por cliente; sin zonas, inventario de
  dispositivos, huella ni apertura de puertas.
- El cliente habilita trabajadores; estos marcan en el kiosco con ARGOS.
- Botones Entrada y Salida con confirmación explícita.
- Varios pares por día civil de Bolivia, sin pares que crucen medianoche.
- Entrada sin salida al cambiar de día: incompleta, sin cierre inventado,
  sin bloquear el día siguiente, corrección opcional del cliente con motivo.
- Horas y reportes en fases posteriores; salarios totalmente excluidos.
- Vacaciones y permisos también quedan fuera de esta fase.

## Decisiones confirmadas en las preguntas posteriores

- Android dedicado exclusivamente a marcación. El cliente administra y enrola
  desde su propio teléfono, tablet o PC; no enrola en el kiosco.
- ControlAcceso puede contratarse sin Gestión Avícola. Ambos reutilizan los
  trabajadores del módulo común Clientes, sin duplicar personas.
- Todos los trabajadores se crean con correo y contraseña, como ahora. Tener
  cuenta no concede acceso a un módulo: se exige contratación por el cliente
  y las funcionalidades correspondientes asignadas al trabajador.
- Si después se contrata Gestión Avícola, se reutiliza la misma cuenta y se
  asignan funcionalidades. La alternativa de cuenta opcional se descartó.
- Enrolamiento exclusivamente con cámara en directo desde el equipo del
  cliente, sin subida de fotos de galería. Al completarse correctamente queda
  habilitado para marcar; se puede deshabilitar después.
- En A0 se confirma una captura facial válida por enrolamiento, con opción de
  repetir si no se detecta bien el rostro o falla la prueba de vida pasiva.
- Para enrolar y marcar se pulsa «Iniciar captura»; cuando la cámara está lista
  aparece una cuenta visible de 3 segundos y se toma una sola foto automáticamente.
  No hay segundo botón «Capturar», vídeo grabado ni gestos guiados.
- El cliente puede sustituir el registro facial, conservando cuenta e historial.
- Reiniciar el Android debe devolverlo al kiosco listo para marcar con su
  sesión restringida vigente, sin un nuevo login por el mero reinicio. También
  hay que configurar y probar el arranque automático en Android.
- Tras una marcación correcta se muestran brevemente nombre, acción y hora
  boliviana; después se limpia la pantalla. Sin documento ni fotografía.
- Para cada marcación, el trabajador dispone de hasta tres intentos de captura
  si no se reconoce su rostro. Tras el tercero fallido se crea una incidencia
  para que el cliente la verifique y corrija; no se inventa una marcación.
- Confirmado por el usuario: como no hubo identificación facial, la incidencia
  queda inicialmente sin trabajador asignado. El cliente identifica a la
  persona al revisarla, sin pedir nombres o códigos en el kiosco ni conservar
  fotos de los intentos.
- Al crearla, el cliente recibe una notificación interna que lleva a su bandeja
  de incidencias. Solo se emite una por incidencia, no una por intento fallido.
- El cliente puede registrar manualmente entradas y salidas desde su
  administración, incluso de días anteriores, nunca futuras.
- El registro manual exige motivo, autor y fecha real de creación. Es válido
  al guardar, sin segunda aprobación, y se distingue de la marcación facial.
  Se permite crear una jornada sin marcaciones previas para resolver el fallo.
- La restricción de día actual continúa aplicándose al kiosco. Cada par,
  incluso manual, permanece dentro del mismo día civil boliviano.
- Trajano-Icarus conserva las plantillas cifradas y administra enrolamiento,
  sustitución y revocación. ARGOS no almacena perfiles ni conecta directamente
  a base de datos; procesa captura y candidatos de cada petición.
- Sin plantillas ni cola offline en la tablet. El nuevo flujo de ARGOS tampoco
  usa la caché de candidatos del ICARUS legacy; aquella caché del servidor era
  distinta de las plantillas locales de IMCA para reconocimiento offline.
- Para A0 se estima hasta 20 trabajadores activos por cliente en el piloto.
  Es una carga de referencia para medir identificación 1:N, no un límite de alta
  ni motivo para recortar candidatos silenciosamente.
- Habrá un solo Android dedicado por cliente. Cada trabajador completa su
  marcación antes de que empiece la siguiente; si llegan juntos, hacen fila
  ante la misma tablet. La carga de 20 no supone capturas simultáneas.
- El usuario acepta para el piloto una espera estimada de 4–6 minutos, más el
  tiempo de interacción, cuando llegan juntos 20 trabajadores. Es una estimación
  basada en marcaciones sin reintentos; las incidencias alargan la fila. No es
  un resultado medido ni un SLA.
- Después de la foto, el resultado debería llegar en 5 segundos y a los 10
  segundos sin respuesta el kiosco mostrará estado incierto y consultará la
  operación. No enviará otra foto ni creará otra marcación automáticamente.
  La cuenta previa de 3 segundos no forma parte de esa espera.

## Evidencia revisada

| Archivo actual | Hallazgo que afecta el plan |
|---|---|
| `Icarus/src/Clientes/Icarus.Clientes.Domain/Modulos.cs` | `ControlAcceso = 2` existe. |
| `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/VerificadorEntitlement.cs` | Verifica funcionalidades; falta una consulta de módulo y elegibilidad para el kiosco. |
| `Icarus/src/Clientes/Icarus.Clientes.Domain/Trabajador.cs` | El cese tiene fecha y no pone `EstaActivo` a false. La elegibilidad debe comprobar ambos datos. |
| `Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs` | El alta actual de trabajador crea también cuenta con correo y contraseña. La fase 1 reutiliza ese alta; no cambia el modelo de cuentas. |
| `web/src/app/navegacion.tsx` y `web/src/app/router.tsx` | Trabajadores está en la navegación del Cliente y exige ese rol, no Gestión Avícola. La pantalla actual ofrece funcionalidades avícolas: debe adaptarse a los módulos contratados. |
| `web/src/features/auth/AuthContext.tsx` | El cierre actual borra el access token en memoria, pero no revoca el refresh del servidor. No basta para convertir una sesión administrativa en kiosco. |
| `web/src/main.tsx` y `web/vite.config.ts` | La app registra service worker y precachea el shell. El kiosco necesita una entrada aislada que no instale ese service worker. |
| `dev/ARGOS/ARGOS/views.py` | El contrato local de identificación admite embeddings o consulta ICARUS legacy; convierte IDs a enteros y expone candidatos/puntuaciones. No hay ruta de registro en este archivo. |
| `dev/ARGOS/ARGOS/api_client.py` | Consulta `/api/imca/biometria/embeddings/{cliente_id}` del sistema antiguo. |

Las dos últimas rutas son del repositorio hermano ARGOS, fuera de este repo.
No se puede afirmar que esa copia sea la imagen exacta desplegada en VPS.
La respuesta 48 del agenteVPS verifica relojes y zonas horarias, no el contrato
facial. No se ejecutaron pruebas biométricas ni se modificó ARGOS.

## Alternativas y propuesta técnica

### Activación del kiosco

- Reutilizar el login normal: exige revocación y aislamiento adicional de
  tokens, pestañas y restauración offline existentes.
- Entrada web y origen exclusivos para kiosco: elegida como propuesta técnica.
  Reutiliza React, tema y API, con un arranque distinto. El cliente se autentica
  para activar únicamente una sesión de kiosco; nunca obtiene un JWT de
  administración en ese navegador.

Esta propuesta concreta y sustituye la frase del diseño general que decía
«sustituir la sesión normal». No crea un segundo módulo ni una app nativa.
Requiere coordinación futura de DNS, HTTPS y proxy con agenteVPS.

### Biometría y baja fricción

- Se conserva identificación 1:N dentro del cliente, sin elegir nombre ni
  escribir documento en el kiosco.
- El usuario elige detección pasiva de presentación fraudulenta en ARGOS,
  sin gestos. Su eficacia contra fotos y pantallas debe medirse en la tablet
  real; el soporte de una biblioteca no certifica la solución ni garantiza
  detectar todo ataque.
- ARGOS ya compara imágenes para Caserito y ofrece identificación con
  candidatos externos. Se conserva como motor: no se añade almacén de perfiles.
  Trajano-Icarus selecciona y descifra las plantillas del tenant exclusivamente
  para procesarlas; ni el navegador ni los logs reciben esos vectores.
- No se elige proveedor de prueba de vida ni se inventan umbrales. La tarea
  externa A0 debe fijar modelo, versiones, licencias y criterios medibles antes
  de implementar el adaptador real.

### Errores y olvidos

- Una entrada abierta hoy permite ofrecer Salida, pero solo después de
  identificar al trabajador y con una segunda confirmación explícita.
- Un error de red después del envío significa resultado desconocido, no
  necesariamente rechazo. Se consulta la operación antes de permitir otra.
- Al corregir, el cliente puede completar, sustituir o anular valores mediante
  una nueva revisión; nunca sobrescribir eventos originales.
- La fase 1 presenta historial e incompletos. No incorpora aprobación diaria
  obligatoria ni notificaciones por cada olvido.

## Límites que el plan debe expresar

1. Un par incompleto no permite conocer presencia física ni calcular tiempo
   real de salida. La pantalla no afirmará «está dentro» como hecho.
2. Un intervalo medido no equivale todavía a horas laborables computables.
3. La auditoría funcional privada guarda referencias y ajustes; la prohibición
   anti-PII se aplica a logs, trazas y diagnósticos. Sin persistencia funcional
   de referencias no existiría el historial solicitado.
4. Bloqueo del sistema operativo y sesión restringida son controles distintos.
   Pantalla completa por sí sola no bloquea el equipo.

## Pendientes delimitados

- Plataforma inicial resuelta: Android dedicado. Quedan por comprobar modelo
  concreto, cámara, solución de bloqueo y arranque automático en el equipo real.
- DNS/origen definitivo, política de bloqueo y cámara: confirmar con agenteVPS
  y el equipo real antes del piloto. No se solicita despliegue en esta sesión.
- A0 de ARGOS: prueba de vida, rendimiento compartido con Caserito y contrato
  compatible para extracción/identificación sin persistencia. Custodia resuelta
  en Trajano-Icarus. Se conoce la batería VPS doc 34 y el runner unittest del
  repo. El plan distingue tareas
  independientes del motor de las que requieren esa decisión y aceptación.

El detalle técnico del spec es una propuesta para revisión e implementación
posterior. No presentar estos pendientes como decisiones ya aprobadas ni el
plan como completamente desbloqueado.
