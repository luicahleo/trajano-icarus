# Subproyecto 2 — Arquitectura inicial: backend, frontend PWA, Identity y módulo Clientes — Diseño

Fecha: 2026-08-12
Estado: aprobado en brainstorming (sesión de la misma fecha)

## Contexto

Trajano-Icarus es la refactorización de ICARUS tomando como referencia
estructural a Caserito (`repos/dev_Caserito`). El subproyecto 1 dejó la
gobernanza de agentes y la puerta de calidad mínima. Este subproyecto 2 crea el
andamiaje técnico completo: solución .NET bajo `Icarus/`, frontend PWA bajo
`web/`, building blocks, Identity, observabilidad centralizada y el primer
módulo de negocio (`Clientes`). Referencia estructural, nunca copia de código.

## Decisiones tomadas en el brainstorming

1. **Nombre del módulo: `Clientes`.** Agregados `Cliente` (raíz) y
   `Trabajador`. Se descartaron `Organizacion` (generalidad especulativa) y
   `Directorio` (poco expresivo). El lenguaje ubicuo es «cliente/trabajador».
2. **Identity desacoplada del dominio.** Un usuario de Identity NO es
   necesariamente un trabajador. El vínculo `Usuario ↔ Trabajador` es opcional
   (0..1) y se guarda como `TrabajadorId` (Guid) sin FK ni referencia de
   proyecto entre módulos. Motivo: cuentas de soporte y administración no son
   trabajadores; crear trabajadores ficticios contaminaría el dominio.
3. **Un trabajador pertenece a un único cliente, siempre.** 1:N estricto con
   `Trabajador.ClienteId` obligatorio. Se descartó N:M (contratistas
   compartidos): no es un caso real del negocio hoy.
4. **Multiempresa desde el primer incremento.** El `Administrador` crea
   clientes y les asigna módulos; los clientes entran a usar los módulos
   habilitados. Toda entidad de negocio cuelga de `ClienteId` y las consultas
   filtran por tenant. Retrofit de multi-tenancy a posteriori es prohibitivo.
5. **«Logs» = observabilidad técnica centralizada.** Building block único
   (Serilog + correlation ID + exception middleware); sink consola siempre, Seq
   opcional vía docker-compose. La auditoría de acciones administrativas se
   difiere: será una feature de dominio con diseño propio, no un log. La
   auditoría nominal de accesos de trabajadores está prohibida por la regla
   anti-PII.
6. **Roles: `Administrador`, `SoporteTecnico`, `Cliente`, `Trabajador`.**
   Sistema cerrado: no hay registro público; el `Administrador` da de alta las
   cuentas. Se descartó un rol `Testing` (puerta trasera de elevación de
   privilegios): para probar por rol se usan usuarios semilla en entornos
   dev/test y auth simulada en tests de integración.
7. **Enfoque A: monolito modular por capas (estilo Caserito).** Tres proyectos
   por módulo (`Domain`/`Application`/`Infrastructure`). Las fronteras las
   fuerzan el compilador y los tests de arquitectura. Motivo principal: es la
   estructura que mejor sostiene TDD — `Domain` no referencia nada y los tests
   unitarios corren en milisegundos.
8. **La aplicación es una PWA.** Una sola SPA React (no hay app de
   administración separada): las vistas de gestión de clientes son pantallas
   más, visibles solo para `Administrador`. Manifest + service worker desde el
   andamiaje con `vite-plugin-pwa` (paridad con Caserito).
9. **Stack fijado por paridad con Caserito:** .NET 10 (SDK pineado en
   `global.json`), SQL Server + EF Core 10, MediatR, FluentValidation, ASP.NET
   Core Identity + JWT bearer, Serilog, xUnit + NSubstitute + NetArchTest +
   Testcontainers.MsSql, CPM con `Directory.Packages.props`, analizadores
   Roslynator + SonarAnalyzer globales. Frontend: React 19, Vite 8, TypeScript
   6, Vitest 4, react-router 7, @tanstack/react-query, react-hook-form,
   vite-plugin-pwa.

## Arquitectura

```
Icarus/
├── Icarus.sln
├── Directory.Build.props        # LangVersion, nullable, warnings como errores
├── Directory.Packages.props     # versiones centralizadas (CPM) + analizadores globales
├── global.json                  # pin del SDK (10.0.x)
├── src/
│   ├── BuildingBlocks/
│   │   ├── Icarus.BuildingBlocks.Domain/          # Entity, AggregateRoot, ValueObject, IDomainEvent
│   │   ├── Icarus.BuildingBlocks.Application/     # ICurrentUser, repos, IUnitOfWork, behaviors MediatR
│   │   ├── Icarus.BuildingBlocks.Infrastructure/  # EF base, interceptores, UnitOfWork
│   │   └── Icarus.BuildingBlocks.Observability/   # Serilog, correlation ID, exception middleware
│   ├── Identity/
│   │   ├── Icarus.Identity.Domain/                # Usuario, Rol (enum de los 4 roles)
│   │   ├── Icarus.Identity.Application/           # login, refresh, gestión de usuarios
│   │   └── Icarus.Identity.Infrastructure/        # ASP.NET Identity, JWT, seeds dev/test
│   ├── Clientes/
│   │   ├── Icarus.Clientes.Domain/                # Cliente (módulos habilitados), Trabajador
│   │   ├── Icarus.Clientes.Application/           # casos de uso + asignación de módulos
│   │   └── Icarus.Clientes.Infrastructure/        # EF Core, repositorios, migraciones
│   └── Host/
│       └── Icarus.Host/                           # composición: endpoints, auth, OpenAPI, health
└── tests/
    ├── Icarus.UnitTests/
    ├── Icarus.IntegrationTests/                   # WebApplicationFactory + Testcontainers
    └── Icarus.ArchitectureTests/                  # NetArchTest
```

Reglas de dependencia (compilador + ArchitectureTests):

- `Domain` no referencia ningún proyecto de aplicación. `Application` solo
  referencia su `Domain` y BuildingBlocks. `Infrastructure` referencia su
  `Application`. `Host` referencia los `Infrastructure` y BuildingBlocks.
- Los módulos no se referencian entre sí. Identity no conoce a Clientes: el
  vínculo `Usuario ↔ Trabajador` es un Guid sin FK.
- Se evaluará `BuildingBlocks.Contracts` (como en Caserito) solo cuando un
  segundo consumidor lo justifique; no se crea por anticipado.

Frontend bajo `web/`: SPA PWA con Vite + React + TypeScript, estructura por
features:

```
web/src/
├── lib/                 # cliente HTTP (correlation ID, 401→refresh), tipos compartidos
├── features/
│   ├── auth/            # login, sesión, guardas de rol
│   ├── admin/clientes/  # gestión de clientes y módulos (solo Administrador)
│   ├── admin/usuarios/  # alta de cuentas (solo Administrador)
│   └── trabajadores/    # gestión de trabajadores (Administrador y Cliente)
├── app/                 # router, shell, navegación por rol
└── pwa/                 # manifest, service worker (vite-plugin-pwa)
```

## Componentes

### Identity

ASP.NET Core Identity con JWT (access token corto + refresh token en cookie
`HttpOnly`). Claims: `sub`, `rol`, `clienteId`. `Usuario` lleva `Rol` (uno de
los cuatro), `ClienteId` opcional (nulo para `Administrador` y
`SoporteTecnico`) y `TrabajadorId` opcional (nulo salvo cuenta de trabajador).
Sin registro público: alta de cuentas solo por `Administrador`. Seeds de
usuarios de prueba por rol solo en entornos dev/test.

### Clientes

- `Cliente`: nombre/razón social, identificador fiscal, estado
  (activo/suspendido), **módulos habilitados** (enum con `GestionAvicola` y
  `ControlAcceso` como valores previstos; ninguno con endpoints todavía).
  Crear/suspender y asignar módulos: solo `Administrador`.
- `Trabajador`: `ClienteId` obligatorio, nombre, documento de identidad (único
  por cliente), cargo y fechas. Sin biometría (fuera de alcance). Gestión:
  `Administrador`, y `Cliente` sobre su propia empresa.

### Autorización: rol + entitlement + tenant

Tres capas: política de rol (qué operación), filtro de tenant (`Cliente` y
`Trabajador` solo ven su `ClienteId`; vía query filters globales de EF Core con
`ICurrentUser`), y entitlement (un endpoint de un módulo de negocio exige que
el cliente tenga ese módulo habilitado). El mecanismo de entitlement se
construye y se prueba en este incremento aunque aún no haya endpoints de
módulos de negocio.

### Observability (building block)

Serilog con JSON estructurado, middleware de correlation ID (genera o propaga
`X-Correlation-ID`, lo expone en la respuesta), exception middleware único,
enrichers estándar (módulo, entorno, `usuarioId` técnico). Anti-PII: nunca
nombres, documentos, credenciales ni datos biométricos en logs. Sink consola;
Seq opcional en `docker-compose.dev.yml`. Los caminos donde el log ES el
requisito (p.ej. error no controlado con correlation ID) se prueban con sink
en memoria de Serilog.

## Flujo de datos y persistencia

Una base SQL Server, un schema por módulo (`identity`, `clientes`), EF Core 10
por módulo con migraciones en su `Infrastructure`. Request: endpoint (minimal
APIs en Host, agrupadas por módulo) → MediatR → handler en `Application`
(validación FluentValidation) → repositorio (interfaz en `Application`,
implementación en `Infrastructure`) → Unit of Work por request. Frontend:
react-query para estado de servidor, react-hook-form para formularios, wrapper
fetch con correlation ID y renovación de token en 401. Access token en memoria;
refresh token solo en cookie `HttpOnly`.

## Manejo de errores

- Excepciones de dominio tipadas → exception middleware las mapea a
  400/404/409 con ProblemDetails (RFC 7807) + correlation ID.
- Validación de entrada → 400 con errores por campo.
- Excepción no controlada → 500 con mensaje genérico + correlation ID; detalle
  técnico solo en logs. Mensajes genéricos, sin datos del trabajador ni
  credenciales, en respuestas y en logs.

## Testing y calidad

- `Icarus.UnitTests`: Domain y Application puros (xUnit + NSubstitute), sin
  base de datos.
- `Icarus.IntegrationTests`: WebApplicationFactory + Testcontainers.MsSql,
  auth de prueba por rol. Requiere Docker.
- `Icarus.ArchitectureTests`: NetArchTest — Domain sin dependencias, módulos
  aislados, capas respetadas.
- `web`: Vitest + Testing Library; prioridad en guardas de rol y cliente HTTP.
- Puerta de calidad: `verify.ps1`/`verify.sh` se extiende con `dotnet build`,
  `dotnet test`, `npm run lint` y `vitest run`. `ci.yml` suma jobs backend y
  frontend (los tests de integración con Testcontainers quedan en el gate local
  y en CI solo si hay Docker disponible; a decidir en el plan).

## Fuera de alcance

Gestión Avícola, Control de Acceso, biometría, auditoría de acciones,
sincronización offline de datos, app móvil/Capacitor, despliegue, registro
público de usuarios, `BuildingBlocks.Contracts`.

## Desviaciones conocidas respecto de Caserito

- Sin Capacitor/Android: la movilidad se resuelve con PWA.
- Sin login social (Google/Facebook): sistema cerrado con alta centralizada.
- Sin SignalR, MailKit ni WebPush en este incremento.
