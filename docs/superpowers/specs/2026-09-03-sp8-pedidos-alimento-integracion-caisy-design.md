# SP8 — Pedidos de alimento e integración con CAISY

Diseño validado mediante brainstorming con el usuario el 2026-09-03, después
de revisar el ICARUS legacy, su documentación, una Notificación de Precios de
Alimentos real y dos notas de entrega reales.

## Objetivo

Permitir que un cliente o un trabajador autorizado prepare y envíe un pedido
de alimento desde la PWA Trajano-Icarus, y que una persona de CAISY con la
funcionalidad `GestorPedidoAlimento` lo procese desde una nueva aplicación de
oficina Trajano-GestorCaisy. Ambas aplicaciones comparten el backend, Identity y una
única base de datos; la comunicación bidireccional se materializa mediante el
estado, el historial y notificaciones internas persistentes.

## Aprendizajes del sistema anterior

El legacy aporta el vocabulario inicial y varias reglas útiles, pero no se
migra literalmente:

- Maneja doce combinaciones de tipo y presentación: `SJ-PRE`, `SJ-1`, `SJ-2`,
  `SJ-3`, `SJ-P1` y `SJ-P2`, cada una en bolsa (`B`) o granel (`G`).
- Una bolsa contiene 40 kg. En granel existían mínimos de 2 toneladas por tipo
  y 6 toneladas por pedido.
- La publicación de precios estaba modelada con columnas fijas y duplicación
  de historial. Se sustituye por cabecera y detalles normalizados.
- Editar eliminaba físicamente detalles, los totales incluían borradores, el
  estado incompleto no registraba cantidades recibidas y faltaba aislamiento
  tenant consistente. Esos comportamientos no se conservan.
- El pedido móvil no era realmente offline. SP8 es deliberadamente online.

Los documentos reales confirmaron que el precio publicado final por cada 40 kg
incluye tres aportes. La nota de entrega puede desglosarlos y, en granel,
mostrar un precio por kg redondeado; los cálculos del sistema deben usar siempre
el precio final por 40 kg congelado, no reconstruirse desde el valor impreso
redondeado por kg.

## Alcance y límites

SP8 incluye:

- catálogo y publicación de Notificaciones de Precios de Alimentos;
- creación, edición, borrado lógico y envío de pedidos;
- devolución para corregir, rechazo definitivo, aceptación y actualización de
  fecha estimada;
- despacho, una entrega y una nota de entrega por pedido;
- carga privada de una o varias imágenes de respaldo de la nota;
- confirmación de recepción conforme o con diferencias;
- notificaciones internas e historial íntegro de transiciones;
- balance basado en la recepción real.

Quedan fuera:

- planificación futura de consumo o cálculo de cantidades por galpón;
- funcionamiento offline del pedido;
- transportistas y proceso interno de preparación de CAISY;
- múltiples entregas o múltiples notas por pedido;
- conciliación posterior de diferencias;
- OCR de notas de entrega, correo, SMS y push;
- `GestorRecepcionHuevos` y futuras funciones de Trajano-GestorCaisy.

## Aplicaciones y autorización

### Trajano-Icarus

La funcionalidad asignable nueva es `PedidoAlimento`; no se reutiliza
`Alimentacion`, que representa el suministro efectivo a las aves. El Cliente y
los Trabajadores con `PedidoAlimento` pueden ver todos los pedidos del tenant,
crear borradores, editar o borrar cualquier borrador, enviar, consultar el
historial y completar la recepción. El creador se conserva solo como id técnico
de auditoría; no otorga propiedad exclusiva.

### Trajano-GestorCaisy

Será una aplicación ASP.NET Core MVC separada dentro del mismo monorepo y un
desplegable independiente. Consume exclusivamente la API de Trajano-Icarus: no
tiene DbContext ni acceso SQL directo. Usa el mismo Identity. Se añade el rol
base `GestorCaisy`, sin tenant, y permisos globales componibles
`FuncionalidadesCaisy`; SP8 incorpora solo `GestorPedidoAlimento`. Así, futuras
cuentas podrán combinar funciones sin crear un rol nuevo por cada puesto. Tener
esa funcionalidad permite publicar precios y procesar pedidos de todos los
tenants, sin conceder facultades administrativas ajenas.

El Administrador de plataforma crea, desactiva y asigna funcionalidades a las
cuentas de CAISY. SP8 incluye la mínima API y pantalla administrativa necesaria
para esa gestión; nunca se crean cuentas CAISY desde la aplicación de oficina.

## Notificación de Precios de Alimentos

### Modelo

`NotificacionPreciosAlimentos` es una cabecera global, versionada e inmutable
tras publicarse:

- `Id`, `FechaDocumento`, `VigenteDesde`, `Estado` (`Borrador`, `Publicada`,
  `Anulada`), `DocumentoOriginalId`, marcas de auditoría técnicas;
- aportes por equivalente de 40 kg: `AporteCaisy`, `Fondo` y `Servicios`;
- colección `DetallePrecioAlimento` con `TipoAlimento`, `Presentacion`,
  `PrecioFinalPor40Kg`, `EdadDesdeDias?` y `EdadHastaDias?`.

La identidad del producto no incluye la presentación: `SJ-1` bolsa y `SJ-1`
granel son dos detalles de precio del mismo tipo. En cada publicación debe
existir como máximo un detalle por `(TipoAlimento, Presentacion)`.

Una publicación es vigente desde `VigenteDesde` hasta que otra publicación con
fecha posterior entra en vigor. El backend resuelve en cada operación la última
`Publicada` con `VigenteDesde <= fecha de negocio`; no necesita un proceso
programado. Una publicación futura puede cargarse y publicarse anticipadamente.

### Importación del PDF

El Gestor carga el PDF original. Un importador determinista extrae una propuesta
de cabecera, aportes y detalles y la guarda como borrador editable. La UI muestra
el resultado y las discrepancias con la publicación vigente. Publicar exige una
confirmación explícita y validaciones completas; nunca se publica
automáticamente. Si el formato no puede interpretarse, se devuelve una lista de
errores y no se crean precios parciales.

La columna «Precio actual» del documento se usa como control: cuando existe una
publicación vigente, debe coincidir con su precio final. La columna «Nuevo
precio» es el nuevo valor canónico. Las franjas de edad son recomendaciones
informativas y no bloquean pedidos. Una publicación ya efectiva no se edita: una
corrección se expresa mediante otra publicación o una anulación auditada antes
de que entre en vigor.

## Pedido y cantidades

`PedidoAlimento` es agregado raíz tenant con `ClienteId` desnormalizado,
`CreadoPor`, `Estado`, timestamps, versión de concurrencia e historial. Sus
detalles guardan:

- tipo y presentación;
- cantidad solicitada en la unidad natural;
- equivalentes de 40 kg;
- `PrecioFinalPor40Kg` y `NotificacionPreciosAlimentosId` como snapshot;
- subtotal solicitado.

Un pedido utiliza exclusivamente una presentación, pero puede contener varios
tipos compatibles:

- Bolsa: cantidad entera de bolsas; equivalentes = bolsas.
- Granel: cantidad entera de toneladas; kilogramos = toneladas × 1000;
  equivalentes = toneladas × 25.
- Granel exige al menos 2 toneladas enteras por tipo y 6 toneladas enteras en
  total.
- No existen toneladas decimales.

El borrador puede construirse sin congelar precios. Al enviar, el servidor fija
`FechaPedido` con la fecha de negocio de Bolivia (`America/La_Paz`), rechaza una
fecha futura suministrada por el cliente, toma la publicación vigente y congela
precios y subtotales de todas las líneas dentro de la misma transacción. Si
falta precio vigente para una línea, el envío falla completo y el borrador se
conserva.

## Límite semanal

El máximo inicial es 3 pedidos enviados por cliente y semana ISO, configurable
en backend sin cambiar código. Cuentan los pedidos que hayan salido del borrador
y no estén borrados. Una devolución y reenvío del mismo pedido no vuelve a
consumir cupo. Los borradores no cuentan. La comprobación y el envío deben ser
atómicos y resistentes a concurrencia; el mensaje informa el límite sin revelar
datos de otros tenants.

## Máquina de estados

Se implementa mediante métodos explícitos del agregado y comandos nombrados, no
con un `Goto`/`Exit` público ni con setters genéricos. Cada salida comprueba sus
guardas; cada entrada registra la transición, produce la notificación necesaria
y actualiza los datos propios del nuevo estado.

```text
Borrador --EnviarACaisy--> Solicitado
Solicitado --DevolverParaCorreccion--> Borrador
Solicitado --Rechazar--> Rechazado [final]
Solicitado --Aceptar--> Aceptado
Aceptado --ActualizarEntregaEstimada--> Aceptado
Aceptado --RegistrarDespacho--> Despachado
Despachado --ConfirmarRecepcionConforme--> RecibidoConforme [final]
Despachado --ConfirmarRecepcionConDiferencias--> RecibidoConDiferencias [final]
```

Reglas:

- solo `Borrador` se edita o se borra lógicamente;
- CAISY nunca cambia tipos ni cantidades solicitadas;
- devolver exige motivo y reutiliza el mismo pedido; conserva historial y
  snapshots de cada envío;
- rechazar exige motivo y es terminal;
- aceptar exige `FechaEntregaEstimada >= hoy`; CAISY puede cambiarla hasta el
  despacho, con nueva notificación e historial;
- no hay cancelación del solicitante después de enviar en SP8;
- abrir o leer un pedido no cambia su estado;
- todos los comandos mutables usan control de concurrencia; los reintentos HTTP
  o dobles clics no duplican transición, notificación ni cupo.

El historial `TransicionPedidoAlimento` conserva estado origen/destino, fecha
UTC, actor técnico, motivo cuando corresponda y valores relevantes como la
entrega estimada. Los motivos se muestran a usuarios autorizados, pero no se
envían a Seq.

## Despacho, nota y recepción

CAISY registra manualmente una única entrega y una única nota por pedido. La
nota admite varias imágenes para páginas o reverso. `EntregaPedidoAlimento`
incluye número y fecha de nota, fecha de despacho, total neto informado y líneas
con cantidad entregada. Las cantidades deben respetar la presentación y no
pueden ser negativas; las diferencias frente a lo solicitado son válidas y se
destacan.

En `Despachado`, Cliente o Trabajador autorizado registra por línea la cantidad
realmente recibida:

- si coincide todo, termina `RecibidoConforme`;
- si alguna línea difiere, termina `RecibidoConDiferencias` y el sistema calcula
  el detalle de diferencias;
- SP8 no reabre ni concilia posteriormente el pedido.

## Documentos privados

Las imágenes son respaldo probatorio de la nota en papel, por lo que deben ser
consultables en pedidos históricos. Se almacenan inicialmente en un volumen
Docker persistente y privado detrás de `IAlmacenDocumentosPedido`; SQL conserva
solo clave lógica opaca, MIME, tamaño, hash, nombre seguro y relación con la
nota. Nunca se guarda Base64, ruta física ni URL pública.

El backend valida firma/MIME/tamaño y conserva el original inmutable con su hash
para que el respaldo no pierda valor probatorio. Además genera una copia de
visualización segura: corrige orientación, elimina metadatos y comprime sin
perder legibilidad. La UI muestra esa copia; el original solo se descarga como
adjunto autorizado. Los nombres físicos son UUID. El contrato permite migrar a
almacenamiento S3 compatible sin tocar el dominio. El volumen debe formar parte
del backup externo de la VPS; un volumen por sí solo no es copia de seguridad.

Los documentos publicados son inmutables. Si CAISY corrige una imagen antes de
la recepción, la anterior se desactiva y queda trazabilidad de la sustitución.

## Notificaciones internas

Las notificaciones son entidades persistentes, no simples mensajes en memoria.
Cada destinatario puede listarlas, ver el contador, marcarlas como leídas y
navegar al pedido. Con la aplicación abierta, el backend puede además avisar en
tiempo real; el registro persistente sigue siendo la fuente de verdad.

Eventos mínimos:

- CAISY: pedido solicitado o reenviado;
- tenant: devuelto para corregir, rechazado, aceptado, cambio de entrega
  estimada y despachado;
- CAISY: recepción conforme o con diferencias.

Se evita almacenar texto duplicado mutable: la notificación conserva tipo,
pedido y metadatos técnicos; la UI construye el mensaje localizado.

## Balance

Un borrador, solicitado, aceptado, rechazado o despachado no representa gasto
real y no se suma al balance. Solo `RecibidoConforme` y
`RecibidoConDiferencias` generan gasto, calculado con la cantidad realmente
recibida por línea y el `PrecioFinalPor40Kg` congelado al envío. El total de la
nota se conserva para comparación, pero no sustituye el cálculo canónico.

## Persistencia y consistencia

Las entidades viven en `GestionAvicola`, schema `gestion_avicola`, aprovechando
el filtro tenant actual. El catálogo de precios es global; pedidos,
notificaciones tenant y sus consultas respetan aislamiento. Las consultas
globales de CAISY se autorizan con una política específica y repositorios
explícitos, no mediante `IgnoreQueryFilters` disperso.

Para notificaciones creadas dentro del mismo backend basta una transacción
local que guarde agregado, historial y notificaciones. No se introduce bus ni
sincronización entre bases. Si en el futuro existen integraciones externas, se
añadirá outbox sin cambiar el contrato de estados.

## Observabilidad y privacidad

Todos los comandos relevantes implementan el contrato actual de registro de
vuelo y llegan a Seq mediante Serilog. Solo se registran ids técnicos,
transición, presentación, conteos y resultados numéricos permitidos. Nunca se
registran imágenes, número de nota, nombres de archivo, motivos, observaciones,
contenido extraído del PDF ni datos nominales. Los errores de autorización y
tenant son genéricos para impedir enumeración.

## Estrategia de implementación

La especificación se entrega en tres bloques dependientes para mantener TDD y
revisiones acotadas:

1. SP8A: rol/aplicación GestorCaisy y Notificación de Precios de Alimentos.
2. SP8B: borrador, envío y procesamiento bidireccional hasta aceptación.
3. SP8C: despacho, documentos, recepción y balance.

Cada bloque termina con migración, tests unitarios e integración, UI aprobada,
puerta de calidad y commit independiente.
