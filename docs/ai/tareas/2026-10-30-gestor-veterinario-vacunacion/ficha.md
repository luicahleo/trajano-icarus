---
id: 2026-10-30-gestor-veterinario-vacunacion
tipo: funcionalidad
modulos: [GestionAvicola, GestorCaisy, Identity, Clientes, Host, web, docs]
temas: [vacunacion, gestor-veterinario, programas-vacunacion, campana, notificaciones, tareas-trabajador]
resumen: El Gestor CAISY con GestorVeterinario publica los programas de vacunación; el trabajador ve sus tareas en una campana general.
aprobado: 1573cd5
commits: [3e4fffb, 494df97]
---

# Gestor veterinario: publica los programas de vacunación (reemplaza al Administrador)

## Qué cambió

- El catálogo de programas pasa del Administrador a GestorCaisy
  (`GestorVeterinario = 4`), con ciclo Borrador → Publicado.
- Gestión en `/programas-vacunacion-caisy`; `/vacunacion/programas` solo lee
  publicados. El PWA pierde `/admin/vacunacion`.
- Ampliación: campana general en el PWA y página `/avicola/tareas`.

## Por qué

El catálogo es de CAISY y se prepara en borrador, como los precios de alimento.

## Decisiones

- Publicado inmutable, sin versionado ni vigencia; publicar exige cronograma.
- Borrador eliminado: borrado lógico con nombre reservado.
- Desactivar retira las tareas pendientes de todos los galpones.
- Administrador sin gestión ni lectura; el gestor no ve galpones ni tareas.
- La campana filtra con la fecha local y aísla el fallo de cada fuente.

## Archivos clave

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/ProgramaVacunacion.cs` — estados y guardas.
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Vacunacion/Programas.cs` — handlers.
- `Icarus/src/Host/Icarus.Host/Endpoints/ProgramasVacunacionCaisyEndpoints.cs` — API del gestor.
- `Icarus/src/Clientes/Icarus.Clientes.Infrastructure/Autorizacion/RequisitoCatalogoVacunacion.cs` — política de lectura.
- `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/ProgramasVacunacionController.cs` — UI MVC.
- `web/src/app/notificaciones/fuentes.ts` — contrato de fuentes de la campana.
- `web/src/features/avicola/TareasVacunacionPage.tsx` — tareas del trabajador.

## Cómo probarlo

- Unitarias, integración, GestorCaisy y Vitest de `app/notificaciones/`.
- Criterios 1 a 23 de `tarea.md`.

## Pendientes

- Sin token de concurrencia: importar y publicar a la vez puede romper la inmutabilidad.
- Faltan pruebas del 413 y de la extensión del Excel; el DELETE repetido da 404.
- `.superdesign/init/pages.md` y `routes.md` citan `/admin/vacunacion`.

## Enlaces

- [Tarea](tarea.md)
- [Spec](../../../superpowers/specs/2026-10-30-gestor-veterinario-vacunacion-design.md)
- [Plan](../../../superpowers/plans/2026-10-30-gestor-veterinario-vacunacion.md)
- [Spec de la campana](../../../superpowers/specs/2026-11-01-campana-tareas-vacunacion-trabajador-design.md)
- [Plan de la campana](../../../superpowers/plans/2026-11-01-campana-tareas-vacunacion-trabajador.md)
