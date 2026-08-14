# Entitlement por funcionalidad y roles simplificados — Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Aplicar el modelo aprobado en `docs/superpowers/specs/2026-08-14-entitlement-por-funcionalidad-design.md`: tres roles (se elimina `SoporteTecnico`), entitlement en dos niveles (módulo contractual del `Administrador` al `Cliente`; funcionalidad operativa del `Cliente` al `Trabajador`), alta embebida de cuentas de `Cliente`/`Trabajador` dentro de `POST /clientes` y `POST /clientes/{clienteId}/trabajadores` (orquestada desde el Host, sin que Clientes referencie Identity), y la eliminación del CRUD de usuarios (`POST /identidad/usuarios` y el paquete `Usuarios/` de Identity). Un solo módulo concreto, `GestionAvicola`, con 8 funcionalidades; `ControlAcceso` queda previsto sin funcionalidades.

**Architecture:** Monolito modular por capas. La regla de oro sigue siendo el aislamiento de módulos (tests de arquitectura lo fuerzan): Clientes NO referencia a Identity y viceversa. El Host es el único que referencia ambos y es donde vive la orquestación de cuentas. `VerificadorEntitlement` pasa a distinguir por la presencia de `TrabajadorId` en `ICurrentUser` (solo el rol `Trabajador` lo lleva), de modo que Clientes no necesita conocer los nombres de rol de Identity: `TrabajadorId` nulo ⇒ semántica de rol `Cliente` (todas las funcionalidades de los módulos de su cliente); `TrabajadorId` presente ⇒ semántica de rol `Trabajador` (solo sus funcionalidades asignadas).

**Tech Stack:** .NET 10, EF Core 10 + SQL Server (schema `clientes`), MediatR, FluentValidation, ASP.NET Core Authorization, xUnit + NSubstitute + NetArchTest + Testcontainers.MsSql. **Sin paquetes NuGet nuevos.**

## Global Constraints

- Identificadores, comentarios y mensajes en español correcto, UTF-8 sin BOM. Nunca mojibake. Identificadores C# sin acentos ni eñes (`Funcionalidades`, `ProduccionHuevos`, `Vacunacion`, `Alimentacion`, `Contrasena`).
- Anti-PII: email y contraseña nunca en logs ni en mensajes de error. Los conflictos de unicidad (identificador fiscal, documento, email) devuelven 409 genéricos sin revelar el dato duplicado; un tenant ajeno es indistinguible de uno inexistente (404 genérico).
- Soft delete transversal (glosario, regla 1): nunca borrado físico. La compensación de una cuenta que no se pudo registrar **suspende/desactiva** la entidad recién creada; no la borra.
- Ninguna fecha del dominio admite futuro (glosario, regla 2).
- **Interpretación registrada del spec**: el enum `Modulos` se conserva como contrato de módulos del cliente (spec: «un `Modulos` sigue siendo el contrato del cliente»); la granularidad nueva es `Funcionalidades`, un enum aparte con su relación declarativa módulo → funcionalidades.
- Aislamiento de módulos: Clientes no usa el enum `Rol` de Identity ni los literales de rol. El `AltaCuentasServicio` (Host) sí usa `nameof(Rol.Cliente)`/`nameof(Rol.Trabajador)`, porque el Host referencia ambos módulos.
- `Rol` se persiste como texto en `Usuario` (ASP.NET Identity), así que renumerar el enum no requiere migración de Identity: solo cambian el enum, la semilla y los tests.
- Un test que nunca se vio en rojo no prueba nada: cada test se corre primero en rojo por el motivo correcto.
- Ejecutar `./verify.ps1` antes de cada commit (exige Docker corriendo: Testcontainers). Prohibido `--no-verify`. Nunca relajar un gate.
- Commits en `develop`, directos, conventional commits en español.
- Los pasos de borrado/renombrado se marcan como «rojo» por la rotura de compilación o de runtime que causan en el código y tests restantes, y se dejan verdes ajustando esos consumidores en el mismo task.

---

### Task 1: Identity — tres roles (eliminar `SoporteTecnico`)

**Files:**
- Modify: `Icarus/src/Identity/Icarus.Identity.Domain/Rol.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Domain/ReglasRol.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/SemillaIdentidad.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Identity/ReglasRolTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/IdentityEndpointsTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `Rol` con 3 miembros; `ReglasRol.RequiereCliente` intacto (aplica a `Cliente` y `Trabajador`); semilla sin cuenta `SoporteTecnico`. Lo consumen todos los tasks siguientes.

- [x] **Step 1: Quitar `SoporteTecnico` del modelo y de la semilla (rojo)**

En `Rol.cs` dejar `Administrador = 0`, `Cliente = 1`, `Trabajador = 2` (los valores no se persisten: el rol se guarda como texto). En `ReglasRol.cs` actualizar el comentario (solo `Cliente` y `Trabajador` son de empresa). En `SemillaIdentidad.cs` quitar la constante `EmailSoporte` y la línea `CrearSiNoExiste(usuarios, EmailSoporte, Rol.SoporteTecnico, …)`.

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: FALLA de compilación en `ReglasRolTests.cs:18` (`Rol.SoporteTecnico`) y `SemillaIdentidad.cs:25`.

- [x] **Step 2: Actualizar los tests (verde)**

- `ReglasRolTests.cs`: quitar `[InlineData(Rol.SoporteTecnico)]`.
- `IdentityEndpointsTests.cs`:
  - Los dos tests de rotación de refresh (líneas ~166-226) usan `SemillaIdentidad.EmailSoporte`: reemplazar por `SemillaIdentidad.EmailAdmin`.
  - Los tres tests de `POST /identidad/usuarios` siguen vigentes hasta el Task 6 (el endpoint se elimina ahí) pero ya no pueden nombrar el rol `SoporteTecnico` (el validador lo rechazaría con 400):
    - `CrearUsuarioSinTokenDevuelve401`: rol `"SoporteTecnico"` → `"Trabajador"` (el cuerpo es irrelevante, da 401 sin token).
    - `CrearUsuarioConRolClienteDevuelve403`: rol → `"Trabajador"` (el 403 lo produce la autorización antes de la validación).
    - `CrearUsuarioComoAdminPermiteLoginDeLaNuevaCuenta`: rol → `"Cliente"` y agregar `clienteId = SemillaIdentidad.ClienteDemoId` (el validador exige `ClienteId` para los roles de empresa).

Run:
```bash
dotnet test Icarus/tests/Icarus.UnitTests --nologo
dotnet test Icarus/tests/Icarus.IntegrationTests --nologo
```
Expected: PASS (requiere Docker para los de integración).

- [x] **Step 3: Commit**

```bash
git add Icarus/src/Identity Icarus/tests
./verify.ps1
git commit -m "feat(identity): reduce los roles a Administrador, Cliente y Trabajador"
```

---

### Task 2: Clientes.Domain — `Funcionalidades`, su módulo y la asignación al trabajador

**Files:**
- Create: `Icarus/src/Clientes/Icarus.Clientes.Domain/Funcionalidades.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesModulos.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Domain/Trabajador.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Persistencia/ConfiguracionTrabajador.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/SemillaClientes.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Migrations/` (generada por `dotnet ef`)
- Test: `Icarus/tests/Icarus.UnitTests/Clientes/FuncionalidadesTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Clientes/TrabajadorTests.cs`

**Interfaces:**
- Consumes: `Trabajador`, `Modulos` (plan 3); `AggregateRoot`.
- Produces (los usan los tasks 3 y 4):

```csharp
namespace Icarus.Clientes.Domain;

[Flags]
public enum Funcionalidades
{
    Ninguno = 0,
    Granjas = 1,
    Galpones = 2,
    ProduccionHuevos = 4,
    Mortalidad = 8,
    Vacunacion = 16,
    Alimentacion = 32,
    Despachos = 64,
    Precios = 128,
}

// Relación declarativa módulo -> funcionalidades (spec). Todas las
// funcionalidades de GestionAvicola; ControlAcceso queda previsto, sin ninguna.
public static class FuncionalidadesModulos
{
    public static Modulos ModuloDe(Funcionalidades funcionalidad);
    public static Funcionalidades FuncionalidadesDelModulo(Modulos modulo);
}
```

`Trabajador` gana:
```csharp
public Funcionalidades Funcionalidades { get; private set; }   // Ninguno por defecto
public void DefinirFuncionalidades(Funcionalidades funcionalidades);  // reemplaza el conjunto
```
El cese o la desactivación NO tocan las funcionalidades (trazabilidad, spec). La regla «solo funcionalidades de módulos habilitados para el cliente» es transversal (necesita el cliente) y se valida en el handler (task 3), no en el agregado.

- [x] **Step 1: Escribir los tests en rojo**

`FuncionalidadesTests.cs`:
- `TodasLasFuncionalidadesPertenecenAGestionAvicola`: para cada miembro != `Ninguno`, `FuncionalidadesModulos.ModuloDe(f)` == `Modulos.GestionAvicola`.
- `ControlAccesoNoTieneFuncionalidades`: `FuncionalidadesModulos.FuncionalidadesDelModulo(Modulos.ControlAcceso)` == `Funcionalidades.Ninguno`.
- `NingunoNoPerteneceANingunModulo`: `ModuloDe(Ninguno)` == `Modulos.Ninguno`.
- `LosValoresNumericosSonEstables`: los 8 valores son potencias de dos distintas (se persisten como entero).

`TrabajadorTests.cs` (agregar):
- `TrabajadorNuevoEmpiezaSinFuncionalidades`: `Funcionalidades == Funcionalidades.Ninguno`.
- `DefinirFuncionalidadesReemplazaElConjunto`: asignar `Granjas`, luego `Granjas | Precios`, y verificar el último valor y que `Tiene…` se puede derivar con `HasFlag`.
- `CeseNoLiberaLasFuncionalidades`: asignar `Granjas`, cesar, y verificar que `Funcionalidades` conserva `Granjas`.
- `DesactivarNoLiberaLasFuncionalidades`: asignar `Vacunacion`, desactivar, y verificar que conserva `Vacunacion`.

- [x] **Step 2: Correr y verificar rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: FALLA de compilación (`Funcionalidades`/`FuncionalidadesModulos` no existen; `Trabajador.Funcionalidades` no existe).

- [x] **Step 3: Implementación**

`Funcionalidades.cs` con `#pragma warning disable S2346` (el miembro cero se nombra en español, mismo criterio que `Modulos`). `FuncionalidadesModulos.cs` con un `switch` que mapea las 8 a `Modulos.GestionAvicola` (`Ninguno` → `Modulos.Ninguno`) y el inverso por agrupación. `Trabajador.cs`: inicializar `Funcionalidades = Funcionalidades.Ninguno` en el ctor y agregar `DefinirFuncionalidades`.

- [x] **Step 4: Persistencia y migración**

`ConfiguracionTrabajador.cs`:
```csharp
builder.Property(t => t.Funcionalidades).HasConversion<int>().HasDefaultValue(0);
```
`SemillaClientes.cs`: al trabajador demo asignarle `trabajador.DefinirFuncionalidades(Funcionalidades.Granjas)` (lo necesitan los tests de entitlement del task 4: el trabajador demo debe tener al menos una funcionalidad asignada).

Generar la migración:
```bash
cd Icarus
dotnet tool restore
dotnet ef migrations add EntitlementFuncionalidades --project src/Clientes/Icarus.Clientes.Infrastructure --startup-project src/Clientes/Icarus.Clientes.Infrastructure
dotnet build Icarus.sln --nologo
```
Expected: migración que agrega la columna `Funcionalidades int NOT NULL DEFAULT 0` a `clientes.trabajadores`. Verificar leyendo la migración que solo agrega esa columna (el filtro global sigue sin ser parte del esquema).

- [x] **Step 5: Correr y verificar verde**

Run:
```bash
dotnet test Icarus/tests/Icarus.UnitTests --nologo
dotnet test Icarus/tests/Icarus.IntegrationTests --nologo
```
Expected: PASS (los de integración siguen verdes: la columna nueva no rompe nada y el trabajador demo queda sembrado con `Granjas`).

- [x] **Step 6: Commit**

```bash
git add Icarus/src/Clientes/Icarus.Clientes.Domain Icarus/src/Clientes/Icarus.Clientes.Infrastructure Icarus/tests
./verify.ps1
git commit -m "feat: funcionalidades de negocio, su módulo y la asignación al trabajador (dominio)"
```

---

### Task 3: Clientes.Application — altas con `email`/`contrasena` y asignación de funcionalidades

**Files:**
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Application/Clientes/CrearClienteCommand.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Application/Clientes/CrearClienteValidator.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/CrearTrabajadorCommand.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/CrearTrabajadorValidator.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/DefinirFuncionalidadesTrabajadorCommand.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/DefinirFuncionalidadesTrabajadorHandler.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/DefinirFuncionalidadesTrabajadorValidator.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/IRepositorioTrabajadores.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Repositorios/RepositorioTrabajadores.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs` (solo el `CrearTrabajadorRequest` y la construcción del comando; la orquestación llega en el task 5)
- Test: `Icarus/tests/Icarus.UnitTests/Clientes/CrearClienteHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Clientes/CrearTrabajadorHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Clientes/DefinirFuncionalidadesTrabajadorHandlerTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/ClientesEndpointsTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/TrabajadoresEndpointsTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/EntitlementTests.cs`

**Interfaces:**
- Consumes: `Funcionalidades`, `FuncionalidadesModulos`, `Cliente`, `Trabajador` (task 2); `IRepositorioClientes`, `IRepositorioTrabajadores`, `IUnitOfWork`, `NotFoundException`, `ConflictException`, MediatR, FluentValidation.
- Produces:

```csharp
namespace Icarus.Clientes.Application.Clientes;

// El alta embebida (spec): el Host recibe email y contrasena y crea la cuenta;
// el handler de Clientes solo crea el cliente.
public sealed record CrearClienteCommand(
    string RazonSocial, string IdentificadorFiscal, string Email, string Contrasena) : IRequest<Guid>;

namespace Icarus.Clientes.Application.Trabajadores;

public sealed record CrearTrabajadorCommand(
    Guid ClienteId, string Nombre, string DocumentoIdentidad, string Cargo,
    DateOnly FechaIngreso, string Email, string Contrasena) : IRequest<Guid>;

public sealed record DefinirFuncionalidadesTrabajadorCommand(
    Guid ClienteId, Guid TrabajadorId, IReadOnlyList<string> Funcionalidades) : IRequest;

public sealed record TrabajadorResumen(
    Guid Id, string Nombre, string DocumentoIdentidad, string Cargo,
    DateOnly FechaIngreso, DateOnly? FechaCese, IReadOnlyList<string> Funcionalidades);
```

Los handlers `CrearClienteHandler` y `CrearTrabajadorHandler` NO cambian su lógica (crean la entidad y guardan); solo compilan con los campos nuevos. La unicidad de email la resuelve Identity en la orquestación del Host (task 5), no el handler de Clientes.

- [x] **Step 1: Escribir los tests en rojo**

`CrearClienteHandlerTests.cs` y `CrearTrabajadorHandlerTests.cs`: adaptar las llamadas a los comandos con `email` y `contrasena` ficticios. `DefinirFuncionalidadesTrabajadorHandlerTests.cs` (nuevo):
- `FuncionalidadesDeModuloHabilitadoSeAsignan`: cliente con `Modulos.GestionAvicola`, trabajador válido; `["Granjas", "precios"]` → `trabajador.Funcionalidades.HasFlag(Granjas)` y `HasFlag(Precios)`; `SaveChanges` recibido.
- `FuncionalidadDeModuloNoHabilitadoLanzaReglaDeNegocio`: cliente sin el módulo → `ReglaNegocioException`, sin `SaveChanges`.
- `ClienteInexistenteLanzaNotFound` y `TrabajadorInexistenteLanzaNotFound`.

- [x] **Step 2: Correr y verificar rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: FALLA de compilación (comandos con campos nuevos; tipos nuevos no existen).

- [x] **Step 3: Implementación — comandos, validadores y handler de funcionalidades**

`CrearClienteValidator` y `CrearTrabajadorValidator`: agregar `Email` (`NotEmpty().EmailAddress()`) y `Contrasena` (`NotEmpty().MinimumLength(12)`), mismos criterios que el validador de usuarios que se elimina en el task 6.

`DefinirFuncionalidadesTrabajadorHandler`: obtiene el cliente con `ObtenerPorIdAsync` (filtro de tenant: un `ClienteId` ajeno da el mismo 404 que uno inexistente, anti-enumeración), obtiene el trabajador con `ObtenerPorIdAsync`, agrega flags solo si `cliente.TieneModulo(FuncionalidadesModulos.ModuloDe(funcionalidad))` para cada una (si no, `ReglaNegocioException` con mensaje genérico «Funcionalidad no disponible para este cliente.»), y guarda. `DefinirFuncionalidadesTrabajadorValidator`: `ClienteId`/`TrabajadorId` NotEmpty, `Funcionalidades` NotNull, y `RuleForEach` que cada nombre parsea como `Funcionalidades` != `Ninguno`.

`IRepositorioTrabajadores`/`TrabajadorResumen`: agregar la lista de funcionalidades al resumen. `RepositorioTrabajadores.ListarPorClienteAsync`: proyectar `t.Funcionalidades` con el mismo helper de `ListarTodosAsync` de Clientes (cadena separada por comas → lista).

`ClientesEndpoints.cs` (solo cableado, sin orquestación): `CrearTrabajadorRequest` gana `Email` y `Contrasena`, y la construcción del `CrearTrabajadorCommand` los pasa. `POST /clientes` enlaza `CrearClienteCommand` directo, así que el cuerpo ya admite los campos nuevos.

- [x] **Step 4: Actualizar los cuerpos de los tests de integración**

Hasta el task 5 el endpoint ignora `email`/`contrasena` (aún no hay orquestación), pero la validación los exige: hay que enviarlos para que el gate quede verde.
- `ClientesEndpointsTests.CrearClienteComoAdmin` y `CrearClienteSinTokenDevuelve401`/`CrearClienteConRolClienteDevuelve403`: agregar `email` y `contrasena` ficticios al cuerpo.
- `TrabajadoresEndpointsTests.CuerpoTrabajador`: agregar `email` y `contrasena` ficticios.
- `EntitlementTests.CrearClienteConCuenta`: agregar `email` y `contrasena` al alta del cliente (el alta de la cuenta vía `POST /identidad/usuarios` sigue igual hasta el task 5).

- [x] **Step 5: Correr y verificar verde**

Run:
```bash
dotnet test Icarus/tests/Icarus.UnitTests --nologo
dotnet test Icarus/tests/Icarus.IntegrationTests --nologo
```
Expected: PASS (unitarios con los handlers nuevos; integración con los cuerpos actualizados).

- [x] **Step 6: Commit**

```bash
git add Icarus/src/Clientes/Icarus.Clientes.Application Icarus/src/Clientes/Icarus.Clientes.Infrastructure Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs Icarus/tests
./verify.ps1
git commit -m "feat: altas con email y contrasena y asignacion de funcionalidades (aplicacion)"
```

---

### Task 4: Claim `trabajadorId` en el token (Identity, BuildingBlocks, Host)

**Files:**
- Modify: `Icarus/src/Identity/Icarus.Identity.Domain/ClaimsIdentidad.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Application/Sesiones/IVerificadorCredenciales.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Application/Sesiones/IConsultaUsuarios.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Application/Sesiones/IniciarSesionHandler.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Application/Sesiones/RenovarSesionHandler.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/VerificadorCredenciales.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/EmisorAccessTokens.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/Usuarios/ConsultaUsuarios.cs`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/ICurrentUser.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Servicios/CurrentUserService.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Identity/EmisorAccessTokensTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Identity/IniciarSesionHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Identity/RenovarSesionHandlerTests.cs`
- Test: `Icarus/tests/Icarus.UnitTests/Host/CurrentUserServiceTests.cs`

**Interfaces:**
- Consumes: `Usuario.TrabajadorId` (plan 2), claims de Identity.
- Produces (los consume el entitlement del task 5):

```csharp
namespace Icarus.Identity.Domain;
public static class ClaimsIdentidad
{
    public const string Subject = "sub";
    public const string Rol = "rol";
    public const string ClienteId = "clienteId";
    public const string TrabajadorId = "trabajadorId";
}

namespace Icarus.Identity.Application.Sesiones;
public sealed record CredencialValida(Guid UsuarioId, string Rol, Guid? ClienteId, Guid? TrabajadorId);
public sealed record UsuarioResumen(Guid Id, string Email, string Rol, Guid? ClienteId, Guid? TrabajadorId);

namespace Icarus.BuildingBlocks.Application;
public interface ICurrentUser
{
    bool EstaAutenticado { get; }
    Guid? UsuarioId { get; }
    string? Rol { get; }
    Guid? ClienteId { get; }
    Guid? TrabajadorId { get; }
}
```

`EmisorAccessTokens.Emitir(Guid usuarioId, string rol, Guid? clienteId, Guid? trabajadorId, out int expiraEnSegundos)` escribe el claim `trabajadorId` solo cuando no es nulo (mismo patrón que `clienteId`). `IniciarSesionHandler` y `RenovarSesionHandler` pasan `credencial.TrabajadorId` / `usuario.TrabajadorId`. `VerificadorCredenciales` y `ConsultaUsuarios` incluyen `TrabajadorId` en sus proyecciones. `CurrentUserService` lee `ClaimsIdentidad.TrabajadorId`.

- [x] **Step 1: Escribir los tests en rojo**

`EmisorAccessTokensTests.cs`:
- `TokenConTrabajadorIncluyeElClaimTrabajadorId`: `Emitir(usuarioId, "Trabajador", clienteId, trabajadorId, out _)` incluye `trabajadorId`.
- `TokenSinTrabajadorOmiteElClaimTrabajadorId`: `Emitir(usuarioId, "Cliente", clienteId, null, out _)` no lo incluye.

`CurrentUserServiceTests.cs`:
- `UsuarioAutenticadoExponeTrabajadorId`: con el claim presente, `servicio.TrabajadorId` lo devuelve.
- `SinTrabajadorIdDevuelveNull`: sin el claim, `TrabajadorId` es null.

`IniciarSesionHandlerTests` y `RenovarSesionHandlerTests`: adaptar las firmas (`CredencialValida`/`UsuarioResumen` con el parámetro nuevo y los `.Returns(...)` del emisor con el argumento nuevo).

- [x] **Step 2: Correr y verificar rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: FALLA de compilación (firmas nuevas no existen aún).

- [x] **Step 3: Implementación**

Aplicar los cambios de la sección **Interfaces** en los 10 archivos de producción. El claim viaja en el access token y en el `/me` se expone desde `ICurrentUser` (el endpoint `/me` se toca en el task 6).

- [x] **Step 4: Correr y verificar verde**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: PASS (los de integración no se tocan en este task: la emisión extra de un claim no rompe los endpoints existentes).

- [x] **Step 5: Commit**

```bash
git add Icarus/src/Identity Icarus/src/BuildingBlocks Icarus/src/Host/Icarus.Host/Servicios/CurrentUserService.cs Icarus/tests
./verify.ps1
git commit -m "feat(identity): claim trabajadorId en el access token para el entitlement operativo"
```

---

### Task 5: Clientes — entitlement por funcionalidad según rol, sondeo y tests

**Files:**
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Application/Autorizacion/IVerificadorEntitlement.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/VerificadorEntitlement.cs`
- Delete: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/RequisitoModuloHabilitado.cs`
- Create: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/RequisitoFuncionalidadHabilitada.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/PoliticasClientes.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/DependencyInjection.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs` (solo el sondeo)
- Test: `Icarus/tests/Icarus.IntegrationTests/EntitlementTests.cs`

**Interfaces:**
- Consumes: `Funcionalidades`, `FuncionalidadesModulos`, `Modulos` (task 2); `ICurrentUser.TrabajadorId` (task 4).
- Produces:

```csharp
namespace Icarus.Clientes.Application.Autorizacion;

// trabajadorId null => semántica de rol Cliente (todas las funcionalidades de
// los módulos de su cliente); trabajadorId presente => solo sus funcionalidades
// asignadas (rol Trabajador). Clientes no conoce los nombres de rol de Identity.
public interface IVerificadorEntitlement
{
    Task<bool> TieneFuncionalidadAsync(
        Guid clienteId, Guid? trabajadorId, Funcionalidades funcionalidad,
        CancellationToken cancellationToken = default);
}

namespace Icarus.Clientes.Infrastructure.Autorizacion;

public sealed class RequisitoFuncionalidadHabilitada : IAuthorizationRequirement
{
    public RequisitoFuncionalidadHabilitada(Funcionalidades funcionalidad);
    public Funcionalidades Funcionalidad { get; }
}

public static class PoliticasClientes
{
    public const string Prefijo = "Funcionalidad:";
    public static string Para(Funcionalidades funcionalidad) => Prefijo + funcionalidad.ToString();
}
```

`VerificadorEntitlement.TieneFuncionalidadAsync`:
- si `trabajadorId` presente: lee `Trabajadores.IgnoreQueryFilters()` donde `Id == trabajadorId && ClienteId == clienteId && EstaActivo`, y responde `asignadas.HasFlag(funcionalidad)`;
- si nulo: lee `Clientes.IgnoreQueryFilters()` donde `Id == clienteId && EstaActivo` y responde `modulos.HasFlag(FuncionalidadesModulos.ModuloDe(funcionalidad))`.

`RequisitoFuncionalidadHabilitada.cs` reemplaza a `RequisitoModuloHabilitado.cs` (mismo patrón, con `_usuario.TrabajadorId` en vez de rol): si el usuario no está autenticado o no lleva `ClienteId`, no pasa; si `TieneFuncionalidadAsync(clienteId, _usuario.TrabajadorId, requisito.Funcionalidad, …)` es true, `context.Succeed`.

`DependencyInjection.cs` registra `IAuthorizationHandler, ManejadorFuncionalidadHabilitada` y una política por funcionalidad en un bucle sobre `Enum.GetValues<Funcionalidades>()` != `Ninguno`.

`ClientesEndpoints.MapSondeoEntitlement` reemplaza los dos sondeos por módulo por:
- `GET /clientes/sondeo/funcionalidad/granjas` → `PoliticasClientes.Para(Funcionalidades.Granjas)`
- `GET /clientes/sondeo/funcionalidad/vacunacion` → `PoliticasClientes.Para(Funcionalidades.Vacunacion)`

- [x] **Step 1: Escribir los tests en rojo**

Reescribir `EntitlementTests.cs` contra las rutas y semántica nuevas:
- `ClienteConModuloHabilitadoRecibe200`: `EmailCliente` (cliente demo con `GestionAvicola`) → `granjas` 200 y `vacunacion` 200 (el rol Cliente tiene todas las funcionalidades de sus módulos).
- `TrabajadorConFuncionalidadAsignadaRecibe200`: `EmailTrabajador` (trabajador demo sembrado con `Granjas` en el task 2) → `granjas` 200.
- `TrabajadorSinFuncionalidadAsignadaDevuelve403`: `EmailTrabajador` → `vacunacion` 403.
- `TrabajadorNuevoSinFuncionalidadesDevuelve403`: crear trabajador nuevo (sin funcionalidades) y su cuenta rol `Trabajador` vía `POST /identidad/usuarios` → `granjas` 403.
- `ClienteSinModulosRecibe403`: `CrearClienteConCuenta(modulos: null)` → `granjas` 403.
- `ClienteSuspendidoPierdeElEntitlement`: suspender → 403.
- `RolAdministradorNoPasaElEntitlement`: `EmailAdmin` → 403.
- `SinTokenDevuelve401`.

`CrearClienteConCuenta` mantiene su forma actual (alta de cliente con `email`/`contrasena` + asignación de módulos + alta de cuenta vía `POST /identidad/usuarios`); el task 6 la cambia al alta embebida.

- [x] **Step 2: Correr y verificar rojo**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --nologo`
Expected: FALLA (las rutas `/clientes/sondeo/funcionalidad/*` no existen todavía → 404/403 distinto al esperado; el mecanismo por módulo ya no aplica).

- [x] **Step 3: Implementación**

Aplicar los contratos de la sección **Interfaces**: nuevo verificador, requisito/manejador por funcionalidad (borrar `RequisitoModuloHabilitado.cs`), políticas por funcionalidad y su registro en DI, y el sondeo nuevo en `ClientesEndpoints.cs`.

- [x] **Step 4: Correr y verificar verde**

Run:
```bash
dotnet test Icarus/tests/Icarus.UnitTests --nologo
dotnet test Icarus/tests/Icarus.IntegrationTests --nologo
```
Expected: PASS (el entitlement por rol queda probado de punta a punta contra SQL Server).

- [x] **Step 5: Commit**

```bash
git add Icarus/src/Clientes Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs Icarus/tests/Icarus.IntegrationTests/EntitlementTests.cs
./verify.ps1
git commit -m "feat: entitlement por funcionalidad segun rol (Cliente completo, Trabajador asignado)"
```

---

### Task 6: Host — orquestación del alta embebida, política de trabajadores y endpoint de funcionalidades

**Files:**
- Create: `Icarus/src/Host/Icarus.Host/Servicios/AltaCuentasServicio.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs` (política `GestionTrabajadores` solo `Cliente`)
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs` (registrar `AltaCuentasServicio`)
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/ClientesEndpoints.cs` (alta embebida + `PUT .../funcionalidades`)
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/IdentidadEndpoints.cs` (`/me` con `trabajadorId`)
- Test: `Icarus/tests/Icarus.UnitTests/Host/AltaCuentasServicioTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/ClientesEndpointsTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/TrabajadoresEndpointsTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/EntitlementTests.cs`

**Interfaces:**
- Consumes: comandos del task 3 (`CrearClienteCommand`, `CrearTrabajadorCommand`, `DefinirFuncionalidadesTrabajadorCommand`, `SuspenderClienteCommand`, `DesactivarTrabajadorCommand`); `IRegistradorUsuarios` (Identity, aún en `Usuarios/` hasta el task 7); `nameof(Rol.Cliente)`/`nameof(Rol.Trabajador)` (el Host referencia Identity).
- Produces:

```csharp
namespace Icarus.Host.Servicios;

// Orquestación de la cuenta embebida (spec). Clientes no referencia Identity:
// este servicio es el único punto que une ambos módulos. Si la cuenta no se
// puede registrar, la entidad recién creada se deja no operativa (soft delete,
// glosario) y se devuelve un conflicto genérico (anti-PII).
public sealed class AltaCuentasServicio
{
    public async Task<Guid> CrearClienteConCuentaAsync(CrearClienteCommand comando, CancellationToken cancellationToken);
    public async Task<Guid> CrearTrabajadorConCuentaAsync(CrearTrabajadorCommand comando, CancellationToken cancellationToken);
}
```

`CrearClienteConCuentaAsync`: `clienteId = mediator.Send(comando)`; `cuentaId = registrador.RegistrarAsync(comando.Email, comando.Contrasena, nameof(Rol.Cliente), clienteId, null, ct)`; si `cuentaId` es null → `mediator.Send(new SuspenderClienteCommand(clienteId))` y `ConflictException("No se pudo registrar el cliente.")`; si no, devuelve `clienteId`.

`CrearTrabajadorConCuentaAsync`: `trabajadorId = mediator.Send(comando)`; `registrador.RegistrarAsync(comando.Email, comando.Contrasena, nameof(Rol.Trabajador), comando.ClienteId, trabajadorId, ct)`; si null → `mediator.Send(new DesactivarTrabajadorCommand(trabajadorId))` y `ConflictException("No se pudo registrar el trabajador.")`.

Nota de diseño registrada: la atómica entre contextos no es transaccional a nivel BD (no hay MSDTC en contenedores). La orden sigue el spec («1. crear la entidad, 2. registrar la cuenta») y la compensación por soft delete deja la entidad no operativa (invisible para las consultas normales); el residual es un identificador (RIF/documento) reservado tras una cuenta fallida, aceptado para un sistema cerrado y documentado aquí.

- [x] **Step 1: Escribir los tests en rojo**

`AltaCuentasServicioTests.cs` (NSubstitute sobre `ISender` y `IRegistradorUsuarios`):
- `ClienteCreadoRegistraCuentaRolClienteYDevuelveId`: el mediador devuelve un `clienteId`; el registrador devuelve un `Guid`; verificar `RegistrarAsync(email, contrasena, "Cliente", clienteId, null, …)` y el id devuelto, sin compensación.
- `CuentaDeClienteFallidaSuspendeElClienteYDevuelveConflict`: el registrador devuelve null; verificar `ConflictException`, `SuspenderClienteCommand(clienteId)` recibido y que no devuelve el id.
- `TrabajadorCreadoRegistraCuentaRolTrabajadorYDevuelveId`: verificar `RegistrarAsync(…, "Trabajador", clienteId, trabajadorId, …)`.
- `CuentaDeTrabajadorFallidaDesactivaElTrabajadorYDevuelveConflict`: verificar `DesactivarTrabajadorCommand(trabajadorId)` y `ConflictException`.

- [x] **Step 2: Correr y verificar rojo**

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: FALLA de compilación (`AltaCuentasServicio` no existe).

- [x] **Step 3: Implementación — servicio, política y endpoints**

- `AltaCuentasServicio.cs` según el contrato.
- `Identity/DependencyInjection.cs`: `GestionTrabajadores` pasa a `RequireClaim(ClaimsIdentidad.Rol, nameof(Rol.Cliente))` (solo Cliente; el Administrador queda fuera de trabajadores, spec).
- `Program.cs`: `builder.Services.AddScoped<AltaCuentasServicio>();`.
- `ClientesEndpoints.cs`:
  - `POST /clientes` → `await altaCuentas.CrearClienteConCuentaAsync(command, http.RequestAborted)` (sigue `SoloAdministrador`).
  - `POST /clientes/{clienteId}/trabajadores` → `await altaCuentas.CrearTrabajadorConCuentaAsync(new CrearTrabajadorCommand(clienteId, cuerpo.Nombre, cuerpo.DocumentoIdentidad, cuerpo.Cargo, cuerpo.FechaIngreso, cuerpo.Email, cuerpo.Contrasena), http.RequestAborted)` (política `GestionTrabajadores`, ahora solo Cliente).
  - Nuevo `PUT /clientes/{clienteId:guid}/trabajadores/{trabajadorId:guid}/funcionalidades` con cuerpo `{ funcionalidades }` → `DefinirFuncionalidadesTrabajadorCommand` → `Results.NoContent()`, política `GestionTrabajadores`.
  - El resto de endpoints de trabajadores (`cese`, `DELETE`) queda con `GestionTrabajadores` (solo Cliente).
- `IdentidadEndpoints.cs`: `/me` devuelve también `actual.TrabajadorId`.

- [x] **Step 4: Verificar build y unitarios**

Run:
```bash
dotnet build Icarus/Icarus.sln --nologo
dotnet test Icarus/tests/Icarus.UnitTests --nologo
```
Expected: build con 0 warnings; unitarios en verde (incluidos los 4 de `AltaCuentasServicio`).

- [x] **Step 5: Actualizar los tests de integración al nuevo flujo**

- `ClientesEndpointsTests`:
  - `CrearClienteComoAdmin` ahora crea también la cuenta rol `Cliente`: se puede verificar `POST /identidad/sesion` con el `email` del alta → 200.
  - Nuevo: `CrearClienteConEmailYaEnUsoDevuelve409SinClienteActivo`: usar el email de la cuenta semilla `cliente@icarus.test` → 409, y verificar en `GET /clientes` que el cliente recién intentado NO aparece activo (quedó suspendido por la compensación).
  - El resto de casos (401/403/409 por RIF, suspender/reactivar, módulos) se ajusta a los cuerpos con `email`/`contrasena`.
- `TrabajadoresEndpointsTests` (la gestión de trabajadores pasa a ser solo `Cliente`):
  - Los casos que usaban `EmailAdmin` para crear/cesar/desactivar trabajadores pasan a `EmailCliente` sobre `ClienteDemoId` (`CesarConFechaFuturaDevuelve400`, `DesactivarTrabajadorLoQuitaDeLaListaSinBorrarlo`, `DocumentoDuplicadoEnElMismoClienteDevuelve409SinDetalle`).
  - Nuevo: `AdministradorYaNoGestionaTrabajadoresDevuelve403`: `EmailAdmin` sobre `POST /clientes/{clienteId}/trabajadores` → 403.
  - `MismoDocumentoEnOtroClienteSePermite` ya no puede usar `EmailAdmin`: crear un segundo cliente con su cuenta embebida, iniciar sesión como ese `Cliente` y crear el trabajador con el documento repetido → 201.
  - `CrearTrabajadorComoClienteEnSuEmpresaDevuelve201`: verificar además que la cuenta del trabajador permite el login con su `email`.
- `EntitlementTests`:
  - `CrearClienteConCuenta` deja de usar `POST /identidad/usuarios`: el alta de `POST /clientes` crea la cuenta. Los escenarios de trabajador nuevo usan el `POST /clientes/{clienteId}/trabajadores` embebido y `PUT .../funcionalidades` para asignar.

- [x] **Step 6: Correr la suite de integración y la puerta**

Run: `dotnet test Icarus/tests/Icarus.IntegrationTests --nologo` y luego `./verify.ps1`.
Expected: PASS (todo el flujo de alta embebida, tenant y entitlement queda probado de punta a punta).

- [x] **Step 7: Commit**

```bash
git add Icarus/src/Host Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs Icarus/tests
./verify.ps1
git commit -m "feat: alta embebida de cuentas y gestion de funcionalidades (solo rol Cliente)"
```

---

### Task 7: Identity — eliminar el CRUD de usuarios y el paquete `Usuarios/`

**Files:**
- Delete: `Icarus/src/Identity/Icarus.Identity.Application/Usuarios/CrearUsuarioCommand.cs`
- Delete: `Icarus/src/Identity/Icarus.Identity.Application/Usuarios/CrearUsuarioHandler.cs`
- Delete: `Icarus/src/Identity/Icarus.Identity.Application/Usuarios/CrearUsuarioValidator.cs`
- Move: `Icarus/src/Identity/Icarus.Identity.Application/Usuarios/IRegistradorUsuarios.cs` → `Icarus/src/Identity/Icarus.Identity.Application/RegistroCuentas/IRegistradorUsuarios.cs` (namespace `Icarus.Identity.Application.RegistroCuentas`)
- Move: `Icarus/src/Identity/Icarus.Identity.Infrastructure/Usuarios/RegistradorUsuarios.cs` → `Icarus/src/Identity/Icarus.Identity.Infrastructure/RegistroCuentas/RegistradorUsuarios.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs` (namespace del registrador)
- Modify: `Icarus/src/Host/Icarus.Host/Endpoints/IdentidadEndpoints.cs` (quitar `POST /usuarios` y el `using` de `Icarus.Identity.Application.Usuarios`)
- Modify: `Icarus/src/Host/Icarus.Host/Servicios/AltaCuentasServicio.cs` (using del namespace nuevo de `IRegistradorUsuarios`)
- Delete: `Icarus/tests/Icarus.UnitTests/Identity/CrearUsuarioHandlerTests.cs`
- Test: `Icarus/tests/Icarus.IntegrationTests/IdentityEndpointsTests.cs` (quitar los 3 tests de `POST /identidad/usuarios`)

**Interfaces:**
- Consumes: el alta embebida del task 6 ya no usa `POST /identidad/usuarios`.
- Produces: `IRegistradorUsuarios`/`RegistradorUsuarios` reubicados como servicio de registro de cuentas (no CRUD), consumidos únicamente por `AltaCuentasServicio` (Host). `ConsultaUsuarios`/`IConsultaUsuarios` (sesiones) NO se tocan: quedan en `Infrastructure/Usuarios/` porque son consulta de sesión, no CRUD de cuentas.

- [x] **Step 1: Quitar el endpoint y el paquete (rojo)**

Eliminar los tres archivos de `Application/Usuarios` y mover la interfaz y su implementación a `RegistroCuentas/`. Quitar `POST /usuarios` de `IdentidadEndpoints.cs`. Borrar `CrearUsuarioHandlerTests.cs`.

Run: `dotnet test Icarus/tests/Icarus.UnitTests --nologo`
Expected: FALLA de compilación (`CrearUsuarioCommand`/`CrearUsuarioHandler` referenciados desde el endpoint y el DI; `IRegistradorUsuarios` en su namespace viejo).

- [x] **Step 2: Ajustar los consumidores (verde)**

- `Identity/DependencyInjection.cs`: actualizar el `using`/registro a `Icarus.Identity.Infrastructure.RegistroCuentas`.
- `AltaCuentasServicio.cs`: `using Icarus.Identity.Application.RegistroCuentas;`.
- `IdentityEndpointsTests.cs`: quitar `CrearUsuarioSinTokenDevuelve401`, `CrearUsuarioConRolClienteDevuelve403` y `CrearUsuarioComoAdminPermiteLoginDeLaNuevaCuenta` (el endpoint ya no existe).

- [x] **Step 3: Correr y verificar verde**

Run:
```bash
dotnet build Icarus/Icarus.sln --nologo
dotnet test Icarus/tests/Icarus.UnitTests --nologo
dotnet test Icarus/tests/Icarus.IntegrationTests --nologo
```
Expected: build con 0 warnings; suites en verde (ningún test usa ya `POST /identidad/usuarios`).

- [x] **Step 4: Puerta completa y commit**

Run: `./verify.ps1` (todos los gates).
Expected: verde. Si el gate de mojibake o enlaces falla, corregir el contenido, no el gate.

```bash
git add Icarus/src/Identity Icarus/src/Host Icarus/tests
git commit -m "refactor(identity): elimina el CRUD de usuarios (la cuenta nace con el alta embebida)"
```

---

## Registro de cierre

- **Estado**: plan completo. Las 7 tareas quedaron implementadas y commiteadas en `develop` (352841f, 1ca03ff, 144e878, 5e6afd1, ad87123, a98c42e, 2a40a0a). El spec se commitó al arrancar junto con el plan (08be897, sesión de planificación).
- **Verificación**: `./verify.ps1` corrido antes de cada commit, siempre verde (80 unitarios, 4 de arquitectura, 40 de integración con Testcontainers, frontend completo).
- **Notas de implementación**: `IEmisorAccessTokens` y `UsuarioActualDiseno` (DesignTime factory) se ajustaron en el task 4 como consumidores de la firma nueva; en el task 2 el valor por defecto de `Funcionalidades` se configuró con el enum (`HasDefaultValue(Funcionalidades.Ninguno)`), no con `0` entero. En el task 6, el test de asignación de funcionalidades exige que el cliente tenga `GestionAvicola` habilitado.
- **Sin implementar (anotado, fuera del plan)**: módulos de negocio concretos de GestionAvicola (granjas, galpones, producción, etc.) y el módulo ControlAcceso (previsto, sin funcionalidades). El frontend React (plan 4) sigue sin existir.
