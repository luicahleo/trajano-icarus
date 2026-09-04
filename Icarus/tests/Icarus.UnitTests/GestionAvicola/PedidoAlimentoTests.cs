using Icarus.BuildingBlocks.Domain;
using Icarus.GestionAvicola.Domain;
using Xunit;

namespace Icarus.UnitTests.GestionAvicola;

// SP8B Tarea 1 (spec: "Pedido y cantidades" y "Máquina de estados"): el pedido
// es compartido del tenant, admite una sola presentación, congeló precios al
// enviar y solo el borrador se edita o se borra lógicamente. CAISY nunca altera
// líneas. Regla de compatibilidad decidida en el plan: los tipos de fase
// levante (preiniciador a finalizador) no se mezclan con los de postura
// (postura uno y dos) en un mismo pedido.
public class PedidoAlimentoTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid CreadoPor = Guid.NewGuid();
    private static readonly DateOnly Hoy = new(2026, 9, 1);

    private static DatosDetallePedido Linea(TipoAlimento tipo, int cantidad = 100,
        PresentacionAlimento presentacion = PresentacionAlimento.Bolsa) =>
        new(tipo, presentacion, cantidad);

    private static DatosPrecioEnvio Precio(TipoAlimento tipo,
        PresentacionAlimento presentacion = PresentacionAlimento.Bolsa, decimal valor = 180m) =>
        new(tipo, presentacion, valor, Guid.NewGuid());

    private static PedidoAlimento BorradorDeBolsas(int bolsas = 100) =>
        new(ClienteId, CreadoPor, [Linea(TipoAlimento.PosturaUno, bolsas)]);

    private static readonly TipoAlimento[] TiposPostura =
        [TipoAlimento.PosturaUno, TipoAlimento.PosturaDos];

    private static PedidoAlimento BorradorDeGranel(params int[] toneladas) =>
        new(ClienteId, CreadoPor,
            toneladas.Select((t, i) => Linea(TiposPostura[i], t, PresentacionAlimento.Granel)).ToList());

    [Fact]
    public void UnPedidoNaceBorradorEnElTenantConSusLineas()
    {
        var pedido = new PedidoAlimento(
            ClienteId, CreadoPor,
            [Linea(TipoAlimento.PosturaUno, 100), Linea(TipoAlimento.PosturaDos, 50)]);

        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
        Assert.Equal(ClienteId, pedido.ClienteId);
        Assert.Equal(CreadoPor, pedido.CreadoPor);
        Assert.True(pedido.EstaActivo);
        Assert.Equal(2, pedido.Detalles.Count);
        Assert.Null(pedido.FechaPedido);
        Assert.Empty(pedido.Historial);
    }

    [Fact]
    public void ElPedidoSoloAdmiteUnaPresentacion()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() => new PedidoAlimento(
            ClienteId, CreadoPor,
            [Linea(TipoAlimento.PosturaUno, 100), Linea(TipoAlimento.PosturaDos, 3, PresentacionAlimento.Granel)]));

        Assert.Equal("El pedido solo admite una presentación.", excepcion.Message);
    }

    [Fact]
    public void NoAdmiteLineasDuplicadasDelMismoTipo()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() => new PedidoAlimento(
            ClienteId, CreadoPor,
            [Linea(TipoAlimento.PosturaUno, 100), Linea(TipoAlimento.PosturaUno, 50)]));

        Assert.Equal("Cada tipo de alimento solo puede aparecer una vez en el pedido.", excepcion.Message);
    }

    [Fact]
    public void NoAdmiteTiposDeLevanteYPosturaMezclados()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() => new PedidoAlimento(
            ClienteId, CreadoPor,
            [Linea(TipoAlimento.Iniciador, 100), Linea(TipoAlimento.PosturaUno, 50)]));

        Assert.Equal("El pedido no puede mezclar tipos de levante y de postura.", excepcion.Message);
    }

    [Fact]
    public void LasCantidadesDebenSerPositivas()
    {
        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            new PedidoAlimento(ClienteId, CreadoPor, [Linea(TipoAlimento.PosturaUno, 0)]));

        Assert.Equal("La cantidad solicitada debe ser mayor que cero.", excepcion.Message);
    }

    [Fact]
    public void UnaBolsaEsUnEquivalenteDe40Kg()
    {
        var pedido = BorradorDeBolsas(100);

        var linea = pedido.Detalles.Single();
        Assert.Equal(100, linea.Equivalentes40Kg);
        Assert.Null(linea.PrecioFinalPor40Kg);
        Assert.Null(linea.SubtotalSolicitado);
    }

    [Fact]
    public void UnaToneladaSon25EquivalentesDe40Kg()
    {
        var pedido = BorradorDeGranel(3);

        var linea = pedido.Detalles.Single();
        Assert.Equal(75, linea.Equivalentes40Kg);
    }

    [Fact]
    public void EnviarCongelaFechaYPreciosConSubtotales()
    {
        var pedido = BorradorDeBolsas(100);

        pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]);

        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);
        Assert.Equal(Hoy, pedido.FechaPedido);
        var linea = pedido.Detalles.Single();
        Assert.Equal(180m, linea.PrecioFinalPor40Kg);
        Assert.Equal(18000m, linea.SubtotalSolicitado);
        Assert.Equal(18000m, pedido.TotalSolicitado);
        var transicion = Assert.Single(pedido.Historial);
        Assert.Equal(EstadoPedidoAlimento.Borrador, transicion.EstadoOrigen);
        Assert.Equal(EstadoPedidoAlimento.Solicitado, transicion.EstadoDestino);
    }

    [Fact]
    public void EnviarGranelExigeDosToneladasPorLinea()
    {
        var pedido = BorradorDeGranel(1, 5);

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno, PresentacionAlimento.Granel)]));

        Assert.Equal("El envío granel exige al menos dos toneladas por tipo.", excepcion.Message);
        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
    }

    [Fact]
    public void EnviarGranelExigeSeisToneladasTotales()
    {
        var pedido = BorradorDeGranel(2, 3);

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno, PresentacionAlimento.Granel)]));

        Assert.Equal("El envío granel exige al menos seis toneladas en total.", excepcion.Message);
        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
    }

    [Fact]
    public void EnviarFallaCompletoSiFaltaPrecioDeUnaLinea()
    {
        var pedido = new PedidoAlimento(ClienteId, CreadoPor,
            [Linea(TipoAlimento.PosturaUno, 100), Linea(TipoAlimento.PosturaDos, 50)]);

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]));

        Assert.Equal("Falta precio vigente para una línea del pedido.", excepcion.Message);
        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
        Assert.Null(pedido.FechaPedido);
        Assert.All(pedido.Detalles, d => Assert.Null(d.PrecioFinalPor40Kg));
        Assert.Empty(pedido.Historial);
    }

    [Fact]
    public void SoloUnBorradorSePuedeEditarYDesactivar()
    {
        var pedido = BorradorDeBolsas();
        pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]);

        var excepcionEditar = Assert.Throws<ReglaNegocioException>(() =>
            pedido.EditarDetalles([Linea(TipoAlimento.PosturaUno, 120)]));
        var excepcionDesactivar = Assert.Throws<ReglaNegocioException>(pedido.Desactivar);

        Assert.Equal("Solo un borrador se puede editar.", excepcionEditar.Message);
        Assert.Equal("Solo un borrador se puede desactivar.", excepcionDesactivar.Message);
        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);
        Assert.True(pedido.EstaActivo);
    }

    [Fact]
    public void SoloUnBorradorSePuedeEnviar()
    {
        var pedido = BorradorDeBolsas();
        pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]);

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]));

        Assert.Equal("Solo un pedido en borrador se puede enviar.", excepcion.Message);
        Assert.Single(pedido.Historial);
    }

    [Fact]
    public void DevolverParaCorreccionExigeMotivoYVuelveABorrador()
    {
        var pedido = BorradorDeBolsas(100);
        pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]);

        var excepcion = Assert.Throws<ReglaNegocioException>(() => pedido.DevolverParaCorreccion("   ", CreadoPor));

        Assert.Equal("El motivo es obligatorio.", excepcion.Message);
        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);

        pedido.DevolverParaCorreccion("Falta indicar el tipo correcto", CreadoPor);

        Assert.Equal(EstadoPedidoAlimento.Borrador, pedido.Estado);
        // La devolución conserva los precios congelados del último envío.
        Assert.Equal(180m, pedido.Detalles.Single().PrecioFinalPor40Kg);
        Assert.Equal(2, pedido.Historial.Count);
        var devolucion = pedido.Historial[1];
        Assert.Equal(EstadoPedidoAlimento.Solicitado, devolucion.EstadoOrigen);
        Assert.Equal(EstadoPedidoAlimento.Borrador, devolucion.EstadoDestino);
        Assert.Equal("Falta indicar el tipo correcto", devolucion.Motivo);
        // Reutiliza el mismo pedido: vuelve a ser editable.
        pedido.EditarDetalles([Linea(TipoAlimento.PosturaUno, 120)]);
        Assert.Single(pedido.Detalles);
    }

    [Fact]
    public void RechazarExigeMotivoYEsTerminal()
    {
        var pedido = BorradorDeBolsas();
        pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]);

        var excepcion = Assert.Throws<ReglaNegocioException>(() => pedido.Rechazar("", CreadoPor));

        Assert.Equal("El motivo es obligatorio.", excepcion.Message);
        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);

        pedido.Rechazar("Sin precio para esa presentación", CreadoPor);

        Assert.Equal(EstadoPedidoAlimento.Rechazado, pedido.Estado);
        Assert.Throws<ReglaNegocioException>(() => pedido.Rechazar("Otra vez", CreadoPor));
        Assert.Throws<ReglaNegocioException>(() =>
            pedido.EditarDetalles([Linea(TipoAlimento.PosturaUno, 120)]));
    }

    [Fact]
    public void AceptarExigeFechaEntregaEstimadaDesdeHoy()
    {
        var pedido = BorradorDeBolsas();
        pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]);

        var excepcion = Assert.Throws<ReglaNegocioException>(() =>
            pedido.Aceptar(Hoy.AddDays(-1), Hoy, CreadoPor));

        Assert.Equal("La fecha de entrega estimada debe ser hoy o posterior.", excepcion.Message);
        Assert.Equal(EstadoPedidoAlimento.Solicitado, pedido.Estado);

        pedido.Aceptar(Hoy.AddDays(3), Hoy, CreadoPor);

        Assert.Equal(EstadoPedidoAlimento.Aceptado, pedido.Estado);
        Assert.Equal(Hoy.AddDays(3), pedido.FechaEntregaEstimada);
        var transicion = pedido.Historial[pedido.Historial.Count - 1];
        Assert.Equal(Hoy.AddDays(3), transicion.FechaEntregaEstimada);
    }

    [Fact]
    public void SoloUnAceptadoActualizaLaEntregaEstimada()
    {
        var pedido = BorradorDeBolsas();
        pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]);

        var excepcionSolicitado = Assert.Throws<ReglaNegocioException>(() =>
            pedido.ActualizarEntregaEstimada(Hoy.AddDays(2), Hoy, CreadoPor));

        Assert.Equal("Solo un pedido aceptado permite actualizar la entrega estimada.", excepcionSolicitado.Message);

        pedido.Aceptar(Hoy.AddDays(3), Hoy, CreadoPor);
        var excepcionPasada = Assert.Throws<ReglaNegocioException>(() =>
            pedido.ActualizarEntregaEstimada(Hoy.AddDays(-1), Hoy, CreadoPor));

        Assert.Equal("La fecha de entrega estimada debe ser hoy o posterior.", excepcionPasada.Message);

        pedido.ActualizarEntregaEstimada(Hoy.AddDays(10), Hoy, CreadoPor);

        Assert.Equal(Hoy.AddDays(10), pedido.FechaEntregaEstimada);
        Assert.Equal(EstadoPedidoAlimento.Aceptado, pedido.Estado);
        Assert.Equal(3, pedido.Historial.Count);
        Assert.Equal(EstadoPedidoAlimento.Aceptado, pedido.Historial[2].EstadoOrigen);
        Assert.Equal(Hoy.AddDays(10), pedido.Historial[2].FechaEntregaEstimada);
    }

    [Fact]
    public void ElHistorialConservaTodoElRecorridoDelPedido()
    {
        var pedido = BorradorDeBolsas();
        pedido.EnviarACaisy(Hoy, CreadoPor, [Precio(TipoAlimento.PosturaUno)]);
        pedido.DevolverParaCorreccion("Revise la cantidad", CreadoPor);
        pedido.EditarDetalles([Linea(TipoAlimento.PosturaUno, 150)]);
        pedido.EnviarACaisy(Hoy.AddDays(1), CreadoPor, [Precio(TipoAlimento.PosturaUno)]);
        pedido.Aceptar(Hoy.AddDays(4), Hoy.AddDays(1), CreadoPor);

        Assert.Equal(4, pedido.Historial.Count);
        Assert.Equal(
            new[]
            {
                EstadoPedidoAlimento.Solicitado,
                EstadoPedidoAlimento.Borrador,
                EstadoPedidoAlimento.Solicitado,
                EstadoPedidoAlimento.Aceptado,
            },
            pedido.Historial.Select(t => t.EstadoDestino).ToArray());
        Assert.Equal(150, pedido.Detalles.Single().CantidadSolicitada);
        // El segundo envío actualiza el congelado con la nueva cantidad.
        Assert.Equal(27000m, pedido.Detalles.Single().SubtotalSolicitado);
    }
}
