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
