# Plan — Borrar borradores y corregir publicaciones de precios de alimento

## Objetivo

Permitir que un GestorCaisy descarte una Notificación de Precios de Alimentos
importada desde PDF o Excel mientras siga en `Borrador`, y definir en la UI el
tratamiento correcto de una notificación publicada con datos incorrectos, sin
romper la inmutabilidad, la auditoría ni los precios congelados de pedidos ya
enviados.

## Contexto existente

- La entidad global es `NotificacionPreciosAlimentos`.
- Una importación PDF/XLSX siempre crea un borrador editable.
- La entidad ya tiene `EstaActivo` para borrado lógico y el filtro global de EF
  excluye registros inactivos.
- El dominio ya tiene `AnularFutura(DateOnly hoy)`: solo permite anular una
  publicación cuya `VigenteDesde` todavía no ha comenzado.
- Las publicaciones efectivas son inmutables.
- Los pedidos guardan un snapshot de precio y de la notificación al enviarse;
  una corrección posterior no debe recalcularlos.
- La API vive en `Icarus/src/Host/Icarus.Host/Endpoints/` y la aplicación
  server-rendered en `Icarus/src/Apps/Trajano.GestorCaisy/`.

## Decisiones de negocio

1. **Borrador**
   - Se puede eliminar mediante borrado lógico.
   - La operación exige confirmación en la UI.
   - Debe ser idempotente y estar protegida por la misma política de
     autorización de `GestorPedidoAlimento`.
   - No se elimina físicamente el documento original ni se registra contenido
     del archivo en logs. El registro queda inactivo para auditoría técnica.

2. **Publicación futura**
   - Se mantiene la operación de anulación existente.
   - La UI debe mostrar `Anular publicación`, pedir un motivo obligatorio y
     conservar el registro con estado `Anulada`.
   - Tras anularla, el gestor debe importar o crear otra notificación corregida.

3. **Publicación efectiva**
   - No se puede editar, borrar ni anular.
   - La corrección se hace creando una nueva notificación publicada con los
     datos correctos y una fecha `VigenteDesde` explícita.
   - La UI debe explicar esta regla y ofrecer `Importar corrección` o
     `Importar otra notificación`, sin modificar silenciosamente la publicación
     anterior.
   - Los pedidos ya enviados conservan su precio y el identificador de la
     publicación original.

4. **Auditoría y errores**
   - Las acciones deben dejar actor técnico, fecha y motivo donde el modelo de
     auditoría existente lo permita.
   - Los mensajes mostrados al usuario deben ser genéricos y no incluir datos
     sensibles ni contenido del documento.

## Alcance

Incluye dominio, aplicación, endpoint HTTP, cliente HTTP de GestorCaisy,
controlador, modelos/vistas y pruebas. No incluye modificar pedidos existentes,
recalcular balances, cambiar el formato de los importadores ni crear un proceso
automático de corrección de precios.

## Plan de implementación TDD

### 1. Inspección y contrato

Leer antes de modificar:

- `docs/dominio/glosario-avicola.md`.
- `docs/superpowers/specs/2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md`.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionPreciosAlimentos.cs`.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs`.
- `Icarus/src/Host/Icarus.Host/Endpoints/PreciosAlimentosEndpoints.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/IApiIcarusClient.cs` y
  `ApiIcarusClient.cs`.
- `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosController.cs`.
- Vistas y pruebas existentes de precios.

Confirmar los nombres reales de las abstracciones de repositorio, auditoría,
excepciones y control de concurrencia antes de escribir código.

### 2. Borrado lógico de borrador en backend

Escribir primero pruebas rojas en los tests unitarios de GestionAvicola:

- un borrador puede descartarse y queda `EstaActivo == false`;
- una publicación no puede descartarse por esta operación;
- una notificación anulada no puede descartarse como borrador;
- repetir la operación no produce una segunda transición ni un error técnico
  inesperado;
- un conflicto de concurrencia se traduce al error de aplicación ya utilizado
  por edición/publicación.

Implementar después:

- método explícito del agregado, por ejemplo `DescartarBorrador()`, que valide
  `Estado == Borrador` y cambie `EstaActivo`;
- comando/handler de aplicación con autorización, consulta del agregado,
  control de concurrencia y persistencia;
- auditoría técnica usando el patrón existente;
- endpoint `DELETE /api/precios-alimentos/{id}` (o el verbo/ruta equivalente
  que ya use el proyecto), limitado a borradores.

No exponer setters genéricos ni borrar físicamente filas.

### 3. Cliente HTTP y MVC de GestorCaisy

Añadir pruebas rojas/verdes en `Trajano.GestorCaisy.Tests` para:

- que `IApiIcarusClient` invoque la ruta de descarte;
- que el controlador solo ofrezca la acción para un borrador;
- que la acción requiera antiforgery, confirme la operación y redirija al
  listado después de éxito;
- que muestre el error de negocio de la API sin filtrar detalles internos;
- que publicaciones futuras conserven la acción de anular existente;
- que publicaciones efectivas no muestren anular ni borrar y muestren la
  explicación/acción de crear una corrección.

Implementar:

- método `DescartarBorradorAsync` en el cliente y su contrato;
- acción POST MVC con confirmación explícita y antiforgery;
- modelo de vista con capacidades derivadas del estado y de la fecha de
  negocio, evitando duplicar reglas contradictorias del dominio;
- botón `Eliminar borrador` y página/modal de confirmación;
- textos en español correcto, con acentos y UTF-8 sin BOM.

### 4. Tratamiento visible de publicaciones incorrectas

Mantener la anulación de publicaciones futuras, pero mejorar el flujo si falta
alguna pieza:

- aceptar y persistir motivo de anulación si el contrato actual todavía no lo
  soporta;
- mostrar estado `Anulada` en el historial;
- para una publicación efectiva, mostrar una explicación clara y un enlace a
  importar una nueva notificación, sin permitir edición ni anulación;
- asegurar que la fecha de vigencia de la corrección sea visible antes de
  publicar.

Si añadir motivo requiere una migración o ampliar el contrato de auditoría,
documentarlo en el plan de ejecución y mantener compatibilidad con los clientes
existentes. No inventar una migración si el mecanismo de auditoría actual ya
conserva el motivo.

### 5. Pruebas de integración y regresión

Añadir o ampliar pruebas de `Icarus.IntegrationTests` para verificar con la API:

- importar crea un borrador;
- eliminarlo hace que no aparezca en el listado normal;
- obtener o eliminar una publicación devuelve el error de negocio apropiado;
- anular una publicación futura sigue funcionando;
- anular una publicación efectiva sigue rechazándose;
- una nueva publicación corregida no cambia los snapshots de pedidos enviados.

Añadir pruebas MVC de flujo completo para la visibilidad de las acciones y la
confirmación antiforgery.

### 6. Verificación y cierre

Ejecutar durante TDD los tests dirigidos y comprobar que cada test nuevo se vio
fallar por el motivo esperado antes de implementar. Al integrar ejecutar:

```powershell
dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests/Trajano.GestorCaisy.Tests.csproj
dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj
.\verify.ps1
```

Los tests de integración requieren Docker corriendo. Si no se ejecutan, anotar
el motivo exacto en el cierre. Revisar `git diff --check`, leer el diff propio y
preservar los cambios ajenos existentes. No usar `--no-verify` ni relajar gates.

## Criterios de aceptación

- Un borrador importado desde PDF o XLSX puede eliminarse desde GestorCaisy con
  confirmación y desaparece del listado normal.
- La eliminación es lógica, autorizada, concurrente e idempotente.
- Una publicación futura puede anularse con trazabilidad.
- Una publicación efectiva no puede borrarse ni editarse.
- La UI guía al gestor hacia una nueva publicación corregida.
- Ningún pedido enviado ni balance histórico cambia por una corrección.
- API, MVC, pruebas y mensajes mantienen el aislamiento, la auditoría y la
  política anti-PII del proyecto.

## Commit previsto

`feat(precios): descartar borradores y corregir publicaciones`
