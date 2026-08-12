# Exploración eficiente

Leer de más cuesta en cada turno posterior, no solo en el turno que lee. Estas
reglas son las mismas que resume `AGENTS.md`; acá está el detalle.

## Orden de exploración

1. Empezar solo con `git status --short --branch` y `git log -5 --oneline`.
2. No ejecutar listados recursivos ni volcados de todos los archivos al iniciar.
3. Buscar por término, símbolo o ruta probable. Limitar primero el resultado y
   refinar la búsqueda antes de ampliarla.
4. Leer fragmentos antes que archivos completos. Abrir solo el spec, el plan, el
   código y los tests vinculados con la tarea actual.
5. Revisar primero `git diff --stat` y después solo los diffs relevantes.
6. Resumir logs y errores por causa, archivo y línea. No pegar salidas extensas.

## Qué no hacer

- No releer todos los specs históricos. `docs/superpowers/specs/` y
  `docs/superpowers/plans/` son memoria consultable, no contexto obligatorio.
- No abrir un archivo entero para confirmar un detalle que una búsqueda responde.
- No repetir una exploración cuyo resultado ya está en la conversación.
- No pegar el contenido de un archivo que ya se leyó para "tenerlo a mano".

## Señales de que se está explorando mal

- Se leyeron más de tres archivos sin haber formulado todavía la hipótesis.
- Se listó un directorio completo para encontrar un solo archivo.
- Se cargó un documento de proceso para una tarea trivial.
