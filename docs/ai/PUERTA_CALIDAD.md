# Puerta de calidad

No hay revisión humana de código: hay un solo desarrollador. La puerta sustituye
a esa revisión, así que su autoridad no es negociable.

## Cómo se ejecuta

```powershell
./verify.ps1
```

En POSIX, `./verify.sh`. Ambos son envoltorios de `node quality/verify.mjs`, que
ejecuta los gates en orden y **se detiene en el primero que falla** para dar
retroalimentación rápida.

Los gates de backend necesitan el SDK de .NET 10 y Docker corriendo: `Backend
tests` incluye tests de integración con Testcontainers.MsSql desde el plan 2.
Los gates de frontend necesitan Node 22 y `npm install` previo en `web/`. El
resto corre en segundos.

## Gates vigentes

| Gate | Qué comprueba | Cómo se arregla un fallo |
|---|---|---|
| Tests de la puerta | Que los propios scripts de `quality/` estén verdes | Según el mensaje del test |
| Adaptadores | Que cada archivo generado (`CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, los `.*ignore`) coincida con el manifiesto | `node quality/generar-adaptadores.mjs`; si el cambio era deliberado, va en `quality/adaptadores/manifiesto.mjs` |
| Mojibake | Ausencia del carácter de reemplazo y de las secuencias que delatan UTF-8 leído como Latin-1, en todo archivo versionado que git clasifique como texto | Escribir el carácter correcto en UTF-8 |
| Enlaces | Que todo enlace relativo de los `.md` versionados apunte a un archivo existente | Corregir el enlace o crear el destino |
| Frontend lint | ESLint (flat config) sobre `web/` (`npm run lint`) | Según el error de ESLint |
| Frontend build | `tsc -b && vite build` sobre `web/` (los errores de tipos son errores) | Según el error del compilador o del bundler |
| Frontend tests | Vitest + Testing Library sobre `web/` (`npm run test`) | Según el test que falla |
| Backend build | Que `Icarus/Icarus.sln` compile sin errores ni warnings (los warnings son errores); el compilador y los analizadores son la verificación mecánica de las reglas de estilo y diseño | Según el error del compilador o del analizador |
| Backend tests | Que los tests unitarios, de arquitectura y de integración de la solución estén verdes; son la verificación mecánica de las reglas de arquitectura y del dominio | Según el test que falla |

Los enlaces absolutos `http` y `https` no se comprueban: verificar la red haría
el gate lento y no determinista.

### Autoexcepción del gate de mojibake

Este documento necesita poder nombrar las secuencias que el gate detecta, así que
se marcaría a sí mismo. La solución no es una lista de archivos exentos —que se
convierte en un agujero permanente— sino una regla:

> En archivos `.md`, el gate ignora lo que esté **entre acentos graves**.

Un mojibake accidental nunca está entre acentos graves; una cita deliberada del
patrón sí. En archivos que no son `.md` no hay excepción: en `.mjs` los patrones
se escriben como escapes `\uXXXX`.

La regla vale para los spans en línea, no para los bloques cercados. Dentro de un
bloque de código en un `.md`, citar el patrón con su escape.

## Reglas innegociables

1. Nunca `--no-verify`, ni en commit ni en push.
2. Nunca relajar una baseline, un umbral o una exclusión para que pase el gate.
   Si el gate falla, el problema está en el contenido.
3. Las baselines solo se mueven hacia mejor, en commit propio que explique la
   mejora.
4. Nunca afirmar verde sin haber ejecutado el comando y visto la salida.

## Agregar un gate

Un gate nuevo es un archivo `quality/check-<algo>.mjs` con funciones puras
exportadas y un bloque CLI, su archivo de tests en `quality/__tests__/`, y una
entrada en la lista `GATES` de `quality/verify.mjs`. Si además corre en CI,
`.github/workflows/ci.yml` ya lo cubre: ese workflow invoca la puerta entera.

Este documento describe los gates, no cuenta tests ni proyectos: esas cifras
caducan en cada commit.
