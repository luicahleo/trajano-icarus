# Checklist de revisión común

> Portable: vale para cualquier proyecto con la misma base (.NET con building
> blocks, React y puerta en Node). Lo propio de cada proyecto va en
> [dominio.md](dominio.md).

Se aplica en toda revisión, ligera o profunda, sobre el diff de la tarea. Cada
hallazgo indica archivo y línea, gravedad (grave, media o menor), escenario
concreto de fallo y evidencia.

## 1. Criterios y evidencia

- Cada prueba citada como evidencia comprueba de verdad lo que dice su
  criterio. Léela: no te fíes del nombre.
- Un criterio con evidencia `manual` que podía automatizarse con sensatez es
  hallazgo medio.
- El rojo registrado describe un fallo por el motivo correcto (falta la regla),
  no un error de compilación o de importación ajeno al criterio.

## 2. Pruebas vacías o engañosas

Grave si la prueba es la única evidencia de un criterio; media en otro caso.

- Aserción débil: solo comprueba que algo no es nulo, existe o es verdadero
  (`Assert.NotNull`, `Assert.True(true)`, `toBeDefined()`, `toBeTruthy()`,
  `assert.ok(x)` sobre un valor que siempre existe) sin mirar el valor que
  importa.
- Sin aserción, o con la aserción en una rama que no se ejecuta.
- Prueba del doble: verifica lo que se configuró en el mock o el stub, no el
  comportamiento del código.
- Fórmula duplicada: calcula el esperado con la misma fórmula que el código. El
  esperado se escribe literal.
- Un `try`/`catch` que traga el fallo, o un `catch` vacío.
- Nombre engañoso: promete algo que el cuerpo no comprueba.
- Prueba omitida o exclusiva: `Skip`, `.skip`, `.only`, `.todo`.

## 3. Pruebas existentes modificadas

- Lista las pruebas existentes que toca el diff (`git diff --stat` sobre los
  archivos de prueba) y mira en cada una las líneas borradas.
- Cada aserción cambiada, debilitada o borrada responde a un cambio de
  comportamiento que acuerda `tarea.md`. Si no, es grave: la prueba se adaptó al
  código.

## 4. .NET

- Inyección de dependencias: cada registro nuevo o cambiado tiene el lifetime
  correcto. Un singleton que captura un servicio scoped (DbContext, usuario
  actual, unidad de trabajo) es grave. Un lifetime distinto del habitual del
  proyecto lleva un comentario con su motivo; si falta, hallazgo menor.
- `CancellationToken`: se recibe y se propaga hasta EF Core, `HttpClient` y toda
  llamada asíncrona. `CancellationToken.None` o `default` donde había un token
  disponible es hallazgo medio.
- Prohibido bloquear lo asíncrono: `.Result`, `.Wait()` y
  `.GetAwaiter().GetResult()`. `async void` solo en manejadores de eventos.
- Prohibido el operador `!` para silenciar una advertencia de nulabilidad: se
  corrige el flujo o se valida. Lo mismo vale para `#pragma warning disable` y
  `SuppressMessage` sin motivo escrito.

## 5. TypeScript y React

- Prohibido silenciar el compilador o el linter con `any`, `as unknown as`,
  `// @ts-ignore`, `// @ts-expect-error` o `eslint-disable` sin motivo escrito.

## 6. Seguridad básica

- Cada endpoint nuevo declara su autorización; ninguno queda anónimo por
  omisión.
- Ninguna credencial, token o secreto en el código, la configuración
  versionada, los logs ni los mensajes de error. Los errores al cliente son
  genéricos.
- La entrada externa se valida antes de usarla.

## 7. Proceso y puerta

- Un cambio en `quality/` o en la lista de gates no quita un gate, no debilita
  sus argumentos, no sube un umbral y no amplía una exclusión.
- El cambio no contradice una decisión aceptada de `docs/decisiones/` sin un
  registro nuevo que la sustituya. Si lo hace, es grave.
- No hay alcance que `tarea.md` no pida.
