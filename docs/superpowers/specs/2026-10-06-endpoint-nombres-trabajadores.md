# Endpoint de solo nombres de trabajadores para pantallas compartidas

## Objetivo

Un Trabajador con la funcionalidad `PedidoAlimento` o `DespachoHuevo` recibe
403 al abrir `/pedidos` o `/despachos` en la consola de red, porque esas
páginas resuelven el nombre del autor de cada registro consultando
`GET /clientes/{clienteId}/trabajadores` — un endpoint reservado al rol
Cliente. La página no se rompe (cae a "Autor no disponible" y oculta el
filtro "Autor"), pero el Trabajador nunca ve quién despachó o pidió, ni
cuando el autor fue él mismo, y la consola acumula 403 en cada reintento.

## Diagnóstico

- `ClientesEndpoints.cs:61-63` expone `GET /{clienteId}/trabajadores` bajo la
  política `GestionTrabajadores`, que exige rol `Cliente`
  (`Icarus.Identity.Infrastructure/DependencyInjection.cs:77-78`). Devuelve
  `TrabajadorResumen` completo: `DocumentoIdentidad` (PII), `Cargo`,
  `FechaIngreso`, `FechaCese` y `Funcionalidades` — datos que un compañero
  Trabajador no debe ver de otro.
- `web/src/features/pedidos-alimento/PedidosAlimentoPage.tsx:84-88` y
  `web/src/features/despacho-huevo/DespachosHuevoPage.tsx:76-79` llaman a ese
  mismo endpoint vía `listarTrabajadores(clienteId)` para cruzar
  `creadoPorTrabajadorId`/`autorId` contra un nombre, sin distinguir el rol de
  quien mira la página. Ambas rutas están gateadas por
  `RequiereFuncionalidad`, no por rol (`router.tsx:211-220, 259-269`), así que
  un Trabajador sí llega a esas páginas.
- El filtro de tenant de EF Core (`ClientesDbContext.cs:34-35`,
  `_clienteIdActual` desde `ICurrentUser.ClienteId`) ya acota
  `ListarPorClienteAsync` a la empresa del llamante sin importar su rol — el
  único bloqueo real es la política del endpoint, no el aislamiento de datos.
- `control-acceso` usa el mismo endpoint (`listarTrabajadoresDeAcceso`), pero
  sus tres páginas están gateadas por `RequiereRol roles={['Cliente']}`
  (`router.tsx:114-148`): un Trabajador nunca las alcanza, así que no
  necesitan cambio.

## Corrección

Agregar un endpoint nuevo, de solo lectura y de alcance mínimo, que cualquier
usuario autenticado de la empresa (Cliente o Trabajador) pueda consultar para
resolver nombres — sin tocar el endpoint existente ni sus consumidores
actuales (`TrabajadoresPage.tsx`, las tres páginas de `control-acceso`), que
siguen siendo exclusivos de Cliente.

- **Backend** (`Icarus.Clientes.Application/Trabajadores`): nuevo
  `ListarNombresTrabajadoresQuery(Guid ClienteId) : IRequest<IReadOnlyList<TrabajadorNombreResumen>>`
  y su handler, reutilizando `IRepositorioTrabajadores.ListarPorClienteAsync`
  (ya tenant-safe) y proyectando solo `Id` y `Nombre`. No se toca el
  repositorio ni `ListarTrabajadoresQuery`/`TrabajadorResumen` existentes.
- **Endpoint** (`ClientesEndpoints.cs`): `GET /clientes/{clienteId}/trabajadores/nombres`,
  bajo una política nueva `ConsultaNombresTrabajadores` que acepta rol
  `Cliente` o `Trabajador` (`RequireClaim` con ambos valores).
- **Política** (`PoliticasAutorizacion.cs` +
  `Icarus.Identity.Infrastructure/DependencyInjection.cs`): se agrega la
  constante y su registro; no se modifica `GestionTrabajadores`.
- **Frontend**: nuevo tipo `TrabajadorNombreResumen { id, nombre }` en
  `lib/tipos.ts` y `listarNombresTrabajadores(clienteId)` en
  `features/trabajadores/api.ts`. `PedidosAlimentoPage.tsx` y
  `DespachosHuevoPage.tsx` cambian su import y su `queryFn` para usar esta
  función en vez de `listarTrabajadores`; el resto de cada componente no
  cambia (ambos solo leen `.id` y `.nombre`).

## Fuera de alcance

- No se toca `TrabajadoresPage.tsx` ni las páginas de `control-acceso`: son
  exclusivas de Cliente y siguen usando el endpoint completo.
- No se agrega paginación ni búsqueda al endpoint nuevo: el historial de
  trabajadores de una empresa es una lista acotada, igual que el endpoint
  existente que ya consume sin paginar.
- No se cambia el aislamiento de tenant: ya es correcto vía filtro global de
  EF Core.

## Criterios de aceptación

1. Un Trabajador con funcionalidad `PedidoAlimento` o `DespachoHuevo` recibe
   200 (no 403) al consultar `GET /clientes/{clienteId}/trabajadores/nombres`
   de su propia empresa, y la respuesta trae únicamente `id` y `nombre` de
   cada trabajador activo (sin documento, cargo ni funcionalidades).
2. Un Trabajador de otra empresa que intenta leer el `clienteId` ajeno sigue
   recibiendo su propia lista (filtrada por tenant), nunca la del otro
   cliente.
3. `/pedidos` y `/despachos` abiertos como Trabajador ya no generan 403 en
   consola por este motivo, y muestran el nombre real del autor (incluido el
   propio) en vez de "Autor no disponible".
4. El endpoint `GET /clientes/{clienteId}/trabajadores` original sigue
   exigiendo rol Cliente sin cambios.
