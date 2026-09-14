# CAISY no ve borradores del tenant

Diseño validado con el usuario el 2026-09-14. Corrige una fuga de visibilidad:
las bandejas globales de CAISY muestran hoy los borradores que el Cliente o el
Trabajador todavía no enviaron.

## El problema

Las dos consultas globales arrancan sin excluir ningún estado:

- `RepositorioPedidosAlimento.ListarPaginadoCaisyAsync` (línea 79)
- `RepositorioDespachosHuevo.ListarPaginadoCaisyAsync` (línea 61)

El filtro por estado es opcional, así que la bandeja sin filtro devuelve todo,
incluidos los borradores ajenos. Peor: un gestor puede pedir
`?estado=Borrador` y obtener exactamente la lista de lo que nadie le envió. Y
el detalle por id (`/api/pedidos-alimento-caisy/{id}` y su gemelo de
despachos) abre un borrador completo por URL directa, sin validar el estado.

Un borrador es trabajo en curso del tenant. Que CAISY lo lea antes de que se
lo envíen no rompe ninguna regla de seguridad, pero sí rompe la expectativa
del usuario: lo que está a medio escribir es privado hasta que se envía.

## La regla

Un registro es visible para CAISY **desde que el tenant lo envía**:

| Agregado | Visible para CAISY desde |
|---|---|
| `PedidoAlimento` | `Solicitado` (todo menos `Borrador`) |
| `DespachoHuevo` | `Despachado` (todo menos `Borrador`) |

Los estados posteriores siguen visibles, incluidos los terminales: CAISY tiene
que poder consultar lo que rechazó y lo que ya recibió.

## Dónde se aplica

En un solo lugar por agregado: la consulta CAISY del repositorio, **antes de
cualquier otro filtro y antes del `CountAsync`**, para que el total de la
paginación cuente lo mismo que se muestra.

```csharp
var consulta = db.PedidosAlimento.Include(p => p.Detalles).AsNoTracking()
    .Where(p => p.Estado != EstadoPedidoAlimento.Borrador);
```

Puesto ahí y no dentro del `if (estado is { } e)`, un `?estado=Borrador`
devuelve la página vacía en vez de la lista completa. El filtro que elige el
usuario no puede reabrir la puerta que acabamos de cerrar.

El detalle por id responde **404, no 403**: un 403 confirmaría que el registro
existe, que es justamente lo que no queremos decirle a quien pregunta por un
id que no le corresponde. Es además el código que ya devuelve la ruta cuando
el id no existe, así que los dos casos se ven iguales desde afuera.

## El pedido devuelto

`DevolverParaCorreccion` (`PedidoAlimento.cs:147`) regresa el pedido a
`Borrador`. Con esta regla, un pedido que CAISY devolvió **desaparece de su
bandeja** hasta que el cliente lo corrija y lo reenvíe.

Es deliberado. Una bandeja de trabajo muestra lo que espera acción propia, y
CAISY ya actuó sobre ese pedido: lo devolvió con un motivo. La pelota es del
cliente. Al reenviarlo vuelve a `Solicitado` y reaparece.

El costo asumido: CAISY no puede responder «¿qué devolví que nadie me
reenvió?». Si esa necesidad aparece, se resuelve con un filtro de seguimiento
sobre el historial de transiciones, sin migración ni estado nuevo. Se evaluó
agregar un estado `Devuelto` explícito y se descartó por ahora: paga una
migración y un cambio en la máquina de estados por una necesidad que todavía
no está confirmada.

No se toca `Rechazar` (`PedidoAlimento.cs:157`), que es terminal y distinto de
la devolución. El dominio ya modela bien las dos intenciones.

## Trampa: el otro «Borrador»

`Trajano.GestorCaisy` y sus pruebas usan mucho la palabra `Borrador`, pero se
refieren al **borrador de una notificación de precios de alimento**, que es un
documento propio de CAISY que CAISY misma edita antes de publicar. No tiene
relación con este cambio y **no se toca**.

Un ejecutor que busque «Borrador» y filtre por todos lados rompería el
catálogo de precios. El cambio se limita a los dos métodos
`ListarPaginadoCaisyAsync` y a los dos handlers de detalle CAISY.

## Alcance

**Entra:** las dos consultas de listado, los dos detalles por id, y los tests.

**No entra:** la máquina de estados; los borradores de precios del MVC; la
vista del tenant, que sigue viendo los suyos; las acciones de CAISY sobre un
pedido, que ya exigen `Solicitado` y por lo tanto nunca pudieron operar sobre
un borrador.

## Tests

Dos pruebas existentes **afirman el comportamiento equivocado** y hay que
reescribirlas, no solo agregar cobertura. Las dos usan borradores como
material barato para ejercitar la paginación:

- `PedidosAlimentoEndpointsTests.cs:271` — `LaBandejaCaisyFiltraYPagina`
- `DespachosHuevoCaisyEndpointsTests.cs:290` — `LaBandejaCaisyListaFiltraPorEstadoYPagina`

Se reescriben para paginar sobre registros ya enviados. En despachos eso
obliga a despachar los tres, no solo el primero.

Pruebas nuevas, las tres visibles en rojo antes del arreglo:

1. El borrador recién creado no aparece en la bandeja CAISY sin filtro.
2. `?estado=Borrador` devuelve una página vacía.
3. El detalle por id de un borrador responde 404.

Ambos agregados, en los dos archivos correspondientes.
