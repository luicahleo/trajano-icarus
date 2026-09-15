# Discrepancias de «Precio actual» al publicar precios de alimento

## Objetivo

Cuando un Gestor CAISY intenta publicar un borrador de precios de alimento cuyo
valor importado en la columna **PRECIO ACTUAL** no coincide con la publicación
aplicable, conservar el bloqueo de publicación y mostrar todas las
discrepancias de forma accionable.

Cada discrepancia debe identificar la línea de negocio (`TIPO` y
`PRESENTACIÓN`), la columna que falló, el valor leído o editado en el borrador y
el valor vigente esperado. El gestor puede entonces corregir el borrador antes
de intentarlo de nuevo.

## Contexto y problema actual

El Excel se importa como borrador editable. Al publicar,
`PublicarNotificacionPreciosHandler` compara `PrecioActualDocumento` de cada
detalle contra el precio final de la publicación aplicable a la fecha del
documento. Ya encuentra todas las discrepancias, pero hoy devuelve un único
mensaje genérico bajo `Documento`; el MVC lo muestra como una sola alerta.

La comprobación no ocurre durante la carga del Excel. Esta ubicación es
intencional: el borrador puede editarse después de importar y puede cambiar la
publicación aplicable entre la carga y el intento de publicación.

## Decisiones

- Se conserva el control bloqueante al publicar. Una discrepancia nunca publica
  parcial ni automáticamente el borrador.
- Se conserva la importación all-or-nothing para errores de estructura del
  archivo. Un Excel sintácticamente válido sigue creando un borrador aunque su
  `PRECIO ACTUAL` resulte luego desfasado.
- La API devuelve una entrada de validación por cada discrepancia, bajo una
  clave estable por detalle, por ejemplo
  `Detalles[<id>].PrecioActualDocumento`. El texto de cada entrada incluye:
  tipo, presentación, columna `PRECIO ACTUAL`, valor del borrador y valor
  vigente esperado, formateados con la cultura de interfaz.
- El detalle se calcula desde el borrador persistido, no desde la fila original
  de Excel. Por esa razón la interfaz identifica la **línea de precio** y no el
  número de fila: tras editar el borrador, la fila de origen ya no es una
  referencia fiable.
- La interfaz MVC conserva y muestra todas las entradas de validación en la
  página de confirmación de publicación, como lista o tabla de discrepancias;
  no las aplana en un único `TempData["Error"]`. La acción de volver a editar el
  borrador permanece disponible.
- No se exponen nombres de tablas, constraints, SQL ni excepciones técnicas.
  Los precios, tipo y presentación forman parte de la información que un
  Gestor CAISY ya administra y sí se pueden mostrar.
- Los registros de vuelo conservan solo los contadores técnicos existentes;
  nunca almacenan precios ni valores de una discrepancia.

## Contrato de error

Un intento de publicación con dos diferencias responde HTTP 400 con el formato
de validación existente (`errors`), una entrada por línea. Ejemplo conceptual:

```json
{
  "errors": {
    "Detalles[4f...].PrecioActualDocumento": [
      "Tipo: Iniciador; presentación: Bolsa; columna: PRECIO ACTUAL; valor del borrador: 180,00; valor vigente esperado: 182,50."
    ]
  }
}
```

El identificador del detalle es un id técnico que solo sirve para asociar el
error a la fila del borrador. La interfaz no necesita imprimirlo.

## Fuera de alcance

- Convertir la discrepancia en advertencia confirmable o permitir publicar con
  diferencias.
- Adelantar este control a la carga del Excel o crear una consulta de
  previsualización de vigencia.
- Cambiar los errores de sintaxis, columnas faltantes o celdas ilegibles del
  importador; esos continúan indicando fila cuando esté disponible.
- Cambios en los precios de huevo: su regla de publicación es distinta.
- Guardar fila, columna o valores del Excel original como nuevos datos en SQL.

## Criterios de aceptación

1. Con una discrepancia, publicar responde 400, no cambia el estado del
   borrador y muestra tipo, presentación, `PRECIO ACTUAL`, valor del borrador y
   valor vigente esperado.
2. Con varias discrepancias, la respuesta y la interfaz incluyen todas, no solo
   la primera.
3. Sin discrepancias, la publicación conserva el comportamiento actual.
4. Un error de formato durante la importación sigue usando el contrato actual
   de fila/columna y no crea el borrador.
5. La interfaz no revela detalles técnicos de infraestructura y el registro de
   vuelo no registra valores de precios.
