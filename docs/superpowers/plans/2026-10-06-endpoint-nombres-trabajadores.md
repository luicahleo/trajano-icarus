# Endpoint de solo nombres de trabajadores — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un Trabajador con `PedidoAlimento` o `DespachoHuevo` puede resolver
nombres de autor en `/pedidos` y `/despachos` sin recibir 403, consultando un
endpoint nuevo de alcance mínimo (solo `id`+`nombre`) en vez del endpoint
completo de gestión de trabajadores (reservado a Cliente).

**Architecture:** Nuevo query/handler en `Icarus.Clientes.Application` que
reutiliza el repositorio ya tenant-safe existente; nuevo endpoint con una
política nueva que acepta Cliente o Trabajador; dos páginas del frontend
cambian de función de API sin tocar su lógica de cruce id→nombre.

**Tech Stack:** .NET 10 / ASP.NET Core Minimal APIs / MediatR (backend);
React + Vitest + Testing Library (frontend); xUnit + Testcontainers.MsSql
(integración).

## Global Constraints

- Español correcto (acentos, sin voseo) en nombres de dominio, comentarios y
  mensajes de commit.
- No exponer `DocumentoIdentidad`, `Cargo`, `FechaIngreso`, `FechaCese` ni
  `Funcionalidades` en el endpoint nuevo — anti-PII (AGENTS.md): solo `Id` y
  `Nombre`.
- No modificar `GET /clientes/{clienteId}/trabajadores` (el endpoint
  completo), su política `GestionTrabajadores`, ni `TrabajadoresPage.tsx` o
  las páginas de `control-acceso`.
- `./verify.ps1` en verde antes de cada commit (requiere Docker — Task 1 usa
  Testcontainers.MsSql). Prohibido `--no-verify`.
- Sin `git push`: la Task 4 cierra con resumen, el push lo autoriza el
  usuario aparte.

---

### Task 1: Query, handler y endpoint backend

**Files:**
- Create: `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/ListarNombresTrabajadoresQuery.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/ListarNombresTrabajadoresHandler.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Domain/../../Identity/Icarus.Identity.Infrastructure/Autenticacion/PoliticasAutorizacion.cs`
  (ruta real: `Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/PoliticasAutorizacion.cs`)
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs:74-80`
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs:61-63` (agregar ruta nueva a continuación)
- Test: `Icarus/tests/Icarus.IntegrationTests/TrabajadoresEndpointsTests.cs`

**Interfaces:**
- Consumes: `IRepositorioTrabajadores.ListarPorClienteAsync(Guid, CancellationToken)`
  (ya existe, devuelve `IReadOnlyList<TrabajadorResumen>`, tenant-safe vía
  filtro global de `ClientesDbContext`).
- Produces: `ListarNombresTrabajadoresQuery(Guid ClienteId) : IRequest<IReadOnlyList<TrabajadorNombreResumen>>`
  y `TrabajadorNombreResumen(Guid Id, string Nombre)`, consumidos por el
  endpoint de esta misma tarea.

- [ ] **Step 1: Escribir el test de integración que debe fallar**

Agregar al final de `TrabajadoresEndpointsTests.cs` (reutiliza
`LoginComo`, `PedidoAutenticado`, `CrearClienteConCuenta`, `CuerpoTrabajador`
ya definidos en ese archivo):

```csharp
    [Fact]
    public async Task UnTrabajadorConsultaNombresDeSuPropiaEmpresaYNoVeDatosSensibles()
    {
        var (clienteId, tokenCliente) = await CrearClienteConCuenta();
        var cliente = _factory.CreateClient();

        var documento = $"8{Random.Shared.Next(10000000, 99999999)}";
        var email = $"trabajador-{Guid.NewGuid():N}@icarus.test";
        var altaTrabajador = PedidoAutenticado(
            HttpMethod.Post, $"/api/clientes/{clienteId}/trabajadores", tokenCliente);
        altaTrabajador.Content = JsonContent.Create(CuerpoTrabajador(documento));
        var respuestaAlta = await cliente.SendAsync(altaTrabajador);
        Assert.Equal(HttpStatusCode.Created, respuestaAlta.StatusCode);

        var tokenTrabajador = await LoginComo(email);
        var consulta = PedidoAutenticado(
            HttpMethod.Get, $"/api/clientes/{clienteId}/trabajadores/nombres", tokenTrabajador);
        var respuesta = await cliente.SendAsync(consulta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var primero = cuerpo.EnumerateArray().Single();
        Assert.Equal("Nombre Ficticio", primero.GetProperty("nombre").GetString());
        Assert.False(primero.TryGetProperty("documentoIdentidad", out _));
        Assert.False(primero.TryGetProperty("funcionalidades", out _));
    }
```

- [ ] **Step 2: Confirmar que falla**

Run: `dotnet test Icarus/Icarus.sln --filter UnTrabajadorConsultaNombresDeSuPropiaEmpresaYNoVeDatosSensibles`
Expected: FAIL (404, porque la ruta `/trabajadores/nombres` no existe)

- [ ] **Step 3: Crear el query y el DTO**

`Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/ListarNombresTrabajadoresQuery.cs`:

```csharp
using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed record ListarNombresTrabajadoresQuery(Guid ClienteId)
    : IRequest<IReadOnlyList<TrabajadorNombreResumen>>;

public sealed record TrabajadorNombreResumen(Guid Id, string Nombre);
```

- [ ] **Step 4: Crear el handler**

`Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/ListarNombresTrabajadoresHandler.cs`:

```csharp
using MediatR;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed class ListarNombresTrabajadoresHandler
    : IRequestHandler<ListarNombresTrabajadoresQuery, IReadOnlyList<TrabajadorNombreResumen>>
{
    private readonly IRepositorioTrabajadores _trabajadores;

    public ListarNombresTrabajadoresHandler(IRepositorioTrabajadores trabajadores) =>
        _trabajadores = trabajadores;

    public async Task<IReadOnlyList<TrabajadorNombreResumen>> Handle(
        ListarNombresTrabajadoresQuery request, CancellationToken cancellationToken)
    {
        var trabajadores = await _trabajadores.ListarPorClienteAsync(request.ClienteId, cancellationToken);
        return trabajadores.Select(t => new TrabajadorNombreResumen(t.Id, t.Nombre)).ToList();
    }
}
```

- [ ] **Step 5: Agregar la política**

En `Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/PoliticasAutorizacion.cs`,
agregar junto a `GestionTrabajadores`:

```csharp
    // Nombres de trabajadores para resolver autoría en pantallas compartidas
    // por Cliente y Trabajador (pedidos de alimento, despachos de huevo):
    // nunca expone documento de identidad ni funcionalidades.
    public const string ConsultaNombresTrabajadores = "ConsultaNombresTrabajadores";
```

En `Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs:74-80`,
agregar al builder existente:

```csharp
            .AddPolicy(PoliticasAutorizacion.ConsultaNombresTrabajadores,
                politica => politica.RequireClaim(
                    ClaimsIdentidad.Rol, nameof(Rol.Cliente), nameof(Rol.Trabajador)))
```

- [ ] **Step 6: Agregar el endpoint**

En `Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs`, a
continuación del `MapGet("/{clienteId:guid}/trabajadores", ...)` existente
(línea 63):

```csharp
        grupo.MapGet("/{clienteId:guid}/trabajadores/nombres", async (Guid clienteId, ISender mediator) =>
            Results.Ok(await mediator.Send(new ListarNombresTrabajadoresQuery(clienteId))))
            .RequireAuthorization(PoliticasAutorizacion.ConsultaNombresTrabajadores);
```

- [ ] **Step 7: Confirmar que el test pasa**

Run: `dotnet test Icarus/Icarus.sln --filter UnTrabajadorConsultaNombresDeSuPropiaEmpresaYNoVeDatosSensibles`
Expected: PASS

- [ ] **Step 8: Correr la puerta completa y commitear**

```bash
./verify.ps1
git add Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/ListarNombresTrabajadoresQuery.cs \
        Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/ListarNombresTrabajadoresHandler.cs \
        Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/PoliticasAutorizacion.cs \
        Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs \
        Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs \
        Icarus/tests/Icarus.IntegrationTests/TrabajadoresEndpointsTests.cs
git commit -m "feat(clientes): agrega endpoint de solo nombres de trabajadores para Cliente y Trabajador"
```

---

### Task 2: Frontend — tipo, función de API y páginas consumidoras

**Files:**
- Modify: `web/src/lib/tipos.ts`
- Modify: `web/src/features/trabajadores/api.ts`
- Modify: `web/src/features/pedidos-alimento/PedidosAlimentoPage.tsx:31,84-88`
- Modify: `web/src/features/despacho-huevo/DespachosHuevoPage.tsx:30,76-80`
- Modify: `web/src/features/pedidos-alimento/PedidosAlimentoPage.test.tsx:52`
- Modify: `web/src/features/despacho-huevo/DespachosHuevoPage.test.tsx:44`

**Interfaces:**
- Consumes: `GET /clientes/{clienteId}/trabajadores/nombres` (Task 1),
  respuesta `[{ id, nombre }]`.
- Produces: `listarNombresTrabajadores(clienteId: string): Promise<TrabajadorNombreResumen[]>`,
  usado por ambas páginas en vez de `listarTrabajadores`.

- [ ] **Step 1: Agregar el tipo**

En `web/src/lib/tipos.ts`, junto a `TrabajadorResumen`:

```ts
export interface TrabajadorNombreResumen {
  id: string;
  nombre: string;
}
```

- [ ] **Step 2: Agregar la función de API**

En `web/src/features/trabajadores/api.ts`, junto a `listarTrabajadores`:

```ts
export async function listarNombresTrabajadores(clienteId: string): Promise<TrabajadorNombreResumen[]> {
  return peticion<TrabajadorNombreResumen[]>({ ruta: `/clientes/${clienteId}/trabajadores/nombres` });
}
```

Actualizar el import de tipos en el mismo archivo para incluir
`TrabajadorNombreResumen` junto a `TrabajadorResumen`.

- [ ] **Step 3: Actualizar el test de PedidosAlimentoPage (debe fallar primero)**

En `PedidosAlimentoPage.test.tsx:52`, cambiar la ruta stub de
`'GET /api/clientes/cli1/trabajadores'` a
`'GET /api/clientes/cli1/trabajadores/nombres'`.

- [ ] **Step 4: Confirmar que falla**

Run: `npx vitest run src/features/pedidos-alimento/PedidosAlimentoPage.test.tsx`
Expected: FAIL (el componente sigue llamando a la ruta vieja, que ya no está
en el stub; el mock HTTP debería devolver 404/sin handler para la ruta vieja)

- [ ] **Step 5: Cambiar el import y el queryFn en PedidosAlimentoPage.tsx**

Línea 31: cambiar `import { listarTrabajadores } from '../trabajadores/api';`
por `import { listarNombresTrabajadores } from '../trabajadores/api';`.

Líneas 84-88: cambiar `queryFn: () => listarTrabajadores(clienteId!)` por
`queryFn: () => listarNombresTrabajadores(clienteId!)`. El resto del
componente no cambia (solo lee `.id` y `.nombre`, ambos presentes en
`TrabajadorNombreResumen`).

- [ ] **Step 6: Confirmar que el test de PedidosAlimentoPage pasa**

Run: `npx vitest run src/features/pedidos-alimento/PedidosAlimentoPage.test.tsx`
Expected: PASS

- [ ] **Step 7: Repetir el mismo cambio para DespachosHuevoPage**

En `DespachosHuevoPage.test.tsx:44`, cambiar la ruta stub de
`'GET /api/clientes/cli1/trabajadores'` a
`'GET /api/clientes/cli1/trabajadores/nombres'`.

Run: `npx vitest run src/features/despacho-huevo/DespachosHuevoPage.test.tsx`
Expected: FAIL

En `DespachosHuevoPage.tsx:30`, cambiar el import de `listarTrabajadores` a
`listarNombresTrabajadores`; en las líneas 76-80, cambiar el `queryFn` de la
misma forma que en el Step 5.

Run: `npx vitest run src/features/despacho-huevo/DespachosHuevoPage.test.tsx`
Expected: PASS

- [ ] **Step 8: Correr la puerta completa y commitear**

```bash
./verify.ps1
git add web/src/lib/tipos.ts web/src/features/trabajadores/api.ts \
        web/src/features/pedidos-alimento/PedidosAlimentoPage.tsx \
        web/src/features/pedidos-alimento/PedidosAlimentoPage.test.tsx \
        web/src/features/despacho-huevo/DespachosHuevoPage.tsx \
        web/src/features/despacho-huevo/DespachosHuevoPage.test.tsx
git commit -m "fix(web): resuelve nombres de autor en pedidos y despachos con el endpoint de solo lectura"
```

---

### Task 3: Cierre

- [ ] **Step 1:** Correr `./verify.ps1` una vez más sobre el estado final.
- [ ] **Step 2:** Revisar `git log --oneline -5` y `git status --short --branch`.
- [ ] **Step 3:** Entregar un resumen: tareas completadas, cualquier
  desviación necesaria (nombres/firmas que no compilaron tal cual el plan),
  y confirmar que **no** se hizo `git push` — eso lo autoriza el usuario
  aparte.
