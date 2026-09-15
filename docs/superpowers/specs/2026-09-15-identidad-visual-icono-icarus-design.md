# Icono de identidad visual Icarus

## Objetivo

Incorporar el SVG entregado como marca visual coherente de Trajano-Icarus en la
PWA, la aplicación MVC Trajano.GestorCaisy y los metadatos de instalación y
navegador.

## Decisiones

- El SVG fuente se limpia antes de versionarlo: se eliminan los atributos
  generados por Blazor (`b-y8l9ktuq7y`), los atributos de Dark Reader y tamaños
  inline. Se conserva `viewBox="0 0 800 800"` y el único `path` entregado.
- Se generan dos variantes desde la misma geometría:
  - una monocroma con `fill="currentColor"` para insertarla inline en las
    cabeceras y navegación de las aplicaciones;
  - una de marca con gradiente fijo `#2E86AB` a `#00D2FF` para favicon e iconos
    PWA.
- La variante gradiente usa un id propio, `icarus-gradient`; la variante
  monocroma no contiene `defs`. Nunca se mezclan `fill="currentColor"` en el
  grupo y `fill="url(#... )"` en el SVG, porque el fill del grupo prevalece.
- El icono es una marca de producto: aparece como identificación de la
  aplicación, favicon y PWA. No sustituye iconos semánticos de Material UI
  (agregar, borrar, vacunación, despacho, etc.), que continúan describiendo
  acciones o módulos.
- La PWA usa el asset gradiente como favicon SVG y genera iconos raster 192 y
  512 desde esa misma fuente. Se conserva el fallback `.ico` para navegadores
  que no usen SVG como favicon.
- La MVC referencia el favicon SVG y `.ico` desde su layout. No carga el SVG
  como imagen remota ni lo duplica por vista.
- La marca inline es decorativa cuando el nombre de la aplicación está presente
  como texto; lleva `aria-hidden="true"` y no compite con ese nombre accesible.
- Antes de incorporar el asset se confirma que el usuario tiene derecho a usar
  el diseño fuente. La procedencia o licencia se documenta junto al asset si
  aplica.

## Fuera de alcance

- Rediseñar el tema, paleta, tipografía o la navegación.
- Reemplazar los iconos semánticos existentes de Material UI.
- Cambiar los nombres de producto, títulos de páginas o textos de negocio.
- Introducir una librería de conversión de imágenes; el generador existente de
  iconos PWA se adapta o se usa una herramienta ya disponible en el proyecto.

## Criterios de aceptación

1. El SVG limpiado renderiza correctamente con fondo claro y oscuro, sin
   atributos de Blazor ni Dark Reader.
2. La PWA muestra la marca en su cabecera, carga `favicon.svg` y declara los
   iconos 192/512 correctos en el manifest.
3. Trajano.GestorCaisy muestra la marca en su cabecera y entrega favicon SVG y
   fallback `.ico` desde `wwwroot`.
4. Las páginas no duplican ids de gradiente ni pierden el color por la cascada
   de `fill`.
5. Los iconos de acciones y módulos existentes no se reemplazan.
