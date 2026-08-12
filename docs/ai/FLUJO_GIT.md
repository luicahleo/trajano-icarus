# Flujo git

Un solo desarrollador. **No hay pull requests**, y este documento es la única
descripción del flujo: si algo lo contradice, este documento manda.

## Ramas

| Rama | Papel |
|---|---|
| `develop` | Rama por defecto y de trabajo. Commit y push directos. |
| `master` | Producción. Solo recibe `develop`, y solo a pedido explícito. |

No se crean ramas de trabajo salvo pedido explícito del usuario.

## Ciclo normal

1. Trabajar en `develop`.
2. Ejecutar `./verify.ps1` y ver la salida en verde.
3. `git add` de las rutas concretas del cambio. Nunca `git add -A` a ciegas.
4. `git commit` con mensaje en español y en modo convencional
   (`feat:`, `fix:`, `chore:`, `docs:`, `test:`, `ci:`).
5. `git push`.

## Promoción a producción

Solo a pedido explícito del usuario:

1. `./verify.ps1` completo en verde sobre `develop`.
2. Merge fast-forward de `develop` a `master`.
3. Push de `master`, que dispara su propio run de CI.

La compuerta de despliegue del subproyecto 4 consultará
`ci.yml/runs?head_sha=<sha>&branch=master&event=push` y exigirá
`conclusion == success`. El filtro `event=push` implica que el commit tiene que
estar en `master` con su propio run en verde: por eso el mecanismo funciona sin
pull requests.

## Prohibiciones

- Nunca `--no-verify`.
- Nunca `push --force` sobre `develop` ni `master`.
- Nunca merge ni push a `master` sin pedido explícito.
- Nunca reescribir historia ya publicada.
