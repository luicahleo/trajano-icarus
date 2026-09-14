# Trazabilidad de granja y autor, y listados filtrables y paginados

Diseño validado con el usuario el 2026-09-14. Cubre dos necesidades que
aparecieron juntas: saber **de qué granja y de quién** salió un pedido de
alimento o un despacho de huevo, y que los listados **no se vuelvan
inmanejables** cuando la base crezca.

## Objetivo

- Un pedido de alimento registra la granja de origen, igual que ya lo hace el
  despacho de huevo.
- Los dos registran quién los creó, de forma que el tenant pueda identificar a
  la persona sin que ningún dato personal salga del módulo ni llegue a CAISY.
- Los dos listados del tenant y los dos de CAISY filtran y paginan.
- Queda un patrón reusable para que el resto de los listados del sistema lo
  repitan sin volver a diseñarlo.

## El muro de arquitectura, y por qué define la solución

La primera idea fue guardar el nombre de quien crea el registro. No se puede
directamente: la prueba `GestionAvicolaNoSeReferenciaConOtrosModulos` prohíbe
que los tres assemblies de GestionAvicola dependan de `Icarus.Clientes` o
`Icarus.Identity`, y el nombre de un trabajador vive en Clientes.

El muro no es un estorbo a rodear: es la razón por la que este diseño termina
siendo mejor que el original.

**Decisión:** GestionAvicola guarda **identificadores, nunca nombres**.

- `CreadoPorTrabajadorId` (`Guid?`) — nulo cuando el registro lo creó el
  propio Cliente. Sale de `ICurrentUser.TrabajadorId`, que ya viaja en el
  token; no hay consulta cruzada en ninguna parte.
- El **nombre lo resuelve la PWA del tenant**, cruzando ese id contra
  `/clientes/{clienteId}/trabajadores`, endpoint que ya consume para su
  pantalla de Trabajadores.

Tres ventajas que no se buscaban:

1. El nombre nunca queda congelado en la base. Si un trabajador se corrige o
   se da de baja, el histórico no arrastra un nombre viejo.
2. No hace falta desnormalizar ni migrar Identity.
3. Un dato personal menos almacenado en un módulo que no lo necesita.

### Qué ve CAISY

Ninguna persona. Ve el **folio** del registro, y con eso conversa con el
cliente: «el pedido P-000123». El cliente, del otro lado, sí sabe quién lo
generó.

Conviene dejar dicho lo que **no** motivó esta decisión: la regla anti-PII del
proyecto apunta a biométricos, documentos de identidad, credenciales y
registros nominales de **acceso**. La autoría de un pedido comercial no es
nada de eso, y el usuario confirmó que CAISY ya conoce a sus clientes. La
encapsulación se eligió por arquitectura y por higiene del dato, no porque
mostrar el nombre estuviera prohibido.

## El folio

Hoy un pedido solo tiene un GUID: impronunciable por teléfono. Se agrega un
número correlativo legible.

| Decisión | Valor |
|---|---|
| Alcance | **Secuencia global por tipo**, no por cliente |
| Generación | `SEQUENCE` de SQL Server, valor por defecto en la columna |
| Formato visible | `P-000123` para pedidos, `D-000045` para despachos |
| Persistencia del formato | **No se persiste.** Se compone al proyectar el DTO |

Global y no por tenant porque generar el siguiente número por cliente obliga a
serializar escrituras del mismo tenant, y porque CAISY necesitaría siempre el
par cliente + número para identificar uno solo. Con una secuencia global, el
folio es único en todo el sistema y la base lo resuelve sin bloqueos.

El prefijo no se guarda: una columna `Numero` entera basta, y el `P-`/`D-` es
presentación. Guardar el texto compuesto duplicaría el dato y abriría la
puerta a que diverja.

## La granja

`DespachoHuevo` **ya tiene** `GranjaId` y `CreadoPor` en el dominio: ahí no
hay campo que crear, solo que exponer. `PedidoAlimento` no tiene granja: es la
columna nueva.

`GranjaId` es obligatoria en el pedido. El comando **no la recibe del
frontend**: el handler resuelve la granja activa del cliente, porque hoy el
sistema garantiza una granja activa por cliente. El día que haya varias, el
formulario gana un selector y el comando un parámetro, sin tocar el esquema.

Los registros existentes son datos de prueba y se descartan: la base de
desarrollo se recrea. No hay backfill.

## Filtros y paginación

### Estado actual

| Listado | Pagina hoy | Filtra hoy |
|---|---|---|
| Pedidos de alimento — CAISY | sí (1–100) | estado, presentación |
| Despachos de huevo — CAISY | sí | estado |
| Pedidos de alimento — tenant | **no** | no |
| Despachos de huevo — tenant | **no** | no |

Los dos del tenant devuelven la colección entera. Son los que más crecen: un
tenant activo genera recogidas y pedidos todos los días.

### Filtros por listado

| Filtro | Tenant | CAISY |
|---|---|---|
| Granja | sí | sí |
| Rango de fechas (desde/hasta) | sí | sí |
| Estado | sí | sí (ya existe) |
| Autor | **sí** | no |
| Folio | sí | sí |
| Presentación | sí (pedidos) | sí (ya existe) |

El autor no aparece en CAISY, coherente con mostrarle solo el folio.

### El patrón reusable

Un contrato único en `BuildingBlocks.Application` que el resto de los listados
va a repetir:

- `PeticionPaginada` — página y tamaño, con los mismos límites que ya usa
  `ListarPedidosCaisyValidator`: página ≥ 1, tamaño entre 1 y 100.
- `Pagina<T>` — items, total, número de página y tamaño.

Que el criterio 1–100 ya exista y esté probado es la razón de copiarlo en vez
de inventar otro: el sistema queda con una sola respuesta a «cuántos por
página como máximo».

En la PWA, un componente de barra de filtros y otro de paginación, ambos
reusables, para que el siguiente listado sea trabajo de ensamblado.

## Alcance

**Entra:** las dos columnas nuevas del pedido más el folio en ambos agregados;
el contrato común de paginación; filtros y paginación en los cuatro listados
(dos del tenant, dos de CAISY); los componentes reusables de la PWA; la
semilla poblando los campos nuevos.

**No entra:** el resto de los listados del sistema. Quedan inventariados abajo
para planes siguientes, que solo tendrán que repetir el patrón.

**No entra tampoco:** mostrar nombres de personas a CAISY; desnormalizar
nombres en GestionAvicola; numeración por tenant; filtros por granja en
listados que no son de pedido ni de despacho.

## Inventario para los planes siguientes

Listados que hoy devuelven la colección completa, en orden sugerido por cuánto
crecen:

1. `/galpones/{id}/produccion` — recogidas diarias, el que más crece
2. `/galpones/{id}/mortalidad` — ídem
3. `/vacunacion/tareas` y `/galpones/{id}/vacunacion/tareas`
4. `/pedidos-alimento/notificaciones` y `/despachos-huevo/notificaciones`
5. `/precios-alimentos` y `/precios-huevo` — crecen por publicación
6. `/clientes` y `/clientes/{id}/trabajadores`
7. `/usuarios-caisy`
8. `/granjas` y `/granjas/{id}/galpones` — los que menos crecen
9. `/balance-alimentos`

## Riesgo asumido

El listado del tenant resuelve nombres en el cliente, no en el servidor. Si la
PWA muestra una página de pedidos de veinte trabajadores distintos, necesita
la lista de trabajadores cargada. Es una sola consulta cacheada por TanStack
Query, no una por fila; y esa lista ya se pide en otra pantalla del mismo rol.
Si un id no aparece en la lista (trabajador dado de baja), la UI muestra el
folio y «autor no disponible», nunca un error.
