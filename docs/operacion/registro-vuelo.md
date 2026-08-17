# Guía operativa del registro de vuelo

Esta guía ayuda a interpretar errores o comportamientos inesperados en la
narración estructurada de Icarus. El contrato completo está en
`docs/superpowers/specs/2026-08-17-registro-vuelo-seq-design.md` y las consultas
generales de observabilidad en `docs/operacion/observabilidad.md`.

## Qué es un registro de vuelo

Un registro de vuelo es la secuencia técnica de una mutación: inicio,
decisiones relevantes, validaciones, persistencia, transacciones,
compensaciones y resultado. La correlación principal es el `TraceId` generado
por ASP.NET para la petición. No existe un segundo identificador de vuelo.

La infraestructura usa `ILogger`; Serilog escribe los eventos en consola JSON y
los envía a Seq cuando `Seq:Url` está configurado. La caída de Seq no debe
alterar el resultado funcional.

## Diagnóstico inicial

1. Obtener el `TraceId` de la respuesta HTTP (`X-Trace-Id`) o de la entrada en
   Seq.
2. Filtrar por aplicación y ordenar cronológicamente:

   ```text
   Aplicacion = 'Icarus' and TraceId = '0123456789abcdef0123456789abcdef'
   ```

3. Revisar primero `operation.started` y el último evento de la operación.
4. Comparar la secuencia con la operación y el contexto de persistencia.
5. Buscar el mismo `TraceId` en `backend.error` y
   `http.request.completed`.

Consultas útiles:

```text
Aplicacion = 'Icarus' and TraceId = '...' and Operation = 'clientes.alta_con_cuenta'
Aplicacion = 'Icarus' and TraceId = '...' and EventName = 'operation.decision'
Aplicacion = 'Icarus' and TraceId = '...' and Outcome = 'rejected'
Aplicacion = 'Icarus' and TraceId = '...' and Outcome = 'failed'
Aplicacion = 'Icarus' and TraceId = '...' and PersistenceContext = 'Clientes'
Aplicacion = 'Icarus' and TraceId = '...' and PersistenceContext = 'Identity'
Aplicacion = 'Icarus' and TraceId = '...' and Release = '...'
```

## Interpretación de eventos

| Evento | Interpretación | Acción habitual |
|---|---|---|
| `operation.started` | Comenzó una mutación registrable. | Confirmar que el nombre sea el esperado. |
| `operation.decision` | Una decisión cambió el recorrido. | Revisar `ReasonCode`, nunca un valor personal. |
| `operation.completed` | La operación terminó correctamente. | Comparar con el resultado HTTP. |
| `operation.rejected` | Una regla esperada impidió completar. | Revisar `ReasonCode`; no buscar mensajes de usuario en logs. |
| `operation.failed` | Hubo un error inesperado. | Buscar el `backend.error` del mismo `TraceId`. |
| `persistence.save_changes.completed` | EF guardó cambios. | Revisar `PersistenceContext`, `RowsAffected` y `DurationMs`. |
| `persistence.save_changes.failed` | EF no pudo guardar. | Revisar `backend.error`; no se registran entidades ni SQL. |
| `transaction.committed` | EF confirmó una transacción física. | Es un commit técnico, no una compensación. |
| `transaction.rolled_back` | EF revirtió una transacción física. | Confirmar qué escritura estaba dentro de ella. |
| `operation.compensation.*` | Se ejecutó compensación lógica de negocio. | No interpretarla como rollback físico. |
| `backend.error` | Diagnóstico de una excepción no controlada. | Usar `ErrorId` y `TraceId`; conservar el stack solo en el sistema de logs. |
| `http.request.completed` | Resumen del transporte HTTP. | Comparar ruta, estado y duración sin duplicar la narración. |

`operation.failed` indica que la operación quedó incompleta, pero no duplica la
excepción. El mensaje y el stack deben buscarse en `backend.error`.

## Caso piloto: alta de cliente con cuenta

El camino correcto suele contener, en orden aproximado:

1. `operation.started` para `clientes.alta_con_cuenta`.
2. `operation.decision` con `account_identifier_available`.
3. `operation.started` para `clientes.crear`.
4. Persistencia en `Clientes` y, si corresponde, commit físico.
5. Registro de la cuenta en Identity.
6. Persistencia en `Identity` y, si corresponde, commit físico.
7. `operation.completed` con `Outcome=succeeded`.

Si Identity rechaza la cuenta después de crear el cliente:

1. `operation.decision` con `ReasonCode=identity_rejected`.
2. `operation.compensation.started` para
   `clientes.suspender_alta_incompleta`.
3. Persistencia de la suspensión en `Clientes`.
4. `operation.compensation.completed` con `Outcome=compensated`.
5. `operation.rejected` de la operación raíz.

Si la compensación falla, debe aparecer
`operation.compensation.failed` y la raíz debe terminar como `failed`. No debe
aparecer una afirmación de rollback físico si la primera escritura ya fue
confirmada.

## Problemas frecuentes

### Falta `TraceId`

Verificar que la petición haya pasado por `RequestObservabilityMiddleware` y que
se esté consultando el mismo `TraceId` de `X-Trace-Id`. Una petición sin
`Activity.Current` sigue recibiendo el identificador generado por el middleware.

### No aparece la narración en Seq

Comprobar `Seq:Url`, conectividad del sink y la propiedad `Aplicacion='Icarus'`.
Revisar primero la consola JSON: Seq es opcional y no es la única fuente de los
eventos.

### Aparece `operation.failed` sin `backend.error`

Revisar el orden cronológico, el `TraceId` y el `ErrorId`. El evento de
operación no contiene la excepción por diseño; si falta `backend.error`, el
problema está en el manejo global o en la correlación del proveedor de logs.

### Falta un evento de persistencia

Confirmar que se usó uno de los contextos instrumentados (`Clientes` o
`Identity`) y que el `DbContext` se resolvió desde el contenedor de DI. Las
migraciones y semillas de arranque no forman parte del vuelo de una petición.

### Se añadió un campo y desapareció

Comprobar el descriptor de la operación. El campo debe estar declarado con el
tipo correcto y no pertenecer a la lista prohibida. Los campos desconocidos,
incorrectos o sensibles se omiten deliberadamente.

## Regla anti-PII

Nunca añadir al registro correo, NIT, nombres, razón social, documentos,
teléfonos, direcciones, contraseñas, hashes, tokens, cookies, credenciales,
biometría, `TrabajadorId`, cuerpos, entidades, SQL, parámetros ni mensajes de
excepción o validación que puedan contener entrada del usuario.

Para correlacionar un dato protegido usar presencia, categoría, longitud o un
código cerrado. No crear hashes directos de valores de baja entropía.

## Verificación para cambios futuros

Después de modificar la taxonomía, el behavior, los interceptores o el piloto:

```powershell
dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter "FullyQualifiedName~RegistroVuelo"
dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj --filter "FullyQualifiedName~PersistenciaRegistroVuelo|FullyQualifiedName~RegistroVueloAltaCliente"
./ejecutar-puerta-calidad.ps1
```

La puerta completa debe quedar verde antes de commit y push. Si el cambio deja
la feature a medias, documentar el estado en `docs/ai/HANDOFF.md` usando la
plantilla; eliminarlo únicamente al cerrar la feature.
