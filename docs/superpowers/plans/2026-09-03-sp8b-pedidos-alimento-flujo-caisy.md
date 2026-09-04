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

- [ ] Rojo: un pedido nace `Borrador`, pertenece al tenant y acepta solo una
  presentación; líneas duplicadas/tipos incompatibles fallan.
- [ ] Rojo: bolsa entera; granel entero, mínimo 2 t por línea y 6 t total al
  enviar; equivalencias 1 bolsa = 40 kg y 1 t = 25 equivalentes.
- [ ] Rojo: editar/desactivar solo en borrador; CAISY no altera líneas.
- [ ] Rojo: transiciones válidas e inválidas de `EnviarACaisy`,
  `DevolverParaCorreccion`, `Rechazar`, `Aceptar` y
  `ActualizarEntregaEstimada`, con motivo/fecha obligatorios.
- [ ] Verde: métodos explícitos, sin setter/goto genérico, y `rowversion`.
- [ ] Comando: `dotnet test Icarus/tests/Icarus.UnitTests --filter PedidoAlimento`.
- [ ] Commit previsto: `feat(avicola): modelar pedidos de alimento`.

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

- [ ] Rojo: crear/editar/desactivar respeta tenant y permite a Cliente o
  Trabajador autorizado actuar sobre borradores compartidos.
- [ ] Rojo: enviar congela publicación/precios vigentes y fecha Bolivia en una
  transacción; falta de precio deja el borrador intacto.
- [ ] Rojo: máximo configurable inicial 3 por cliente/semana ISO; dos envíos
  concurrentes no superan el límite y un reenvío no suma otro cupo.
- [ ] Implementar opciones validadas al arrancar, consulta bloqueable/índice y
  traducción uniforme de concurrencia a 409.
- [ ] Generar migración `PedidosAlimentoInicial` y verificar filtros tenant sin
  `.Value` sobre `Guid?`.
- [ ] Commit previsto: `feat(avicola): crear y enviar pedidos de alimento`.

## Tarea 3 — Notificaciones internas persistentes

**Crear/modificar:**

- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Domain/NotificacionInterna.cs`
- `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/Notificaciones/`
- configuración, repositorio y migración en Infrastructure.
- `Icarus/tests/Icarus.UnitTests/GestionAvicola/NotificacionesInternasTests.cs`

- [ ] Rojo: enviar/reenviar crea una notificación CAISY una sola vez; devolver,
  rechazar, aceptar o cambiar fecha crea una por destinatario tenant.
- [ ] Rojo: listar no leídas, marcar propia como leída e impedir acceso cruzado.
- [ ] Persistir tipo, pedido, destinatario técnico y metadatos estructurados; el
  texto se compone en UI y los motivos no se duplican ni se registran en Seq.
- [ ] Incluir un endpoint de sondeo con ETag/`since`; SignalR queda opcional si
  no reduce complejidad, sin convertirlo en fuente de verdad.
- [ ] Commit previsto: `feat(avicola): notificar cambios de pedidos`.

## Tarea 4 — API de pedidos y procesamiento CAISY

**Crear/modificar:**

- `Icarus/src/Host/Icarus.Host/Endpoints/PedidosAlimentoEndpoints.cs`
- `Icarus/src/Clientes/Icarus.Clientes.Domain/Funcionalidades.cs`
- `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesModulos.cs`
- `Icarus/src/Clientes/Icarus.Clientes.Domain/FuncionalidadesTrabajador.cs`
- autorización de Clientes/Identity correspondiente.
- `Icarus/tests/Icarus.IntegrationTests/PedidosAlimentoEndpointsTests.cs`

- [ ] Rojo: CRUD borrador, envío, detalle/historial, listado tenant y bandeja
  global CAISY con filtros/paginación.
- [ ] Rojo: devolver/rechazar exige motivo; aceptar exige fecha válida;
  actualización ETA solo en `Aceptado`; segunda transición devuelve 409.
- [ ] Rojo: 403 sin función y 404 genérico para ids de otro tenant.
- [ ] Añadir `PedidoAlimento` con un bit nuevo sin renumerar flags existentes.
- [ ] Comando: `dotnet test Icarus/tests/Icarus.IntegrationTests --filter PedidosAlimento`.
- [ ] Commit previsto: `feat(api): exponer flujo inicial de pedidos de alimento`.

## Tarea 5 — PWA Trajano-Icarus

**Crear/modificar:**

- `web/src/features/pedidos-alimento/` (API, tipos, lista, detalle y formulario).
- `web/src/app/router.tsx`, `web/src/app/navegacion.tsx`,
  `web/src/app/paginasDiferidas.tsx`, `web/src/lib/tipos.ts`.
- `web/src/features/trabajadores/TrabajadoresPage.tsx`.
- tests colocados junto a cada página/componente.

- [ ] Rojo: Cliente/Trabajador autorizado ve la bandeja tenant compartida;
  crea y edita cantidades enteras, borra borrador y envía con resumen de precio.
- [ ] Mostrar publicación vigente, recomendación por edades de galpones sin
  obligar galpón/cantidad, cupo semanal y estados/historial.
- [ ] Devolución reabre el mismo borrador y muestra el motivo; rechazo queda
  terminal; aceptación/ETA aparecen en detalle y notificaciones.
- [ ] Asegurar que la feature falla de forma explícita sin red y no entra en
  `offline.ts`, IndexedDB ni precalentado.
- [ ] Comandos: `npm test -- --run pedidos-alimento` y `npm run typecheck` desde `web/`.
- [ ] Commit previsto: `feat(web): gestionar pedidos de alimento`.

## Tarea 6 — Bandeja CAISY MVC

**Crear/modificar:** controladores, vistas, cliente API y pruebas bajo
`Icarus/src/Apps/Trajano.GestorCaisy/` y
`Icarus/tests/Trajano.GestorCaisy.Tests/`.

- [ ] Tras aprobación visual, probar y construir bandeja paginada, filtros,
  detalle congelado e historial.
- [ ] Implementar devolver para corrección, rechazar, aceptar y cambiar ETA con
  confirmaciones, validación y protección contra doble envío.
- [ ] Añadir campana/contador y marcado de notificaciones.
- [ ] Commit previsto: `feat(gestor-caisy): procesar pedidos entrantes`.

## Cierre SP8B

- [ ] Suites dirigidas y completas, `./verify.ps1`, `git diff --check` y lectura del diff.
- [ ] Actualizar glosario, `AGENTS.md`, adaptadores y este plan.
- [ ] Commit final del bloque y push a `develop` solo con la puerta verde.
