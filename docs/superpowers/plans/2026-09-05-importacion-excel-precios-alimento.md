# Plan — Importación Excel de precios de alimento

Spec: `docs/superpowers/specs/2026-09-05-importacion-excel-precios-alimento-design.md`.

- [ ] Añadir contrato, parser ClosedXML y pruebas unitarias rojas/verdes.
- [ ] Integrar comando, API y almacenamiento del formato original.
- [ ] Actualizar GestorCaisy para aceptar PDF/XLSX y añadir pruebas dirigidas.
- [ ] Ejecutar tests afectados, puerta de calidad y revisar diff.

Verificación prevista: `dotnet test Icarus/tests/Icarus.UnitTests/Icarus.UnitTests.csproj`;
`dotnet test Icarus/tests/Trajano.GestorCaisy.Tests/Trajano.GestorCaisy.Tests.csproj`;
`./verify.ps1`.
