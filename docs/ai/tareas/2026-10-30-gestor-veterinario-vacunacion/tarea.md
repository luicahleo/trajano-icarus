# Gestor veterinario: publica los programas de vacunación (reemplaza al Administrador)

## Pedido

Trasladar la gestión del catálogo global de programas de vacunación del
Administrador de plataforma a una cuenta de oficina de CAISY con la
funcionalidad nueva `GestorVeterinario`, en Trajano.GestorCaisy, con un ciclo
Borrador → Publicado como el de los precios de alimento. Las diez decisiones del
usuario están en la bitácora (evento `decision`).

- Spec: [`docs/superpowers/specs/2026-10-30-gestor-veterinario-vacunacion-design.md`](../../../superpowers/specs/2026-10-30-gestor-veterinario-vacunacion-design.md)
- Plan: [`docs/superpowers/plans/2026-10-30-gestor-veterinario-vacunacion.md`](../../../superpowers/plans/2026-10-30-gestor-veterinario-vacunacion.md)

## Alcance

- `FuncionalidadesCaisy.GestorVeterinario = 4` y su política.
- Dominio: `EstadoProgramaVacunacion` (Borrador, Publicado); publicar,
  eliminar borrador (borrado lógico), desactivar solo publicados; publicado
  inmutable.
- Application y API: rutas de gestión `/programas-vacunacion-caisy` para el
  gestor; `/vacunacion/programas` queda de solo lectura y solo publicados para
  cliente y trabajador; el Administrador pierde gestión y lectura del catálogo.
- Migración EF: columna `Estado`; los programas existentes con cronograma pasan
  a Publicado y el resto queda en Borrador.
- Cuentas: script `crear-usuario-gestor-veterinario.ps1` y semilla de
  desarrollo `gve@icarus.test`.
- Trajano.GestorCaisy: sección «Programas de vacunación» completa, incluida la
  subida del Excel con errores por fila; aterrizaje tras iniciar sesión según
  funcionalidad.
- PWA: se retira `/admin/vacunacion`.
- Documentación: glosario, `gestion-avicola.md`, `maquinas-estados.md`,
  `api.md`, `gestorcaisy.md`, `identidad-clientes-permisos.md`, `datos.md`,
  `frontend-pwa.md`, `web/README.md`, `AGENTS.md` y adaptadores.

## Fuera de alcance

- Versionado, vigencia o reactivación de programas.
- Liberar el nombre de un programa eliminado o desactivado.
- Cambios en tareas, asignación, bandeja del trabajador y formato del Excel.
- Notificaciones para el gestor veterinario.
- Alta de la cuenta en el bootstrap de producción (`SeedCuentasInicialesProduccion`).

## Criterios de aceptación

1. Una cuenta CAISY con `GestorVeterinario` inicia sesión en GestorCaisy y
   aterriza en «Programas de vacunación»; el menú lateral muestra el grupo
   «Sanidad» y no muestra campana.
2. El gestor crea un programa: queda como «Borrador» y no aparece en el selector
   «Asignar plan de vacunación» de un cliente.
3. En el borrador, el gestor edita nombre, cantidad de aves y observaciones, y
   sube el Excel del cronograma; el detalle muestra los ítems y la fecha de
   emisión tomada de la primera fecha del Excel.
4. Al subir un Excel con una fila inválida (por ejemplo, edad «abc» en la fila
   2), la pantalla muestra «Fila 2: La edad debe ser un número entero mayor que
   cero.» y el cronograma anterior no cambia.
5. Un borrador sin cronograma no ofrece «Publicar».
6. El gestor publica el borrador tras una confirmación; el programa pasa a
   «Publicado», ya no ofrece editar, importar ni eliminar, y aparece en el
   selector de asignación del cliente.
7. El gestor elimina otro borrador tras una confirmación; desaparece de su
   lista y no aparece para ningún cliente.
8. El cliente asigna el programa publicado a un galpón; el trabajador con
   `Vacunacion` ve sus tareas en la bandeja como antes.
9. El gestor desactiva el programa publicado tras una confirmación que advierte
   que se retiran sus tareas pendientes; el programa se ve «Inactivo» en su
   lista, deja de estar en el selector del cliente y las tareas pendientes del
   galpón desaparecen; las completadas siguen en el historial.
10. En ninguna pantalla de GestorCaisy aparecen galpones, clientes ni tareas.
11. El Administrador ya no ve «Vacunación» en el menú del PWA y
    `/admin/vacunacion` muestra la página de no encontrado.
12. En Development existe la cuenta `gve@icarus.test` con la contraseña de las
    cuentas semilla.

## Plan de pruebas

- Unitarias (`Icarus.UnitTests`): transiciones y guardas de `ProgramaVacunacion`;
  handlers de gestión y de consumo; asignación de borrador y de programa
  publicado; política `CatalogoVacunacion` sin Administrador; bit nuevo de
  `FuncionalidadesCaisy`; DTOs del gestor sin datos del tenant.
- Integración (`Icarus.IntegrationTests`, Docker): ciclo completo del gestor,
  visibilidad por actor, 403 del Administrador y del gestor en rutas del
  tenant, importación con fila inválida, flujo gestor → cliente → trabajador.
- GestorCaisy (`Trajano.GestorCaisy.Tests`): `ReclamosCaisy`, controlador,
  cliente API, flujo con API falsa (errores por fila en el HTML), menú lateral,
  aterrizaje tras sesión, contratos sin datos del tenant; la prueba de
  arquitectura existente sigue verde.
- Frontend (`web`, Vitest): navegación del Administrador sin «Vacunación»; API
  sin funciones de escritura del catálogo; `lint` y `typecheck`.
- Puerta: `./verify.ps1` completo.
- Prueba manual: criterios 1 a 12 en el entorno levantado por operaciones.

Riesgos:

- El filtro EF de tenant es permisivo para cuentas sin `ClienteId`: la única
  barrera del gestor ante tareas y galpones es la política de cada endpoint. Lo
  cubren pruebas de integración explícitas.
- La migración convierte datos de prueba existentes; un programa sin ítems
  activos queda como borrador y deja de ser visible para los clientes.
- El nombre de un borrador eliminado sigue reservado (regla de SP7).
