# Plan — tres intentos, incidencias y notificación interna

Estado: bloques 1, 2, 3 y 4 completados y verificados en esta sesión. Ejecutar **solo este bloque** del módulo
ControlAcceso. Leer primero el [spec](../specs/2026-09-29-control-acceso-incidencias-design.md),
el [plan general](2026-09-25-control-acceso-fase1.md), `AGENTS.md` y
`docs/dominio/glosario-avicola.md`. El historial de chat no es requisito.
`docs/ai/HANDOFF.md` narra una sesión del 28 de septiembre y contiene estados
anteriores a este plan; verificar cualquier afirmación suya contra git.

## Reglas de ejecución

- Trabajar en `develop`; preservar los dos cambios ajenos de observabilidad que
  figuran en `git status`. No tocar ARGOS, VPS ni `master`.
- Para cada bloque: escribir una prueba útil, verla fallar por la causa
  esperada, implementar, verla pasar y leer el diff propio. No marcar verde por
  una prueba que nunca estuvo roja.
- Antes de **cada** commit/push de código ejecutar `./verify.ps1` completo con
  Docker activo. Si falla, corregir contenido, no relajar gates. Commit y push
  directos a `develop` tras la puerta. Actualizar este plan con resultados
  reales, sin inventar conteos.
- No registrar biometría, documentos, credenciales ni accesos nominales en logs
  o errores. Datos funcionales privados solo en el dominio y SQL autorizados.

## 1. Backend: flujo durable y contador

Rutas de partida:
`Icarus/src/ControlAcceso/Icarus.ControlAcceso.Domain/OperacionMarcacion.cs`,
`Icarus/src/ControlAcceso/Icarus.ControlAcceso.Application/Marcaciones/RegistrarMarcacionHandler.cs`,
`Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/Persistencia/ControlAccesoDbContext.cs`,
`Icarus/src/Host/Icarus.Host/Endpoints/KioscoMarcacionesEndpoints.cs` y
`Icarus/tests/Icarus.IntegrationTests/ControlAcceso/MarcacionesTests.cs`.
Hoy los rechazos faciales vuelven directamente desde el handler sin operación
durable; esa brecha impide contar intentos con seguridad.

- [x] Crear `Icarus/tests/Icarus.IntegrationTests/ControlAcceso/IntentosMarcacionTests.cs`.
  Resultado: 6/6 verdes (entregado en `dfd573a`).
- [x] Añadir entidad de flujo e incidencia en
  `Icarus/src/ControlAcceso/Icarus.ControlAcceso.Domain/`; puertos en
  `Icarus/src/ControlAcceso/Icarus.ControlAcceso.Application/Marcaciones/` y
  repositorios/configuraciones en `Icarus/src/ControlAcceso/Icarus.ControlAcceso.Infrastructure/`.
  Migración y restricciones incluidas (entregado en `dfd573a`).
- [x] Distinguir rechazo facial concluyente de error técnico y conflicto de
  secuencia. Persistir resultado por clave de captura y contador en una unidad
  coherente; tercero crea incidencia. Consulta/reconciliación usa la misma
  operación, sin reenviar foto. Una captura fallida no crea `JornadaAcceso`
  (entregado en `dfd573a`).
- [x] Extender `KioscoMarcacionesEndpoints.cs` y DTOs con identificador opaco
  de flujo, contador y consulta de operación; respetar autorización Kiosco y
  antiforgery. No aceptar tenant o contador proporcionado por el navegador
  (entregado en `dfd573a`).
- [x] Ejecutar `dotnet test ... --filter FullyQualifiedName~IntentosMarcacionTests`:
  6/6 verdes. Commit previsto entregado:
  `feat(control-acceso): limita a tres intentos la marcación`.

## 2. Backend: bandeja, resolución y notificación

Rutas de partida:
`Icarus/src/ControlAcceso/Icarus.ControlAcceso.Application/Marcaciones/RegistrarMarcacionManualHandler.cs`,
`Icarus/src/Host/Icarus.Host/Endpoints/ControlAccesoJornadasEndpoints.cs` y
`Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionInterna.cs`.
La última es solo referencia de patrón: pertenece a pedidos avícolas.

- [x] Crear `Icarus/tests/Icarus.IntegrationTests/ControlAcceso/IncidenciasAccesoTests.cs`.
  Rojo verificado: tercero sin aviso, duplicación de aviso, acceso entre tenants,
  resolución sin motivo, corrección fallida que cierra incidencia, descarte que
  crea marcación y doble resolución concurrente.
- [x] Persistir notificación propia de ControlAcceso en la misma operación que
  crea la incidencia; unicidad por incidencia, destinatario tenant y enlace
  opaco. Si falla la escritura, no dejar incidencia sin aviso. Texto genérico.
- [x] Crear endpoints de lista/detalle/resolución/descarte en
  `Icarus/src/Host/Icarus.Host/Endpoints/ControlAccesoIncidenciasEndpoints.cs`
  con política `ClienteConControlAcceso`. Resolver vincula una marcación manual
  con las mismas validaciones; descartar exige motivo. Las mutaciones son
  idempotentes y admiten `VersionEsperada`.
- [x] Ejecutar `dotnet test ... --filter FullyQualifiedName~IncidenciasAccesoTests`:
  8/8 verdes. Luego `./verify.ps1` verde (frontend OK; backend: 6 arquitectura,
  574 unit, 227 GestorCaisy, 267 integración). Commit y push directos a develop
  con `feat(control-acceso): registra y notifica incidencias`.

## 3. Web: kiosco y administración

Rutas de partida: `web/src/kiosco/MarcacionKioscoPage.tsx`,
`web/src/kiosco/apiKiosco.ts`, `web/src/kiosco/useCapturaFacial.ts`,
`web/src/features/control-acceso/HistorialAccesoPage.tsx`,
`web/src/features/control-acceso/MarcacionManualDialog.tsx` y
`web/src/features/control-acceso/api.ts`. Las pruebas correspondientes viven
junto a esos componentes. Hoy el kiosco usa «Capturar» manual y presenta error
tras el primer rechazo; hay que ajustarlo al spec sin romper confirmación de
Entrada/Salida ni propuesta de Salida.

- [x] Añadir pruebas de UI que fallen por la causa correcta: «Iniciar captura»,
  cámara lista, cuenta visible 3–2–1 y una foto automática por intento; primer
  y segundo rechazo muestran intentos restantes; tercero confirma incidencia
  y libera la tablet. Cámara denegada y respuesta incierta no consumen intento.
  Resultado: `MarcacionKioscoPage.test.tsx` 8/8 verdes.
- [x] Integrar identificador de flujo y claves nuevas por captura. El contador
  visible viene del backend; doble toque no dispara dos peticiones. Mantener
  una sola operación del kiosco a la vez, limpieza de foto/nombre y retorno a
  listo para la fila.
- [x] Añadir bandeja de incidencias al módulo del Cliente, navegación desde
  notificación interna y diálogo para identificar trabajador, registrar o
  corregir manualmente con motivo, o descartar con motivo. Diferenciar hora de
  la incidencia y hora declarada. Probar autorización y ausencia de identidad
  en el aviso. Resultado: `IncidenciasAccesoPage.test.tsx` 4/4 verdes;
  `CampanaNotificaciones.test.tsx` 2/2 verdes.
- [x] Ejecutar desde `web/`:
  `npm run test -- src/kiosco src/features/control-acceso` (12/12 verdes),
  `npm run lint` y `npm run build` verdes. Luego `./verify.ps1` verde.
  Commit previsto: `feat(control-acceso): completa kiosco y bandeja de incidencias`.

## 4. Integración, privacidad y cierre de este bloque

- [x] Extender
  `Icarus/tests/Icarus.IntegrationTests/ControlAcceso/PrivacidadAccesoTests.cs`
  y `web/src/kiosco/privacidadKiosco.test.tsx` para incidencias y avisos con
  centinelas sintéticos. Verificar que no aparecen foto, plantilla, candidato,
  identidad ni hora nominal en logs, errores, URL o almacenamiento del kiosco.
  Resultado: `PrivacidadAccesoTests` 2/2 verdes; `privacidadKiosco.test.tsx`
  2/2 verdes. Ningún centinela aparece en logs ni almacenamiento.
- [x] Probar los recorridos de 1, 2 y 3 rechazos; éxito en segundo/tercero;
  respuesta perdida; doble toque; salida sin entrada; cancelación; reinicio;
  resolución y descarte. Resultados reales:
  - `IntentosMarcacionTests`: 6/6 verdes.
  - `MarcacionKioscoPage.test.tsx`: 8/8 verdes.
  - `IncidenciasAccesoPage.test.tsx`: 4/4 verdes.
  - A0/T6 (ARGOS real/PAD) y T13 (tablet física/piloto) quedan pendientes.
- [x] Ejecutar `./verify.ps1`: verde. No declarar terminados A0/T6 ni T13:
  ARGOS real, PAD, tablet física, capacidad y despliegue pertenecen a esos
  bloques pendientes.

El agente debe detenerse y comunicar un bloqueo concreto si falta una decisión
externa indispensable; no inventar contratos de ARGOS ni declarar piloto verde
con dobles de prueba. Este documento autoriza una implementación futura del
bloque por el agente que reciba el encargo; esta entrega solo crea spec y plan.
