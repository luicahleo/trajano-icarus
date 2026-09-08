# SP8D — Evidencia fotográfica del receptor y recibo imprimible de CAISY

Diseño validado mediante brainstorming con el usuario el 2026-09-08, como
continuación de SP8/SP8C (`2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md`),
ya implementado y en verde en `develop`.

## Objetivo

Cerrar la brecha de evidencia física en la recepción de un pedido de alimento:
hoy el tenant (Cliente o Trabajador con `PedidoAlimento`) confirma la
recepción marcando cantidades por línea, sin adjuntar ningún respaldo del
documento físico que recibió. SP8D agrega esa evidencia y, a la vez, quita
fricción del lado de CAISY reemplazando su carga de fotos (SP8C) por un
recibo imprimible generado desde los datos que ya escribe al despachar.

## Flujo físico de referencia

1. CAISY registra el despacho con los datos de la nota que emite (número,
   fecha, total informado, cantidades por línea) — sin cambios sobre SP8C.
2. CAISY puede imprimir un recibo con esos mismos datos, firmarlo/sellarlo en
   papel y entregarlo al transportista junto con la mercadería. El
   transportista no tiene ningún acceso al sistema (exclusión ya vigente
   desde SP8: "transportistas y proceso interno de preparación de CAISY").
3. El transportista entrega la mercadería y una copia de la nota en la
   granja. El receptor (Cliente o Trabajador) confirma cantidades por línea y
   adjunta una foto de su copia de la nota, en el mismo paso.

Las dos evidencias fotográficas contempladas en el diseño previo de SP8C
(CAISY al despachar, receptor al recibir) se reducen a **una sola, del
receptor**: el dato de CAISY ya vive en la base al registrar el despacho, y
someterlo además a una foto por cada uno de los *n* pedidos que gestiona
agregaba fricción sin evidencia adicional real (su "fuente" es el propio
sistema, no un papel externo). La evidencia que sí falta es la que prueba lo
que físicamente llegó a la granja, por eso el respaldo fotográfico es
exclusivamente responsabilidad del receptor.

## Alcance

Incluye:

- Foto obligatoria del receptor, adjuntada en el mismo paso que la
  confirmación de cantidades (una acción, un envío).
- Recibo PDF generado por el servidor en Trajano.GestorCaisy con los datos
  del despacho, incluidos precios y subtotales congelados; reimprimible sin
  límite de veces sobre cualquier pedido con entrega registrada.
- Eliminación completa de la carga de respaldo fotográfico por parte de
  CAISY (SP8C): comando, handler, endpoint, UI y pruebas asociadas.

Fuera de alcance (se mantiene lo ya excluido en SP8):

- transportista como actor del sistema;
- conciliación automática de diferencias;
- OCR de notas, correo, SMS, push;
- múltiples entregas o notas por pedido;
- funcionamiento offline (la feature sigue siendo siempre online).

No se agregan estados nuevos a la máquina de `PedidoAlimento`: SP8D opera
dentro de la transición existente `Despachado → RecibidoConforme /
RecibidoConDiferencias`.

## Eliminación de la carga de CAISY (SP8C)

Se retira por completo:

- `AgregarDocumentoNotaCommand`, `AgregarDocumentoNotaHandler`,
  `AgregarDocumentoNotaValidator`.
- El método de dominio `PedidoAlimento.AgregarDocumentoNota` y
  `PedidoAlimento.ReemplazarDocumentoNota`, y
  `EntregaPedidoAlimento.ReemplazarDocumento` (ya no hace falta sustitución:
  la foto del receptor se crea una sola vez, atómicamente, junto con la
  confirmación).
- El endpoint de subida de respaldo en la API.
- La sección de carga de imágenes en la pantalla de despacho de
  Trajano.GestorCaisy.
- Las pruebas unitarias e de integración que cubrían esas rutas; se agrega en
  su lugar una prueba que confirma que el endpoint retirado ya no existe
  (404/405).

`RegistrarDespachoPedidoCommand` no cambia: sigue siendo la fuente de
número de nota, fecha, total informado y cantidades entregadas por línea —
esos datos, ya persistidos, son los que alimentan el recibo PDF.

## Modelo de datos

`DocumentoNotaEntrega` (dominio, ya existe) pasa a representar
exclusivamente el respaldo fotográfico del receptor; no necesita un campo de
origen porque ya no hay más que una procedencia posible. Se mantiene:

- el mismo almacén privado `IAlmacenDocumentosPedido` y su pipeline (valida
  firma/MIME/tamaño/dimensiones, conserva el original inmutable con hash,
  genera una copia de visualización segura: corrige orientación, quita
  metadatos EXIF/GPS y comprime);
- SQL conserva solo clave lógica opaca, MIME, tamaño, hash, nombre seguro —
  nunca Base64, ruta física ni URL pública;
- `docs/operacion/respaldos-notas.md` sigue aplicando tal cual, ahora
  entendido como "respaldo del receptor" en lugar de "respaldo de CAISY";
  se actualiza su texto para reflejarlo.

Cuota: el límite configurable `MaxDocumentosPorNota` (antes pensado para las
hasta 8 imágenes de CAISY) se reemplaza por un límite único de **1
documento activo por entrega**, el del receptor, configurable igual que hoy
en `AlmacenDocumentosPedido`. No existe comando de reemplazo posterior: la
recepción es terminal (spec SP8: "no se reabre ni concilia posteriormente
el pedido"), así que no hay oportunidad de sustituir la foto después de
confirmar. Antes de confirmar, la elección del archivo es puramente local en
el navegador (el usuario puede volver a elegir otra foto las veces que
quiera): no se sube nada al servidor hasta el envío final.

## Flujo de confirmación de recepción (un solo paso)

`ConfirmarRecepcionPedidoCommand` se extiende: además de `PedidoId` y
`LineasRecibidas`, recibe `Stream Contenido` y `NombreArchivo` (multipart).
El validador exige `Contenido` no nulo — sin foto, el comando se rechaza
antes de tocar el agregado.

`ConfirmarRecepcionPedidoHandler`, dentro de la misma transacción:

1. Guarda el archivo vía `IAlmacenDocumentosPedido` (mismo pipeline de
   validación y generación de vista segura ya existente).
2. Agrega el `DocumentoNotaEntrega` a la entrega.
3. Llama a `pedido.ConfirmarRecepcion(...)` sin cambios en su lógica de
   negocio actual (comparación de líneas, cálculo de diferencias, transición
   de estado).
4. Notifica a CAISY, registra vuelo, guarda cambios.

Si algo falla a mitad de camino, la transacción entera revierte: no queda un
documento huérfano ni una recepción a medio confirmar. Un reintento por
doble clic sigue chocando con el estado (`Despachado` ya no lo es) y
responde 409, igual que hoy.

En la PWA (`PedidoAlimentoDetallePage.tsx`), el bloque "Confirmar recepción"
suma un control de cámara/archivo junto a los campos de cantidad. Antes de
subir, el navegador redimensiona/comprime la imagen (canvas, calidad
ajustada) pensando en conectividad rural — es una optimización de ancho de
banda; el servidor igual reprocesa la imagen sin importar la compresión
previa (defensa en profundidad, no todo cliente comprime igual). El botón
"Confirmar recepción" queda deshabilitado hasta que haya una foto
seleccionada.

## Recibo PDF en Trajano.GestorCaisy

Nueva query de solo lectura, sin tocar el agregado ni la máquina de estados:
`ObtenerReciboPedidoPdfQuery(PedidoId)`, autorizada igual que el resto de
`GestorPedidoAlimento`, disponible sobre cualquier pedido con `Entrega`
(Despachado, RecibidoConforme o RecibidoConDiferencias) — reimprimible sin
límite de veces.

Contenido: número y fecha de nota, fecha de despacho, cliente/granja
destino, líneas (tipo, presentación, cantidad entregada, precio congelado
por 40 kg, subtotal), total informado y total canónico. Todo dato ya
persistido — no se inventa ni recalcula nada nuevo.

Implementación: nueva librería de generación de PDF (el proyecto hoy solo
*importa* PDF, nunca genera uno). Se recomienda **QuestPDF** — licencia
Community gratuita mientras los ingresos anuales de la organización que lo
usa sean menores a USD 1M, sin binario nativo externo (a diferencia de
wkhtmltopdf), corre bien en el contenedor Linux ya usado. Si ese umbral de
licencia se vuelve una duda real más adelante, la alternativa es
`PdfSharpCore` (sin restricción de ingresos, con menos funciones de layout).

Expuesta como endpoint `GET /pedidos/{id}/recibo.pdf` en la API, consumida
por un controller action en Trajano.GestorCaisy que la sirve para
descarga/impresión.

## Cambios de UI en Trajano.GestorCaisy

Se retira por completo la sección de carga de respaldo del flujo de
despacho. En su lugar, el detalle de un pedido con entrega (despachado o
posterior) muestra el botón **"Imprimir recibo"** como única acción
relacionada a documentos del lado de CAISY.

## Observabilidad y anti-PII

Mismo contrato ya vigente: el registro de vuelo de `avicola.pedidos.recibir`
suma el hecho de que hubo un adjunto (siempre 1, dado que la foto es
obligatoria) — nunca nombre de archivo, número de nota ni contenido de
imagen. La generación del PDF no registra datos de negocio, solo el id
técnico del pedido y el resultado (éxito/error), igual que las descargas de
documentos existentes.

## Testing (TDD)

- **Dominio:** `ConfirmarRecepcion` sigue intacta en su lógica de
  diferencias (no se toca); se agrega la creación del `DocumentoNotaEntrega`
  del receptor dentro del mismo método de agregado.
- **Aplicación:** `ConfirmarRecepcionPedidoHandler` rechaza sin `Contenido`;
  transacción atómica (falla de almacén revierte todo, sin documento
  huérfano); reintento tras confirmación exitosa responde 409 sin duplicar.
- **Aplicación (retiro SP8C):** se eliminan las pruebas de
  `AgregarDocumentoNotaHandler`/`ReemplazarDocumentoNota`; se agrega una
  prueba que confirma que el endpoint de carga de CAISY ya no existe
  (404/405).
- **Integración (Testcontainers.MsSql):** flujo completo Despachado →
  confirmar con foto → RecibidoConforme/ConDiferencias, documento
  persistido y asociado a la entrega.
- **PDF:** la query genera un PDF válido con los datos esperados;
  autorización (solo `GestorPedidoAlimento`); 404 genérico si el pedido no
  tiene entrega o es de otro alcance.
- **Frontend (PWA):** el botón de confirmar queda deshabilitado sin foto;
  la compresión client-side no rompe el flujo si el navegador no soporta
  canvas (fallback: sube el original tal cual, el servidor igual lo
  reprocesa).

## Estrategia de implementación

Un solo bloque, dado el tamaño acotado: retiro de la carga de CAISY,
extensión de la confirmación de recepción y recibo PDF pueden avanzar en
paralelo dentro de la misma rama, con TDD por componente y una migración de
base de datos si el modelo actual requiere ajustar la cuota por nota (a
verificar contra el esquema real al escribir el plan). Termina con
migración, tests unitarios e integración, UI aprobada, puerta de calidad y
commit en `develop`.
