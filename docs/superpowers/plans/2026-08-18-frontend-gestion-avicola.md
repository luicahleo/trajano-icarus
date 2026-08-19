# Frontend Gestión Avícola (SP5/SP6) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> Estado 2026-08-19: Tasks 1–8 implementadas y commiteadas; Tasks 9–16 pendientes. Ver `docs/ai/HANDOFF.md`.

**Goal:** Construir la UI de la PWA para usar lo implementado en SP5/SP6: granjas, galpones, producción diaria, mortalidad y eficiencia, con permisos por funcionalidad, diseño mobile-first y estados de error/conectividad.

**Architecture:** Feature nueva `web/src/features/avicola/` sobre el stack existente (sin librerías nuevas). Único cambio de backend autorizado: `GET /identidad/me` devuelve además `modulos` y `funcionalidades` (query nueva en Clientes). Online-first: la recogida/mortalidad ya genera `IdempotencyKey` en el cliente; la cola offline es un subproyecto futuro.

**Tech Stack:** React 19 + TypeScript estricto, Vite 8, MUI 9, TanStack Query 5, React Hook Form + zod, React Router 7, Vitest + Testing Library. Backend: .NET 10, MediatR, EF Core, xUnit + NSubstitute + Testcontainers.MsSql.

**Spec:** `docs/superpowers/specs/2026-08-18-frontend-gestion-avicola-design.md` (leerlo primero; es la fuente de las decisiones).

## Global Constraints

- Idioma: textos, identificadores de dominio y tests en español correcto; UTF-8 sin BOM; nunca mojibake.
- Anti-PII: `ApiError` nunca transporta cuerpos ni datos nominales; nada de `console.log` con respuestas o formularios.
- TDD: cada test se ve en rojo antes de implementar. Correr primero el test dirigido; `npm run build` al integrar rutas o tipos.
- Sin librerías nuevas de UI, formularios, estado remoto ni iconos (web/AGENTS.md).
- Imports relativos (sin alias `@/`). `sealed` en todo lo de C#. La constante 30 vive UNA vez: `HUEVOS_POR_MAPLE` en el frontend, `Maple.HuevosPorMaple` en el backend.
- La `Fecha` la fija el servidor: la UI NUNCA manda fecha en altas; sí manda `hora` (`HH:mm`). La UI usa la fecha local del dispositivo solo para decidir qué día consultar y si el día está sellado; el sellado real lo impone el backend (rechaza edición de días pasados).
- `queryKey` por parámetros; invalidar por prefijo `['avicola', ...]` tras cada mutación.
- Ocultar UI no sustituye la autorización del backend; las guardas son UX.
- Commits por tarea con el test dirigido en verde. **Nunca hacer push** (lo hace el usuario). **No ejecutar `verify.ps1`**: el usuario corre `ejecutar-puerta-calidad.ps1` personalmente.
- Docker corriendo para los tests de integración del backend (Testcontainers.MsSql).
- Rutas relativas a la raíz del repo (`Trajano-Icarus/`). Comandos de frontend desde `web/`; de backend desde la raíz.
- Alcance: NO tocar el backend salvo las Tasks 1–2 (el `/me` acordado). Vacunación, alimentación, despachos y precios quedan fuera.

---

### Task 1: Backend — Query `ObtenerPermisosActuales` (Clientes)

**Files:**
- Test: `Icarus/tests/Icarus.UnitTests/Clientes/ObtenerPermisosActualesHandlerTests.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Application/Autorizacion/ObtenerPermisosActualesQuery.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Application/Autorizacion/IConsultaPermisosActuales.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/ConsultaPermisosActuales.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/DependencyInjection.cs` (agregar el registro junto al de `VerificadorEntitlement`)

**Interfaces:**
- Produces: `ObtenerPermisosActualesQuery(Guid ClienteId, Guid? TrabajadorId) : IRequest<PermisosActuales>`; `PermisosActuales(IReadOnlyList<string> Modulos, IReadOnlyList<string> Funcionalidades)`; `IConsultaPermisosActuales.ObtenerAsync(Guid clienteId, Guid? trabajadorId, CancellationToken) → Task<PermisosActuales>`. La Task 2 los consume desde el Host vía MediatR.

- [ ] **Step 1: Escribir el test que falla**

```csharp
using Icarus.Clientes.Application.Autorizacion;
using NSubstitute;
using Xunit;

namespace Icarus.UnitTests.Clientes;

public class ObtenerPermisosActualesHandlerTests
{
    private readonly IConsultaPermisosActuales _consulta = Substitute.For<IConsultaPermisosActuales>();
    private readonly ObtenerPermisosActualesHandler _handler;

    public ObtenerPermisosActualesHandlerTests() => _handler = new ObtenerPermisosActualesHandler(_consulta);

    [Fact]
    public async Task DelegaEnLaConsultaConLosIdsDelUsuarioActual()
    {
        var clienteId = Guid.NewGuid();
        var trabajadorId = Guid.NewGuid();
        var esperado = new PermisosActuales([], ["Granjas"]);
        _consulta.ObtenerAsync(clienteId, trabajadorId, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _handler.Handle(
            new ObtenerPermisosActualesQuery(clienteId, trabajadorId), CancellationToken.None);

        Assert.Same(esperado, resultado);
    }

    [Fact]
    public async Task SinTrabajadorConsultaComoCliente()
    {
        var clienteId = Guid.NewGuid();
        var esperado = new PermisosActuales(["GestionAvicola"], ["Granjas", "Galpones"]);
        _consulta.ObtenerAsync(clienteId, null, Arg.Any<CancellationToken>()).Returns(esperado);

        var resultado = await _handler.Handle(
            new ObtenerPermisosActualesQuery(clienteId, null), CancellationToken.None);

        Assert.Same(esperado, resultado);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ObtenerPermisosActualesHandlerTests"`
Expected: FALLA la compilación (los tipos no existen).

- [ ] **Step 3: Implementación mínima**

`Autorizacion/IConsultaPermisosActuales.cs`:

```csharp
namespace Icarus.Clientes.Application.Autorizacion;

// Lectura de entitlement para /identidad/me (spec frontend avícola): la PWA
// necesita saber qué mostrar sin sondear 403. La implementación vive en
// Infrastructure junto a VerificadorEntitlement (misma fuente de datos).
public interface IConsultaPermisosActuales
{
    Task<PermisosActuales> ObtenerAsync(
        Guid clienteId, Guid? trabajadorId, CancellationToken cancellationToken = default);
}
```

`Autorizacion/ObtenerPermisosActualesQuery.cs`:

```csharp
using MediatR;

namespace Icarus.Clientes.Application.Autorizacion;

// Permisos efectivos del usuario actual: el cliente recibe los módulos de su
// tenant y todas las funcionalidades de esos módulos; el trabajador, solo sus
// funcionalidades asignadas (un trabajador no tiene módulos, ver
// Funcionalidades.cs).
public sealed record ObtenerPermisosActualesQuery(Guid ClienteId, Guid? TrabajadorId)
    : IRequest<PermisosActuales>;

public sealed record PermisosActuales(
    IReadOnlyList<string> Modulos, IReadOnlyList<string> Funcionalidades);

public sealed class ObtenerPermisosActualesHandler
    : IRequestHandler<ObtenerPermisosActualesQuery, PermisosActuales>
{
    private readonly IConsultaPermisosActuales _consulta;

    public ObtenerPermisosActualesHandler(IConsultaPermisosActuales consulta) => _consulta = consulta;

    public Task<PermisosActuales> Handle(
        ObtenerPermisosActualesQuery request, CancellationToken cancellationToken) =>
        _consulta.ObtenerAsync(request.ClienteId, request.TrabajadorId, cancellationToken);
}
```

`Infrastructure/Autorizacion/ConsultaPermisosActuales.cs`:

```csharp
using Icarus.Clientes.Application.Autorizacion;
using Icarus.Clientes.Domain;
using Icarus.Clientes.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Icarus.Clientes.Infrastructure.Autorizacion;

public sealed class ConsultaPermisosActuales : IConsultaPermisosActuales
{
    private readonly ClientesDbContext _db;

    public ConsultaPermisosActuales(ClientesDbContext db) => _db = db;

    // Ignora los filtros globales y exige EstaActivo explícitamente, igual que
    // VerificadorEntitlement: un cliente suspendido o un trabajador
    // desactivado no tienen permisos que mostrar.
    public async Task<PermisosActuales> ObtenerAsync(
        Guid clienteId, Guid? trabajadorId, CancellationToken cancellationToken = default)
    {
        if (trabajadorId is { } id)
        {
            var asignadas = await _db.Trabajadores.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.Id == id && t.ClienteId == clienteId && t.EstaActivo)
                .Select(t => (Funcionalidades?)t.Funcionalidades)
                .SingleOrDefaultAsync(cancellationToken);
            return new PermisosActuales([], NombresFuncionalidades(asignadas ?? Funcionalidades.Ninguno));
        }

        var modulos = await _db.Clientes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == clienteId && c.EstaActivo)
            .Select(c => (Modulos?)c.ModulosHabilitados)
            .SingleOrDefaultAsync(cancellationToken);
        var habilitados = modulos ?? Modulos.Ninguno;
        return new PermisosActuales(NombresModulos(habilitados), NombresFuncionalidades(FuncionalidadesDe(habilitados)));
    }

    private static Funcionalidades FuncionalidadesDe(Modulos modulos)
    {
        var acumulado = Funcionalidades.Ninguno;
        foreach (var modulo in Enum.GetValues<Modulos>())
            if (modulo != Modulos.Ninguno && modulos.HasFlag(modulo))
                acumulado |= FuncionalidadesModulos.FuncionalidadesDelModulo(modulo);
        return acumulado;
    }

    private static IReadOnlyList<string> NombresModulos(Modulos modulos) =>
        Enum.GetValues<Modulos>()
            .Where(m => m != Modulos.Ninguno && modulos.HasFlag(m))
            .Select(m => m.ToString())
            .ToList();

    private static IReadOnlyList<string> NombresFuncionalidades(Funcionalidades funcionalidades) =>
        Enum.GetValues<Funcionalidades>()
            .Where(f => f != Funcionalidades.Ninguno && funcionalidades.HasFlag(f))
            .Select(f => f.ToString())
            .ToList();
}
```

En `DependencyInjection.cs`, junto al registro existente de `IVerificadorEntitlement`:

```csharp
services.AddScoped<IConsultaPermisosActuales, ConsultaPermisosActuales>();
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ObtenerPermisosActualesHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Clientes Icarus/tests/Icarus.UnitTests/Clientes/ObtenerPermisosActualesHandlerTests.cs
git commit -m "feat(clientes): consulta de permisos actuales para /identidad/me"
```

---

### Task 2: Backend — `/identidad/me` devuelve `modulos` y `funcionalidades`

**Files:**
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/IdentidadEndpoints.cs:38-40`
- Test: `Icarus/tests/Icarus.IntegrationTests/IdentityEndpointsTests.cs` (agregar tests al final de la clase)

**Interfaces:**
- Consumes: `ObtenerPermisosActualesQuery`, `PermisosActuales` (Task 1); `ICurrentUser`, `SemillaIdentidad.EmailAdmin/EmailCliente/EmailTrabajador` (existentes).
- Produces: JSON de `/identidad/me` con dos campos nuevos camelCase: `modulos: string[]` y `funcionalidades: string[]`. El frontend (Task 3) los consume.

- [ ] **Step 1: Escribir los tests que fallan**

Agregar a `IdentityEndpointsTests` (usa los helpers existentes `LoginComo` y `PedidoAutenticado`; la semilla da al cliente demo el módulo GestionAvicola y al trabajador demo solo `Granjas`, ver `SemillaClientes`):

```csharp
    [Fact]
    public async Task MeComoClienteDevuelveModulosYTodasLasFuncionalidades()
    {
        var cliente = _factory.CreateClient();
        var token = await LoginComo(SemillaIdentidad.EmailCliente);

        var respuesta = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/identidad/me", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var modulos = cuerpo.GetProperty("modulos").EnumerateArray().Select(e => e.GetString()).ToList();
        var funcionalidades = cuerpo.GetProperty("funcionalidades").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("GestionAvicola", modulos);
        Assert.Contains("ProduccionHuevos", funcionalidades);
        Assert.Contains("Mortalidad", funcionalidades);
    }

    [Fact]
    public async Task MeComoTrabajadorDevuelveSoloSusFuncionalidadesAsignadas()
    {
        var cliente = _factory.CreateClient();
        var token = await LoginComo(SemillaIdentidad.EmailTrabajador);

        var respuesta = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/identidad/me", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var modulos = cuerpo.GetProperty("modulos").EnumerateArray().Select(e => e.GetString()).ToList();
        var funcionalidades = cuerpo.GetProperty("funcionalidades").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Empty(modulos);
        Assert.Equal(["Granjas"], funcionalidades);
    }

    [Fact]
    public async Task MeComoAdminDevuelveListasVacias()
    {
        var cliente = _factory.CreateClient();
        var token = await LoginComo(SemillaIdentidad.EmailAdmin);

        var respuesta = await cliente.SendAsync(PedidoAutenticado(HttpMethod.Get, "/identidad/me", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(cuerpo.GetProperty("modulos").EnumerateArray());
        Assert.Empty(cuerpo.GetProperty("funcionalidades").EnumerateArray());
    }
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~IdentityEndpointsTests"` (Docker corriendo)
Expected: FALLAN los 3 tests nuevos (`modulos` no existe en la respuesta → `KeyNotFoundException`).

- [ ] **Step 3: Implementación mínima**

Reemplazar el `MapGet("/me", ...)` en `IdentidadEndpoints.cs` por:

```csharp
        // Sesión actual: el frontend la usa para las guardas y navegación por
        // rol. Modulos y funcionalidades alimentan las guardas por
        // funcionalidad de la PWA (spec frontend avícola).
        grupo.MapGet("/me", async (ICurrentUser actual, ISender mediator) =>
        {
            var permisos = actual.ClienteId is { } clienteId
                ? await mediator.Send(new ObtenerPermisosActualesQuery(clienteId, actual.TrabajadorId))
                : new PermisosActuales([], []);
            return Results.Ok(new
            {
                actual.UsuarioId,
                actual.Rol,
                actual.ClienteId,
                actual.TrabajadorId,
                permisos.Modulos,
                permisos.Funcionalidades,
            });
        })
        .RequireAuthorization();
```

Agregar los usings necesarios: `Icarus.Clientes.Application.Autorizacion` y `MediatR` (verificar los que ya tiene el archivo; el Host ya referencia Clientes porque `ClientesEndpoints` usa sus commands).

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~IdentityEndpointsTests"`
Expected: PASS (todos los tests de la clase, incluidos los preexistentes).

- [ ] **Step 5: Commit**

```bash
git add Icarus/src/Host/Icarus.Host/Endpoints/IdentidadEndpoints.cs Icarus/tests/Icarus.IntegrationTests/IdentityEndpointsTests.cs
git commit -m "feat(identidad): /me devuelve modulos y funcionalidades del usuario actual"
```

---

### Task 3: Frontend — Tipos de dominio avícola y `AuthContext` con permisos

**Files:**
- Modify: `web/src/lib/tipos.ts`
- Modify: `web/src/features/auth/AuthContext.tsx`
- Test: `web/src/features/auth/AuthContext.test.tsx` (agregar test; y actualizar TODOS los stubs de `/identidad/me` del repo, ver Step 3)

**Interfaces:**
- Consumes: JSON ampliado de `/identidad/me` (Task 2).
- Produces:
  - `type Funcionalidad = 'Granjas' | 'Galpones' | 'ProduccionHuevos' | 'Mortalidad' | 'Vacunacion' | 'Alimentacion' | 'Despachos' | 'Precios'`.
  - `UsuarioActual` con dos campos nuevos requeridos: `modulos: Modulo[]` y `funcionalidades: Funcionalidad[]`.
  - `EstadoAuth` con `modulos: Modulo[]`, `funcionalidades: Funcionalidad[]` y `tieneFuncionalidad: (...f: Funcionalidad[]) => boolean` (semántica ANY, igual que `tieneRol`).
  - Tipos avícola: `Granja`, `Galpon`, `RecogidaResumen`, `ProduccionDia`, `MortalidadRegistro`, `MortalidadDia`, `EficienciaDia`, `EficienciaGalpon` (firmas exactas en Step 3). Los usan las Tasks 5 en adelante.

- [ ] **Step 1: Escribir el test que falla**

Agregar a `AuthContext.test.tsx` (seguir el patrón de stubs del archivo; el stub de `GET /api/identidad/me` ahora incluye `modulos` y `funcionalidades`):

```tsx
  test('expone modulos y funcionalidades y evalua tieneFuncionalidad', async () => {
    // stub de renovación + me como en los demás tests del archivo, con:
    //   rol: 'Trabajador', clienteId: 'c1', trabajadorId: 't1',
    //   modulos: [], funcionalidades: ['ProduccionHuevos', 'Mortalidad']
    renderConSesion();

    await waitFor(() => expect(estadoActual().funcionalidades).toEqual(['ProduccionHuevos', 'Mortalidad']));
    expect(estadoActual().modulos).toEqual([]);
    expect(estadoActual().tieneFuncionalidad('ProduccionHuevos')).toBe(true);
    expect(estadoActual().tieneFuncionalidad('Granjas')).toBe(false);
    expect(estadoActual().tieneFuncionalidad('Granjas', 'Mortalidad')).toBe(true);
  });
```

Adaptar `renderConSesion`/`estadoActual` a los helpers reales del archivo (son locales de ese test); lo que se prueba es el contrato nuevo.

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/auth/AuthContext.test.tsx`
Expected: FALLA (las propiedades nuevas no existen en `EstadoAuth` → error de compilación TS o assertions en rojo).

- [ ] **Step 3: Implementación mínima**

En `web/src/lib/tipos.ts`, agregar al final:

```ts
export type Funcionalidad =
  | 'Granjas'
  | 'Galpones'
  | 'ProduccionHuevos'
  | 'Mortalidad'
  | 'Vacunacion'
  | 'Alimentacion'
  | 'Despachos'
  | 'Precios';

// Contratos de Gestión Avícola (espejo camelCase de los DTOs del backend;
// DateOnly llega como 'yyyy-MM-dd' y TimeOnly como 'HH:mm:ss').
export interface Granja {
  id: string;
  nombre: string;
}

export interface Galpon {
  id: string;
  numero: string;
  capacidadMaxima: number;
  gallinasActuales: number;
  fechaNacimientoLote: string;
  descripcion: string | null;
}

export interface RecogidaResumen {
  id: string;
  fecha: string;
  hora: string;
  cantidadMaples: number;
  unidadesIncompletas: number;
  maplesDescarte: number;
  unidadesDescarte: number;
  gallinasVivas: number;
  totalVendible: number;
  totalDescarte: number;
}

export interface ProduccionDia {
  galponId: string;
  fecha: string;
  recogidas: RecogidaResumen[];
  totalMaples: number;
  totalUnidadesIncompletas: number;
  totalVendible: number;
  totalMaplesDescarte: number;
  totalUnidadesDescarte: number;
  totalDescarte: number;
}

export interface MortalidadRegistro {
  id: string;
  fecha: string;
  hora: string;
  cantidadMuertas: number;
  gallinasVivas: number;
}

export interface MortalidadDia {
  galponId: string;
  fecha: string;
  registros: MortalidadRegistro[];
  totalMuertas: number;
}

export interface EficienciaDia {
  fecha: string;
  totalMaples: number;
  totalUnidadesIncompletas: number;
  totalVendible: number;
  totalMaplesDescarte: number;
  totalUnidadesDescarte: number;
  totalDescarte: number;
  gallinasVivas: number;
  eficiencia: number;
  bajoUmbral: boolean;
}

export interface EficienciaGalpon {
  galponId: string;
  desde: string;
  hasta: string;
  dias: EficienciaDia[];
}
```

Y en la misma `tipos.ts`, extender `UsuarioActual`:

```ts
export interface UsuarioActual {
  usuarioId: string;
  rol: Rol;
  clienteId: string | null;
  trabajadorId: string | null;
  modulos: Modulo[];
  funcionalidades: Funcionalidad[];
}
```

En `AuthContext.tsx`: importar `Funcionalidad` y `Modulo`; extender `EstadoAuth`:

```ts
export interface EstadoAuth {
  usuario: UsuarioActual | null;
  estaAutenticado: boolean;
  cargando: boolean;
  rol: Rol | null;
  clienteId: string | null;
  modulos: Modulo[];
  funcionalidades: Funcionalidad[];
  tieneRol: (...roles: Rol[]) => boolean;
  tieneFuncionalidad: (...funcionalidades: Funcionalidad[]) => boolean;
  iniciarSesion: (cred: Credenciales) => Promise<void>;
  cerrarSesion: () => void;
}
```

Agregar junto a `tieneRol`:

```ts
  const tieneFuncionalidad = useCallback(
    (...funcionalidades: Funcionalidad[]) =>
      usuario ? funcionalidades.some((f) => usuario.funcionalidades.includes(f)) : false,
    [usuario],
  );
```

Y en el `useMemo` del estado agregar `modulos: usuario?.modulos ?? []`, `funcionalidades: usuario?.funcionalidades ?? []` y `tieneFuncionalidad` (con sus dependencias).

**Actualización obligatoria de stubs existentes:** `UsuarioActual` tiene campos nuevos requeridos, así que todo stub de `/identidad/me` debe incluirlos. Buscarlos con `grep -rn "identidad/me" web/src` y a cada cuerpo simulado agregar `modulos: [], funcionalidades: []` (o valores acordes al rol que simula el test). Archivos conocidos: `web/src/features/trabajadores/TrabajadoresPage.test.tsx` (helper `baseFetch`), `web/src/features/auth/AuthContext.test.tsx`, `web/src/features/auth/ProtectedRoute.test.tsx`, `web/src/features/auth/RequiereRol.test.tsx`, `web/src/app/AppLayout.test.tsx`, `web/src/app/inicioSegunRol.test.ts` (si construye usuarios), `web/src/features/admin/clientes/*.test.tsx`. Verificar con `npm run build` que no queda ninguno (TS los marca).

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/auth src/features/trabajadores src/app`
Expected: PASS (los tests existentes con los stubs actualizados + el test nuevo).

- [ ] **Step 5: Commit**

```bash
git add web/src/lib/tipos.ts web/src/features/auth/AuthContext.tsx web/src/features/auth/AuthContext.test.tsx web/src/features/trabajadores/TrabajadoresPage.test.tsx web/src/app
git commit -m "feat(web): tipos avicola y permisos (modulos/funcionalidades) en AuthContext"
```

---

### Task 4: Frontend — Guarda `RequiereFuncionalidad` y hook `useFuncionalidad`

**Files:**
- Create: `web/src/features/auth/RequiereFuncionalidad.tsx`
- Create: `web/src/features/auth/useFuncionalidad.ts`
- Test: `web/src/features/auth/RequiereFuncionalidad.test.tsx`

**Interfaces:**
- Consumes: `tieneFuncionalidad` de `EstadoAuth` (Task 3).
- Produces: `<RequiereFuncionalidad funcionalidades={Funcionalidad[]}>children</RequiereFuncionalidad>` (redirige a `/` si el usuario no tiene NINGUNA de las listadas) y `useFuncionalidad(...f: Funcionalidad[]) → boolean`. Los usan el router (Tasks 7+) y las páginas para ocultar acciones.

- [ ] **Step 1: Escribir el test que falla**

`web/src/features/auth/RequiereFuncionalidad.test.tsx` (patrón de `RequiereRol.test.tsx`: stub de renovación + me, `AuthProvider` real, `MemoryRouter`):

```tsx
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './AuthContext';
import { RequiereFuncionalidad } from './RequiereFuncionalidad';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function fetchConSesion(funcionalidades: string[]) {
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const req = init !== undefined ? new Request(String(input), init) : input instanceof Request ? input : new Request(String(input));
    const ruta = new URL(req.url).pathname;
    if (ruta === '/api/identidad/sesion/renovar')
      return respuesta(200, { accessToken: 't', expiraEnSegundos: 900 });
    if (ruta === '/api/identidad/me')
      return respuesta(200, {
        usuarioId: 'u1', rol: 'Trabajador', clienteId: 'c1', trabajadorId: 't1',
        modulos: [], funcionalidades,
      });
    return new Response('', { status: 404 });
  });
  vi.stubGlobal('fetch', fn);
  return fn;
}

function renderGuarda() {
  return render(
    <MemoryRouter initialEntries={['/protegida']}>
      <AuthProvider>
        <Routes>
          <Route
            path="/protegida"
            element={
              <RequiereFuncionalidad funcionalidades={['ProduccionHuevos']}>
                <div>Zona de recogida</div>
              </RequiereFuncionalidad>
            }
          />
          <Route path="/" element={<div>Inicio</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('RequiereFuncionalidad', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('deja pasar cuando el usuario tiene la funcionalidad', async () => {
    fetchConSesion(['ProduccionHuevos', 'Mortalidad']);
    renderGuarda();
    expect(await screen.findByText('Zona de recogida')).toBeInTheDocument();
  });

  test('redirige al inicio cuando no la tiene', async () => {
    fetchConSesion(['Granjas']);
    renderGuarda();
    expect(await screen.findByText('Inicio')).toBeInTheDocument();
    expect(screen.queryByText('Zona de recogida')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/auth/RequiereFuncionalidad.test.tsx`
Expected: FALLA la compilación (el componente no existe).

- [ ] **Step 3: Implementación mínima**

`web/src/features/auth/RequiereFuncionalidad.tsx`:

```tsx
import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import type { Funcionalidad } from '../../lib/tipos';
import { useAuth } from './AuthContext';

// Guarda de ruta por funcionalidad (spec): pasa si el usuario tiene ALGUNA de
// las listadas. Es UX; la autorización real la hace el backend (403).
export function RequiereFuncionalidad({
  funcionalidades,
  children,
}: {
  funcionalidades: Funcionalidad[];
  children: ReactNode;
}) {
  const { tieneFuncionalidad } = useAuth();

  if (!tieneFuncionalidad(...funcionalidades)) return <Navigate to="/" replace />;
  return <>{children}</>;
}
```

`web/src/features/auth/useFuncionalidad.ts`:

```ts
import type { Funcionalidad } from '../../lib/tipos';
import { useAuth } from './AuthContext';

// Para ocultar acciones puntuales dentro de una página (botones de Cliente,
// acciones de edición, etc.). Semántica ANY, igual que la guarda de ruta.
export function useFuncionalidad(...funcionalidades: Funcionalidad[]): boolean {
  const { tieneFuncionalidad } = useAuth();
  return tieneFuncionalidad(...funcionalidades);
}
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/auth/RequiereFuncionalidad.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add web/src/features/auth/RequiereFuncionalidad.tsx web/src/features/auth/useFuncionalidad.ts web/src/features/auth/RequiereFuncionalidad.test.tsx
git commit -m "feat(web): guarda de ruta y hook por funcionalidad"
```

---

### Task 5: Frontend — `features/avicola/api.ts`, constantes y formatos

**Files:**
- Create: `web/src/features/avicola/api.ts`
- Create: `web/src/features/avicola/constantes.ts`
- Create: `web/src/features/avicola/formatos.ts`
- Test: `web/src/features/avicola/api.test.ts`
- Test: `web/src/features/avicola/formatos.test.ts`

**Interfaces:**
- Consumes: `peticion` de `web/src/lib/http.ts`; tipos avícola de `lib/tipos.ts` (Task 3).
- Produces (firmas exactas; las usan todas las páginas):
  - `HUEVOS_POR_MAPLE: 30` y `hoyIso() → string` (`yyyy-MM-dd` local) en `constantes.ts`.
  - `totalHuevos(maples: number, sueltos: number) → number`, `formatearConteo(maples: number, sueltos: number) → string` (`"10 maples + 5 (= 305)"`) en `formatos.ts`.
  - En `api.ts`: `listarGranjas()`, `crearGranja(nombre)`, `renombrarGranja(id, nombre)`, `listarGalpones(granjaId)`, `crearGalpon(granjaId, datos)`, `obtenerGalpon(id)`, `actualizarGalpon(id, datos)`, `ajustarInventarioGalpon(id, gallinasActuales)`, `desactivarGalpon(id)`, `listarProduccion(galponId, fecha?)`, `registrarProduccion(galponId, datos)`, `editarProduccion(id, datos)`, `desactivarProduccion(id)`, `listarMortalidad(galponId, fecha?)`, `registrarMortalidad(galponId, datos)`, `editarMortalidad(id, datos)`, `desactivarMortalidad(id)`, `obtenerEficiencia(galponId, desde?, hasta?)`.

- [ ] **Step 1: Escribir los tests que fallan**

`web/src/features/avicola/formatos.test.ts`:

```ts
import { formatearConteo, totalHuevos } from './formatos';
import { HUEVOS_POR_MAPLE, hoyIso } from './constantes';

describe('formatos avícola', () => {
  test('un maple son treinta huevos', () => {
    expect(HUEVOS_POR_MAPLE).toBe(30);
  });

  test('el total suma maples y sueltos', () => {
    expect(totalHuevos(10, 5)).toBe(305);
    expect(totalHuevos(0, 0)).toBe(0);
  });

  test('el conteo se muestra como maples + sueltos con total', () => {
    expect(formatearConteo(10, 5)).toBe('10 maples + 5 (= 305)');
  });

  test('hoyIso devuelve la fecha local en formato ISO', () => {
    expect(hoyIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});
```

`web/src/features/avicola/api.test.ts`:

```ts
import {
  listarGalpones,
  registrarProduccion,
  registrarMortalidad,
  obtenerEficiencia,
} from './api';

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? null : JSON.stringify(cuerpo), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

describe('api avícola', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('listarGalpones llama al endpoint anidado de la granja', async () => {
    const fetchMock = vi.fn(async () => respuesta(200, []));
    vi.stubGlobal('fetch', fetchMock);

    await listarGalpones('g1');

    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.method).toBe('GET');
    expect(req.url).toContain('/api/granjas/g1/galpones');
  });

  test('registrarProduccion manda el cuerpo con idempotencyKey y sin fecha', async () => {
    const fetchMock = vi.fn(async () => respuesta(201, { id: 'p1' }));
    vi.stubGlobal('fetch', fetchMock);

    await registrarProduccion('gal1', {
      hora: '10:30',
      cantidadMaples: 10,
      unidadesIncompletas: 5,
      maplesDescarte: 1,
      unidadesDescarte: 2,
      idempotencyKey: 'k-1',
    });

    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.method).toBe('POST');
    expect(req.url).toContain('/api/galpones/gal1/produccion');
    const cuerpo = JSON.parse(await req.clone().text());
    expect(cuerpo).toEqual({
      hora: '10:30',
      cantidadMaples: 10,
      unidadesIncompletas: 5,
      maplesDescarte: 1,
      unidadesDescarte: 2,
      idempotencyKey: 'k-1',
    });
    expect(cuerpo).not.toHaveProperty('fecha');
  });

  test('registrarMortalidad manda cantidad e idempotencyKey', async () => {
    const fetchMock = vi.fn(async () => respuesta(201, { id: 'm1' }));
    vi.stubGlobal('fetch', fetchMock);

    await registrarMortalidad('gal1', { hora: '06:15', cantidadMuertas: 12, idempotencyKey: 'k-2' });

    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.url).toContain('/api/galpones/gal1/mortalidad');
    expect(JSON.parse(await req.clone().text())).toEqual({
      hora: '06:15',
      cantidadMuertas: 12,
      idempotencyKey: 'k-2',
    });
  });

  test('obtenerEficiencia arma la query de rango', async () => {
    const fetchMock = vi.fn(async () =>
      respuesta(200, { galponId: 'gal1', desde: '2026-08-01', hasta: '2026-08-18', dias: [] }));
    vi.stubGlobal('fetch', fetchMock);

    await obtenerEficiencia('gal1', '2026-08-01', '2026-08-18');

    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.url).toContain('/api/galpones/gal1/eficiencia');
    expect(req.url).toContain('desde=2026-08-01');
    expect(req.url).toContain('hasta=2026-08-18');
  });
});
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola`
Expected: FALLA la compilación (los módulos no existen).

- [ ] **Step 3: Implementación mínima**

`web/src/features/avicola/constantes.ts`:

```ts
// Unidad de empaque del dominio (glosario): un maple son 30 huevos. Se declara
// una sola vez acá; nunca repetir el 30 como número suelto.
export const HUEVOS_POR_MAPLE = 30;

// Fecha local del dispositivo en formato ISO (yyyy-MM-dd). La UI la usa para
// decidir qué día consultar y si mostrar el día como sellado; la Fecha real
// de cada registro la fija el servidor (spec SP6).
export function hoyIso(): string {
  const ahora = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${ahora.getFullYear()}-${pad(ahora.getMonth() + 1)}-${pad(ahora.getDate())}`;
}
```

`web/src/features/avicola/formatos.ts`:

```ts
import { HUEVOS_POR_MAPLE } from './constantes';

export function totalHuevos(maples: number, sueltos: number): number {
  return maples * HUEVOS_POR_MAPLE + sueltos;
}

// Representación de dominio del conteo (spec): "10 maples + 5 (= 305)".
export function formatearConteo(maples: number, sueltos: number): string {
  return `${maples} maples + ${sueltos} (= ${totalHuevos(maples, sueltos)})`;
}
```

`web/src/features/avicola/api.ts`:

```ts
import { peticion } from '../../lib/http';
import type {
  EficienciaGalpon,
  Galpon,
  Granja,
  MortalidadDia,
  ProduccionDia,
} from '../../lib/tipos';

export interface DatosGalpon {
  numero: string;
  capacidadMaxima: number;
  gallinasActuales: number;
  fechaNacimientoLote: string;
  descripcion: string | null;
}

export interface DatosRecogida {
  hora: string | null;
  cantidadMaples: number;
  unidadesIncompletas: number;
  maplesDescarte: number;
  unidadesDescarte: number;
  idempotencyKey: string;
}

export interface DatosBajas {
  hora: string | null;
  cantidadMuertas: number;
  idempotencyKey: string;
}

// Granjas
export const listarGranjas = () => peticion<Granja[]>({ ruta: '/granjas' });

export const crearGranja = (nombre: string) =>
  peticion<{ id: string }>({ ruta: '/granjas', metodo: 'POST', cuerpo: { nombre } });

export const renombrarGranja = (id: string, nombre: string) =>
  peticion<void>({ ruta: `/granjas/${id}`, metodo: 'PUT', cuerpo: { nombre } });

// Galpones
export const listarGalpones = (granjaId: string) =>
  peticion<Galpon[]>({ ruta: `/granjas/${granjaId}/galpones` });

export const crearGalpon = (granjaId: string, datos: DatosGalpon) =>
  peticion<{ id: string }>({ ruta: `/granjas/${granjaId}/galpones`, metodo: 'POST', cuerpo: datos });

export const obtenerGalpon = (id: string) => peticion<Galpon>({ ruta: `/galpones/${id}` });

export const actualizarGalpon = (
  id: string,
  datos: { numero: string; descripcion: string | null; capacidadMaxima: number },
) => peticion<void>({ ruta: `/galpones/${id}`, metodo: 'PUT', cuerpo: datos });

export const ajustarInventarioGalpon = (id: string, gallinasActuales: number) =>
  peticion<void>({ ruta: `/galpones/${id}/inventario`, metodo: 'PUT', cuerpo: { gallinasActuales } });

export const desactivarGalpon = (id: string) =>
  peticion<void>({ ruta: `/galpones/${id}`, metodo: 'DELETE' });

// Producción (recogidas). La fecha la fija el servidor: nunca se manda.
export const listarProduccion = (galponId: string, fecha?: string) =>
  peticion<ProduccionDia>({
    ruta: `/galpones/${galponId}/produccion${fecha ? `?fecha=${fecha}` : ''}`,
  });

export const registrarProduccion = (galponId: string, datos: DatosRecogida) =>
  peticion<{ id: string }>({
    ruta: `/galpones/${galponId}/produccion`,
    metodo: 'POST',
    cuerpo: datos,
  });

export const editarProduccion = (
  id: string,
  datos: {
    hora: string;
    cantidadMaples: number;
    unidadesIncompletas: number;
    maplesDescarte: number;
    unidadesDescarte: number;
  },
) => peticion<void>({ ruta: `/produccion/${id}`, metodo: 'PUT', cuerpo: datos });

export const desactivarProduccion = (id: string) =>
  peticion<void>({ ruta: `/produccion/${id}`, metodo: 'DELETE' });

// Mortalidad (bajas)
export const listarMortalidad = (galponId: string, fecha?: string) =>
  peticion<MortalidadDia>({
    ruta: `/galpones/${galponId}/mortalidad${fecha ? `?fecha=${fecha}` : ''}`,
  });

export const registrarMortalidad = (galponId: string, datos: DatosBajas) =>
  peticion<{ id: string }>({
    ruta: `/galpones/${galponId}/mortalidad`,
    metodo: 'POST',
    cuerpo: datos,
  });

export const editarMortalidad = (
  id: string,
  datos: { hora: string; cantidadMuertas: number },
) => peticion<void>({ ruta: `/mortalidad/${id}`, metodo: 'PUT', cuerpo: datos });

export const desactivarMortalidad = (id: string) =>
  peticion<void>({ ruta: `/mortalidad/${id}`, metodo: 'DELETE' });

// Eficiencia diaria
export const obtenerEficiencia = (galponId: string, desde?: string, hasta?: string) => {
  const params = new URLSearchParams();
  if (desde) params.set('desde', desde);
  if (hasta) params.set('hasta', hasta);
  const query = params.toString();
  return peticion<EficienciaGalpon>({
    ruta: `/galpones/${galponId}/eficiencia${query ? `?${query}` : ''}`,
  });
};
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola
git commit -m "feat(web): cliente api, constantes y formatos de gestion avicola"
```

---

### Task 6: Frontend — Banner global de conectividad

**Files:**
- Create: `web/src/app/useConexion.ts`
- Create: `web/src/app/BannerSinConexion.tsx`
- Modify: `web/src/app/AppLayout.tsx` (montar el banner bajo el AppBar)
- Test: `web/src/app/BannerSinConexion.test.tsx`

**Interfaces:**
- Produces: `useConexion() → boolean` (true = online). El banner se monta una sola vez en `AppLayout`; las páginas con formularios de envío usan `useConexion()` para deshabilitar el submit (Tasks 11 y 13).

- [ ] **Step 1: Escribir el test que falla**

`web/src/app/BannerSinConexion.test.tsx`:

```tsx
import { act, render, screen } from '@testing-library/react';
import { BannerSinConexion } from './BannerSinConexion';

describe('BannerSinConexion', () => {
  test('no se muestra cuando hay conexión', () => {
    render(<BannerSinConexion />);
    expect(screen.queryByText(/sin conexión/i)).not.toBeInTheDocument();
  });

  test('aparece al perder la conexión y se oculta al volver', () => {
    render(<BannerSinConexion />);

    act(() => {
      window.dispatchEvent(new Event('offline'));
    });
    expect(screen.getByText(/sin conexión/i)).toBeInTheDocument();

    act(() => {
      window.dispatchEvent(new Event('online'));
    });
    expect(screen.queryByText(/sin conexión/i)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/app/BannerSinConexion.test.tsx`
Expected: FALLA la compilación (los módulos no existen). Ojo: jsdom reporta `navigator.onLine === true` por defecto, así que el primer test pasaría solo; el rojo real es la compilación.

- [ ] **Step 3: Implementación mínima**

`web/src/app/useConexion.ts`:

```ts
import { useSyncExternalStore } from 'react';

function suscribir(aviso: () => void): () => void {
  window.addEventListener('online', aviso);
  window.addEventListener('offline', aviso);
  return () => {
    window.removeEventListener('online', aviso);
    window.removeEventListener('offline', aviso);
  };
}

// true = hay conexión. Fuente: navigator.onLine + eventos online/offline.
export function useConexion(): boolean {
  return useSyncExternalStore(
    suscribir,
    () => navigator.onLine,
    () => true,
  );
}
```

`web/src/app/BannerSinConexion.tsx`:

```tsx
import { Alert } from '@mui/material';
import { useConexion } from './useConexion';

// Aviso global de pérdida de conexión (spec): los datos pueden estar
// desactualizados y los envíos se bloquean mientras no vuelva la red.
export function BannerSinConexion() {
  const enLinea = useConexion();
  if (enLinea) return null;
  return (
    <Alert severity="warning" sx={{ borderRadius: 0 }}>
      Sin conexión: los datos pueden estar desactualizados y no se pueden
      guardar registros.
    </Alert>
  );
}
```

En `AppLayout.tsx`, montar `<BannerSinConexion />` inmediatamente después del `</AppBar>` (importarlo).

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/app/BannerSinConexion.test.tsx src/app/AppLayout.test.tsx`
Expected: PASS (el banner no rompe los tests del layout: jsdom está online por defecto).

- [ ] **Step 5: Commit**

```bash
git add web/src/app/useConexion.ts web/src/app/BannerSinConexion.tsx web/src/app/BannerSinConexion.test.tsx web/src/app/AppLayout.tsx
git commit -m "feat(web): banner global de perdida de conexion"
```

---

### Task 7: Frontend — `AvicolaInicioPage` (primera vez / redirección) y ruta `/avicola`

**Files:**
- Create: `web/src/features/avicola/AvicolaInicioPage.tsx`
- Modify: `web/src/app/paginasDiferidas.tsx` (export lazy)
- Modify: `web/src/app/router.tsx` (ruta `/avicola`)
- Test: `web/src/features/avicola/AvicolaInicioPage.test.tsx`

**Interfaces:**
- Consumes: `listarGranjas`, `crearGranja` (Task 5); `RequiereFuncionalidad` (Task 4); tipos `Granja`.
- Produces: ruta `/avicola` guardada con `RequiereFuncionalidad(['Granjas', 'Galpones', 'ProduccionHuevos', 'Mortalidad'])`. `queryKey` de granjas: `['avicola', 'granjas']` (lo reutilizan Tasks 8+).

- [ ] **Step 1: Escribir el test que falla**

`web/src/features/avicola/AvicolaInicioPage.test.tsx`. Helpers `respuesta`, `fetchSimulado`, `baseFetch` y `renderPagina`: copiar el patrón de `web/src/features/trabajadores/TrabajadoresPage.test.tsx` (stub por `METODO ruta`, `QueryClient` sin retry, `AuthProvider` real), con el stub de me incluyendo `modulos: ['GestionAvicola'], funcionalidades: ['Granjas', 'Galpones']` y rol `Cliente`:

```tsx
describe('AvicolaInicioPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('con granja existente redirige a la lista de galpones', async () => {
    baseFetchConGranjas(respuesta(200, [{ id: 'gr1', nombre: 'Granja Norte' }]));
    renderPagina('/avicola');

    expect(await screen.findByText('Lista de galpones')).toBeInTheDocument();
  });

  test('sin granja muestra el alta y crea la primera granja', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchConGranjas([
      respuesta(200, []),
      respuesta(200, [{ id: 'gr1', nombre: 'Granja Nueva' }]),
    ], {
      'POST /api/granjas': respuesta(201, { id: 'gr1' }),
    });
    renderPagina('/avicola');

    expect(await screen.findByText('Creá tu granja')).toBeInTheDocument();
    await usuario.type(screen.getByLabelText('Nombre de la granja'), 'Granja Nueva');
    await usuario.click(screen.getByRole('button', { name: 'Crear granja' }));

    expect(llamadaCon(fetchMock, 'POST', '/granjas')).toBe(true);
    expect(await screen.findByText('Lista de galpones')).toBeInTheDocument();
  });

  test('sin granja y sin funcionalidad Granjas muestra aviso sin formulario', async () => {
    // me con funcionalidades: ['ProduccionHuevos'] (trabajador recolector)
    baseFetchTrabajadorSinGranjas();
    renderPagina('/avicola');

    expect(await screen.findByText(/no tiene una granja configurada/i)).toBeInTheDocument();
    expect(screen.queryByLabelText('Nombre de la granja')).not.toBeInTheDocument();
  });
});
```

En `renderPagina`, montar dos rutas: `/avicola` → `<AvicolaInicioPage />` y `/avicola/galpones` → `<div>Lista de galpones</div>` (placeholder local del test; la página real llega en Task 8).

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/AvicolaInicioPage.test.tsx`
Expected: FALLA la compilación (la página no existe).

- [ ] **Step 3: Implementación mínima**

`web/src/features/avicola/AvicolaInicioPage.tsx`:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Box, Button, CircularProgress, Container, TextField, Typography } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Navigate, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { ApiError } from '../../lib/http';
import { useFuncionalidad } from '../auth/useFuncionalidad';
import { crearGranja, listarGranjas } from './api';

export const CLAVE_GRANJAS = ['avicola', 'granjas'] as const;

const esquema = z.object({ nombre: z.string().trim().min(1, 'Ingresá el nombre de la granja.') });
type DatosFormulario = z.infer<typeof esquema>;

// Entrada de la sección (spec): sin granja, alta de primera vez; con granja,
// directo a los galpones (un cliente tiene una sola granja activa).
export function AvicolaInicioPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const puedeCrearGranja = useFuncionalidad('Granjas');
  const granjas = useQuery({ queryKey: CLAVE_GRANJAS, queryFn: listarGranjas });

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<DatosFormulario>({ resolver: zodResolver(esquema) });

  const creacion = useMutation({
    mutationFn: (datos: DatosFormulario) => crearGranja(datos.nombre),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: CLAVE_GRANJAS });
      navigate('/avicola/galpones');
    },
  });

  if (granjas.isLoading) return <CircularProgress sx={{ display: 'block', mx: 'auto', mt: 4 }} />;
  if (granjas.isError)
    return <Alert severity="error">No se pudo cargar la granja. Reintentá más tarde.</Alert>;
  if ((granjas.data ?? []).length > 0) return <Navigate to="/avicola/galpones" replace />;

  if (!puedeCrearGranja)
    return (
      <Container sx={{ py: 4 }}>
        <Alert severity="info">
          La cuenta no tiene una granja configurada. Pedile al titular que la cree.
        </Alert>
      </Container>
    );

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h5" component="h1" gutterBottom>
        Creá tu granja
      </Typography>
      <Typography variant="body2" sx={{ mb: 2 }}>
        Es el primer paso: después vas a cargar los galpones.
      </Typography>
      {creacion.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {creacion.error instanceof ApiError
            ? creacion.error.message
            : 'No se pudo crear la granja.'}
        </Alert>
      )}
      <Box
        component="form"
        onSubmit={handleSubmit((datos) => creacion.mutate(datos))}
        sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}
      >
        <TextField
          label="Nombre de la granja"
          {...register('nombre')}
          error={!!errors.nombre}
          helperText={errors.nombre?.message}
          autoFocus
        />
        <Button type="submit" variant="contained" disabled={creacion.isPending}>
          Crear granja
        </Button>
      </Box>
    </Container>
  );
}
```

En `paginasDiferidas.tsx` agregar:

```tsx
export const AvicolaInicioPage = lazy(() =>
  import('../features/avicola/AvicolaInicioPage').then((modulo) => ({ default: modulo.AvicolaInicioPage })),
);
```

En `router.tsx` agregar dentro del layout (importar `RequiereFuncionalidad` y `AvicolaInicioPage`):

```tsx
          {
            path: '/avicola',
            element: (
              <ProtectedRoute>
                <RequiereFuncionalidad funcionalidades={['Granjas', 'Galpones', 'ProduccionHuevos', 'Mortalidad']}>
                  <AvicolaInicioPage />
                </RequiereFuncionalidad>
              </ProtectedRoute>
            ),
          },
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola/AvicolaInicioPage.test.tsx && npm run build`
Expected: PASS (3 tests) y build sin errores (la ruta nueva compila).

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola/AvicolaInicioPage.tsx web/src/features/avicola/AvicolaInicioPage.test.tsx web/src/app/paginasDiferidas.tsx web/src/app/router.tsx
git commit -m "feat(web): entrada avicola con alta de primera granja"
```

---

### Task 8: Frontend — `GalponesPage`: lista de tarjetas + alta de galpón + renombrar granja

**Files:**
- Create: `web/src/features/avicola/GalponesPage.tsx`
- Create: `web/src/features/avicola/TarjetaGalpon.tsx`
- Modify: `web/src/app/paginasDiferidas.tsx`, `web/src/app/router.tsx` (ruta `/avicola/galpones`)
- Test: `web/src/features/avicola/GalponesPage.test.tsx`

**Interfaces:**
- Consumes: `listarGranjas`, `listarGalpones`, `crearGalpon`, `renombrarGranja`, `obtenerEficiencia`, `CLAVE_GRANJAS` (Tasks 5 y 7); `useFuncionalidad`; `hoyIso`.
- Produces: `TarjetaGalpon({ galpon }: { galpon: Galpon })` (la reusa ninguna otra página, pero la Task 9 la extiende con acciones); ruta `/avicola/galpones` guardada con `RequiereFuncionalidad(['Galpones'])`. queryKeys: `['avicola', 'galpones', granjaId]`, `['avicola', 'eficiencia', galponId, desde, hasta]`.

- [ ] **Step 1: Escribir el test que falla**

`web/src/features/avicola/GalponesPage.test.tsx` (mismos helpers por copia del patrón; me con rol Cliente, `funcionalidades: ['Granjas', 'Galpones', 'ProduccionHuevos']`). Datos:

```tsx
const granja = { id: 'gr1', nombre: 'Granja Norte' };
const galpon = {
  id: 'ga1', numero: '1', capacidadMaxima: 5000, gallinasActuales: 4800,
  fechaNacimientoLote: '2026-01-15', descripcion: null,
};

describe('GalponesPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('muestra el encabezado de la granja y las tarjetas de galpones', async () => {
    baseFetchAvicola({
      'GET /api/granjas': respuesta(200, [granja]),
      'GET /api/granjas/gr1/galpones': respuesta(200, [galpon]),
      'GET /api/galpones/ga1/eficiencia': respuesta(200, {
        galponId: 'ga1', desde: hoyIso(), hasta: hoyIso(),
        dias: [{ fecha: hoyIso(), totalMaples: 100, totalUnidadesIncompletas: 0, totalVendible: 3000,
                 totalMaplesDescarte: 0, totalUnidadesDescarte: 0, totalDescarte: 0,
                 gallinasVivas: 4800, eficiencia: 62.5, bajoUmbral: true }],
      }),
    });
    renderPagina('/avicola/galpones');

    expect(await screen.findByText('Granja Norte')).toBeInTheDocument();
    expect(screen.getByText('Galpón 1')).toBeInTheDocument();
    expect(screen.getByText(/4\.?800 \/ 5\.?000 gallinas|4800 \/ 5000 gallinas/)).toBeInTheDocument();
    expect(screen.getByText(/62,5 ?%|62\.5 ?%/)).toBeInTheDocument();
    expect(screen.getByText(/bajo umbral/i)).toBeInTheDocument();
  });

  test('estado vacío invita a crear el primer galpón', async () => {
    baseFetchAvicola({
      'GET /api/granjas': respuesta(200, [granja]),
      'GET /api/granjas/gr1/galpones': respuesta(200, []),
    });
    renderPagina('/avicola/galpones');

    expect(await screen.findByText(/todavía no hay galpones/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /crear el primero/i })).toBeInTheDocument();
  });

  test('el alta crea un galpón y refresca la lista', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({
      'GET /api/granjas': respuesta(200, [granja]),
      'GET /api/granjas/gr1/galpones': [respuesta(200, []), respuesta(200, [galpon])],
      'POST /api/granjas/gr1/galpones': respuesta(201, { id: 'ga1' }),
      'GET /api/galpones/ga1/eficiencia': respuesta(200, { galponId: 'ga1', desde: hoyIso(), hasta: hoyIso(), dias: [] }),
    });
    renderPagina('/avicola/galpones');

    await usuario.click(await screen.findByRole('button', { name: /nuevo galpón|crear el primero/i }));
    await usuario.type(screen.getByLabelText('Número'), '1');
    await usuario.type(screen.getByLabelText('Capacidad máxima'), '5000');
    await usuario.type(screen.getByLabelText('Gallinas actuales'), '4800');
    fireEvent.change(screen.getByLabelText('Fecha de poblado del lote'), { target: { value: '2026-01-15' } });
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(llamadaCon(fetchMock, 'POST', '/granjas/gr1/galpones')).toBe(true);
    expect(await screen.findByText('Galpón 1')).toBeInTheDocument();
  });

  test('renombrar la granja desde el encabezado', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({
      'GET /api/granjas': [respuesta(200, [granja]), respuesta(200, [{ ...granja, nombre: 'Granja Sur' }])],
      'GET /api/granjas/gr1/galpones': respuesta(200, []),
      'PUT /api/granjas/gr1': respuesta(204),
    });
    renderPagina('/avicola/galpones');

    await usuario.click(await screen.findByRole('button', { name: /renombrar/i }));
    await usuario.clear(screen.getByLabelText('Nombre de la granja'));
    await usuario.type(screen.getByLabelText('Nombre de la granja'), 'Granja Sur');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(llamadaCon(fetchMock, 'PUT', '/granjas/gr1')).toBe(true);
    expect(await screen.findByText('Granja Sur')).toBeInTheDocument();
  });

  test('un trabajador sin funcionalidad Galpones no ve las acciones de alta', async () => {
    // me con rol Trabajador y funcionalidades: ['ProduccionHuevos'] — pero la
    // guarda de ruta exige Galpones, así que este caso verifica solo los
    // botones: simular me con ['Galpones'] pero sin 'Granjas' → no ve Renombrar.
    baseFetchAvicolaConFuncionalidades(['Galpones', 'ProduccionHuevos'], {
      'GET /api/granjas': respuesta(200, [granja]),
      'GET /api/granjas/gr1/galpones': respuesta(200, []),
    });
    renderPagina('/avicola/galpones');

    expect(await screen.findByText(/todavía no hay galpones/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /renombrar/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /nuevo galpón/i })).not.toBeInTheDocument();
  });
});
```

(Definir los helpers `baseFetchAvicola`, `baseFetchAvicolaConFuncionalidades`, `llamadaCon` en el propio archivo siguiendo el patrón de `TrabajadoresPage.test.tsx`; la ruta `/avicola/galpones` se monta directo con la página, sin guarda, para probar la página.)

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/GalponesPage.test.tsx`
Expected: FALLA la compilación (los componentes no existen).

- [ ] **Step 3: Implementación mínima**

`web/src/features/avicola/TarjetaGalpon.tsx`:

```tsx
import { Card, CardActionArea, CardContent, Chip, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import type { Galpon } from '../../lib/tipos';
import { obtenerEficiencia } from './api';
import { hoyIso } from './constantes';

// Tarjeta de galpón (spec): número, inventario y eficiencia de hoy con la
// señal del umbral del 70 %.
export function TarjetaGalpon({ galpon }: { galpon: Galpon }) {
  const navigate = useNavigate();
  const hoy = hoyIso();
  const eficiencia = useQuery({
    queryKey: ['avicola', 'eficiencia', galpon.id, hoy, hoy],
    queryFn: () => obtenerEficiencia(galpon.id, hoy, hoy),
  });
  const dia = eficiencia.data?.dias[0];

  return (
    <Card>
      <CardActionArea onClick={() => navigate(`/avicola/galpones/${galpon.id}`)}>
        <CardContent>
          <Typography variant="h6">Galpón {galpon.numero}</Typography>
          <Typography variant="body2">
            {galpon.gallinasActuales} / {galpon.capacidadMaxima} gallinas
          </Typography>
          {dia && (
            <Typography variant="body2" component="div" sx={{ mt: 1 }}>
              Eficiencia de hoy: {dia.eficiencia} %{' '}
              {dia.bajoUmbral && (
                <Chip size="small" color="error" label="Bajo umbral — considerar descarte" />
              )}
            </Typography>
          )}
        </CardContent>
      </CardActionArea>
    </Card>
  );
}
```

`web/src/features/avicola/GalponesPage.tsx`: página con (1) `useQuery` de granjas (`CLAVE_GRANJAS`) y toma `granjas[0]`; (2) `useQuery` de galpones con `queryKey: ['avicola', 'galpones', granja?.id]`, `enabled: !!granja`; (3) encabezado con nombre de granja + botón "Renombrar" (diálogo RHF+zod con un campo `nombre`, mutation `renombrarGranja` e invalidación de `CLAVE_GRANJAS`), visible solo con `useFuncionalidad('Granjas')`; (4) grilla de `TarjetaGalpon` (`Box` con `display: 'grid', gap: 2, gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))'`); (5) estado vacío: texto "Todavía no hay galpones." + botón "Crear el primero" (abre el mismo diálogo de alta); (6) botón "Nuevo galpón" visible solo con `useFuncionalidad('Galpones')`; (7) diálogo de alta con RHF+zod:

```ts
const esquemaGalpon = z.object({
  numero: z.string().trim().min(1, 'Ingresá el número del galpón.').max(10),
  capacidadMaxima: z.coerce.number().int().positive('La capacidad debe ser mayor que cero.'),
  gallinasActuales: z.coerce.number().int().min(0, 'No puede ser negativo.'),
  fechaNacimientoLote: z.string().min(1, 'Ingresá la fecha de poblado.'),
  descripcion: z.string().trim().max(500).optional(),
});
```

El submit llama `crearGalpon(granja.id, { ...datos, descripcion: datos.descripcion || null })`; los inputs numéricos con `inputMode: 'numeric'`; la fecha con `type="date"` e `InputLabelProps={{ shrink: true }}`; errores de validación del backend mapeados a campos con `setError` como en `TrabajadoresPage`; `Alert` con `error.message` si `ApiError`. Al éxito: invalidar `['avicola', 'galpones']` y cerrar. Estados: `isLoading` → `CircularProgress`, `isError` → `Alert` con botón "Reintentar" (`granjas.refetch()` / `galpones.refetch()`).

En `paginasDiferidas.tsx` agregar `GalponesPage` lazy; en `router.tsx`:

```tsx
          {
            path: '/avicola/galpones',
            element: (
              <ProtectedRoute>
                <RequiereFuncionalidad funcionalidades={['Galpones']}>
                  <GalponesPage />
                </RequiereFuncionalidad>
              </ProtectedRoute>
            ),
          },
```

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola/GalponesPage.test.tsx && npm run build`
Expected: PASS (5 tests) y build sin errores.

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola/GalponesPage.tsx web/src/features/avicola/TarjetaGalpon.tsx web/src/features/avicola/GalponesPage.test.tsx web/src/app/paginasDiferidas.tsx web/src/app/router.tsx
git commit -m "feat(web): lista de galpones con alta y renombrado de granja"
```

---

### Task 9: Frontend — Acciones de galpón: editar, ajustar inventario y desactivar

**Files:**
- Modify: `web/src/features/avicola/TarjetaGalpon.tsx` (o extraer los diálogos a `GalponAcciones.tsx` si la tarjeta crece; preferido: nuevo archivo `web/src/features/avicola/GalponAcciones.tsx` con los tres diálogos y los botones, usado desde `GalponesPage`)
- Test: `web/src/features/avicola/GalponAcciones.test.tsx`

**Interfaces:**
- Consumes: `actualizarGalpon`, `ajustarInventarioGalpon`, `desactivarGalpon` (Task 5); `useFuncionalidad('Galpones')`.
- Produces: `<GalponAcciones galpon={galpon} />` que renderiza los botones Editar / Inventario / Desactivar con sus diálogos. Nada más lo consume fuera de `GalponesPage`.

- [ ] **Step 1: Escribir el test que falla**

`web/src/features/avicola/GalponAcciones.test.tsx` (helpers copiados; rol Cliente con `['Granjas', 'Galpones']`; montar una mini-vista que liste un galpón con `GalponAcciones` dentro de `QueryClientProvider` + `AuthProvider`):

```tsx
describe('GalponAcciones', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('editar manda numero, descripcion y capacidad', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({
      'PUT /api/galpones/ga1': respuesta(204),
    });
    renderConGalpon();

    await usuario.click(await screen.findByRole('button', { name: 'Editar' }));
    await usuario.clear(screen.getByLabelText('Número'));
    await usuario.type(screen.getByLabelText('Número'), '2');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'PUT');
    expect(JSON.parse(await (llamada![0] as Request).clone().text())).toEqual({
      numero: '2', descripcion: null, capacidadMaxima: 5000,
    });
  });

  test('ajustar inventario manda el total absoluto', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({ 'PUT /api/galpones/ga1/inventario': respuesta(204) });
    renderConGalpon();

    await usuario.click(await screen.findByRole('button', { name: 'Inventario' }));
    await usuario.clear(screen.getByLabelText('Gallinas actuales'));
    await usuario.type(screen.getByLabelText('Gallinas actuales'), '4750');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(llamadaCon(fetchMock, 'PUT', '/galpones/ga1/inventario')).toBe(true);
  });

  test('desactivar pide confirmación y llama al DELETE', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({ 'DELETE /api/galpones/ga1': respuesta(204) });
    renderConGalpon();

    await usuario.click(await screen.findByRole('button', { name: 'Desactivar' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(llamadaCon(fetchMock, 'DELETE', '/galpones/ga1')).toBe(true);
  });

  test('sin funcionalidad Galpones no muestra acciones', async () => {
    baseFetchAvicolaConFuncionalidades(['ProduccionHuevos'], {});
    renderConGalpon();

    await screen.findByText('Galpón 1');
    expect(screen.queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/GalponAcciones.test.tsx`
Expected: FALLA la compilación.

- [ ] **Step 3: Implementación mínima**

`web/src/features/avicola/GalponAcciones.tsx`: componente con tres botones (`size="small"`) y tres diálogos RHF+zod, siguiendo el patrón de diálogos de `TrabajadoresPage`:

- **Editar**: campos `numero` (requerido, máx 10), `descripcion` (opcional, máx 500), `capacidadMaxima` (int > 0; zod: `.refine` o mensaje si < gallinasActuales actuales: "La capacidad no puede ser menor que las gallinas actuales."). Valores iniciales del galpón. Submit → `actualizarGalpon(galpon.id, { numero, descripcion: descripcion || null, capacidadMaxima })`.
- **Inventario**: campo `gallinasActuales` (int ≥ 0 y ≤ capacidadMaxima). Texto de ayuda: "Total absoluto de gallinas vivas; la mortalidad lo descuenta sola." Submit → `ajustarInventarioGalpon(galpon.id, gallinasActuales)`.
- **Desactivar**: diálogo de confirmación con texto "El galpón dejará de estar disponible. Los registros no se borran." y botón "Confirmar" → `desactivarGalpon(galpon.id)`.

Toda mutación invalida `['avicola', 'galpones']` y cierra su diálogo; errores con `Alert` (`ApiError.message`) y mapeo de `erroresValidacion` a campos. Si `!useFuncionalidad('Galpones')` el componente devuelve `null`. Montarlo dentro de cada tarjeta en `GalponesPage` (debajo del `CardActionArea`, como `CardActions`).

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola/GalponAcciones.test.tsx src/features/avicola/GalponesPage.test.tsx`
Expected: PASS (los tests de la Task 8 siguen verdes: los botones nuevos no pisan los `name` usados).

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola/GalponAcciones.tsx web/src/features/avicola/GalponAcciones.test.tsx web/src/features/avicola/GalponesPage.tsx web/src/features/avicola/TarjetaGalpon.tsx
git commit -m "feat(web): acciones de galpon (editar, inventario, desactivar)"
```

---

### Task 10: Frontend — `GalponPage`: detalle del día (lista cronológica, sellado)

**Files:**
- Create: `web/src/features/avicola/GalponPage.tsx`
- Modify: `web/src/app/paginasDiferidas.tsx`, `web/src/app/router.tsx` (ruta `/avicola/galpones/:galponId`)
- Test: `web/src/features/avicola/GalponPage.test.tsx`

**Interfaces:**
- Consumes: `obtenerGalpon`, `listarProduccion`, `listarMortalidad`, `obtenerEficiencia` (Task 5); `formatearConteo`, `hoyIso` (Task 5); `useFuncionalidad`.
- Produces: ruta `/avicola/galpones/:galponId` guardada con `RequiereFuncionalidad(['Galpones'])`; estado interno `fecha` (`useState(hoyIso())`) que Tasks 11–13 reusan vía props a los diálogos. queryKeys: `['avicola', 'galpon', galponId]`, `['avicola', 'produccion', galponId, fecha]`, `['avicola', 'mortalidad', galponId, fecha]`.

- [ ] **Step 1: Escribir el test que falla**

`web/src/features/avicola/GalponPage.test.tsx` (helpers copiados; montar `Route path="/avicola/galpones/:galponId"` con `initialEntries={['/avicola/galpones/ga1']}`). Datos:

```tsx
const galpon = {
  id: 'ga1', numero: '1', capacidadMaxima: 5000, gallinasActuales: 4790,
  fechaNacimientoLote: '2026-01-15', descripcion: null,
};
const produccionHoy = {
  galponId: 'ga1', fecha: hoyIso(),
  recogidas: [
    { id: 'p1', fecha: hoyIso(), hora: '09:30:00', cantidadMaples: 10, unidadesIncompletas: 5,
      maplesDescarte: 1, unidadesDescarte: 2, gallinasVivas: 4800, totalVendible: 305, totalDescarte: 32 },
    { id: 'p2', fecha: hoyIso(), hora: '14:00:00', cantidadMaples: 20, unidadesIncompletas: 0,
      maplesDescarte: 0, unidadesDescarte: 0, gallinasVivas: 4790, totalVendible: 600, totalDescarte: 0 },
  ],
  totalMaples: 30, totalUnidadesIncompletas: 5, totalVendible: 905,
  totalMaplesDescarte: 1, totalUnidadesDescarte: 2, totalDescarte: 32,
};
const mortalidadHoy = {
  galponId: 'ga1', fecha: hoyIso(),
  registros: [
    { id: 'm1', fecha: hoyIso(), hora: '06:15:00', cantidadMuertas: 10, gallinasVivas: 4790 },
  ],
  totalMuertas: 10,
};
const eficienciaHoy = {
  galponId: 'ga1', desde: hoyIso(), hasta: hoyIso(),
  dias: [{ fecha: hoyIso(), totalMaples: 30, totalUnidadesIncompletas: 5, totalVendible: 905,
           totalMaplesDescarte: 1, totalUnidadesDescarte: 2, totalDescarte: 32,
           gallinasVivas: 4790, eficiencia: 18.89, bajoUmbral: true }],
};
```

Tests:

```tsx
describe('GalponPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('encabezado con inventario y eficiencia del día con señal de umbral', async () => {
    baseFetchAvicola({
      'GET /api/galpones/ga1': respuesta(200, galpon),
      'GET /api/galpones/ga1/produccion': respuesta(200, produccionHoy),
      'GET /api/galpones/ga1/mortalidad': respuesta(200, mortalidadHoy),
      'GET /api/galpones/ga1/eficiencia': respuesta(200, eficienciaHoy),
    });
    renderPagina('/avicola/galpones/ga1');

    expect(await screen.findByText('Galpón 1')).toBeInTheDocument();
    expect(screen.getByText(/4790 \/ 5000 gallinas/)).toBeInTheDocument();
    expect(screen.getByText(/18,89 ?%|18\.89 ?%/)).toBeInTheDocument();
    expect(screen.getByText(/bajo umbral/i)).toBeInTheDocument();
  });

  test('lista cronológica unificada de recogidas y bajas', async () => {
    baseFetchAvicola({ /* mismos stubs */ });
    renderPagina('/avicola/galpones/ga1');

    const items = await screen.findAllByRole('listitem');
    expect(items[0]).toHaveTextContent('06:15');
    expect(items[0]).toHaveTextContent('10 bajas');
    expect(items[1]).toHaveTextContent('09:30');
    expect(items[1]).toHaveTextContent('10 maples + 5 (= 305)');
    expect(items[1]).toHaveTextContent('descarte 1 maples + 2 (= 32)');
    expect(items[2]).toHaveTextContent('14:00');
    expect(items[2]).toHaveTextContent('20 maples + 0 (= 600)');
  });

  test('totales del día en el resumen', async () => {
    baseFetchAvicola({ /* mismos stubs */ });
    renderPagina('/avicola/galpones/ga1');

    expect(await screen.findByText(/total del día/i)).toBeInTheDocument();
    expect(screen.getByText(/905 huevos vendibles/)).toBeInTheDocument();
    expect(screen.getByText(/32 de descarte/)).toBeInTheDocument();
    expect(screen.getByText(/10 bajas/)).toBeInTheDocument();
  });

  test('un día pasado se muestra solo lectura con aviso de sellado', async () => {
    const ayer = '2026-08-17';
    baseFetchAvicola({
      'GET /api/galpones/ga1': respuesta(200, galpon),
      [`GET /api/galpones/ga1/produccion?fecha=${ayer}`]: respuesta(200, { ...produccionHoy, fecha: ayer }),
      [`GET /api/galpones/ga1/mortalidad?fecha=${ayer}`]: respuesta(200, { ...mortalidadHoy, fecha: ayer }),
      [`GET /api/galpones/ga1/eficiencia?desde=${ayer}&hasta=${ayer}`]: respuesta(200, { ...eficienciaHoy, desde: ayer, hasta: ayer }),
    });
    renderPagina('/avicola/galpones/ga1');

    fireEvent.change(await screen.findByLabelText('Fecha'), { target: { value: ayer } });

    expect(await screen.findByText(/día sellado: no se puede corregir/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /registrar recogida/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Eliminar' })).not.toBeInTheDocument();
  });

  test('el día de hoy muestra las acciones de registro', async () => {
    baseFetchAvicola({ /* mismos stubs del día */ });
    renderPagina('/avicola/galpones/ga1');

    expect(await screen.findByRole('button', { name: /registrar recogida/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /registrar bajas/i })).toBeInTheDocument();
  });
});
```

Ojo con el helper `fetchSimulado`: la clave debe contemplar la query string. Copiar el helper de `TrabajadoresPage.test.tsx` y ajustarlo para que matchee primero por clave exacta `METODO path+search` y, si no hay regla exacta, por `METODO path` (así sirven tanto los stubs del día sin query como los de una fecha concreta). Este mismo helper ajustado se reusa en las Tasks 12, 13 y 14.

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/GalponPage.test.tsx`
Expected: FALLA la compilación (la página no existe).

- [ ] **Step 3: Implementación mínima**

`web/src/features/avicola/GalponPage.tsx`. Estructura:

```tsx
export function GalponPage() {
  const { galponId = '' } = useParams();
  const [fecha, setFecha] = useState(hoyIso());
  const esHoy = fecha === hoyIso();
  // useQuery: galpon (['avicola','galpon',galponId]), produccion, mortalidad,
  // eficiencia (['avicola','eficiencia',galponId,fecha,fecha]) — los tres con
  // la fecha seleccionada.
  // ...
}
```

- Encabezado: `Galpón {numero}` + `{gallinasActuales} / {capacidadMaxima} gallinas` + eficiencia del día (`dias[0]`) con el chip "Bajo umbral — considerar descarte" (`color="error"`) si `bajoUmbral`. Botón/enlace "Ver eficiencia" → `/avicola/galpones/{id}/eficiencia`.
- Selector: `<TextField type="date" label="Fecha" value={fecha} onChange={...} InputLabelProps={{ shrink: true }} inputProps={{ max: hoyIso() }} />`.
- Resumen del día: "Total del día: {totalVendible} huevos vendibles · {totalDescarte} de descarte · {totalMuertas} bajas".
- Lista unificada: combinar `recogidas` y `registros` en un array de eventos `{ hora, tipo: 'recogida' | 'bajas', datos }`, ordenar por `hora`, renderizar como `List`/`ListItem` (role `listitem`). Recogida: `{hora} — {formatearConteo(cantidadMaples, unidadesIncompletas)}` + línea secundaria `descarte {formatearConteo(maplesDescarte, unidadesDescarte)}` solo si `totalDescarte > 0`. Bajas: `{hora} — {cantidadMuertas} bajas`. La hora se muestra como `hora.slice(0, 5)`.
- Si `esHoy`: botones "Registrar recogida" y "Registrar bajas" (abren diálogos de Tasks 11 y 13 — en esta task dejar `useState` y diálogos placeholder mínimos con los botones deshabilitados NO; mejor: los diálogos llegan en 11/13; en esta task los botones existen pero sin diálogo todavía — marcar con comentario `// Task 11/13`). Botones "Editar" y "Eliminar" por item (sin diálogo hasta Task 12). Si no es hoy: `Alert severity="info"` "Día sellado: no se puede corregir" y ninguna acción.
- Acciones de registro visibles solo con `useFuncionalidad('ProduccionHuevos')` (recogida) y `useFuncionalidad('Mortalidad')` (bajas).
- Estados: carga → `CircularProgress`; error en cualquiera de las queries → `Alert` con reintento; galpón 404 → `Alert` "No se encontró el galpón." (caso `ApiError` con `status === 404`).

Registrar la ruta en `router.tsx` con `RequiereFuncionalidad(['Galpones'])` y el lazy en `paginasDiferidas.tsx`.

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola/GalponPage.test.tsx && npm run build`
Expected: PASS (5 tests) y build sin errores.

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola/GalponPage.tsx web/src/features/avicola/GalponPage.test.tsx web/src/app/paginasDiferidas.tsx web/src/app/router.tsx
git commit -m "feat(web): detalle del dia del galpon con sellado y eficiencia"
```

---

### Task 11: Frontend — `RegistrarRecogidaDialog` (con sección de bajas y total en vivo)

**Files:**
- Create: `web/src/features/avicola/RegistrarRecogidaDialog.tsx`
- Modify: `web/src/features/avicola/GalponPage.tsx` (enchufar el diálogo en el botón "Registrar recogida")
- Test: `web/src/features/avicola/RegistrarRecogidaDialog.test.tsx`

**Interfaces:**
- Consumes: `registrarProduccion`, `registrarMortalidad`, `DatosRecogida`, `DatosBajas` (Task 5); `totalHuevos`, `HUEVOS_POR_MAPLE` (Task 5); `useConexion` (Task 6).
- Produces: `<RegistrarRecogidaDialog galponId={string} abierto={boolean} alCerrar={() => void} />`. Al éxito invalida `['avicola', 'produccion']`, `['avicola', 'mortalidad']`, `['avicola', 'eficiencia']` y `['avicola', 'galpon']`.

- [ ] **Step 1: Escribir el test que falla**

`web/src/features/avicola/RegistrarRecogidaDialog.test.tsx` (helpers copiados; montar el diálogo con `abierto` dentro de `QueryClientProvider` + `AuthProvider`; stub de me con rol Trabajador y `funcionalidades: ['ProduccionHuevos', 'Mortalidad']`):

```tsx
describe('RegistrarRecogidaDialog', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('el total en vivo suma maples y sueltos', async () => {
    const usuario = userEvent.setup();
    baseFetchAvicola({});
    renderDialog();

    await usuario.type(screen.getByLabelText('Maples'), '10');
    await usuario.type(screen.getByLabelText('Unidades sueltas'), '5');

    expect(screen.getByText('= 305 huevos')).toBeInTheDocument();
  });

  test('guardar sin bajas envía solo producción con idempotencyKey', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({
      'POST /api/galpones/ga1/produccion': respuesta(201, { id: 'p1' }),
    });
    renderDialog();

    await usuario.type(screen.getByLabelText('Maples'), '10');
    await usuario.type(screen.getByLabelText('Unidades sueltas'), '5');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(llamadaCon(fetchMock, 'POST', '/galpones/ga1/produccion')).toBe(true);
    expect(llamadaCon(fetchMock, 'POST', '/galpones/ga1/mortalidad')).toBe(false);
    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'POST');
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo.cantidadMaples).toBe(10);
    expect(cuerpo.idempotencyKey).toBeTruthy();
  });

  test('con la sección de bajas envía producción y mortalidad', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({
      'POST /api/galpones/ga1/produccion': respuesta(201, { id: 'p1' }),
      'POST /api/galpones/ga1/mortalidad': respuesta(201, { id: 'm1' }),
    });
    renderDialog();

    await usuario.type(screen.getByLabelText('Maples'), '10');
    await usuario.click(screen.getByRole('button', { name: /¿hubo bajas\?/i }));
    await usuario.type(screen.getByLabelText('Gallinas muertas'), '8');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(llamadaCon(fetchMock, 'POST', '/galpones/ga1/produccion')).toBe(true);
    expect(llamadaCon(fetchMock, 'POST', '/galpones/ga1/mortalidad')).toBe(true);
  });

  test('rechaza 30 unidades sueltas sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({});
    renderDialog();

    await usuario.type(screen.getByLabelText('Unidades sueltas'), '30');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/menos de 30|entre 0 y 29/)).toBeInTheDocument();
    expect(llamadaCon(fetchMock, 'POST', '/produccion')).toBe(false);
  });

  test('si falla la mortalidad tras guardar la recogida, avisa y no reenvía producción', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({
      'POST /api/galpones/ga1/produccion': respuesta(201, { id: 'p1' }),
      'POST /api/galpones/ga1/mortalidad': [
        respuesta(500, { title: 'Error interno' }),
        respuesta(201, { id: 'm1' }),
      ],
    });
    renderDialog();

    await usuario.type(screen.getByLabelText('Maples'), '10');
    await usuario.click(screen.getByRole('button', { name: /¿hubo bajas\?/i }));
    await usuario.type(screen.getByLabelText('Gallinas muertas'), '8');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/la recogida se guardó; las bajas no/i)).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: /reintentar bajas/i }));

    const postsProduccion = fetchMock.mock.calls.filter(
      ([arg]) => (arg as Request).method === 'POST' && (arg as Request).url.includes('/produccion'),
    );
    expect(postsProduccion).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/RegistrarRecogidaDialog.test.tsx`
Expected: FALLA la compilación (el componente no existe).

- [ ] **Step 3: Implementación mínima**

`web/src/features/avicola/RegistrarRecogidaDialog.tsx`. Esquema zod:

```ts
const esquema = z.object({
  hora: z.string().regex(/^\d{2}:\d{2}$/, 'Hora inválida.'),
  cantidadMaples: z.coerce.number().int().min(0),
  unidadesIncompletas: z.coerce.number().int().min(0).max(29, 'Las unidades sueltas deben ser menos de 30.'),
  maplesDescarte: z.coerce.number().int().min(0),
  unidadesDescarte: z.coerce.number().int().min(0).max(29, 'Las unidades sueltas deben ser menos de 30.'),
  huboBajas: z.boolean(),
  cantidadMuertas: z.coerce.number().int().min(0),
}).refine((d) => !d.huboBajas || d.cantidadMuertas > 0, {
  path: ['cantidadMuertas'],
  message: 'Ingresá cuántas gallinas murieron.',
});
```

Estructura del componente:

- `Dialog` con `DialogTitle` "Registrar recogida". Campos RHF con `inputMode: 'numeric'`: "Maples", "Unidades sueltas"; debajo, `Typography` con el total en vivo: `= {totalHuevos(watch('cantidadMaples') || 0, watch('unidadesIncompletas') || 0)} huevos` (usar `watch` con fallback 0 y sanear NaN: `Number.isFinite`).
- Bloque de descarte (`fieldset` visual con `Typography variant="subtitle2"` "Huevos de descarte (no cuentan para la eficiencia)"): "Maples de descarte", "Sueltos de descarte", con su propio total en vivo.
- `Accordion` (o sección plegable con `Button` + `Collapse`) titulada "¿Hubo bajas?" que al expandirse setea `huboBajas` (vía `setValue`) y muestra el campo "Gallinas muertas".
- Campo "Hora de la recogida" (`type="time"`), valor inicial la hora actual (`new Date().toTimeString().slice(0, 5)`).
- Estado de envío con `useState` propio (no una sola mutation): fase `'editando' | 'guardando' | 'errorBajas'`. Submit:
  1. `registrarProduccion(galponId, { hora, cantidadMaples, unidadesIncompletas, maplesDescarte, unidadesDescarte, idempotencyKey: crypto.randomUUID() })`.
  2. Si `huboBajas`: `registrarMortalidad(galponId, { hora, cantidadMuertas, idempotencyKey: crypto.randomUUID() })`.
  3. Si el paso 2 falla: fase `'errorBajas'` → `Alert` "La recogida se guardó; las bajas no. Reintentá las bajas." + botón "Reintentar bajas" que repite SOLO el paso 2 con la MISMA `idempotencyKey` de mortalidad (guardarla en un `useRef`).
  4. Éxito total: invalidar `['avicola', 'produccion']`, `['avicola', 'mortalidad']`, `['avicola', 'eficiencia']`, `['avicola', 'galpon']` y `alCerrar()`.
- Botón "Guardar" `disabled={guardando || !enLinea}` (`useConexion()`); si está offline, `helperText` "Sin conexión: no se puede guardar.".
- Errores `ApiError.erroresValidacion` → `setError` por campo (camelCase); `Alert` general con `error.message` en el resto.

En `GalponPage`: reemplazar el placeholder del botón "Registrar recogida" por `const [recogidaAbierta, setRecogidaAbierta] = useState(false)` y montar `<RegistrarRecogidaDialog galponId={galponId} abierto={recogidaAbierta} alCerrar={() => setRecogidaAbierta(false)} />`.

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola/RegistrarRecogidaDialog.test.tsx src/features/avicola/GalponPage.test.tsx && npm run build`
Expected: PASS y build sin errores.

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola/RegistrarRecogidaDialog.tsx web/src/features/avicola/RegistrarRecogidaDialog.test.tsx web/src/features/avicola/GalponPage.tsx
git commit -m "feat(web): registro de recogida con descarte y bajas en un solo flujo"
```

---

### Task 12: Frontend — Editar y eliminar registros del día

**Files:**
- Create: `web/src/features/avicola/EditarRecogidaDialog.tsx`
- Create: `web/src/features/avicola/EditarBajasDialog.tsx`
- Modify: `web/src/features/avicola/GalponPage.tsx` (botones Editar/Eliminar por item con sus diálogos y confirmación)
- Test: `web/src/features/avicola/EditarRegistros.test.tsx`

**Interfaces:**
- Consumes: `editarProduccion`, `desactivarProduccion`, `editarMortalidad`, `desactivarMortalidad` (Task 5); tipos `RecogidaResumen`, `MortalidadRegistro`.
- Produces: `<EditarRecogidaDialog recogida={RecogidaResumen} abierto alCerrar />` y `<EditarBajasDialog registro={MortalidadRegistro} abierto alCerrar />`. Solo los usa `GalponPage`.

- [ ] **Step 1: Escribir el test que falla**

`web/src/features/avicola/EditarRegistros.test.tsx` (montar `GalponPage` como en Task 10 con los stubs del día):

```tsx
describe('Editar y eliminar registros del día', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('editar una recogida manda el PUT con los nuevos valores', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({
      /* stubs del día (Task 10) */ ,
      'PUT /api/produccion/p1': respuesta(204),
    });
    renderPagina('/avicola/galpones/ga1');

    const item = (await screen.findAllByRole('listitem'))[1];
    await usuario.click(within(item).getByRole('button', { name: 'Editar' }));
    await usuario.clear(screen.getByLabelText('Maples'));
    await usuario.type(screen.getByLabelText('Maples'), '12');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'PUT');
    expect((llamada![0] as Request).url).toContain('/api/produccion/p1');
    expect(JSON.parse(await (llamada![0] as Request).clone().text()).cantidadMaples).toBe(12);
  });

  test('eliminar una recogida pide confirmación y desactiva', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({ /* stubs del día */, 'DELETE /api/produccion/p1': respuesta(204) });
    renderPagina('/avicola/galpones/ga1');

    const item = (await screen.findAllByRole('listitem'))[1];
    await usuario.click(within(item).getByRole('button', { name: 'Eliminar' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }));

    expect(llamadaCon(fetchMock, 'DELETE', '/produccion/p1')).toBe(true);
  });

  test('editar bajas manda hora y cantidad', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({ /* stubs del día */, 'PUT /api/mortalidad/m1': respuesta(204) });
    renderPagina('/avicola/galpones/ga1');

    const item = (await screen.findAllByRole('listitem'))[0];
    await usuario.click(within(item).getByRole('button', { name: 'Editar' }));
    await usuario.clear(screen.getByLabelText('Gallinas muertas'));
    await usuario.type(screen.getByLabelText('Gallinas muertas'), '14');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'PUT');
    expect((llamada![0] as Request).url).toContain('/api/mortalidad/m1');
    expect(JSON.parse(await (llamada![0] as Request).clone().text())).toEqual({ hora: '06:15', cantidadMuertas: 14 });
  });

  test('el sellado del backend se muestra como alerta (409/400 al editar)', async () => {
    const usuario = userEvent.setup();
    baseFetchAvicola({
      /* stubs del día */,
      'PUT /api/produccion/p1': respuesta(400, { title: 'El registro está sellado: solo se puede corregir el mismo día.' }),
    });
    renderPagina('/avicola/galpones/ga1');

    const item = (await screen.findAllByRole('listitem'))[1];
    await usuario.click(within(item).getByRole('button', { name: 'Editar' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/está sellado/i)).toBeInTheDocument();
  });
});
```

(`within` de `@testing-library/react`.)

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/EditarRegistros.test.tsx`
Expected: FALLA la compilación (los diálogos no existen) o los botones Editar/Eliminar sin diálogo no disparan nada.

- [ ] **Step 3: Implementación mínima**

`EditarRecogidaDialog.tsx`: como `RegistrarRecogidaDialog` pero sin sección de bajas y sin `idempotencyKey`; valores iniciales desde `recogida` (`hora.slice(0, 5)`); submit → `editarProduccion(recogida.id, { hora, cantidadMaples, unidadesIncompletas, maplesDescarte, unidadesDescarte })`; invalida `['avicola', 'produccion']` y `['avicola', 'eficiencia']`.

`EditarBajasDialog.tsx`: campos "Hora" y "Gallinas muertas" (int > 0); submit → `editarMortalidad(registro.id, { hora, cantidadMuertas })`; invalida `['avicola', 'mortalidad']`, `['avicola', 'galpon']` y `['avicola', 'eficiencia']` (la edición cambia el inventario y el snapshot).

En `GalponPage`, por item con `esHoy` y la funcionalidad correspondiente: botones "Editar" (abre el diálogo con ese registro en estado) y "Eliminar" (diálogo de confirmación: "El registro se desactiva; no se borra. Si era una baja, las gallinas vuelven al inventario." → `desactivarProduccion` o `desactivarMortalidad` según el tipo; invalidar los mismos prefijos que la edición).

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola && npm run build`
Expected: PASS (toda la suite de la feature) y build sin errores.

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola/EditarRecogidaDialog.tsx web/src/features/avicola/EditarBajasDialog.tsx web/src/features/avicola/EditarRegistros.test.tsx web/src/features/avicola/GalponPage.tsx
git commit -m "feat(web): edicion y desactivacion de registros del dia"
```

---

### Task 13: Frontend — `RegistrarBajasDialog` (mortalidad sola)

**Files:**
- Create: `web/src/features/avicola/RegistrarBajasDialog.tsx`
- Modify: `web/src/features/avicola/GalponPage.tsx` (enchufar en "Registrar bajas")
- Test: `web/src/features/avicola/RegistrarBajasDialog.test.tsx`

**Interfaces:**
- Consumes: `registrarMortalidad`, `DatosBajas` (Task 5); `useConexion` (Task 6).
- Produces: `<RegistrarBajasDialog galponId abierto alCerrar />`. Solo la usa `GalponPage`.

- [ ] **Step 1: Escribir el test que falla**

```tsx
describe('RegistrarBajasDialog', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('registra bajas solas con idempotencyKey', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({ 'POST /api/galpones/ga1/mortalidad': respuesta(201, { id: 'm1' }) });
    renderDialog();

    await usuario.type(screen.getByLabelText('Gallinas muertas'), '10');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).method === 'POST');
    const cuerpo = JSON.parse(await (llamada![0] as Request).clone().text());
    expect(cuerpo.cantidadMuertas).toBe(10);
    expect(cuerpo.idempotencyKey).toBeTruthy();
    expect(cuerpo).not.toHaveProperty('fecha');
  });

  test('rechaza cero muertas sin llamar a la API', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({});
    renderDialog();

    await usuario.type(screen.getByLabelText('Gallinas muertas'), '0');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText(/mayor que cero/i)).toBeInTheDocument();
    expect(llamadaCon(fetchMock, 'POST', '/mortalidad')).toBe(false);
  });

  test('sin conexión el botón de guardar queda deshabilitado', async () => {
    baseFetchAvicola({});
    renderDialog();

    act(() => {
      window.dispatchEvent(new Event('offline'));
    });

    expect(await screen.findByRole('button', { name: 'Guardar' })).toBeDisabled();

    act(() => {
      window.dispatchEvent(new Event('online'));
    });
  });
});
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/RegistrarBajasDialog.test.tsx`
Expected: FALLA la compilación.

- [ ] **Step 3: Implementación mínima**

`RegistrarBajasDialog.tsx`: `Dialog` "Registrar bajas" con campos "Hora" (`type="time"`, valor inicial la hora actual) y "Gallinas muertas" (zod: `z.coerce.number().int().positive('La cantidad debe ser mayor que cero.')`, `inputMode: 'numeric'`). Submit → `registrarMortalidad(galponId, { hora, cantidadMuertas, idempotencyKey: crypto.randomUUID() })`; invalida `['avicola', 'mortalidad']`, `['avicola', 'galpon']`, `['avicola', 'eficiencia']` y cierra. Botón deshabilitado con `!useConexion()`. Errores como en Task 11.

Enchufarlo en `GalponPage` al botón "Registrar bajas".

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola && npm run build`
Expected: PASS y build sin errores.

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola/RegistrarBajasDialog.tsx web/src/features/avicola/RegistrarBajasDialog.test.tsx web/src/features/avicola/GalponPage.tsx
git commit -m "feat(web): registro de bajas sin recogida"
```

---

### Task 14: Frontend — `EficienciaPage` (histórico con rango)

**Files:**
- Create: `web/src/features/avicola/EficienciaPage.tsx`
- Modify: `web/src/app/paginasDiferidas.tsx`, `web/src/app/router.tsx` (ruta `/avicola/galpones/:galponId/eficiencia`)
- Test: `web/src/features/avicola/EficienciaPage.test.tsx`

**Interfaces:**
- Consumes: `obtenerEficiencia`, `EficienciaGalpon`, `EficienciaDia` (Tasks 3 y 5); `formatearConteo`, `hoyIso`.
- Produces: ruta `/avicola/galpones/:galponId/eficiencia` guardada con `RequiereFuncionalidad(['ProduccionHuevos'])` (la política del backend de eficiencia es `ProduccionHuevos`).

- [ ] **Step 1: Escribir el test que falla**

```tsx
describe('EficienciaPage', () => {
  beforeEach(() => vi.restoreAllMocks());

  test('consulta por defecto los últimos 14 días y lista cada día', async () => {
    const fetchMock = baseFetchAvicola({
      'GET /api/galpones/ga1/eficiencia?desde=DESDE&hasta=HOY': respuesta(200, {
        galponId: 'ga1', desde: 'DESDE', hasta: 'HOY',
        dias: [
          { fecha: '2026-08-17', totalMaples: 100, totalUnidadesIncompletas: 0, totalVendible: 3000,
            totalMaplesDescarte: 2, totalUnidadesDescarte: 0, totalDescarte: 60,
            gallinasVivas: 4800, eficiencia: 62.5, bajoUmbral: true },
          { fecha: '2026-08-18', totalMaples: 110, totalUnidadesIncompletas: 10, totalVendible: 3310,
            totalMaplesDescarte: 0, totalUnidadesDescarte: 5, totalDescarte: 5,
            gallinasVivas: 4790, eficiencia: 69.1, bajoUmbral: true },
        ],
      }),
    });
    renderPagina('/avicola/galpones/ga1/eficiencia');

    expect(await screen.findByText('2026-08-17')).toBeInTheDocument();
    expect(screen.getByText(/3\.000|3000/)).toBeInTheDocument();
    expect(screen.getAllByText(/bajo umbral/i)).toHaveLength(2);
    // El rango por defecto llega en la URL:
    const llamada = fetchMock.mock.calls.find(([arg]) => (arg as Request).url.includes('/eficiencia'));
    expect((llamada![0] as Request).url).toContain('desde=');
    expect((llamada![0] as Request).url).toContain('hasta=');
  });

  test('cambiar el rango vuelve a consultar', async () => {
    const usuario = userEvent.setup();
    const fetchMock = baseFetchAvicola({
      /* stub genérico con cola: dos respuestas iguales */
    });
    renderPagina('/avicola/galpones/ga1/eficiencia');

    await screen.findByText('2026-08-17');
    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-08-10' } });

    await waitFor(() => {
      const llamadas = fetchMock.mock.calls.filter(([arg]) => (arg as Request).url.includes('desde=2026-08-10'));
      expect(llamadas.length).toBeGreaterThan(0);
    });
  });

  test('estado vacío cuando el rango no tiene días con eventos', async () => {
    baseFetchAvicola({
      /* stub */: respuesta(200, { galponId: 'ga1', desde: 'x', hasta: 'y', dias: [] }),
    });
    renderPagina('/avicola/galpones/ga1/eficiencia');

    expect(await screen.findByText(/sin registros en el rango/i)).toBeInTheDocument();
  });
});
```

Para la clave del stub con fechas dinámicas, hacer el helper tolerante: en vez de clave exacta, permitir una regla `'GET /api/galpones/ga1/eficiencia'` que matchee por prefijo (ajustar el helper copiado: primero intenta clave exacta `METODO path+search`, después `METODO path`).

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/features/avicola/EficienciaPage.test.tsx`
Expected: FALLA la compilación.

- [ ] **Step 3: Implementación mínima**

`EficienciaPage.tsx`:

```tsx
function haceDiasIso(dias: number): string {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() - dias);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${fecha.getFullYear()}-${pad(fecha.getMonth() + 1)}-${pad(fecha.getDate())}`;
}

export function EficienciaPage() {
  const { galponId = '' } = useParams();
  const [desde, setDesde] = useState(haceDiasIso(13)); // hoy + 13 atrás = 14 días
  const [hasta, setHasta] = useState(hoyIso());
  const eficiencia = useQuery({
    queryKey: ['avicola', 'eficiencia', galponId, desde, hasta],
    queryFn: () => obtenerEficiencia(galponId, desde, hasta),
  });
  // ...
}
```

- Selectores `type="date"` "Desde" y "Hasta" (`max: hoyIso()` en ambos, `min: desde` en Hasta).
- Lista/tabla por día: fecha, `{formatearConteo(totalMaples, totalUnidadesIncompletas)}` vendible, `{totalDescarte}` descarte, `{gallinasVivas}` gallinas, `{eficiencia} %` en `Typography` con `color: 'error.main'` si `bajoUmbral`, y chip "Bajo umbral — considerar descarte". En móvil se renderiza como tarjetas (mismo markup con `Box` grid, sin tabla MUI para simplificar responsive).
- Estados: carga, error con reintento, vacío ("Sin registros en el rango elegido.").

Ruta en `router.tsx` con `RequiereFuncionalidad(['ProduccionHuevos'])` y lazy en `paginasDiferidas.tsx`.

- [ ] **Step 4: Ejecutar y ver el verde**

Run: `cd web && npx vitest run src/features/avicola/EficienciaPage.test.tsx && npm run build`
Expected: PASS (3 tests) y build sin errores.

- [ ] **Step 5: Commit**

```bash
git add web/src/features/avicola/EficienciaPage.tsx web/src/features/avicola/EficienciaPage.test.tsx web/src/app/paginasDiferidas.tsx web/src/app/router.tsx
git commit -m "feat(web): historico de eficiencia por galpon con umbral"
```

---

### Task 15: Frontend — `AppLayout` responsive, menú avícola e inicio por rol

**Files:**
- Modify: `web/src/app/AppLayout.tsx`
- Modify: `web/src/app/inicioSegunRol.ts`
- Modify: `web/src/test/setup.ts` (stub de `matchMedia`)
- Test: `web/src/app/AppLayout.test.tsx` (actualizar + nuevos casos), `web/src/app/inicioSegunRol.test.ts` (actualizar)

**Interfaces:**
- Consumes: `useAuth` (rol, `tieneFuncionalidad`); rutas de Tasks 7–14.
- Produces: menú con "Gestión Avícola" visible para Cliente y para Trabajador con alguna funcionalidad avícola; drawer en pantalla angosta; `inicioSegunRol('Cliente')` e `inicioSegunRol('Trabajador')` → `'/avicola'`.

- [ ] **Step 1: Escribir/actualizar los tests que fallan**

En `web/src/test/setup.ts` agregar el stub (jsdom no implementa `matchMedia`):

```ts
if (!window.matchMedia) {
  window.matchMedia = ((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => undefined,
    removeListener: () => undefined,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    dispatchEvent: () => false,
  })) as unknown as typeof window.matchMedia;
}
```

En `inicioSegunRol.test.ts` actualizar expectativas: `inicioSegunRol('Cliente')` → `'/avicola'`; `inicioSegunRol('Trabajador')` → `'/avicola'`; Administrador sigue en `'/admin/clientes'`.

En `AppLayout.test.tsx` agregar:

```tsx
  test('un cliente ve el enlace a Gestión Avícola', async () => {
    // me con rol Cliente, modulos: ['GestionAvicola'], funcionalidades: ['Granjas']
    // ... render del layout con los stubs del archivo
    expect(await screen.findByRole('link', { name: 'Gestión Avícola' })).toBeInTheDocument();
  });

  test('un trabajador sin funcionalidades avícola no ve el enlace', async () => {
    // me con rol Trabajador, modulos: [], funcionalidades: []
    await screen.findByText(/icarus/i);
    expect(screen.queryByRole('link', { name: 'Gestión Avícola' })).not.toBeInTheDocument();
  });

  test('en pantalla angosta la navegación se vuelve menú hamburguesa con drawer', async () => {
    // stub de matchMedia con matches: true para '(max-width:…)' antes de renderizar
    window.matchMedia = ((query: string) => ({
      matches: true, media: query, onchange: null,
      addListener: () => undefined, removeListener: () => undefined,
      addEventListener: () => undefined, removeEventListener: () => undefined,
      dispatchEvent: () => false,
    })) as unknown as typeof window.matchMedia;

    // render del layout con me Cliente...
    const botonMenu = await screen.findByRole('button', { name: /abrir menú/i });
    await userEvent.click(botonMenu);
    expect(await screen.findByRole('presentation')).toBeInTheDocument(); // drawer abierto
    expect(screen.getByRole('link', { name: 'Gestión Avícola' })).toBeInTheDocument();
  });
```

- [ ] **Step 2: Ejecutar y ver el rojo**

Run: `cd web && npx vitest run src/app`
Expected: FALLAN los tests nuevos/actualizados (enlaces y redirecciones viejas).

- [ ] **Step 3: Implementación mínima**

`inicioSegunRol.ts`:

```ts
import type { Rol } from '../lib/tipos';

// Destino de inicio según rol: Administrador ve clientes; Cliente y
// Trabajador entran a Gestión Avícola (spec frontend avícola).
export function inicioSegunRol(rol: Rol): string {
  switch (rol) {
    case 'Administrador':
      return '/admin/clientes';
    case 'Cliente':
    case 'Trabajador':
      return '/avicola';
    default:
      return '/inicio';
  }
}
```

`AppLayout.tsx`: agregar el enlace avícola y el modo móvil.

```tsx
import { AppBar, Box, Button, Drawer, IconButton, List, ListItemButton, ListItemText, Toolbar, Typography, useMediaQuery } from '@mui/material';
import MenuRoundedIcon from '@mui/icons-material/MenuRounded';
// ... imports existentes + useState

const ENLACES_POR_ROL: Partial<Record<Rol, EnlaceMenu[]>> = {
  Administrador: [{ etiqueta: 'Clientes', ruta: '/admin/clientes' }],
  Cliente: [
    { etiqueta: 'Gestión Avícola', ruta: '/avicola' },
    { etiqueta: 'Trabajadores', ruta: '/trabajadores' },
  ],
};

export function AppLayout() {
  const { rol, cerrarSesion, tieneFuncionalidad } = useAuth();
  const esMovil = useMediaQuery('(max-width:600px)');
  const [menuAbierto, setMenuAbierto] = useState(false);
  const navigate = useNavigate();

  const enlacesBase: EnlaceMenu[] = rol ? (ENLACES_POR_ROL[rol] ?? []) : [];
  const enlaces: EnlaceMenu[] = [
    ...enlacesBase,
    // Trabajador: el enlace avícola solo si tiene alguna funcionalidad del
    // módulo (el Cliente ya lo tiene en su lista base).
    ...(rol === 'Trabajador' &&
    tieneFuncionalidad('Granjas', 'Galpones', 'ProduccionHuevos', 'Mortalidad')
      ? [{ etiqueta: 'Gestión Avícola', ruta: '/avicola' }]
      : []),
  ];

  const salir = () => {
    cerrarSesion();
    navigate('/login');
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100dvh' }}>
      <AppBar position="sticky" color="primary">
        <Toolbar sx={{ gap: 1 }}>
          {esMovil && (
            <IconButton color="inherit" aria-label="Abrir menú" onClick={() => setMenuAbierto(true)}>
              <MenuRoundedIcon />
            </IconButton>
          )}
          <Typography
            variant="h6"
            component={RouterLink}
            to="/"
            sx={{ flexGrow: 1, color: 'inherit', textDecoration: 'none' }}
          >
            Icarus
          </Typography>
          {!esMovil &&
            enlaces.map((enlace) => (
              <Button key={enlace.ruta} component={RouterLink} to={enlace.ruta} color="inherit">
                {enlace.etiqueta}
              </Button>
            ))}
          <Button color="inherit" startIcon={<LogoutRoundedIcon />} onClick={salir}>
            Cerrar sesión
          </Button>
        </Toolbar>
      </AppBar>
      <Drawer open={menuAbierto} onClose={() => setMenuAbierto(false)}>
        <List sx={{ width: 240 }}>
          {enlaces.map((enlace) => (
            <ListItemButton
              key={enlace.ruta}
              component={RouterLink}
              to={enlace.ruta}
              onClick={() => setMenuAbierto(false)}
            >
              <ListItemText primary={enlace.etiqueta} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>
      <BannerSinConexion />
      <Box component="main" sx={{ flexGrow: 1 }}>
        <Suspense fallback={<CargandoRuta />}>
          <Outlet />
        </Suspense>
      </Box>
    </Box>
  );
}
```

(`BannerSinConexion` ya se montó en Task 6; acá solo se muestra su posición relativa al Drawer.)

- [ ] **Step 4: Ejecutar y ver el verde + suite completa del frontend**

Run: `cd web && npx vitest run && npm run lint && npm run format:check && npm run build`
Expected: PASS toda la suite, lint y formato limpios, build sin errores. Si tests viejos del layout fallan por el markup nuevo (p. ej. queries por rol de botón que ahora es link dentro del drawer), actualizar esos tests al comportamiento nuevo — sin relajar assertions de negocio.

- [ ] **Step 5: Commit**

```bash
git add web/src/app web/src/test/setup.ts
git commit -m "feat(web): layout responsive con drawer y menu avicola por funcionalidad"
```

---

### Task 16: Cierre — Verificación final y actualización del spec de contexto

- [ ] **Step 1: Suite completa de frontend y backend**

```bash
cd web && npx vitest run && npm run lint && npm run format:check && npm run build && cd ..
dotnet test Icarus/tests/Icarus.UnitTests
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~IdentityEndpointsTests"
```

(Docker corriendo para integración. La suite completa de integración NO es obligatoria en esta sesión: corre el usuario con la puerta de calidad.)

- [ ] **Step 2: Actualizar `AGENTS.md` (sección Proyecto) y regenerar adaptadores**

En la descripción del proyecto, mencionar que la PWA ya incluye la UI de Gestión Avícola (granjas, galpones, recogida, mortalidad, eficiencia) online-first. Luego:

```bash
node quality/generar-adaptadores.mjs
```

- [ ] **Step 3: Commit de cierre**

```bash
git add AGENTS.md CLAUDE.md GEMINI.md .github/copilot-instructions.md docs
git commit -m "docs: ui avicola online-first en la pwa"
```

- [ ] **Step 4: Informar al usuario**

Recordarle que la puerta de calidad (`ejecutar-puerta-calidad.ps1`) y el push quedan a su cargo. Borrar `docs/ai/HANDOFF.md` si el trabajo cerró completo.
