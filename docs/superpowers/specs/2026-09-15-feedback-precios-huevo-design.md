# Feedback visual al importar y revisar precios de huevo

## Objetivo

Dar al Gestor CAISY feedback accionable al cargar un Excel de precios de huevo
y al revisar su borrador. Los errores de importación deben indicar su ubicación
y las diferencias informativas de `PRECIO ACTUAL` deben verse en la línea de
tamaño correspondiente.

## Decisiones

- La importación sigue siendo all-or-nothing: si hay un error de estructura o
  valor, no crea borrador ni guarda el Excel como documento de una publicación.
- Cada error que corresponda a una celda expone fila, columna de negocio y
  valor recibido de manera segura. Ejemplos: `TAMAÑO`, `NUEVO PRECIO AL
  PRODUCTOR`, `SERVICIOS` y las fechas. Los errores de archivo completo o
  cabecera ausente no inventan ubicación.
- El MVC muestra todos los errores en una lista legible en la misma pantalla de
  carga. Nunca revela stack traces, SQL, rutas ni detalles de ClosedXML.
- `PRECIO ACTUAL` sigue siendo informativo y no bloquea publicar huevo. Un
  Excel válido crea un borrador aunque su valor sea distinto.
- Para cada tamaño con `PrecioActualDocumento`, el detalle del borrador compara
  dicho valor con `PrecioAlProductor` de la publicación vigente aplicable a la
  fecha de notificación. No se suma `Servicio`: la columna representa el precio
  anterior al productor.
- Si no existe publicación previa aplicable, no existe el tamaño o el Excel no
  trae `PRECIO ACTUAL`, no se muestra una advertencia.
- La tabla marca cada diferencia e incluye tamaño, columna `PRECIO ACTUAL`,
  valor del Excel y valor esperado. Un resumen aclara que es advertencia, no
  impedimento de publicación. La comparación se calcula al consultar el
  detalle; no se persiste ni se registra en Seq.

## Ejemplos de interfaz

```text
Fila 8, columna NUEVO PRECIO AL PRODUCTOR: el valor «0» debe ser mayor que cero.
Fila 11, columna TAMAÑO: el valor «Mediano XL» no es reconocido.
```

Para una discrepancia no bloqueante:

```text
Primera · PRECIO ACTUAL: Excel 0,7957; precio anterior esperado 0,7954.
```

## Fuera de alcance

- Convertir la advertencia de huevo en bloqueo de publicación.
- Cambiar la regla de alimento, cubierta por
  `2026-09-15-discrepancias-precio-actual-design.md`.
- Guardar coordenadas o valores inválidos del Excel en SQL.
- Corregir automáticamente valores importados.

## Criterios de aceptación

1. Dos celdas inválidas muestran ambas filas, columnas y valores; no se crea
   borrador.
2. Un Excel válido con `PRECIO ACTUAL` distinto crea borrador normalmente.
3. El detalle muestra una advertencia por tamaño con ambos valores.
4. Sin publicación anterior aplicable no hay advertencia inventada.
5. Publicar con advertencias conserva el éxito actual, salvo los controles ya
   existentes de estado y vigencia duplicada.
