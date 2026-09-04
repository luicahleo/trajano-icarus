# SP8B — Pedido de alimento hasta aceptación

**Objetivo:** permitir crear, compartir, editar, borrar y enviar pedidos tenant,
y procesarlos en CAISY mediante devolución, rechazo o aceptación.

**Spec:** `docs/superpowers/specs/2026-09-03-sp8-pedidos-alimento-integracion-caisy-design.md`.

**Dependencia:** SP8A integrado y con una publicación de precios vigente.

## Reglas de ejecución

- TDD estricto y commits por tarea; Docker para integración.
- No implementar offline: sin IndexedDB, service worker ni cola para pedidos.
- Concurrencia optimista e idempotencia para reintentos/doble clic.
- Solo borradores se editan o desactivan. Los borradores no consumen cupo.
- `FechaPedido` y precios se fijan en servidor al enviar, en fecha Bolivia.
- Seq recibe solo ids técnicos, estados, presentación y conteos.

## Tarea 1 — Agregado y máquina de estados inicial

**Crear:**

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/PedidoAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/DetallePedidoAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/EstadoPedidoAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/TransicionPedidoAlimento.cs`
- `Icarus/tests/Icarus.UnitTests/GestionAvicola/PedidoAlimentoTests.cs`

- [x] Rojo: un pedido nace `Borrador`, pertenece al tenant y acepta solo una
  presentación; líneas duplicadas/tipos incompatibles fallan.
  (CS0246 con los tipos por crear; luego 8 fallos por transición sin asignar
  estado, corregida en el agregado)
- [x] Rojo: bolsa entera; granel entero, mínimo 2 t por línea y 6 t total al
  enviar; equivalencias 1 bolsa = 40 kg y 1 t = 25 equivalentes.
- [x] Rojo: editar/desactivar solo en borrador; CAISY no altera líneas.
- [x] Rojo: transiciones válidas e inválidas de `EnviarACaisy`,
  `DevolverParaCorreccion`, `Rechazar`, `Aceptar` y
  `ActualizarEntregaEstimada`, con motivo/fecha obligatorios.
- [x] Verde: métodos explícitos, sin setter/goto genérico, y `rowversion`.
  18/18 con `dotnet test --filter PedidoAlimento` y puerta completa verde.
  Decisión registrada: «tipos compatibles» significa que un pedido no mezcla
  fases de levante (Preiniciador a Finalizador) con postura (PosturaUno,
  PosturaDos); el spec no define otra matriz.
- [x] Comando: `dotnet test Icarus/tests/Icarus.UnitTests --filter PedidoAlimento`.
- [x] Commit previsto: `feat(avicola): modelar pedidos de alimento`.

## Tarea 2 — Persistencia, precios congelados y límite semanal

**Crear/modificar:**

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/PedidosAlimento/`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionPedidoAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionDetallePedidoAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/ConfiguracionTransicionPedidoAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioPedidosAlimento.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Persistencia/GestionAvicolaDbContext.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Migrations/`
- `Icarus/src/Host/Icarus.Host/appsettings.json`
- tests `PedidosAlimentoHandlerTests.cs`.

- [x] Rojo: crear/editar/desactivar respeta tenant y permite a Cliente o
  Trabajador autorizado actuar sobre borradores compartidos.
  (CS0234/CS0246 por los tipos por crear)
- [x] Rojo: enviar congela publicación/precios vigentes y fecha Bolivia en una
  transacción; falta de precio deja el borrador intacto.
- [x] Rojo: máximo configurable inicial 3 por cliente/semana ISO; dos envíos
  concurrentes no superan el límite y un reenvío no suma otro cupo.
  (unidad: cupo agotado da 409 sin guardar, el reintento no consulta cupo; la
  serialización en la base usa UPDLOCK + HOLDLOCK y se verificará con
  integración en la Tarea 4)
- [x] Implementar opciones validadas al arrancar, consulta bloqueable/índice y
  traducción uniforme de concurrencia a 409.
- [x] Generar migración `PedidosAlimentoInicial` y verificar filtros tenant sin
  `.Value` sobre `Guid?`.
  (tablas `pedidos_alimentos`, `detalles_pedidos_alimentos` y
  `transiciones_pedidos_alimentos`, índice `ClienteId_FechaPedido`)
- [x] Commit previsto: `feat(avicola): crear y enviar pedidos de alimento`.
  (15/15 dirigidos, puerta completa verde: 334 unit + 89 integración)

## Tarea 3 — Notificaciones internas persistentes

**Crear/modificar:**

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionInterna.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Notificaciones/`
- configuración, repositorio y migración en Infrastructure.
- `Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesInternasTests.cs`

- [x] Rojo: enviar/reenviar crea una notificación CAISY una sola vez; devolver,
  rechazar, aceptar o cambiar fecha crea una por destinatario tenant.
  (CS0234/CS0246 por los tipos por crear; luego CS4008 por await sobre un
  método void en la verificación, corregido en el test)
- [x] Rojo: listar no leídas, marcar propia como leída e impedir acceso cruzado.
- [x] Persistir tipo, pedido, destinatario técnico y metadatos estructurados; el
  texto se compone en UI y los motivos no se duplican ni se registran en Seq.
  (tabla `notificaciones_internas`, alcance explícito en el repositorio sin
  filtro de tenant porque la bandeja global usa ClienteId nulo)
- [ ] Incluir un endpoint de sondeo con ETag/`since`; SignalR queda opcional si
  no reduce complejidad, sin convertirlo en fuente de verdad.
  (se implementa en la Tarea 4 junto con los endpoints y sus políticas, que
  son su dependencia)
- [x] Commit previsto: `feat(avicola): notificar cambios de pedidos`.
  (10/10 dirigidos, puerta completa verde: 344 unit + 89 integración)

## Tarea 4 — API de pedidos y procesamiento CAISY

**Crear/modificar:**

- `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs`
- `Icarus/src/Clientes/Icarus.Clientes.Domain/Funcionalidades.cs`
- `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesModulos.cs`
- `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesTrabajador.cs`
- autorización de Clientes/Identity correspondiente.
- `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`

- [x] Rojo: CRUD borrador, envío, detalle/historial, listado tenant y bandeja
  global CAISY con filtros/paginación.
  (varios: el binding exigía pagina/tamanoPagina y faltaba exponer
  notificacionPreciosAlimentosId en la línea del detalle)
- [x] Rojo: devolver/rechazar exige motivo; aceptar exige fecha válida;
  actualización ETA solo en `Aceptado`; segunda transición devuelve 409.
  Corrección hallada: las transiciones nuevas descubiertas por DetectChanges
  con Guid ya asignado se marcaban Modified y explotaba el rowversion
  (DbUpdateConcurrency en el primer envío). Arreglado haciendo nacer la
  transición con Id vacío para que EF la registre Added, mismo patrón que
  AgregarDetalle del catálogo de precios. También: el reenvío se distingue por
  FechaPedido ya fijado, porque el historial no se carga en el comando de
  envío.
- [x] Rojo: 403 sin función y 404 genérico para ids de otro tenant.
- [x] Añadir `PedidoAlimento` con un bit nuevo sin renumerar flags existentes.
  (FuncionalidadesTests actualizado al catálogo ampliado; asignable a
  trabajadores)
- [x] Endpoint de sondeo de notificaciones con ETag e If-None-Match (304) y
  corte `since`, en el grupo del tenant y en el de CAISY (pendiente de la
  Tarea 3).
- [x] Comando: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter
  PedidosAlimento` (6/6; puerta completa verde: 346 unit + 95 integración).
- [x] Commit previsto: `feat(api): exponer flujo inicial de pedidos de alimento`.

## Tarea 5 — PWA Trajano-Icarus

**Crear/modificar:**

- `web/src/features/pedidos-alimento/` (API, tipos, lista, detalle y formulario).
- `web/src/app/router.tsx`, `web/src/app/navegacion.tsx`,
  `web/src/app/paginasDiferidas.tsx`, `web/src/lib/tipos.ts`.
- `web/src/features/trabajadores/TrabajadoresPage.tsx`.
- tests colocados junto a cada página/componente.

- [x] Rojo: Cliente/Trabajador autorizado ve la bandeja tenant compartida;
  crea y edita cantidades enteras, borra borrador y envía con resumen de precio.
  (20/20 tests de la feature tras corregir mocks con cuerpo en 204, textos
  partidos de MUI y binding de la presentación en el formulario)
- [x] Mostrar publicación vigente, recomendación por edades de galpones sin
  obligar galpón/cantidad, cupo semanal y estados/historial.
  (se añadieron los endpoints tenant `precios-vigentes` y `cupo` con su prueba
  de integración, porque el catálogo de precios solo estaba expuesto a CAISY)
- [x] Devolución reabre el mismo borrador y muestra el motivo; rechazo queda
  terminal; aceptación/ETA aparecen en detalle y notificaciones.
  (el detalle muestra el motivo del historial y la bandeja compone el mensaje
  de cada notificación; la marca de lectura es idempotente)
- [x] Asegurar que la feature falla de forma explícita sin red y no entra en
  `offline.ts`, IndexedDB ni precalentado.
  (api.ts usa solo `peticion` de `lib/http`, sin `conCacheLectura`)
- [x] Comandos: `npm test -- --run pedidos-alimento` (20/20) y
  `npm run typecheck` desde `web/`, más lint, build y suite completa (244).
- [x] Commit previsto: `feat(web): gestionar pedidos de alimento`.

## Tarea 6 — Bandeja CAISY MVC

**Crear/modificar:** controladores, vistas, cliente API y pruebas bajo
`Icarus/src/Apps/Trajano.GestorCaisy/` y
`Icarus/tests/Trajano.GestorCaisy.Tests/`.

- [x] Tras aprobación visual, probar y construir bandeja paginada, filtros,
  detalle congelado e historial.
  (sin Superdesign, decisión del usuario registrada en SP8A; la bandeja sigue
  el lenguaje visual existente de Precios: tarjeta, tabla, chips y formularios)
- [x] Implementar devolver para corrección, rechazar, aceptar y cambiar ETA con
  confirmaciones, validación y protección contra doble envío.
  (confirmaciones GET + POST con antiforgery, motivo obligatorio de 500,
  fecha desde hoy validada en el controlador y en la API, 409 mostrado como
  mensaje)
- [x] Añadir campana/contador y marcado de notificaciones.
  (panel de novedades con contador en la bandeja y marcado idempotente)
- [x] Commit previsto: `feat(gestor-caisy): procesar pedidos entrantes`.
  (90/90 en Trajano.GestorCaisy.Tests y puerta completa verde)

## Cierre SP8B

- [ ] Suites dirigidas y completas, `./verify.ps1`, `git diff --check` y lectura del diff.
- [ ] Actualizar glosario, `AGENTS.md`, adaptadores y este plan.
- [ ] Commit final del bloque y push a `develop` solo con la puerta verde.
