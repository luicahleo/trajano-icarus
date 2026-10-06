# Acento visual por dominio en el menú lateral de Trajano.GestorCaisy — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agrupar visualmente el menú lateral de Trajano.GestorCaisy en dos bloques —"Alimento" y "Huevo"— con rótulo de grupo y un acento de color distinto por dominio, para que la misma persona que gestiona ambas bandejas (pedidos de alimento y recepción de huevo) identifique de un vistazo en qué dominio está parada, sin depender solo del texto del título de página.

**Architecture:** Cambio puramente de presentación en la aplicación MVC server-rendered `Trajano.GestorCaisy`. No toca controladores, modelos ni la API. Se envuelve cada bloque de enlaces existente en `Views/Shared/_Layout.cshtml` dentro de un `<div>` de grupo con un rótulo (`<span>`), y se agregan reglas CSS en `wwwroot/css/estilos.css` que dan a cada grupo un acento de color propio (aqua para "Alimento", que ya es el color primario del tema; terracota para "Huevo", que el propio CSS documenta como color de énfasis secundario). La verificación es de dos tipos: una prueba de integración que confirma que el HTML renderizado contiene los nuevos rótulos y clases de grupo, y una verificación visual manual en el navegador (el color en sí no es verificable con xUnit).

**Tech Stack:** ASP.NET Core MVC (Razor views, `.cshtml`), CSS plano (sin preprocesador), xUnit + `WebApplicationFactory` para pruebas de integración (`Icarus.GestorCaisy.Tests`).

## Global Constraints

- Código, vistas y CSS en español neutro, sin voseo, con acentos correctos, en UTF-8 sin BOM. Nunca mojibake.
- No introducir ni registrar datos biométricos, documentos de identidad, credenciales ni tokens en logs (no aplica a este cambio, que es solo de presentación, pero no debe romperse).
- No ampliar el alcance: este plan solo toca el menú lateral (`_Layout.cshtml`) y su hoja de estilos. No tocar `Pedidos/Index.cshtml`, `RecepcionesHuevo/Index.cshtml` ni ningún controlador.
- Antes de cada commit y push: ejecutar `./verify.ps1` (o `./verify.sh`) desde la raíz del repositorio. Es obligatorio. Prohibido `--no-verify`.
- La rama de trabajo es `develop`; commits y push directos, sin pull request.
- No relajar ninguna baseline o umbral de `quality/` para que pase el gate.
- Para cambios de UI: antes de marcar la tarea como terminada, iniciar la aplicación y confirmar visualmente el resultado en un navegador (ver Task 2, Step 6). Si no es posible abrir un navegador en el entorno de ejecución, decirlo explícitamente en el resumen final en vez de afirmar que se vio el resultado.

---

## File Structure

- **Modify** `Icarus/src/Apps/Trajano.GestorCaisy/Views/Shared/_Layout.cshtml` — envuelve cada bloque de enlaces del menú lateral (`Alimento`, `Huevo`) en un `<div class="lateral__grupo lateral__grupo--<dominio>">` con un `<span class="lateral__grupo-titulo">` como rótulo.
- **Modify** `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css` — agrega las reglas `.lateral__grupo`, `.lateral__grupo-titulo` y los acentos de color por dominio (`--alimento` en aqua, `--huevo` en terracota), sin modificar ninguna regla existente de `.chip` (los colores de huevo reusan el token `--terracota`, no el color semántico de "rechazado").
- **Modify (test)** `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/LayoutTests.cs` — nueva prueba que confirma que el HTML del menú lateral incluye los dos grupos y sus rótulos cuando el usuario tiene ambas funcionalidades.

No se crean archivos nuevos.

---

### Task 1: Agrupar el menú lateral por dominio en `_Layout.cshtml` (markup + prueba)

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Shared/_Layout.cshtml:40-64`
- Test: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/LayoutTests.cs`

**Interfaces:**
- Consumes: `ReclamosCaisy.TieneGestorPedidoAlimento(ClaimsPrincipal)` y `ReclamosCaisy.TieneGestorRecepcionHuevos(ClaimsPrincipal)` (ya existen en `Icarus/src/Apps/Trajano.GestorCaisy/Autenticacion/ReclamosCaisy.cs`); `AplicacionDePruebas.AccederAsync(string rol = "GestorCaisy", int? funcCaisy = 1)` (ya existe en `Icarus/tests/Trajano.GestorCaisy.Tests/Ayudas/AplicacionDePruebas.cs`) — el bit `3` (`1 | 2`) da ambas funcionalidades a la vez (`ConstantesAutorizacion.BitGestorPedidoAlimento = 1`, `BitGestorRecepcionHuevos = 2`).
- Produces: las clases CSS `lateral__grupo`, `lateral__grupo--alimento`, `lateral__grupo--huevo` y `lateral__grupo-titulo`, que Task 2 usa como selectores para el acento de color. No cambia ninguna clase ni `id` existentes (`lateral`, `lateral__enlace`, `lateral__enlace--activo` se preservan igual).

- [ ] **Step 1: Escribir la prueba que falla**

Abrir `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/LayoutTests.cs` y agregar este método dentro de la clase `LayoutTests` (después del método `LosAssetsDeMarcaSeSirvenDesdeWwwroot`):

```csharp
[Fact]
public async Task ElMenuLateralAgrupaAlimentoYHuevoConRotuloDeDominio()
{
    using var aplicacion = new AplicacionDePruebas();
    var cliente = await aplicacion.AccederAsync(funcCaisy: 3);

    var html = await cliente.GetStringAsync("/Precios");

    Assert.Contains("lateral__grupo--alimento", html);
    Assert.Contains("lateral__grupo--huevo", html);
    Assert.Contains("lateral__grupo-titulo\">Alimento</span>", html);
    Assert.Contains("lateral__grupo-titulo\">Huevo</span>", html);
}
```

- [ ] **Step 2: Correr la prueba y confirmar que falla**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter ElMenuLateralAgrupaAlimentoYHuevoConRotuloDeDominio`

Expected: FAIL — el HTML actual no contiene `lateral__grupo--alimento` ni `lateral__grupo-titulo` (el markup todavía no existe).

- [ ] **Step 3: Modificar `_Layout.cshtml`**

En `Icarus/src/Apps/Trajano.GestorCaisy/Views/Shared/_Layout.cshtml`, reemplazar el bloque `<nav class="lateral" ...> ... </nav>` (líneas 40-64) por:

```cshtml
            <nav class="lateral" aria-label="Navegación principal">
                @if (ReclamosCaisy.TieneGestorPedidoAlimento(User))
                {
                    <div class="lateral__grupo lateral__grupo--alimento">
                        <span class="lateral__grupo-titulo">Alimento</span>
                        <a asp-controller="Precios" asp-action="Index"
                           class="lateral__enlace @(seccion == "precios" ? "lateral__enlace--activo" : "")">
                            Precios de alimento
                        </a>
                        <a asp-controller="Pedidos" asp-action="Index"
                           class="lateral__enlace @(seccion == "pedidos" ? "lateral__enlace--activo" : "")">
                            Pedidos de alimento
                        </a>
                    </div>
                }
                @if (ReclamosCaisy.TieneGestorRecepcionHuevos(User))
                {
                    <div class="lateral__grupo lateral__grupo--huevo">
                        <span class="lateral__grupo-titulo">Huevo</span>
                        <a asp-controller="PreciosHuevo" asp-action="Index"
                           class="lateral__enlace @(seccion == "precios-huevo" ? "lateral__enlace--activo" : "")">
                            Precios de huevo
                        </a>
                        <a asp-controller="RecepcionesHuevo" asp-action="Index"
                           class="lateral__enlace @(seccion == "recepciones-huevo" ? "lateral__enlace--activo" : "")">
                            Recepción de huevo
                        </a>
                    </div>
                }
            </nav>
```

No cambiar la condición `@if` que envuelve todo el `<nav>` (líneas 36-39): sigue igual, solo cambia el contenido interior del `<nav>`.

- [ ] **Step 4: Correr la prueba y confirmar que pasa**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter ElMenuLateralAgrupaAlimentoYHuevoConRotuloDeDominio`

Expected: PASS

- [ ] **Step 5: Correr toda la suite de `Trajano.GestorCaisy.Tests` para descartar regresiones**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`

Expected: PASS (ninguna prueba existente depende de la estructura interna del `<nav>`; en particular `LayoutTests.LaCabeceraMuestraLaMarcaYLosFavicons`, `PedidosControllerTests` y `RecepcionesHuevoControllerTests` deben seguir en verde).

- [ ] **Step 6: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/Views/Shared/_Layout.cshtml Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/LayoutTests.cs
git commit -m "feat(gestor-caisy): agrupa el menu lateral por dominio alimento/huevo"
```

---

### Task 2: Acento de color por dominio en `estilos.css` (CSS + verificación visual)

**Files:**
- Modify: `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css:149-152`

**Interfaces:**
- Consumes: las clases `lateral__grupo`, `lateral__grupo--alimento`, `lateral__grupo--huevo`, `lateral__grupo-titulo` producidas en Task 1, y los tokens de color ya definidos en `:root` (`--aqua`, `--terracota`, `--terracota-oscura`, `--neutro`, `--borde`).
- Produces: nada que otro archivo consuma — es la hoja de estilos final para este cambio.

- [ ] **Step 1: Agregar las reglas CSS**

En `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css`, inmediatamente después de la regla existente:

```css
.lateral__enlace--activo {
    background-color: var(--aqua-claro);
    color: var(--aqua-oscuro);
}
```

agregar:

```css
.lateral__grupo + .lateral__grupo {
    margin-top: 12px;
    padding-top: 12px;
    border-top: 1px solid var(--borde);
}

.lateral__grupo-titulo {
    display: block;
    padding: 4px 12px;
    font-size: 0.75rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: var(--neutro);
}

.lateral__grupo .lateral__enlace {
    border-left: 3px solid transparent;
    padding-left: 9px;
}

.lateral__grupo--alimento .lateral__enlace--activo {
    border-left-color: var(--aqua);
}

.lateral__grupo--huevo .lateral__enlace:hover {
    background-color: rgba(215, 90, 45, 0.08);
}

.lateral__grupo--huevo .lateral__enlace--activo {
    background-color: rgba(215, 90, 45, 0.12);
    color: var(--terracota-oscura);
    border-left-color: var(--terracota);
}
```

No modificar ninguna otra regla del archivo (en particular, no tocar `.chip--rechazado` ni `.chip--anulada`, que también usan tonos de terracota pero para un significado semántico distinto: no hay que confundir "dominio huevo" con "estado rechazado").

- [ ] **Step 2: Confirmar que la suite de pruebas sigue en verde**

Run: `dotnet test Icarus/tests/Trajano.GestorCaisy.Tests`

Expected: PASS (el CSS no tiene pruebas automatizadas propias; este paso solo confirma que no se rompió nada al tocar el archivo).

- [ ] **Step 3: Levantar la aplicación para verificación visual**

Run: `dotnet run --project Icarus/src/Apps/Trajano.GestorCaisy`

- [ ] **Step 4: Iniciar sesión con un usuario que tenga ambas funcionalidades**

Abrir la URL que imprime el comando anterior (por defecto algo como `https://localhost:5001` o el puerto configurado) e iniciar sesión con credenciales de un `GestorCaisy` cuyo `FuncionalidadesCaisy` incluya tanto `GestorPedidoAlimento` (bit 1) como `GestorRecepcionHuevos` (bit 2), es decir máscara `3`. Si no se cuenta con un usuario así en el entorno local, usar el que gestione al menos una de las dos funcionalidades para confirmar que ese único grupo se ve correctamente (rótulo + acento de color), y anotar en el resumen final que no se pudo probar el caso de ambos grupos simultáneos por falta de credenciales.

- [ ] **Step 5: Confirmar visualmente**

En el menú lateral, verificar:
- El bloque "Alimento" muestra el rótulo en mayúsculas pequeñas, y el enlace activo tiene un borde izquierdo y fondo en tono aqua (igual que antes, sin cambio de color).
- El bloque "Huevo" muestra su propio rótulo, separado del anterior por una línea divisoria, y el enlace activo tiene un borde izquierdo y fondo en tono terracota (distinto del aqua de "Alimento").
- Al pasar el mouse sobre un enlace de "Huevo" que no está activo, el fondo se tiñe levemente de terracota (no del aqua que usa "Alimento").

- [ ] **Step 6: Detener el servidor de desarrollo**

Interrumpir el proceso de `dotnet run` (Ctrl+C en la terminal donde quedó corriendo).

- [ ] **Step 7: Ejecutar la puerta de calidad completa**

Run: `./verify.ps1` (desde la raíz del repositorio)

Expected: todos los gates en verde. Si algún gate falla, corregir el contenido señalado por el gate — nunca relajar el gate ni usar `--no-verify`.

- [ ] **Step 8: Commit**

```bash
git add Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css
git commit -m "style(gestor-caisy): acento terracota para el dominio huevo en el menu lateral"
```

- [ ] **Step 9: Push**

```bash
git push
```

(Solo después de que el usuario lo confirme explícitamente si no se había autorizado de antemano — `develop` es la rama de trabajo y no usa pull requests, pero el push en sí sigue siendo una acción visible para otros y debe confirmarse salvo autorización previa.)

---

## Self-Review Notes

- **Cobertura del pedido:** el pedido era diferenciar visualmente, al gestionar, las dos bandejas (pedidos de alimento y recepción de huevo) para la única persona que administra ambas. Task 1 agrupa y rotula; Task 2 da el acento de color distinto. Cubre exactamente la idea acordada en la conversación previa (acento de color por dominio en la barra lateral, sin tocar las tarjetas internas de cada bandeja).
- **Sin placeholders:** todo el markup, CSS y código de prueba están completos y son el contenido final, no bosquejos.
- **Consistencia de nombres:** `lateral__grupo`, `lateral__grupo--alimento`, `lateral__grupo--huevo`, `lateral__grupo-titulo` se usan con el mismo nombre exacto en Task 1 (donde se producen) y Task 2 (donde se consumen como selectores CSS).
- **Fuera de alcance (deliberado):** no se tocan `Pedidos/Index.cshtml` ni `RecepcionesHuevo/Index.cshtml` — su look-and-feel idéntico (mismo patrón de tarjeta/tabla) es intencional y no es lo que generaba la confusión; la señal de dominio vive en la navegación, no en el contenido de cada página.
