# Permisos operativos de trabajadores — Plan de implementación

**Objetivo:** completar el entitlement de Gestión Avícola para que el cliente
administre su única granja y sus galpones, mientras cada trabajador solo puede
consultar la estructura necesaria y operar producción de huevos y/o mortalidad
según la asignación del cliente.

**Arquitectura:** se mantienen los flags numéricos actuales. `Granjas` y
`Galpones` pasan a ser capacidades estructurales exclusivas del cliente;
`ProduccionHuevos` y `Mortalidad` son las únicas asignables al trabajador. Una
política de “alguna funcionalidad operativa” protege la lectura estructural. El
backend calcula permisos efectivos comprobando también que el cliente esté
activo y conserve `GestionAvicola`; la PWA compone navegación, consultas y
acciones a partir de esos permisos.

**Tecnologías:** .NET 10, ASP.NET Core Authorization, MediatR, EF Core, xUnit,
Testcontainers.MsSql; React 19, TypeScript estricto, React Router 7, TanStack
Query 5, MUI 9, Vitest y Testing Library.

**Spec:**
`docs/superpowers/specs/2026-08-19-permisos-operativos-trabajadores-design.md`.

## Restricciones globales

- TDD estricto: escribir cada prueba, verla fallar por el motivo esperado,
  implementar lo mínimo y volver a verla pasar.
- No renumerar `Funcionalidades` ni hacer una migración destructiva de datos.
- El backend es la autoridad; las guardas y la UI son una mejora de UX.
- Mantener aislamiento de tenant y errores genéricos, sin PII.
- No añadir dependencias.
- Preservar los cambios ajenos existentes en `logs/`.
- Ejecutar `./verify.ps1` antes del único commit de la feature. No usar
  `--no-verify`.
- Tras revisar el diff y con la puerta verde, commit y push directos a
  `develop`; no crear rama ni pull request.

---

## Tarea 1: declarar las funcionalidades asignables al trabajador — completada

**Archivos:**

- Crear:
  `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesTrabajador.cs`
- Modificar:
  `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/DefinirFuncionalidadesTrabajadorValidator.cs`
- Modificar:
  `Icarus/src/Clientes/Icarus.Clientes.Application/Trabajadores/DefinirFuncionalidadesTrabajadorHandler.cs`
- Modificar:
  `Icarus/tests/Icarus.UnitTests/Clientes/FuncionalidadesTests.cs`
- Modificar:
  `Icarus/tests/Icarus.UnitTests/Clientes/DefinirFuncionalidadesTrabajadorHandlerTests.cs`

- [x] Escribir pruebas que demuestren que solo `ProduccionHuevos` y
  `Mortalidad` son asignables y que `Granjas`, `Galpones` y las funcionalidades
  futuras se rechazan con mensaje genérico.
- [x] Ejecutar:

  ```powershell
  dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~FuncionalidadesTests|FullyQualifiedName~DefinirFuncionalidadesTrabajadorHandlerTests"
  ```

  Rojo esperado: el catálogo operativo todavía no existe y el handler acepta
  cualquier funcionalidad del módulo.
- [x] Crear un catálogo de dominio con el conjunto operativo asignable y un
  predicado único; usarlo tanto en validación como en aplicación. La lista
  vacía sigue siendo válida y reemplaza todos los permisos.
- [x] Repetir el comando y comprobar verde.

## Tarea 2: calcular permisos efectivos del trabajador — completada

**Archivos:**

- Modificar:
  `Icarus/src/Clientes/Icarus.Clientes.Application/Autorizacion/IVerificadorEntitlement.cs`
- Modificar:
  `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/VerificadorEntitlement.cs`
- Modificar:
  `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/ConsultaPermisosActuales.cs`
- Modificar:
  `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/SemillaClientes.cs`
- Modificar:
  `Icarus/tests/Icarus.IntegrationTests/EntitlementTests.cs`
- Modificar:
  `Icarus/tests/Icarus.IntegrationTests/IdentityEndpointsTests.cs`

- [x] Añadir pruebas de integración para estas condiciones:
  trabajador activo + asignación válida + cliente activo con módulo; módulo
  retirado; cliente suspendido; trabajador desactivado; flags estructurales
  históricos; `/identidad/me` devuelve solo permisos efectivos.
- [ ] Ejecutar:

  ```powershell
  dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~EntitlementTests|FullyQualifiedName~IdentityEndpointsTests"
  ```

  Rojo esperado: el trabajador conserva acceso o permisos publicados cuando el
  cliente pierde el módulo, y los flags estructurales todavía se consideran.
- [x] Hacer que la verificación del trabajador exija simultáneamente cliente y
  trabajador activos, módulo vigente, flag asignado y funcionalidad asignable.
- [x] Hacer que `ConsultaPermisosActuales` aplique exactamente la misma
  intersección. Mantener módulos vacíos para el trabajador.
- [x] Cambiar la semilla del trabajador demo de `Granjas` a
  `ProduccionHuevos`; no modificar registros existentes mediante migración.
- [ ] Repetir el comando y comprobar verde.

## Tarea 3: autorizar lectura estructural sin conceder administración — completada

**Archivos:**

- Modificar:
  `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/PoliticasClientes.cs`
- Modificar:
  `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/RequisitoFuncionalidadHabilitada.cs`
- Modificar:
  `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/DependencyInjection.cs`
- Modificar:
  `Icarus/src/Host/Icarus.Host/Endpoints/GestionAvicolaEndpoints.cs`
- Modificar:
  `Icarus/tests/Icarus.IntegrationTests/GestionAvicolaEndpointsTests.cs`
- Modificar:
  `Icarus/tests/Icarus.IntegrationTests/ProduccionMortalidadEndpointsTests.cs`

- [x] Escribir la matriz de integración: trabajador con producción, solo
  mortalidad, ambas o ninguna. Probar por separado lectura de estructura,
  mutaciones estructurales, producción, eficiencia y mortalidad.
- [ ] Ejecutar:

  ```powershell
  dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~GestionAvicolaEndpointsTests|FullyQualifiedName~ProduccionMortalidadEndpointsTests"
  ```

  Rojo esperado: la lectura de granjas/galpones exige `Granjas`/`Galpones` y no
  permite operar a un trabajador con permisos operativos válidos.
- [x] Incorporar una política con semántica ANY para las funcionalidades
  operativas. Aplicarla solo a `GET /granjas`, `GET /granjas/{id}`,
  `GET /granjas/{granjaId}/galpones` y `GET /galpones/{id}`.
- [x] Mantener las mutaciones estructurales bajo capacidades que solo el
  cliente puede satisfacer. Mantener producción/eficiencia bajo
  `ProduccionHuevos` y mortalidad bajo `Mortalidad`.
- [ ] Repetir el comando y comprobar verde.

## Tarea 4: permitir que el cliente configure al trabajador en la PWA — completada

**Archivos:**

- Modificar: `web/src/lib/tipos.ts`
- Modificar: `web/src/features/trabajadores/api.ts`
- Modificar: `web/src/features/trabajadores/TrabajadoresPage.tsx`
- Modificar: `web/src/features/trabajadores/TrabajadoresPage.test.tsx`

- [x] Escribir pruebas de interfaz para mostrar las asignaciones actuales,
  abrir el diálogo, seleccionar producción/mortalidad, guardar la lista
  completa y quitar todas las funcionalidades.
- [ ] Ejecutar desde `web/`:

  ```powershell
  npm test -- TrabajadoresPage.test.tsx
  ```

  Rojo esperado: no existe acción ni llamada para definir funcionalidades.
- [x] Tipar `TrabajadorResumen.funcionalidades` como `Funcionalidad[]`, añadir
  `definirFuncionalidades`, implementar el diálogo con checkboxes accesibles y
  refrescar la lista tras guardar.
- [x] Mostrar solo Producción de huevos y Mortalidad. No mostrar Granjas,
  Galpones ni funcionalidades futuras.
- [ ] Repetir el comando y comprobar verde.

## Tarea 5: hacer la navegación dependiente de permisos y terminal — completada

**Archivos:**

- Modificar: `web/src/app/inicioSegunRol.ts`
- Modificar: `web/src/app/inicioSegunRol.test.ts`
- Modificar: `web/src/app/RedirigirSegunRol.tsx`
- Modificar: `web/src/features/auth/LoginPage.tsx`
- Modificar: `web/src/features/auth/RequiereFuncionalidad.tsx`
- Modificar: `web/src/features/auth/RequiereFuncionalidad.test.tsx`
- Modificar: `web/src/app/AppLayout.tsx`
- Modificar: `web/src/app/AppLayout.test.tsx`
- Modificar: `web/src/app/router.tsx`

- [x] Escribir pruebas para cliente, trabajador con producción, trabajador con
  mortalidad y trabajador sin permisos. Añadir una regresión que detecte el
  ciclo `/` ↔ `/avicola`.
- [ ] Ejecutar desde `web/`:

  ```powershell
  npm test -- inicioSegunRol.test.ts RequiereFuncionalidad.test.tsx AppLayout.test.tsx
  ```

  Rojo esperado: todo trabajador aterriza en `/avicola` y la guarda rechazada
  vuelve a `/`.
- [x] Calcular el inicio usando rol y funcionalidades efectivas. Usar `/inicio`
  como destino terminal de una guarda sin permiso y mostrar Gestión Avícola en
  el menú solo con producción o mortalidad.
- [x] Proteger `/avicola`, la lista y el detalle de galpones con semántica ANY
  sobre `ProduccionHuevos`/`Mortalidad`; mantener eficiencia bajo producción.
- [ ] Repetir el comando y comprobar verde.

## Tarea 6: componer las pantallas según cada permiso — completada

**Archivos:**

- Modificar: `web/src/features/avicola/AvicolaInicioPage.tsx`
- Modificar: `web/src/features/avicola/GalponesPage.tsx`
- Modificar: `web/src/features/avicola/TarjetaGalpon.tsx`
- Modificar: `web/src/features/avicola/GalponPage.tsx`
- Modificar: `web/src/features/avicola/AvicolaInicioPage.test.tsx`
- Modificar: `web/src/features/avicola/GalponesPage.test.tsx`
- Modificar: `web/src/features/avicola/GalponAcciones.test.tsx`
- Crear si facilita aislar la matriz:
  `web/src/features/avicola/GalponPage.test.tsx`

- [x] Escribir pruebas que inspeccionen las llamadas HTTP y la UI para cuatro
  perfiles: cliente, solo producción, solo mortalidad y ambas.
- [ ] Ejecutar desde `web/`:

  ```powershell
  npm test -- AvicolaInicioPage.test.tsx GalponesPage.test.tsx GalponAcciones.test.tsx GalponPage.test.tsx
  ```

  Rojo esperado: las tarjetas siempre consultan eficiencia y el detalle siempre
  consulta producción, mortalidad y eficiencia, por lo que un permiso parcial
  provoca 403 y bloquea toda la página.
- [x] Condicionar cada `useQuery` con `enabled` según funcionalidad; no renderizar
  ni reintentar consultas no autorizadas. Permitir que cada sección autorizada
  gestione su propio estado de carga/error.
- [x] Ocultar administración de granja, galpones e inventario al trabajador.
  Mantenerla completa para el cliente.
- [x] Asegurar que producción y eficiencia no aparezcan con solo mortalidad, y
  que mortalidad no aparezca con solo producción.
- [ ] Repetir el comando y comprobar verde.

## Tarea 7: integrar, documentar y cerrar — completada

**Archivos:**

- Modificar: `docs/dominio/glosario-avicola.md`
- Actualizar casillas y estado de este plan.

- [x] Añadir al glosario la regla estable: el cliente administra estructura y
  opera todo su módulo; el trabajador solo recibe funcionalidades operativas y
  obtiene lectura estructural implícita.
- [x] Ejecutar suites dirigidas completas:

  ```powershell
  dotnet test Icarus/tests/Icarus.UnitTests
  dotnet test Icarus/tests/Icarus.IntegrationTests
  npm --prefix web test
  npm --prefix web run build
  ```

- [x] Revisar primero `git diff --stat`, después únicamente los diffs de esta
  feature, y ejecutar `git diff --check`.
- [x] Ejecutar la puerta obligatoria:

  ```powershell
  ./verify.ps1
  ```

  Resultado exigido: salida completa en verde. Si falla, corregir el contenido;
  nunca relajar baselines, exclusiones ni umbrales.
- [x] Commit previsto:

  ```powershell
  git add <archivos-de-la-feature>
  git commit -m "feat: aplica permisos operativos a trabajadores"
  git push origin develop
  ```

- [x] Confirmar `develop...origin/develop` limpio salvo cambios ajenos ya
  existentes. Si el bloque no termina, crear `docs/ai/HANDOFF.md`; si termina,
  no dejar handoff pendiente.
