# Plan — Discrepancias de «Precio actual» al publicar precios de alimento

Spec: `docs/superpowers/specs/2026-09-15-discrepancias-precio-actual-design.md`.

**Objetivo:** Mantener el bloqueo de publicación de una notificación de precios
de alimento cuando `PRECIO ACTUAL` no coincide con la publicación aplicable,
pero devolver y mostrar una discrepancia accionable por línea de precio.

**Alcance:** API de Gestión Avícola y MVC Trajano.GestorCaisy. No cambia la
importación de Excel ni el comportamiento de precios de huevo.

**Precondición de árbol:** al planificar se detectó una modificación ajena en
`Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs`.
Antes de la Tarea 1, inspeccionarla y preservarla; no mezclarla ni revertirla.

## Tarea 1: Exponer todas las discrepancias desde el handler

**Archivos:**

- Modificar: `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/ComandosPreciosAlimentos.cs`
- Modificar: `Icarus/tests/Icarus.UnitTests/GestionAvicola/PreciosAlimentosHandlerTests.cs`

**Diseño:** Reemplazar el retorno de entidades de `BuscarDiscrepancias` por un
valor interno que lleve `DetalleId`, tipo, presentación, precio del borrador y
precio vigente esperado. Al encontrar diferencias, generar un
`ValidationFailure` por valor, con clave
`Detalles[{DetalleId}].PrecioActualDocumento` y el texto de interfaz definido
en el spec. No cambiar el estado ni llamar a `SaveChanges` en el rechazo.

**TDD:** Añadir primero una prueba con dos detalles discrepantes. Debe fallar
porque la implementación actual produce un solo error genérico. La prueba debe
comprobar dos errores y que cada uno incluye tipo, presentación, columna y los
dos precios. Mantener la prueba existente de publicación correcta.

**Verificar:**

```powershell
dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~PreciosAlimentosHandlerTests
```

**Commit previsto:** `feat(precios): detallar discrepancias de precio actual al publicar`

## Tarea 2: Conservar todos los errores al cruzar la API hacia MVC

**Archivos:**

- Modificar: `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/PreciosController.cs`
- Modificar: `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ContratosApi.cs` solo si hace falta un modelo de traslado tipado
- Modificar: `Icarus/tests/Trajano.GestorCaisy.Tests/Controladores/PreciosControllerTests.cs`

**Diseño:** En `Publicar`, cuando llegue un 400 de validación con errores de
detalles, preservar cada mensaje para el redirect a
`ConfirmarPublicacion`. No convertir la colección en una sola cadena. Mantener
el manejo actual de 409 y de validaciones no relacionadas.

Preferir una colección serializada de mensajes de interfaz en `TempData` o un
modelo de confirmación reconstruible en GET; no introducir acceso directo a
base de datos ni cambiar el cliente HTTP exclusivo de la API.

**TDD:** Escribir una prueba roja que simule dos errores bajo claves de detalle,
publique y compruebe que el resultado conserva ambos para la pantalla de
confirmación. Probar que un 409 continúa con su mensaje actual.

**Verificar:**

```powershell
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~PreciosControllerTests
```

**Commit previsto:** `feat(gestorcaisy): conservar todas las diferencias de precios`

## Tarea 3: Mostrar la lista accionable en la confirmación de publicación

**Archivos:**

- Modificar: `Icarus/src/Apps/Trajano.GestorCaisy/Views/Precios/ConfirmarPublicacion.cshtml`
- Modificar: `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/css/estilos.css` solo si los estilos existentes no cubren una lista o tabla de errores
- Modificar: `Icarus/tests/Trajano.GestorCaisy.Tests/Integracion/FlujoPreciosTests.cs`

**Diseño:** Renderizar un aviso claro y una lista o tabla con todos los
mensajes. Debe ofrecer volver al borrador para corregirlo y conservar el botón
de reintento. No mostrar ids técnicos, SQL, paths ni detalles de la API.

**TDD:** Crear la prueba de integración roja: simular dos discrepancias,
ejecutar POST de publicación, seguir el redirect y comprobar que se ven ambos
tipos/presentaciones y los textos `PRECIO ACTUAL`, valor del borrador y valor
vigente esperado.

**Verificar:**

```powershell
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~FlujoPreciosTests
```

**Commit previsto:** `feat(gestorcaisy): mostrar discrepancias por línea de precio`

## Tarea 4: Integrar y cerrar

1. Ejecutar los tests dirigidos anteriores y confirmar que cada uno se vio rojo
   antes de su implementación y verde después.
2. Revisar `git diff --check` y `git diff --stat`; confirmar que no se incluyó
   la modificación ajena no relacionada.
3. Ejecutar la puerta completa con Docker en marcha:

```powershell
./verify.ps1
```

4. Leer el diff propio después de la puerta y confirmar que ningún log incluye
   precios, valores de Excel o datos ajenos al registro permitido.
5. Actualizar las casillas del plan a medida que se complete cada tarea,
   confirmar los commits previstos sin `--no-verify` y hacer push directo a
   `develop`.

**Commit de documentación:** `docs(precios): detallar discrepancias de precio actual`

## Estado de ejecución

- [x] Tarea 1 — el handler devuelve una discrepancia por línea (`45be224`).
- [x] Tarea 2 — el MVC conserva todas las diferencias (`13e8166`).
- [x] Tarea 3 — la confirmación muestra la lista accionable (`2fea259`).
- [x] Tarea 4 — `./verify.ps1` verde con Docker; push directo a `develop`.
