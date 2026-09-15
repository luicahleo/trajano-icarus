# Plan — Feedback visual para precios de huevo

Spec: `docs/superpowers/specs/2026-09-15-feedback-precios-huevo-design.md`.

**Objetivo:** Detallar fila, columna y valor en fallos de importación Excel de
huevo, y advertir de forma no bloqueante las diferencias de `PRECIO ACTUAL`.

**Precondición:** preservar la modificación ajena en
`Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs`.

## Tarea 1: Ubicar los errores del Excel

Modificar `PuertosPreciosHuevo.cs`,
`ImportadorPublicacionPrecioHuevoExcel.cs` y
`ImportadorPublicacionPrecioHuevoExcelTests.cs`. Extender el error de
importación con columna y valor opcionales, sin dependencias de ClosedXML en
Application. El handler debe conservar el contrato HTTP existente y formar
mensajes de interfaz completos.

TDD: crear pruebas rojas para precio cero y tamaño desconocido que comprueben
fila, columna y valor; los errores de archivo/cabecera no reciben ubicación
ficticia.

```powershell
dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~ImportadorPublicacionPrecioHuevoExcelTests
```

**Commit previsto:** `feat(precios-huevo): detallar errores de importación Excel`

## Tarea 2: Consultar el precio anterior aplicable

Modificar `ComandosPreciosHuevo.cs`, los contratos y cliente HTTP de
`Trajano.GestorCaisy`, y sus pruebas de handler/cliente. Al obtener el detalle,
resolver la publicación vigente a `FechaNotificacion` y exponer por tamaño un
`PrecioAnteriorEsperado` opcional igual al `PrecioAlProductor`. No persistirlo.

TDD: cubrir precio disponible, falta de publicación previa y tamaño sin
correspondencia.

```powershell
dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosHuevoHandlerTests
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~ApiIcarusClientTests
```

**Commit previsto:** `feat(precios-huevo): exponer precio anterior para revisión`

## Tarea 3: Mostrar feedback en MVC

Modificar `PreciosHuevo/Importar.cshtml`, `PreciosHuevo/Detalles.cshtml`, estilos
solo si hace falta, y sus tests MVC. Mantener el resumen de validación como
lista de problemas. Marcar filas donde `PrecioActualDocumento` difiera de
`PrecioAnteriorEsperado`, mostrando ambos valores y que la advertencia no
bloquea publicar.

TDD: una prueba debe confirmar que dos mensajes de importación se ven; otra que
la advertencia se muestra y que aún se puede publicar.

```powershell
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~PreciosHuevoControllerTests
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~FlujoPreciosHuevoTests
```

**Commit previsto:** `feat(gestorcaisy): advertir diferencias en precios de huevo`

## Tarea 4: Integrar

Confirmar rojo antes de cada implementación, revisar `git diff --check` sin
incluir el cambio ajeno y ejecutar `./verify.ps1` con Docker antes de los
commits y push directo a `develop`.
