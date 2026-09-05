# Importación Excel de Notificaciones de Precios de Alimentos

## Objetivo

Permitir que GestorCaisy cargue una Notificación de Precios de Alimentos en
formato `.xlsx`, además del PDF existente, creando siempre un borrador editable
que requiere revisión y publicación explícita.

## Decisiones

- El Excel tendrá una primera hoja con los campos `FECHA`, `VIGENTE DESDE`,
  `APORTE CAISY`, `FONDO` y `SERVICIOS`, más una tabla con `TIPO`,
  `PRESENTACIÓN`, `EDAD DESDE`, `EDAD HASTA`, `PRECIO ACTUAL` y `NUEVO PRECIO`.
- Los códigos de tipo (`SJ-PRE`, `SJ-1`, `SJ-2`, `SJ-3`, `SJ-P1`, `SJ-P2`) y las
  presentaciones (`B`/`G`) conservan el mismo significado del importador PDF.
- La lectura es all-or-nothing: cualquier error devuelve validaciones y no
  crea un borrador parcial.
- El original se conserva en el almacenamiento privado y el tipo de archivo se
  conserva para descargarlo correctamente.
- La vigencia sigue siendo global: al publicar, todas las granjas consultan la
  publicación vigente; no se crean asignaciones por granja.

## Fuera de alcance

- Importación `.xls` binaria antigua.
- Conversión automática de formatos arbitrarios de Excel.
- Publicación automática tras la carga.
