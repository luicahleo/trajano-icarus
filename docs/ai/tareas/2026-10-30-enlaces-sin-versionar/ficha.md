---
id: 2026-10-30-enlaces-sin-versionar
tipo: calidad
modulos: [quality]
temas: [gate-enlaces, archivos-sin-versionar, puerta-calidad]
resumen: El gate de enlaces revisa también los .md nuevos sin versionar, respetando .gitignore.
aprobado: 7e2d657
commits: [7e2d657]
---

# El gate de enlaces revisa los .md nuevos sin versionar

## Qué cambió

- `archivosMarkdown` lista los .md versionados y los nuevos sin versionar
  (`git ls-files --cached --others --exclude-standard`).
- `PUERTA_CALIDAD.md` describe la cobertura nueva del gate.

## Por qué

El gate solo miraba archivos versionados: un enlace roto en un .md nuevo pasaba
la verificación previa al commit y fallaba después.

## Decisiones

- Se conserva `--exclude-standard`: lo ignorado por `.gitignore` sigue fuera.
- Tarea pequeña, directa a desarrollo y primer piloto de la fase 2 de agentes,
  con revisión desde otro proveedor.

## Archivos clave

- `quality/check-enlaces.mjs` — gate de enlaces y `archivosMarkdown`.
- `quality/__tests__/check-enlaces.test.mjs` — prueba con un .md sin versionar.

## Cómo probarlo

- `node --test quality/__tests__/check-enlaces.test.mjs`
- Crear un .md sin versionar con un enlace roto: el gate falla; al borrarlo
  vuelve a verde.

## Pendientes

- Ninguno. El punto ciego equivalente del gate de mojibake se cerró en
  `2026-10-30-mojibake-sin-versionar`.

## Enlaces

- [Tarea](tarea.md)
- [Puerta de calidad](../../PUERTA_CALIDAD.md)
