# Bootstrap de cuentas mínimas de producción — Plan

## Tarea 1: Contrato y pruebas del bootstrap

- Modificar: `Icarus/src/Identity/Icarus.Identity.Infrastructure/SeedAdminPlataforma.cs`
- Crear: `Icarus/tests/Icarus.UnitTests/Identity/SeedCuentasInicialesProduccionTests.cs`
- Definir las opciones de los cuatro secretos y las cuentas fijas de CAISY.
- Probar primero que una configuración incompleta no crea usuarios y que una
  completa crea las tres cuentas con sus roles y funcionalidades exactas.
- Ejecutar: `dotnet test Icarus/tests/Icarus.UnitTests --filter FullyQualifiedName~SeedCuentasInicialesProduccionTests`

## Tarea 2: Arranque y operación

- Modificar: `Icarus/src/Host/Icarus.Host/Program.cs`, `.env.example` y la
  documentación operativa pertinente.
- Sustituir el seed exclusivo de administrador por el bootstrap completo solo
  en la rama productiva ya protegida por `Migraciones:EjecutarAlArranque`.
- Documentar los cuatro secretos sin valores.
- Ejecutar la prueba dirigida y `./verify.ps1`.

## Cierre

- Revisar `git diff --check` y el diff propio.
- Commit y push directos a `develop` solo con la puerta completa en verde.
