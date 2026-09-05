# Respaldos de los documentos privados (precios y notas de alimento)

Los documentos privados viven en volúmenes Docker fuera del web root, detrás
de los puertos `IAlmacenDocumentosPrecios` (PDF de la Notificación de Precios)
e `IAlmacenDocumentosPedido` (imágenes de respaldo de las notas de entrega).
SQL Server guarda solo la clave lógica UUID, el MIME, el tamaño, el hash
SHA-256 y un nombre seguro; nunca rutas físicas, Base64 ni URL públicas.

## Por qué el volumen no es copia de seguridad

Un volumen Docker sobrevive a la recreación del contenedor, pero no a la
pérdida del host, a un volumen corrupto ni a un borrado accidental. El spec
SP8 exige que el volumen forme parte del **backup externo de la VPS**: sin
esa copia externa, el valor probatorio de los respaldos de notas se pierde.

## Qué respaldar

| Volumen (compose) | Ruta en el contenedor | Contenido |
|---|---|---|
| `documentos-pedidos` | `/app/documentos-pedidos` | Originales (`.bin`) y vistas (`.jpg`) de las notas de alimento |
| `mssql-data` | `/var/opt/mssql` | La base, con las claves lógicas y hashes de cada documento |
| `seq-data` | `/data` | Logs (no es obligatorio restaurarlo, solo conservar ventanas recientes) |

Los archivos de `documentos-pedidos` son **inmutables**: el nombre físico es
UUID y ningún flujo los reescribe ni los borra. Por eso el backup puede ser
incremental por mtime sin riesgo de archivos cambiados a mitad de copia.

## Copia externa sugerida (VPS)

1. Copia diaria del volumen a almacenamiento externo (S3 compatible, otro
   host u almacenamiento objeto del proveedor). Con los archivos inmutables,
   `rsync -a --delete` contra el punto de montaje o una subida por lotes
   diaria alcanza; conservar al menos 30 versiones diarias.
2. Copia diaria del dump lógico de SQL Server en la misma ventana; la pareja
   (dump de base + volumen de esa fecha) es la unidad coherente de
   restauración: una clave lógica sin su archivo no sirve y un archivo sin
   su fila de SQL no se puede consultar.
3. Verificación semanal: restaurar el dump del día en una base temporal,
   montar el volumen de la misma fecha en un contenedor efímero y comprobar
   con una muestra aleatoria que el SHA-256 guardado en
   `gestion_avicola.documentos_nota_entrega.HashSha256` coincide con el hash
   del archivo original del volumen (y lo mismo para
   `notificaciones_precios_alimentos` con su documento original).

## Restauración (prueba y procedimiento)

```bash
# 1) Restaurar la base del dump de la fecha elegida en una instancia temporal.
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='...' -d --name sql-restaura mcr.microsoft.com/mssql/server:2022-latest
# cargar el .bak y RESTORE DATABASE (los pasos estándar de sqlcmd).

# 2) Montar el volumen de esa fecha en un contenedor efímero con la API.
docker run --rm -v documentos-pedidos:/app/documentos-pedidos \
    -e ConnectionStrings__Icarus='...' trajano-icarus-local

# 3) Probar: iniciar sesión como CAISY, abrir un pedido con nota histórica y
#    descargar la vista y el original; ambos deben servir y el original debe
#    verificar contra el hash guardado.
```

La prueba de restauración se considera válida solo si al menos una descarga
de original y una de vista funcionan contra la base restaurada y el hash
coincide. Documentar la fecha y el resultado de cada corrida mensual.

## Cuotas y límites

Los límites se configuran en `AlmacenDocumentosPedido` (appsettings o
variables de entorno `AlmacenDocumentosPedido__*`) y se validan al arrancar:

| Opción | Valor inicial | Efecto |
|---|---|---|
| `Ruta` | (vacío → `/app/documentos-pedidos`) | Directorio del volumen privado |
| `MaxTamanoBytes` | 5 MiB | Tamaño máximo por archivo subido |
| `MaxDimensionesPixeles` | 8000 | Lado máximo de la imagen (por lado) |
| `MaxDocumentosPorNota` | 8 | Cantidad máxima de respaldos activos por nota |

Además, el endpoint rechaza de forma temprana (413) cualquier cuerpo mayor a
5 MiB antes de leerlo. Las cuotas se comparten con el tope de pedidos por
semana (`PedidosAlimento__MaximoPorSemana`): subir respaldos solo es posible
sobre pedidos despachados, que ya consumieron cupo de envío.

## Monitorización

- **Seq**: en la operación de registro de un respaldo solo aparecen el id del
  pedido, la bandeja (CAISY) y el indicador de sustitución. Nunca se registran
  rutas físicas, nombres de archivo originales, número de nota ni contenido de
  la imagen. La descarga no genera eventos con contenido.
- **Volumen**: vigilar el uso de disco del punto de montaje
  `/app/documentos-pedidos` (alertar por encima del 80 %) y el estado del
  volumen (`docker volume ls`, tamaño con `du -sh` sobre el punto de montaje).
  Los archivos son inmutables y de tamaño acotado: el crecimiento es lineal
  con el número de notas y sus respaldos.
- **Errores esperables**: una pérdida de archivo produce un 404 genérico en la
  descarga (sin revelar datos) y un error de permisos o disco lleno produce un
  error genérico en la subida; el pedido despachado queda intacto y el respaldo
  puede reintentarse.

## Migración futura a un almacenamiento S3 compatible

El contrato `IAlmacenDocumentosPedido` (claves UUID opacas para original y
vista, con apertura por clave) es la única puerta que usa el dominio: SQL no
conoce rutas ni URLs. Para migrar:

1. Subir los archivos del volumen a un bucket privado con los nombres UUID
   actuales (carpetas `original/` y `vista/`, o un prefijo por clave).
2. Implementar `IAlmacenDocumentosPedido` contra el bucket (escritura atómica
   simulada con un objeto temporal + copia final, hash calculado antes de
   subir) y cambiarlo en el registro de DI.
3. Validar que las descargas (vista inline y original adjunto) funcionan y que
   el bucket es privado, con firmas para la descarga.
4. Migrar los pendientes en caliente: sin cambios de SQL porque las claves no
   cambian.

La validación de firma/MIME/dimensiones y la generación de la vista segura
viven en la implementación local actual; al migrar, ese preprocesamiento debe
mantenerse antes de la subida para no guardar contenido inválido en el bucket.

## Fallos esperables

- **Archivo perdido del volumen**: la descarga responde 404 genérico (no
  revela datos) y Seq registra solo el id técnico. La corrección es restaurar
  el volumen desde el backup externo; la fila de SQL no se borra.
- **Volumen no escribible** (permisos o disco lleno): la subida de un
  respaldo falla con error genérico; el pedido despachado queda intacto y la
  imagen puede volverse a subir tras corregir el volumen.
- **Hash que no coincide** (archivo corrupto): la restauración debe repetirse
  desde otra copia; nunca se regenera el original, porque su valor es
  probatorio.
