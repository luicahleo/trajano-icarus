# Registro de vuelo narrativo en Seq

**Goal:** Reconstruir por `TraceId` el recorrido técnico de una operación con
logs narrativos estructurados, sin duplicación entre capas ni exposición de
PII.

**Architecture:** Spec aprobado:
`docs/superpowers/specs/2026-08-17-registro-vuelo-seq-design.md`. El contrato de
aplicación declara operaciones y datos seguros; el building block de
observabilidad automatiza el ciclo MediatR, aplica listas permitidas y registra
persistencia/transacciones con `ILogger` sobre Serilog. El alta de cliente con
cuenta demuestra decisiones y compensación lógica. Seq sigue siendo opcional y
la consola JSON, el fallback.

**Tech stack:** .NET 10, MediatR 12, EF Core 10, `Microsoft.Extensions.Logging`,
Serilog y Seq 2026.1.

## Restricciones globales

- Anti-PII estricta: nunca correo, NIT, documentos, nombres, contraseña, hashes,
  tokens, cookies, biometría, `TrabajadorId`, cuerpos, entidades, parámetros SQL
  ni actividad nominal.
- TDD real: cada requisito nuevo debe verse rojo por la causa esperada antes de
  implementar lo mínimo.
- `TraceId` y scopes existentes son la correlación; no crear un ID de vuelo.
- No instrumentar queries rutinarias ni todos los handlers existentes.
- `backend.error` conserva la excepción; `operation.failed` no duplica stack ni
  mensaje.
- `./verify.ps1` antes del commit y del push; nunca `--no-verify`.
- Un único commit coherente al cerrar la feature.

---

## Task 1: contrato seguro y ciclo automático de operaciones

**Files:**

- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/Observability/IOperacionRegistrable.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/Observability/IRegistroVuelo.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/Observability/DatoRegistroVuelo.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/RegistroVuelo.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/RegistroVueloBehavior.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/DescriptorOperacionRegistroVuelo.cs`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Application/Icarus.BuildingBlocks.Application.csproj`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/Icarus.BuildingBlocks.Observability.csproj`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ObservabilityExtensions.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs`
- Create: `Icarus/tests/Icarus.UnitTests/Observability/RegistroVueloTests.cs`
- Create: `Icarus/tests/Icarus.UnitTests/Observability/RegistroVueloBehaviorTests.cs`

- [x] Test rojo: una operación registrable emite inicio y fin con nombre,
  fases, resultado y duración; una request no registrable no añade ruido.
- [x] Test rojo: validación/dominio producen rechazo seguro y un error
  inesperado produce fallo sin adjuntar excepción, stack ni request.
- [x] Test rojo: campos declarados con el tipo correcto se conservan; campos
  desconocidos, de tipo incorrecto o globalmente prohibidos se omiten sin
  romper la operación.
- [x] Implementar contrato de aplicación sin dependencia directa de Serilog.
- [x] Implementar mensajes fijos en español y propiedades estructuradas mediante
  scopes de `ILogger`.
- [x] Registrar el behavior fuera de `ValidationBehavior` para observar también
  rechazos de validación.

**Red/green:**

```powershell
dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter "FullyQualifiedName~RegistroVuelo"
```

---

## Task 2: persistencia y transacciones físicas

**Files:**

- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/DescriptorContextoPersistencia.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/SaveChangesRegistroVueloInterceptor.cs`
- Create: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/TransaccionesRegistroVueloInterceptor.cs`
- Modify: `Icarus/src/BuildingBlocks/Icarus.BuildingBlocks.Observability/ObservabilityExtensions.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/DependencyInjection.cs`
- Modify: `Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs`
- Create: `Icarus/tests/Icarus.UnitTests/Observability/PersistenciaRegistroVueloTests.cs`
- Create: `Icarus/tests/Icarus.IntegrationTests/Observability/PersistenciaRegistroVueloIntegrationTests.cs`

- [x] Test rojo unitario: `SaveChanges` completado informa solo contexto estable,
  filas y duración; el fallo no serializa entidades ni valores.
- [x] Test rojo unitario: commit y rollback físicos usan eventos distintos de
  una compensación lógica y solo se emiten cuando EF observa una transacción.
- [x] Implementar interceptores reutilizables con estado seguro ante más de un
  `DbContext` por scope.
- [x] Registrar descriptores estables `Clientes` e `Identity` al configurar cada
  contexto.
- [x] Test rojo de integración: una escritura real comparte el `TraceId` y
  produce `persistence.save_changes.completed`; una transacción explícita
  produce commit o rollback según corresponda.

**Red/green:**

```powershell
dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter "FullyQualifiedName~PersistenciaRegistroVuelo"
dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj --filter "FullyQualifiedName~PersistenciaRegistroVuelo"
```

---

## Task 3: piloto de alta de cliente con cuenta

**Files:**

- Modify: `Icarus/src/Clientes/Icarus.Clientes.Application/Clientes/CrearClienteCommand.cs`
- Modify: `Icarus/src/Clientes/Icarus.Clientes.Application/Clientes/SuspenderClienteCommand.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Servicios/AltaCuentasServicio.cs`
- Modify: `Icarus/src/Host/Icarus.Host/Program.cs`
- Modify: `Icarus/tests/Icarus.UnitTests/Host/AltaCuentasServicioTests.cs`
- Create: `Icarus/tests/Icarus.IntegrationTests/Observability/RegistroVueloAltaClienteIntegrationTests.cs`

- [x] Test rojo: el camino exitoso narra disponibilidad, escrituras y resultado
  sin correo, NIT, razón social, contraseña ni identificadores creados.
- [x] Test rojo: el rechazo de Identity narra decisión, inicio y fin de la
  compensación lógica, y termina la raíz como rechazada.
- [x] Test rojo: el fallo de compensación emite `operation.compensation.failed`
  y deja la raíz fallida, sin afirmar rollback físico.
- [x] Marcar únicamente las mutaciones del piloto como operaciones MediatR
  registrables con nombres estables.
- [x] Instrumentar la orquestación raíz con códigos cerrados; normalizar códigos
  desconocidos de Identity a `identity_rejected`.
- [x] Verificar por integración la secuencia cronológica compartida por
  `TraceId`.

**Red/green:**

```powershell
dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj --filter "FullyQualifiedName~AltaCuentasServicio|FullyQualifiedName~RegistroVuelo"
dotnet test Icarus/tests/Icarus.IntegrationTests/Icarus.IntegrationTests.csproj --filter "FullyQualifiedName~RegistroVueloAltaCliente"
```

---

## Task 4: operación, integración y cierre

**Files:**

- Modify: `docs/operacion/observabilidad.md`
- Modify: este plan, marcando tareas completadas y desviaciones reales.
- Delete: `docs/ai/HANDOFF.md` al cerrar (está ignorado por Git).

- [x] Documentar consultas por `TraceId`, `Operation`, `EventName`, `Outcome`,
  `ReasonCode`, `PersistenceContext` y `Release`.
- [x] Ejecutar todas las pruebas dirigidas y después `./verify.ps1` con Docker.
  Resultado real: 114 unitarias, 4 arquitectura y 50 integración verdes; la
  puerta completa quedó verde según `logs/puerta-calidad-20260817-230620.log`.
- [x] Revisar `git diff --check`, `git diff --stat` y cada diff propio.
- [x] Hacer commit `feat(observabilidad): añade registro de vuelo narrativo`.
- [x] Ejecutar otra vez `./verify.ps1` inmediatamente antes del push si el árbol
  cambió después de la puerta anterior; push directo a `develop`.
- [x] Borrar el handoff al cerrar y confirmar que `develop` queda alineado con
  `origin/develop`.

**Puerta:**

```powershell
./verify.ps1
```

**Resultado esperado:** todos los gates verdes y ninguna PII, secreto, cuerpo,
entidad o parámetro SQL en contratos, tests ni eventos capturados.
