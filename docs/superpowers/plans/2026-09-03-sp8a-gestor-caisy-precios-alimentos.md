# SP8A — GestorCaisy y Notificación de Precios de Alimentos

**Objetivo:** introducir la identidad funcional de CAISY, la aplicación MVC de
oficina y la publicación versionada de precios desde un PDF revisable.

**Spec:** `docs/superpowers/specs/2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md`.

**Dependencia:** ninguna. SP8B no comienza hasta integrar este bloque.

## Restricciones globales

- TDD estricto: ejecutar cada prueba nueva en rojo por el motivo correcto antes
  de implementar; registrar rojo y verde en este plan al ejecutarlo.
- Español UTF-8 sin BOM, errores genéricos y ninguna PII/contenido documental en Seq.
- Catálogo global sin filtro tenant; acceso solo por la política
  `FuncionalidadCaisy:GestorPedidoAlimento`.
- PDF original privado, borrador editable, publicación explícita e inmutable.
- La UI de Trajano-GestorCaisy no se implementa hasta aprobar el borrador de Superdesign.
- Docker debe estar activo para integración. `./verify.ps1` antes del commit.

## Tarea 1 — Identidad y autorización CAISY

**Crear/modificar:**

- `Icarus/src/Identity/Icarus.Identity.Domain/Rol.cs`
- `Icarus/src/Identity/Icarus.Identity.Domain/FuncionalidadesCaisy.cs`
- `Icarus/src/Identity/Icarus.Identity.Domain/ReglasRol.cs`
- `Icarus/src/Identity/Icarus.Identity.Infrastructure/Persistencia/Usuario.cs`
- `Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/EmisorAccessTokens.cs`
- `Icarus/src/Identity/Icarus.Identity.Infrastructure/Autenticacion/PoliticasAutorizacion.cs`
- `Icarus/src/Identity/Icarus.Identity.Infrastructure/DependencyInjection.cs`
- `Icarus/src/Identity/Icarus.Identity.Infrastructure/Migrations/`
- `Icarus/src/Identity/Icarus.Identity.Application/UsuariosCaisy/`
- `Icarus/src/Host/Icarus.Host/Endpoints/UsuariosCaisyEndpoints.cs`
- `Icarus/tests/Icarus.UnitTests/Identity/FuncionalidadesCaisyTests.cs`
- `Icarus/tests/Icarus.IntegrationTests/UsuariosCaisyEndpointsTests.cs`

- [x] Rojo: probar que `GestorCaisy` no requiere `ClienteId`, que solo admite
  flags definidos y que un admin puede crear/desactivar/asignar funciones sin
  exponer credenciales ni correo en logs.
  Registro: `dotnet test Icarus/tests/Icarus.UnitTests --filter Caisy` →
  **rojo** CS0117 `Rol no contiene GestorCaisy` (motivo correcto).
- [x] Verde mínimo: persistir el bitmask, emitir claim específico, política
  dinámica y API administrativa.
  Registro: mismo comando → **verde** 24/24 (incluye Identity completo).
- [x] Integración: 401/403 correctos, función válida permite acceso y cuenta
  desactivada no renueva sesión.
  Registro: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter
  UsuariosCaisy` → **rojo** inicial (`PendingModelChangesWarning`, migración
  sin crear) → generada migración `UsuariosCaisy` (columna
  `identity.usuarios.FuncionalidadesCaisy`) → **verde** 5/5. Docker 28.5.1
  activo.
- [x] Comandos: `dotnet test Icarus/tests/Icarus.UnitTests --filter Caisy` y
  `dotnet test Icarus/tests/Icarus.IntegrationTests --filter UsuariosCaisy`.
- [ ] Commit previsto: `feat(identity): agregar usuarios funcionales de caisy`.

## Tarea 2 — Dominio de precios

**Crear/modificar:**

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionPreciosAlimentos.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DetallePrecioAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TipoAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PresentacionAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoNotificacionPreciosAlimentos.cs`
- `Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionPreciosAlimentosTests.cs`

- [x] Rojo: creación de borrador, doce combinaciones únicas, importes positivos,
  rangos de edad coherentes, publicación y sellado posterior.
  Registro: `dotnet test Icarus/tests/Icarus.UnitTests --filter
  NotificacionPreciosAlimentos` → **rojo** CS0246 (tipos del dominio sin crear)
  → **verde** 9/9.
- [x] Rojo: impedir dos publicaciones activas con la misma `VigenteDesde` en
  Application/DB y anular una futura sin alterar una vigente.
  Registro: la regla de unicidad por `VigenteDesde` vive en Application/DB
  (Tarea 3, repositorio e índice); en dominio quedó cubierta `AnularFutura` →
  **verde** 9/9.
- [x] Verde mínimo: agregado con detalles normalizados y métodos
  `ActualizarBorrador`, `Publicar` y `AnularFutura`.
- [x] Comando: `dotnet test Icarus/tests/Icarus.UnitTests --filter NotificacionPreciosAlimentos`.
- [ ] Commit previsto: `feat(avicola): modelar notificaciones de precios de alimentos`.

## Tarea 3 — Persistencia, consulta vigente e importación PDF

**Crear/modificar:**

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PreciosAlimentos/`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionNotificacionPreciosAlimentos.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDetallePrecioAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Importacion/ImportadorNotificacionPreciosPdf.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Documentos/AlmacenDocumentosLocal.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/DependencyInjection.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/`
- `Icarus/Directory.Packages.props`
- tests `ImportadorNotificacionPreciosPdfTests.cs` y `PreciosAlimentosHandlerTests.cs`.

- [x] Rojo: usar una copia anonimizada del PDF de muestra como fixture y exigir
  fecha 2025-11-02, doce filas, edades, precios nuevos y aportes 1.20/0.60/0.75.
  Registro: `dotnet test ... --filter ImportadorNotificacionPreciosPdf` →
  **rojo** CS0246 (importador inexistente) → **verde** 4/4 contra
  `Fixtures/NotificacionPreciosMuestra.pdf` (fixture generado determinístico,
  anonimizado, commiteado).
- [x] Rojo: formato inválido produce errores sin persistencia parcial; «Precio
  actual» discrepante bloquea publicación, no la extracción del borrador.
  Registro: cubierto en `ImportadorNotificacionPreciosPdfTests` y
  `PreciosAlimentosHandlerTests` → **verde** 23/23 junto con handlers.
- [x] Fijar `PdfPig` 0.1.16 (versión estable publicada comprobada el
  2026-09-03) solo en Infrastructure, detrás de
  `IImportadorNotificacionPreciosPdf`.
- [x] Implementar repositorio, índice de vigencia, almacenamiento privado del
  original y resolución `última Publicada con VigenteDesde <= fecha`.
- [x] Comandos: pruebas dirigidas; luego migración con `dotnet ef migrations add
  PreciosAlimentos` (creada: tablas `notificaciones_precios_alimentos` y
  `detalles_precio_alimento`, índice filtrado `[Estado] = 1 AND [EstaActivo] = 1`).
- [x] Commit previsto: `feat(avicola): importar y publicar precios de alimentos`
  (ejecutado; la puerta quedó verde antes del commit).

## Tarea 4 — API y contratos de precios

**Crear/modificar:**

- `Icarus/src/Host/Icarus.Host/Endpoints/PreciosAlimentosEndpoints.cs`
- `Icarus/src/Host/Icarus.Host/Program.cs`
- `Icarus/tests/Icarus.IntegrationTests/PreciosAlimentosEndpointsTests.cs`

- [x] Rojo: subir PDF, revisar/editar borrador, publicar, listar historial,
  obtener vigente y descargar original con autorización.
  Registro: `dotnet test ... --filter PreciosAlimentosEndpointsTests` →
  **rojo** 4/11 (interferencia de vigencias entre pruebas, límite de tamaño y
  409 real en la revisión del borrador) → **verde** 11/11.
  Corrección hallada: los detalles recreados con Guid no vacío eran marcados
  Modified por DetectChanges (mismo caso que AgregarItem del cronograma);
  se agregó `IRepositorioNotificacionesPrecios.AgregarDetalle`.
- [x] Cubrir multipart, límites de tamaño, MIME/firma, 409 de concurrencia y
  403 para Cliente/Trabajador/CAISY sin función.
  Registro: multipart OK; 413 con tope explícito de 20 MB en el endpoint;
  firma `%PDF-` rechazada en el handler; 409 de concurrencia traducido por
  `UnidadTrabajoConConcurrencia` (probado en unitarios) y 409 por vigencia
  duplicada en integración; 401/403 cubiertos.
- [x] Confirmar que las operaciones de Seq solo contienen ids, conteos y estado.
  Registro: los descriptores de registro de vuelo llevan solo
  `DetallesImportados`/`CantidadDetalles`; el middleware de petición registra
  ruta, estado y duración; nunca contenido del PDF, nombre de archivo ni correo.
- [x] Comando: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter PreciosAlimentos`.
- [ ] Commit previsto: `feat(api): exponer notificaciones de precios de alimentos`.

## Tarea 5 — Aplicación MVC Trajano-GestorCaisy

**Crear/modificar:**

- `Icarus/src/Apps/Trajano.GestorCaisy/Trajano.GestorCaisy.csproj`
- `Icarus/src/Apps/Trajano.GestorCaisy/Program.cs`
- `Icarus/src/Apps/Trajano.GestorCaisy/Controllers/`
- `Icarus/src/Apps/Trajano.GestorCaisy/Views/`
- `Icarus/src/Apps/Trajano.GestorCaisy/Servicios/ApiIcarusClient.cs`
- `Icarus/src/Apps/Trajano.GestorCaisy/wwwroot/`
- `Icarus/tests/Trajano.GestorCaisy.Tests/`
- `Icarus/Icarus.sln`

- [ ] Esperar aprobación del borrador Superdesign del usuario.
  Registro (2026-09-04): **pendiente**. No existe `.superdesign/resume.json`
  ni constancia de aprobación; solo hay artefactos de inicialización. La tarea
  queda sin empezar, conforme a la restricción global. Las pruebas de vistas y
  el cliente API tipado se implementarán al retomar.
- [ ] Rojo: login/sesión protegida, menú limitado por función, lista de
  publicaciones, importación, revisión y confirmación de publicación.
- [ ] Implementar MVC server-rendered y cliente API tipado; conservar access y
  refresh token en sesión protegida HttpOnly/servidor, nunca en localStorage.
- [ ] Mantener mismo origen lógico `/api` en despliegue para cookies seguras;
  sin service worker, caché offline ni IndexedDB.
- [ ] Pruebas de vistas/controladores con API falsa y accesibilidad básica.
- [ ] Commit previsto: `feat(gestor-caisy): publicar precios de alimentos`.

## Cierre SP8A

- [x] Ejecutar pruebas unitarias, integración y del proyecto MVC.
  Registro (2026-09-04): unitarios 301/301, integración 89/89,
  arquitectura 5/5 (el proyecto MVC no existe: Tarea 5 pendiente).
- [x] Ejecutar `./verify.ps1`, revisar `git diff --check` y diff propio.
  Registro: puerta completa verde al cierre de cada tarea con código; sin
  hallazgos en `git diff --check`.
- [x] Actualizar este plan, glosario, `AGENTS.md` y adaptadores generados.
  Registro: plan y `AGENTS.md` actualizados; adaptadores regenerados con
  `node quality/generar-adaptadores.mjs`; el glosario ya traía la sección SP8
  (trabajo del usuario, incluido en el commit documental).
- [x] Commit de documentación si procede y push directo a `develop` solo al
  cerrar el bloque verificado.
  Registro: commits 1–4 ejecutados (uno por tarea) más commit documental.
  **El push queda pendiente a propósito**: SP8A no está cerrado (Tarea 5 sin
  diseño aprobado), y la instrucción de sesión pide push solo con el bloque
  completo.

## Estado final (2026-09-04)

- Tareas 1–4: terminadas y verificadas, un commit por tarea.
- Tarea 5 (Trajano-GestorCaisy MVC): sin empezar; bloqueada por la aprobación
  del diseño de Superdesign. Ver `docs/ai/HANDOFF.md`.
- SP8B no se inicia hasta integrar este bloque completo (restricción del plan).
