# Control de acceso — brainstorming de la fase 1

Fecha: 2026-09-25. Alcance de esta sesión: documentación exclusivamente.
El usuario solicita brainstorming, spec y plan; no autoriza implementar.

Referencia: [diseño general](2026-09-25-control-acceso-asistencia-design.md).
Resultado técnico: [spec de fase 1](2026-09-25-control-acceso-fase1-design.md)
y [plan](../plans/2026-09-25-control-acceso-fase1.md).

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

## Evidencia revisada

| Archivo actual | Hallazgo que afecta el plan |
|---|---|
| `Icarus/src/Clientes/Icarus.Clientes.Domain/Modulos.cs` | `ControlAcceso = 2` existe. |
| `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/VerificadorEntitlement.cs` | Verifica funcionalidades; falta una consulta de módulo y elegibilidad para el kiosco. |
| `Icarus/src/Clientes/Icarus.Clientes.Domain/Trabajador.cs` | El cese tiene fecha y no pone `EstaActivo` a false. La elegibilidad debe comprobar ambos datos. |
| `Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs` | El alta actual de trabajador crea también cuenta con correo y contraseña. La fase 1 reutiliza ese alta; no cambia el modelo de cuentas. |
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
- Se propone detección pasiva de presentación fraudulenta en ARGOS para
  evitar pedir gestos en cada marcación. Su eficacia debe medirse; el soporte
  de una biblioteca no certifica la solución ni garantiza detectar todo ataque.
- El contrato de custodia en ARGOS aún debe construirse. Es una dependencia
  explícita de la fase, no una capacidad existente.
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

- Equipo inicial del kiosco: consultado al usuario (Android, Windows o ambos).
  No impide documentar el núcleo web; sí condiciona la aceptación operativa.
- DNS/origen definitivo, política de bloqueo y cámara: confirmar con agenteVPS
  y el equipo real antes del piloto. No se solicita despliegue en esta sesión.
- A0 de ARGOS: decisión de prueba de vida y custodia durable, validación de
  rendimiento y contrato versionado. El plan distingue tareas ejecutables sin
  ARGOS real de tareas dependientes de su aceptación.

El detalle técnico del spec es una propuesta para revisión e implementación
posterior. No presentar estos pendientes como decisiones ya aprobadas ni el
plan como completamente desbloqueado.
