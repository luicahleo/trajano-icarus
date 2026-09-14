# El crédito de huevo nace al recibir — los catorce días son referencia — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-09-14-credito-huevo-sin-corte-de-catorce-dias-design.md`

**Goal:** El saldo de crédito por despachos de huevo deja de esperar catorce días: suma todo el huevo que CAISY ya recibió. Los catorce días sobreviven como referencia informativa visible para el Cliente, en una línea secundaria junto al saldo.

**Architecture:** Un solo corte real, en `RepositorioBalanceCreditoHuevo`: el filtro de fecha sale del término de ingresos y reaparece, invertido, en un método nuevo que devuelve cuánto se recibió dentro de la ventana. Ese dato sube por el puerto, el handler y el DTO hasta los dos formularios donde el Cliente ya ve su crédito. Nadie gana ni pierde visibilidad, y ninguna validación nace ni muere.

**Tech Stack:** .NET / MediatR / EF Core (backend), React + TanStack Query + MUI (PWA), xUnit + NSubstitute + Testcontainers.MsSql (tests backend), Vitest + Testing Library (tests frontend).

## Global Constraints

- Todo texto de negocio (mensajes, comentarios, nombres de test) en español correcto, con acentos, UTF-8 sin BOM. **Español neutro, sin voseo**: nunca «confirmá», «podés», «mirá».
- TDD real: cada test se corre **en rojo** antes de escribir el código que lo pone en verde. Un test que pasa desde antes no prueba nada.
- No se toca el camino de CAISY (`ObtenerCreditoHuevoDePedidoCaisyHandler` y su endpoint): sigue inerte detrás del gate de rol `Cliente`. Hereda la fórmula nueva por consumir el mismo repositorio, y eso es todo.
- No se agrega ninguna validación, bloqueo, advertencia ni notificación basada en el saldo.
- Ningún commit usa `--no-verify`. Un commit por task, con el test dirigido en verde.
- `./verify.ps1` completo antes del push final. Docker corriendo: los tests de integración usan Testcontainers.MsSql.
- La rama de trabajo es `develop`; commits directos, sin PR.
- **No hay migración.** El saldo se calcula por consulta y no hay tabla de saldo persistida; ningún cambio toca `Domain/`, `Persistencia/` ni `Migrations/`. Si al terminar `dotnet ef migrations has-pending-model-changes` reporta cambios, algo se hizo mal.

---

## Mapa de archivos

| Archivo | Acción |
|---|---|
| `Icarus/tests/Icarus.IntegrationTests/BalanceCreditoHuevoTests.cs` | Modificar: 2 tests se invierten, 1 se renombra, 1 nuevo |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/PuertoCreditoHuevo.cs` | Modificar: método nuevo en el puerto, constante renombrada |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/Repositorios/RepositorioBalanceCreditoHuevo.cs` | Modificar: fuera el filtro de fecha del saldo, método nuevo |
| `Icarus/tests/Icarus.UnitTests/GestionAvicola/ObtenerBalanceCreditoHuevoHandlerTests.cs` | Modificar: 2 tests tocados, 1 nuevo |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Application/CreditoHuevo/ComandosCreditoHuevo.cs` | Modificar: DTO y handler con el dato nuevo |
| `Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure/SemillaDesarrolloAvicola.cs` | Modificar: solo comentarios de cifras |
| `Icarus/tests/Icarus.IntegrationTests/SemillaDesarrolloAvicolaTests.cs` | Modificar: saldo de `ConMovimiento` + test nuevo |
| `web/src/features/despacho-huevo/api.ts` | Modificar: 2 campos en `BalanceCreditoHuevo` |
| `web/src/features/despacho-huevo/ReferenciaCreditoReciente.tsx` | **Crear**: la línea de referencia |
| `web/src/features/pedidos-alimento/PedidoFormularioPage.tsx` | Modificar: renderiza la línea |
| `web/src/features/despacho-huevo/DespachoHuevoFormularioPage.tsx` | Modificar: ídem |
| `web/src/features/pedidos-alimento/PedidoFormularioPage.test.tsx` | Modificar: 2 tests nuevos |
| `web/src/features/despacho-huevo/DespachoHuevoFormularioPage.test.tsx` | Modificar: 1 test nuevo |
| `consultasPruebasSql/saldo-credito-huevo-cliente-demo.sql` | Modificar: fórmula nueva |
| `docs/ai/HANDOFF.md` | Modificar: cierre (**local, no versionado** — está en `.gitignore`; no intentar comitearlo) |

---

## Task 1 — El saldo deja de esperar catorce días

**Archivos:** `BalanceCreditoHuevoTests.cs`, `PuertoCreditoHuevo.cs`, `RepositorioBalanceCreditoHuevo.cs`

### Paso 1.1 — Tests en rojo

- [ ] Agregar el helper al lado de `SaldoDeAsync` en `BalanceCreditoHuevoTests.cs`:

```csharp
    private async Task<decimal> RecibidoRecienteDeAsync(Guid clienteId)
    {
        using var alcance = _factory.Services.CreateScope();
        var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorioBalanceCreditoHuevo>();
        return await repositorio.ObtenerRecibidoRecienteAsync(clienteId, FechasNegocio.Hoy());
    }
```

- [ ] Reemplazar el test `DespachoRecibidoHaceMenosDe14DiasNoSuma` **completo** por:

```csharp
    // Corrección 2026-09-14 (segunda): el crédito nace cuando CAISY recibe el
    // huevo, no catorce días después. Los catorce días describen el ritmo con
    // que CAISY liquida, que en la práctica varía; nunca fueron condición para
    // que el dinero exista. Este test afirmaba lo contrario.
    [Fact]
    public async Task DespachoRecibidoHaceMenosDe14DiasSumaYSeReportaComoReciente()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await SembrarAsync(DespachoRecibido(clienteId, actorId, FechasNegocio.Hoy().AddDays(-5)));

        var saldo = await SaldoDeAsync(clienteId);
        var reciente = await RecibidoRecienteDeAsync(clienteId);

        Assert.Equal(390 * 12.50m, saldo);
        // El mismo monto, en las dos cifras: el dinero cuenta Y se señala como
        // recién recibido. No son términos que se resten entre sí.
        Assert.Equal(390 * 12.50m, reciente);
    }
```

- [ ] Agregar a `DespachoRecibidoHaceMasDe14DiasSumaAlSaldo`, después del assert existente:

```csharp
        // Fuera de la ventana de referencia: suma al saldo y no se señala.
        Assert.Equal(0m, await RecibidoRecienteDeAsync(clienteId));
```

- [ ] Reemplazar `UnAjusteDeCreditoSumaAlSaldoSinDesfase` **completo** por:

```csharp
    [Fact]
    public async Task UnAjusteDeCreditoSumaAlSaldo()
    {
        var clienteId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        // El despacho es de hoy. Antes de la corrección del 2026-09-14 no
        // aportaba nada y el test aislaba el ajuste; ahora aporta, y el
        // esperado incluye las dos partes.
        var despacho = DespachoRecibido(clienteId, actorId, FechasNegocio.Hoy());
        var ajuste = new AjusteCreditoHuevo(
            clienteId, despacho.Id, Guid.NewGuid(), Guid.NewGuid(), 150m, "Corrección de precio", actorId);
        await SembrarAsync(despacho, ajuste);

        var saldo = await SaldoDeAsync(clienteId);

        Assert.Equal(390 * 12.50m + 150m, saldo);
        // El ajuste no entra en el reciente: no es huevo recibido, es una
        // corrección de un error anterior.
        Assert.Equal(390 * 12.50m, await RecibidoRecienteDeAsync(clienteId));
    }
```

- [ ] Actualizar el comentario de cabecera de la clase: donde dice «se suman los despachos de huevo Recibido con recepción de hace más de 14 días», debe decir «se suman los despachos de huevo Recibido, sin importar hace cuánto (corrección 2026-09-14)».

- [ ] Correr y **ver rojo**. Primero no compila (`ObtenerRecibidoRecienteAsync` no existe); tras el paso 1.2 los asserts fallan por las cifras:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~BalanceCreditoHuevoTests"
```

### Paso 1.2 — El puerto

- [ ] En `PuertoCreditoHuevo.cs`, reemplazar el comentario de la interfaz y agregar el método:

```csharp
// Crédito por huevos despachados (spec SP9, corregido el 2026-09-14):
// disponible desde que CAISY confirma la recepción, sin espera. Sin tabla de
// saldo persistida: se calcula por consulta (suma de ingresos menos egresos
// reales más ajustes).
public interface IRepositorioBalanceCreditoHuevo
{
    Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default);

    // Cuánto del saldo se recibió dentro de la ventana de referencia
    // (DiasReferenciaCredito). Dato informativo que acompaña al saldo, NO un
    // término que se le reste: por construcción vale entre cero y el total de
    // ingresos. Existe porque el ritmo de liquidación de CAISY le sirve al
    // Cliente para decidir cuánto pedir, aunque no condicione nada.
    Task<decimal> ObtenerRecibidoRecienteAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default);

    // ... ObtenerAjustesAsync sin cambios
}
```

- [ ] Renombrar la constante y documentar por qué:

```csharp
public static class ReglasCreditoHuevo
{
    // Ventana de referencia, NO un plazo de disponibilidad: el crédito existe
    // desde la recepción. Catorce días es el ritmo habitual con que CAISY
    // liquida, que en la práctica varía. Se usa solo para señalar qué parte
    // del saldo es reciente (corrección 2026-09-14; antes se llamaba
    // DiasDisponibilidadCredito y filtraba el saldo).
    public const int DiasReferenciaCredito = 14;
}
```

### Paso 1.3 — El repositorio

- [ ] Agregar `using System.Linq.Expressions;` al encabezado.

- [ ] Reemplazar el cuerpo de `ObtenerSaldoDisponibleAsync` y agregar los dos métodos nuevos:

```csharp
    public async Task<decimal> ObtenerSaldoDisponibleAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        // Corrección 2026-09-14: sin filtro de fecha. El crédito nace cuando
        // CAISY recibe el huevo. El filtro que vivía acá ocultaba dinero que
        // ya era del cliente, y no protegía ninguna regla: el saldo no valida
        // nada. Sobrevive invertido en ObtenerRecibidoRecienteAsync, como
        // referencia visible.
        var ingresos = await SumarIngresosAsync(clienteId, null, cancellationToken);

        var recibidoReal = await db.PedidosAlimento
            .Where(p => p.ClienteId == clienteId
                && (p.Estado == EstadoPedidoAlimento.RecibidoConforme
                    || p.Estado == EstadoPedidoAlimento.RecibidoConDiferencias))
            .Select(p => p.Recepcion!.TotalRecibido)
            .SumAsync(cancellationToken);

        // Ajustes de corrección (spec SP9D): compensan un error real.
        var ajustes = await db.AjustesCreditoHuevo
            .Where(a => a.ClienteId == clienteId)
            .SumAsync(a => a.Monto, cancellationToken);

        // El saldo es la cuenta real. No entra el alimento pedido y todavía no
        // recibido: ese alimento no llegó, no consumió crédito, y el pedido
        // todavía puede ser rechazado o devuelto por CAISY.
        return ingresos - recibidoReal + ajustes;
    }

    public Task<decimal> ObtenerRecibidoRecienteAsync(
        Guid clienteId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        var fechaCorte = hoy.AddDays(-ReglasCreditoHuevo.DiasReferenciaCredito);
        return SumarIngresosAsync(clienteId, d => d.FechaRecepcion > fechaCorte, cancellationToken);
    }

    // Una sola copia de la aritmética de ingresos, que es la parte frágil de
    // este archivo (ver comentario de clase): el saldo la usa sin filtro de
    // fecha y el reciente con uno. Duplicarla invitaría a que las dos cifras
    // se desincronicen en silencio.
    private Task<decimal> SumarIngresosAsync(
        Guid clienteId, Expression<Func<DespachoHuevo, bool>>? filtroFecha,
        CancellationToken cancellationToken)
    {
        var despachos = db.DespachosHuevo
            .Where(d => d.ClienteId == clienteId && d.Estado == EstadoDespachoHuevo.Recibido
                && d.FechaRecepcion != null);
        if (filtroFecha is not null)
            despachos = despachos.Where(filtroFecha);
        return despachos
            .SelectMany(d => d.Detalles)
            .Where(det => det.PrecioUnitarioCongelado != null)
            .SumAsync(det =>
                (det.CantidadAmarras * DetalleDespachoHuevo.HuevosPorAmarra + det.UnidadesSueltas)
                    * det.PrecioUnitarioCongelado!.Value,
                cancellationToken);
    }
```

- [ ] Verificar que no quedó ninguna referencia al nombre viejo de la constante:

```
grep -rn "DiasDisponibilidadCredito" Icarus/src Icarus/tests
```

- [ ] Correr y ver verde:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~BalanceCreditoHuevoTests"
```

- [ ] Commit: `fix(avicola): el credito de huevo nace al recibir, sin esperar catorce dias`

---

## Task 2 — El dato de referencia llega al Cliente

**Archivos:** `ObtenerBalanceCreditoHuevoHandlerTests.cs`, `ComandosCreditoHuevo.cs`

### Paso 2.1 — Tests en rojo

- [ ] En `ObtenerBalanceCreditoHuevoHandlerTests.cs`, dentro de `CombinaSaldoYAjustesDelRepositorio`, agregar el stub y los asserts:

```csharp
        _repositorio.ObtenerRecibidoRecienteAsync(ClienteId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(300m);
```

```csharp
        Assert.Equal(300m, resultado.RecibidoReciente);
        Assert.Equal(14, resultado.DiasReferencia);
```

- [ ] Agregar el test de que el gate de rol también cubre el dato nuevo — un dato derivado del saldo es igual de sensible que el saldo:

```csharp
    // El recibido reciente sale de los mismos despachos que el saldo: si el
    // Trabajador no puede ver uno, tampoco el otro. Sin este assert el gate
    // podría dejar pasar la consulta nueva sin que ningún test lo note.
    [Fact]
    public async Task UnTrabajadorTampocoPuedeConsultarElRecibidoReciente()
    {
        _usuarioActual.Rol.Returns("Trabajador");

        await Assert.ThrowsAsync<CreditoHuevoRequiereRolClienteException>(() =>
            CrearHandler().Handle(new ObtenerBalanceCreditoHuevoQuery(), CancellationToken.None));

        await _repositorio.DidNotReceive().ObtenerRecibidoRecienteAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
```

- [ ] Correr y **ver rojo**:

```
dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~ObtenerBalanceCreditoHuevoHandlerTests"
```

### Paso 2.2 — DTO y handler

- [ ] En `ComandosCreditoHuevo.cs`, reemplazar el record y el cuerpo del handler:

```csharp
// RecibidoReciente y DiasReferencia acompañan al saldo, no lo modifican
// (corrección 2026-09-14). DiasReferencia viaja al cliente en vez de
// duplicar el 14 en el frontend: así el texto de la PWA no puede mentir si
// la constante cambia.
public sealed record BalanceCreditoHuevoResumen(
    decimal SaldoDisponible,
    decimal RecibidoReciente,
    int DiasReferencia,
    IReadOnlyList<AjusteCreditoHuevoResumen> Ajustes);
```

```csharp
        var hoy = DespachosHuevo.FechasNegocio.Hoy();
        var saldo = await repositorio.ObtenerSaldoDisponibleAsync(clienteId, hoy, cancellationToken);
        var reciente = await repositorio.ObtenerRecibidoRecienteAsync(clienteId, hoy, cancellationToken);
        var ajustes = await repositorio.ObtenerAjustesAsync(clienteId, cancellationToken);
        return new BalanceCreditoHuevoResumen(
            saldo, reciente, ReglasCreditoHuevo.DiasReferenciaCredito, ajustes);
```

> Nota: el gate de rol y el `ClienteId` de la sesión quedan **antes** de las tres consultas, exactamente donde están hoy. No moverlos.

- [ ] Correr y ver verde (unit + el filtro de crédito completo):

```
dotnet test Icarus/tests/Icarus.UnitTests --filter "FullyQualifiedName~CreditoHuevo"
```

- [ ] Commit: `feat(avicola): el balance de credito informa cuanto se recibio en la ventana de referencia`

---

## Task 3 — La semilla dice la verdad nueva

**Archivos:** `SemillaDesarrolloAvicola.cs`, `SemillaDesarrolloAvicolaTests.cs`

La semilla **no cambia de datos**, solo de cifras declaradas en comentarios y
en el test. El único tenant afectado es `ConMovimiento`, por su despacho
recibido hace cinco días (2088,00).

### Paso 3.1 — Test en rojo

- [ ] En `SemillaDesarrolloAvicolaTests.cs`, agregar el helper junto a `SaldoDe`:

```csharp
    private async Task<decimal> RecibidoRecienteDe(TenantDesarrollo tenant)
    {
        var db = _servicios.GetRequiredService<GestionAvicolaDbContext>();
        var repositorio = new RepositorioBalanceCreditoHuevo(db);
        return await repositorio.ObtenerRecibidoRecienteAsync(
            tenant.ClienteId, DateOnly.FromDateTime(DateTime.UtcNow));
    }
```

- [ ] En `LosSaldosCoincidenConLosDeclaradosEnLaSemilla`, cambiar **solo** la línea de `ConMovimiento` y el comentario:

```csharp
        // Corrección 2026-09-14 (segunda): el saldo ya no espera catorce días,
        // así que ConMovimiento sube por su despacho recibido hace cinco días.
        // Los otros cuatro no se mueven: no tienen despachos dentro de la
        // ventana.
        Assert.Equal(9990m, await SaldoDe(Holgado));
        Assert.Equal(-5985m, await SaldoDe(Negativo));
        Assert.Equal(3915m, await SaldoDe(Insuficiente));
        Assert.Equal(8973m, await SaldoDe(ConMovimiento));
        Assert.Equal(0m, await SaldoDe(Vacio));
```

- [ ] Agregar el test del escenario de la línea de referencia:

```csharp
    // ConMovimiento es el único tenant con un despacho dentro de la ventana de
    // referencia, así que es el único donde la PWA renderiza la línea «de los
    // cuales ... se recibieron en los últimos 14 días». Sin este test, la
    // semilla podría perder ese despacho y el escenario desaparecería en
    // silencio.
    [Fact]
    public async Task SoloConMovimientoTieneHuevoDentroDeLaVentanaDeReferencia()
    {
        await SemillaDesarrolloAvicola.SembrarAsync(_servicios, Tenants);

        Assert.Equal(2088m, await RecibidoRecienteDe(ConMovimiento));
        Assert.Equal(0m, await RecibidoRecienteDe(Holgado));
        Assert.Equal(0m, await RecibidoRecienteDe(Negativo));
        Assert.Equal(0m, await RecibidoRecienteDe(Insuficiente));
        Assert.Equal(0m, await RecibidoRecienteDe(Vacio));

        // El reciente es una parte del saldo, nunca un descuento.
        var saldo = await SaldoDe(ConMovimiento);
        Assert.True(await RecibidoRecienteDe(ConMovimiento) < saldo,
            $"El reciente debía ser una parte del saldo {saldo}, no su total.");
    }
```

- [ ] Correr y **ver rojo** (`ConMovimiento` da 6885, se espera 8973):

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~SemillaDesarrolloAvicolaTests"
```

### Paso 3.2 — Comentarios de la semilla

Los datos sembrados **no se tocan**. Solo las cifras declaradas en comentarios,
que hoy afirman lo contrario del comportamiento nuevo.

- [ ] En `SembrarConMovimientoAsync`, reemplazar el comentario de cabecera por:

```csharp
    // Ingresos 6075 + 2250 + 2088 = 10413 (desde la corrección del 2026-09-14
    // el despacho de hace cinco días también cuenta), recibido real 2340 y
    // ajuste +900. Saldo 8973, de los cuales 2088 están dentro de la ventana
    // de referencia de catorce días. El pedido aceptado no pesa en el saldo.
```

- [ ] Reemplazar el comentario del despacho de hace cinco días:

```csharp
        // Recibido dentro de la ventana de referencia de catorce días: cuenta
        // en el saldo como cualquier otro y además se señala aparte. Es el
        // único caso de la semilla que hace visible esa línea en la PWA.
```

- [ ] Verificar la aritmética contra la base antes de dar por buena la cifra:
      6075 + 2250 + 2088 − 2340 + 900 = 8973.

- [ ] Correr y ver verde:

```
dotnet test Icarus/tests/Icarus.IntegrationTests --filter "FullyQualifiedName~SemillaDesarrolloAvicolaTests"
```

- [ ] Commit: `test(avicola): la semilla declara el saldo de ConMovimiento sin el corte de catorce dias`

---

## Task 4 — La PWA muestra la línea de referencia

**Archivos:** `api.ts`, `ReferenciaCreditoReciente.tsx` (nuevo), los dos formularios y sus tests

### Paso 4.1 — Tests en rojo

- [ ] En `web/src/features/pedidos-alimento/PedidoFormularioPage.test.tsx`, agregar dos tests siguiendo el patrón de los existentes (mismo `respuesta(200, ...)` para `GET /api/despachos-huevo/credito`):

```tsx
  it('muestra cuanto del credito se recibio dentro de la ventana de referencia', async () => {
    // ... montar con:
    'GET /api/despachos-huevo/credito': respuesta(200, {
      saldoDisponible: 8973,
      recibidoReciente: 2088,
      diasReferencia: 14,
      ajustes: [],
    }),

    expect(await pantalla.findByText(/se recibieron en los últimos 14 días/i)).toBeInTheDocument();
  });

  it('no muestra la linea de referencia cuando no hay huevo reciente', async () => {
    // ... montar con recibidoReciente: 0

    expect(pantalla.queryByText(/últimos 14 días/i)).not.toBeInTheDocument();
  });
```

- [ ] En `DespachoHuevoFormularioPage.test.tsx`, agregar el equivalente positivo (el formulario de despacho comparte el bloque). El test existente que verifica que el Trabajador **no** pide `/credito` no se toca: sigue valiendo tal cual.

- [ ] Actualizar los mocks existentes de `/credito` en **ambos** archivos de test para incluir los dos campos nuevos. Un mock sin ellos deja `recibidoReciente` en `undefined`, que no rompe el render pero enmascara el caso.

- [ ] Correr y **ver rojo**:

```
cd web; npm test -- PedidoFormularioPage
```

### Paso 4.2 — El tipo

- [ ] En `web/src/features/despacho-huevo/api.ts`:

```ts
export interface BalanceCreditoHuevo {
  saldoDisponible: number;
  // Parte del saldo recibida dentro de la ventana de referencia. Informativo:
  // NO se resta del saldo (corrección 2026-09-14).
  recibidoReciente: number;
  // Lo manda el backend en vez de fijar 14 acá, para que el texto no mienta
  // si la constante cambia.
  diasReferencia: number;
  ajustes: AjusteCreditoHuevo[];
}
```

### Paso 4.3 — El componente

- [ ] Crear `web/src/features/despacho-huevo/ReferenciaCreditoReciente.tsx`:

```tsx
import { Typography } from '@mui/material';
import { formatoMonedaExacta } from '../../lib/formatos';

interface Props {
  recibidoReciente: number;
  diasReferencia: number;
}

// Ritmo de liquidación de CAISY (corrección 2026-09-14): el crédito ya cuenta
// desde la recepción, pero saber qué parte es reciente le sirve al Cliente
// para decidir cuánto pedir. Es informativo y no condiciona nada, así que se
// muestra en tono secundario y nunca en rojo. Reusado por
// PedidoFormularioPage.tsx y DespachoHuevoFormularioPage.tsx.
export function ReferenciaCreditoReciente({ recibidoReciente, diasReferencia }: Props) {
  if (!recibidoReciente) return null;
  return (
    <Typography variant="caption" color="text.secondary" component="p">
      De ese total, {formatoMonedaExacta(recibidoReciente)} se recibieron en los últimos{' '}
      {diasReferencia} días.
    </Typography>
  );
}
```

> `if (!recibidoReciente)` cubre `0` y `undefined` a la vez — esto último
> importa mientras haya mocks viejos o respuestas cacheadas sin el campo.

### Paso 4.4 — Los dos formularios

- [ ] En `PedidoFormularioPage.tsx` y en `DespachoHuevoFormularioPage.tsx`, importar el componente y montarlo **entre** el `Stack` del saldo y `<AjustesCreditoHuevo ...>`:

```tsx
              <ReferenciaCreditoReciente
                recibidoReciente={credito.recibidoReciente}
                diasReferencia={credito.diasReferencia}
              />
```

> El orden importa: saldo, luego referencia, luego correcciones. La referencia
> explica el número de arriba, no las correcciones de abajo.

- [ ] Correr lint, tests y build del frontend:

```
cd web; npm run lint; npm test; npm run build
```

- [ ] Commit: `feat(web): la PWA muestra que parte del credito se recibio en la ventana de referencia`

---

## Task 5 — El script de auditoría refleja la fórmula nueva

**Archivo:** `consultasPruebasSql/saldo-credito-huevo-cliente-demo.sql`

Este script existe para auditar a mano un saldo que se ve en la PWA. Si
conserva el corte, contradice al producto justo donde se lo usa para
comprobarlo.

- [ ] Quitar `AND d.FechaRecepcion <= @fechaCorte` de las **dos** apariciones
      (el `SELECT` de detalle y el de `@ingresos`).
- [ ] Convertir `@fechaCorte` en referencia, no en filtro: agregar una columna
      `Ventana = CASE WHEN d.FechaRecepcion > @fechaCorte THEN 'reciente' ELSE 'consolidado' END`
      al `SELECT` de detalle.
- [ ] Agregar el cálculo del reciente junto a los otros tres:

```sql
DECLARE @recibidoReciente decimal(18,8) = ISNULL((
    SELECT  SUM(CAST(d0.CantidadAmarras * 180 + d0.UnidadesSueltas AS decimal(10,4))
                * d0.PrecioUnitarioCongelado)
    FROM    gestion_avicola.despachos_huevo AS d
    JOIN    gestion_avicola.detalles_despacho_huevo AS d0
              ON d.Id = d0.DespachoHuevoId
    WHERE   d.EstaActivo = 1
      AND   d.ClienteId = @clienteId
      AND   d.Estado = 2
      AND   d.FechaRecepcion > @fechaCorte
      AND   d0.PrecioUnitarioCongelado IS NOT NULL), 0);
```

- [ ] Agregar `RecibidoReciente = @recibidoReciente` al `SELECT` final, **sin**
      tocar la fórmula del saldo (`@ingresos - @recibidoReal + @ajustes`).
- [ ] Actualizar la cabecera: la explicación del plazo, el resultado esperado
      (sigue siendo 9990 para el tenant demo) y una nota de que con
      `77777777-...` el reciente da 2088.
- [ ] Ejecutarlo contra el stack y comprobar que el saldo del tenant demo sigue
      dando 9990 y que `c3` da 8973 / 2088:

```
docker cp consultasPruebasSql/saldo-credito-huevo-cliente-demo.sql trajano-icarus-sqlserver-1:/tmp/saldo-demo.sql
docker exec trajano-icarus-sqlserver-1 bash -lc '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d Icarus -f 65001 -i /tmp/saldo-demo.sql'
```

> El `-f 65001` no es opcional: sin él sqlcmd lee el archivo como ANSI y los
> acentos de los comentarios salen rotos.

- [ ] Commit: `docs(avicola): la consulta de auditoria del saldo sin el corte de catorce dias`

---

## Task 6 — Cierre

- [ ] Correr la puerta de calidad completa, con Docker arriba:

```
./verify.ps1
```

- [ ] Verificar que no apareció ninguna migración pendiente:

```
dotnet ef migrations has-pending-model-changes --project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure --startup-project Icarus/src/GestionAvicola/Icarus.GestionAvicola.Infrastructure
```

- [ ] Push a `develop`.
- [ ] Actualizar `docs/ai/HANDOFF.md` (**local, no versionado**: está en
      `.gitignore:18`, no intentar comitearlo; el commit final del trabajo es
      el de la Task 5).

### Conteos esperados al cerrar

| Suite | Antes | Después |
|---|---|---|
| Frontend | 288 | 291 (3 nuevos) |
| Architecture | 6 | 6 |
| Unit | 504 | 505 (1 nuevo) |
| GestorCaisy | 193 | 193 |
| Integration | 146 | 147 (1 nuevo) |

Si algún conteo no cuadra, **no ajustar el número de este plan**: averiguar
qué test se perdió o se duplicó.

---

## Verificación manual

Con el stack pc1 reconstruido (`./iniciar-pc1.ps1`, contraseña de la semilla
`Admin123!`, no `Admin123456!`):

| Qué | Cómo |
|---|---|
| El saldo subió donde correspondía | `c3@icarus.test` en `/pedidos/nuevo`: **8.973,00**, no 6.885,00 |
| La línea de referencia se ve | El mismo `c3@`: «De ese total, 2.088,00 se recibieron en los últimos 14 días.» |
| No se ve donde no corresponde | `cliente@`, `c1@`, `c2@`: saldo sin segunda línea |
| El negativo sigue siendo negativo | `c1@`: −5.985,00 en rojo con el chip «Negativo» |
| El Trabajador sigue sin ver nada | `trabajador@`, `t3@`: ni saldo ni línea de referencia, y sin llamada a `/credito` |
| CAISY sigue sin ver nada | `gpa@` en Detalles/Aceptar/Despachar: ninguna cifra de crédito |

El caso de `c3@` es el único que exhibe la línea: si no aparece ahí, el
escenario de la semilla se perdió.
