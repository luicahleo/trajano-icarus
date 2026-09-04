# SP8C — Entrega, recepción y balance de alimento

**Objetivo:** completar el ciclo con una entrega/nota, respaldo privado,
recepción por línea y reconocimiento correcto del gasto.

**Spec:** `docs/superpowers/specs/2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md`.

**Dependencia:** SP8B integrado hasta `Aceptado`.

## Reglas de ejecución

- Una entrega y una nota por pedido; varias imágenes por nota.
- Datos de nota manuales; sin OCR en SP8.
- Imágenes privadas, sin Base64/URL pública/ruta física en SQL ni contenido en Seq.
- Solo recepciones finales cuentan en balance, usando cantidad real y precio congelado.
- TDD estricto, Docker para integración y `./verify.ps1` antes del commit.

## Tarea 1 — Entrega y despacho

**Crear/modificar:**

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EntregaPedidoAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DetalleEntregaPedidoAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PedidoAlimento.cs`
- Application/Infrastructure de `PedidosAlimento`.
- `Icarus/tests/Icarus.UnitTests/GestionAvicola/EntregaPedidoAlimentoTests.cs`

- [x] Rojo: `RegistrarDespacho` solo desde `Aceptado`, crea exactamente una
  entrega/nota y pasa a `Despachado`.
- [x] Rojo: número/fecha de nota y líneas manuales obligatorios; cantidades
  enteras en unidad de presentación; admite diferencias contra lo solicitado.
- [x] Rojo: total informado se conserva para contraste, sin reemplazar el total
  calculado; un segundo despacho o nota devuelve conflicto.
- [x] Verde mínimo y persistencia con índices/constraints.
- [x] Commit previsto: `feat(avicola): registrar despacho de alimento`.

## Tarea 2 — Almacén privado de respaldos

**Crear/modificar:**

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DocumentoNotaEntrega.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Documentos/`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Documentos/`
- opciones de Host y manifiestos Docker/despliegue.
- tests `AlmacenDocumentosPedidoTests.cs` y pruebas de integración de descarga.

- [x] Rojo: validar firma real, MIME permitido, tamaño, dimensiones y límite de
  páginas; rechazar polyglots/extensión falsa.
- [x] Implementar `IAlmacenDocumentosPedido` con claves UUID y escritura
  atómica: conservar original inmutable con hash SHA-256 y generar una copia
  segura para pantalla con orientación normalizada, metadatos eliminados y
  compresión legible.
- [x] SQL guarda clave lógica/metadata; volumen queda fuera del web root. Definir
  backup externo y prueba de restauración en documentación operativa.
- [x] Endpoint autenticado autoriza tenant propietario o CAISY funcional; nunca
  revela existencia a terceros. Servir la vista derivada inline y el original
  solo como adjunto, con cabeceras seguras.
- [x] Reemplazar antes de recepción desactiva la versión previa y conserva auditoría.
- [x] Commit previsto: `feat(avicola): guardar respaldos privados de notas`.

## Tarea 3 — Confirmación de recepción

**Crear/modificar:** dominio, commands/queries y tests de `PedidosAlimento`.

- [x] Rojo: Cliente o Trabajador con `PedidoAlimento` confirma desde
  `Despachado` y registra cantidad realmente recibida por cada línea.
- [x] Rojo: coincidencia completa termina `RecibidoConforme`; cualquier
  diferencia termina `RecibidoConDiferencias`; ambos son terminales.
- [x] Rojo: no se omiten líneas, no hay cantidades negativas/fraccionarias ni
  acceso de otro tenant; reintento no duplica transición/notificación.
- [x] Calcular y persistir snapshot de diferencias y total recibido.
- [x] Notificar resultado a CAISY dentro de la misma transacción.
- [x] Commit previsto: `feat(avicola): confirmar recepcion de alimento`.

## Tarea 4 — API e interfaces de despacho/recepción

**Modificar:**

- `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs`
- `web/src/features/pedidos-alimento/`
- `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/`, `Views/` y cliente API.
- tests de integración, React y MVC correspondientes.

- [x] Rojo: CAISY carga datos y varias imágenes, revisa el resumen y despacha.
- [x] Rojo: tenant visualiza nota histórica, compara solicitado/despachado,
  informa recibido por línea y confirma el estado final.
- [x] Mostrar diferencias numéricas y total sin inferir resolución comercial.
- [x] Verificar descarga autenticada, teclado, foco, errores y responsive de la PWA.
- [x] Commit previsto: `feat(ui): completar entrega y recepcion de alimento`.

## Tarea 5 — Balance por recepción real

**Crear/modificar:** localizar primero el agregado/query vigente de balance; crear
`Icarus.GestionAvicola.Application/BalanceAlimentos/` si aún no existe, con sus
repositorios, endpoints y pruebas.

- [x] Rojo: `Borrador`, `Solicitado`, `Aceptado`, `Rechazado` y `Despachado`
  aportan cero.
- [x] Rojo: ambos estados recibidos suman
  `equivalentes realmente recibidos × PrecioFinalPor40Kg` por línea.
- [x] Rojo: precio vigente posterior y total manual de nota no cambian un
  pedido recibido; filtros de rango/tenant son correctos.
- [x] Añadir índices de consulta y decidir cálculo agregado frente a proyección
  solo con medición; empezar con consulta SQL canónica para evitar doble fuente.
- [x] Commit previsto: `feat(avicola): calcular balance de alimento recibido`.

## Tarea 6 — Operación y cierre

- [ ] Documentar volumen, cuotas, monitorización, backup/restauración y migración
  futura a S3 compatible en `docs/operacion/`.
- [ ] Probar pérdida de archivo, volumen no escribible y hash incorrecto sin
  filtrar rutas ni contenido a Seq.
- [ ] Ejecutar suites completas, `./verify.ps1`, `git diff --check` y revisar diff.
- [ ] Actualizar glosario, `AGENTS.md`, adaptadores y este plan.
- [ ] Commit final y push a `develop` solo si toda la puerta está verde.
