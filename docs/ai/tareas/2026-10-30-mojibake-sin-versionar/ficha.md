---
id: 2026-10-30-mojibake-sin-versionar
tipo: calidad
modulos: [quality]
temas: [gate-mojibake, archivos-sin-versionar, puerta-calidad]
resumen: El gate de mojibake revisa también los archivos nuevos sin versionar, respetando .gitignore.
aprobado: 4bf8645
commits: [4bf8645]
---

# El gate de mojibake revisa los archivos nuevos sin versionar

## Qué cambió

- La búsqueda con `git grep` del gate usa `--untracked`, así que cubre los
  archivos nuevos además de los versionados.
- `PUERTA_CALIDAD.md` describe la cobertura nueva del gate.

## Por qué

`git grep` solo miraba archivos versionados, el mismo punto ciego que tenía el
gate de enlaces: un archivo nuevo con mojibake pasaba la verificación previa al
commit.

## Decisiones

- Se respeta `.gitignore` y se mantiene el manejo de errores que falla cerrado.
- La prueba usa un repositorio temporal y cubre archivo versionado, nuevo e
  ignorado.

## Archivos clave

- `quality/check-mojibake.mjs` — gate de mojibake.
- `quality/__tests__/check-mojibake.test.mjs` — prueba en repositorio temporal.

## Cómo probarlo

- `node --test quality/__tests__/check-mojibake.test.mjs`
- Crear un archivo nuevo sin versionar con el carácter de reemplazo: el gate
  falla; al borrarlo vuelve a verde.

## Pendientes

- Ninguno.

## Enlaces

- [Tarea](tarea.md)
- [Puerta de calidad](../../PUERTA_CALIDAD.md)
- [Tarea gemela del gate de enlaces](../2026-10-30-enlaces-sin-versionar/ficha.md)
