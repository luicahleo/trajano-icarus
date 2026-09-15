# Plan — Icono de identidad visual Icarus

Spec: `docs/superpowers/specs/2026-09-15-identidad-visual-icono-icarus-design.md`.

**Precondición:** hay cambios activos de otro agente en precios de alimento;
no inspeccionarlos para revertirlos, no modificarlos ni incluirlos en commits.

## Tarea 1: Crear y comprobar los assets de marca

Crear el SVG limpio monocromo y el SVG gradiente en una ubicación compartida
razonable o en los árboles estáticos que realmente los consuman. Adaptar el
generador existente `web/scripts/generar-iconos.mjs` para producir los iconos
PWA PNG 192/512 y los fallbacks que requiera cada host sin añadir dependencias
sin autorización.

Pruebas: comprobar por script o test que los SVG contienen el `viewBox`, un
solo path, no incluyen `b-y8l9ktuq7y`/Dark Reader, y que el manifest apunta a
los archivos generados. Inspeccionar visualmente los PNG resultantes.

```powershell
cd web
node scripts/generar-iconos.mjs
npm run build
```

**Commit previsto:** `feat(marca): agregar assets del icono Icarus`

## Tarea 2: Aplicar la marca a la PWA

Modificar `web/index.html`, `web/vite.config.ts`, `web/src/app/AppLayout.tsx`
y las pruebas de layout o configuración existentes. Reemplazar el favicon y
manifest placeholder por los nuevos assets. Insertar la variante monocroma en
la cabecera junto al nombre de la aplicación, con tamaño controlado por MUI y
accesibilidad decorativa. Mantener los iconos Material UI de navegación y
acciones.

TDD: primero una prueba roja que localice la marca en el layout y una que
verifique las rutas de favicon/manifest; luego implementar.

```powershell
cd web
npm run test -- --run
npm run build
```

**Commit previsto:** `feat(web): aplicar identidad visual Icarus`

## Tarea 3: Aplicar favicon y marca a Trajano.GestorCaisy

Localizar el layout MVC y su cabecera antes de editar. Agregar los assets bajo
`Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/`, referenciar favicon SVG y `.ico`
en el `<head>`, e insertar la variante monocroma junto a la marca textual.
Agregar o ajustar pruebas MVC que comprueben las referencias y el contenido de
cabecera.

```powershell
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests --filter FullyQualifiedName~Layout
dotnet test Icarus/tests/Trajano.GestorCaisy.Tests
```

Si no existen pruebas dirigidas de layout, añadir una de integración y ejecutar
solo esa prueba antes de la suite del proyecto.

**Commit previsto:** `feat(gestorcaisy): aplicar icono de marca Icarus`

## Tarea 4: Integrar y cerrar

Ejecutar `git diff --check`, comprobar que solo se incluyen assets y cambios de
marca propios, inspeccionar favicon/manifest y ejecutar `./verify.ps1` con
Docker. Revisar el diff antes de confirmar y hacer push directo a `develop`.
