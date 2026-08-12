# Handoff

> Copiar este archivo a `docs/ai/HANDOFF.md` al cerrar una sesión con trabajo a
> medias. `HANDOFF.md` está en `.gitignore`: es estado efímero, no memoria del
> proyecto. **Borrarlo en cuanto el trabajo cierre**, para que no se convierta en
> documentación obsoleta que el próximo agente crea vigente.

- **Fecha**: AAAA-MM-DD
- **Rama**: develop
- **Último commit**: `<sha corto>` — `<asunto>`

## Objetivo de la sesión

Una o dos frases. Qué se estaba intentando lograr.

## Estado

- Hecho: …
- A medias: … (con la ruta exacta del archivo y qué falta)
- Sin empezar: …

## Verificación

- Último comando ejecutado: `./verify.ps1`
- Resultado observado: … (verde, o el gate que falló y su mensaje)

## Decisiones tomadas en la sesión

Las que no están en ningún spec todavía. Si alguna es duradera, moverla al spec
en vez de dejarla acá.

## Siguiente paso concreto

Una sola acción, con la ruta del archivo por donde retomar.

## Advertencias

Trampas encontradas, cosas que parecían ciertas y no lo eran, comandos que no
funcionan en este entorno.

> Verificar cada afirmación importante de este documento contra git y contra los
> archivos actuales antes de confiar en ella.
