/* ===========================================================================
   Saldo de crédito de huevo — ejemplo con cliente@icarus.test
   ---------------------------------------------------------------------------
   Reproduce a mano lo que calcula
   RepositorioBalanceCreditoHuevo.ObtenerSaldoDisponibleAsync, que en realidad
   NO es una consulta sino tres SUM independientes restadas en C#:

       saldo = ingresos - recibidoReal + ajustes

   Sirve para auditar un saldo que se ve en la PWA sin tener que depurar el
   backend. Los tres bloques de abajo son el mismo SQL que emite EF Core, con
   los filtros globales de tenant ya resueltos al ClienteId fijo.

   Base: Icarus (stack pc1, entorno Development). Ejecutar desde la raíz del
   repo, con el stack levantado. El -f 65001 no es opcional: sin él, sqlcmd
   lee este archivo como ANSI y los acentos de los comentarios salen rotos.

     docker cp consultasPruebasSql/saldo-credito-huevo-cliente-demo.sql trajano-icarus-sqlserver-1:/tmp/saldo-demo.sql
     docker exec trajano-icarus-sqlserver-1 bash -lc '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d Icarus -f 65001 -i /tmp/saldo-demo.sql'

   Resultado esperado con la semilla de desarrollo intacta:
     ingresos 13590.00 - recibidoReal 3600.00 + ajustes 0.00 = saldo 9990.00
     (recibidoReciente 0.00: el demo no tiene despachos en la ventana)

   Con el tenant c3@icarus.test (77777777-...), el único con un despacho dentro
   de la ventana de referencia:
     saldo 8973.00 y recibidoReciente 2088.00.
   =========================================================================== */

SET NOCOUNT ON;

-- cliente@icarus.test = SemillaIdentidad.ClienteDemoId. Cambiar este GUID es
-- lo único que hace falta para auditar otro tenant:
--   33333333-... c1@icarus.test (saldo negativo)
--   55555555-... c2@icarus.test (envío con crédito insuficiente)
--   77777777-... c3@icarus.test (el único con ajuste de corrección)
DECLARE @clienteId uniqueidentifier = '11111111-1111-1111-1111-111111111111';

-- ReglasCreditoHuevo.DiasReferenciaCredito = 14. Ventana de referencia, no un
-- plazo: el crédito existe desde la recepción y la fecha de corte solo separa
-- el huevo «reciente» del «consolidado» (corrección 2026-09-14).
DECLARE @fechaCorte date = DATEADD(day, -14, CAST(GETDATE() AS date));

PRINT '--- Fecha de corte aplicada a los ingresos ---';
SELECT FechaCorte = @fechaCorte;


/* ---------------------------------------------------------------------------
   1) INGRESOS — lo que CAISY le debe al cliente por huevo entregado.

   Solo despachos en estado Recibido (EstadoDespachoHuevo.Recibido = 2), sin
   importar hace cuánto: el crédito nace al recibir (corrección 2026-09-14).
   El precio es el congelado en el despacho, no el vigente hoy: republicar
   precios no mueve el saldo histórico.

   El 180 es DetalleDespachoHuevo.HuevosPorAmarra, un const que EF emite como
   literal. El CAST a decimal(10,4) es el que genera EF para multiplicar el
   conteo entero de huevos por el precio unitario.
   --------------------------------------------------------------------------- */
PRINT '--- 1) Detalle de los ingresos, despacho por despacho ---';
SELECT  d.Id               AS DespachoId,
        d.FechaRecepcion,
        Ventana            = CASE WHEN d.FechaRecepcion > @fechaCorte
                                  THEN 'reciente' ELSE 'consolidado' END,
        d0.CantidadAmarras,
        d0.UnidadesSueltas,
        Huevos             = d0.CantidadAmarras * 180 + d0.UnidadesSueltas,
        d0.PrecioUnitarioCongelado,
        Subtotal           = CAST(d0.CantidadAmarras * 180 + d0.UnidadesSueltas AS decimal(10,4))
                             * d0.PrecioUnitarioCongelado
FROM    gestion_avicola.despachos_huevo AS d
JOIN    gestion_avicola.detalles_despacho_huevo AS d0
          ON d.Id = d0.DespachoHuevoId
WHERE   d.EstaActivo = 1                    -- soft delete
  AND   d.ClienteId = @clienteId
  AND   d.Estado = 2                        -- EstadoDespachoHuevo.Recibido
  AND   d.FechaRecepcion IS NOT NULL
  AND   d0.PrecioUnitarioCongelado IS NOT NULL
ORDER BY d.FechaRecepcion, d0.Id;

DECLARE @ingresos decimal(18,8) = ISNULL((
    SELECT  SUM(CAST(d0.CantidadAmarras * 180 + d0.UnidadesSueltas AS decimal(10,4))
                * d0.PrecioUnitarioCongelado)
    FROM    gestion_avicola.despachos_huevo AS d
    JOIN    gestion_avicola.detalles_despacho_huevo AS d0
              ON d.Id = d0.DespachoHuevoId
    WHERE   d.EstaActivo = 1
      AND   d.ClienteId = @clienteId
      AND   d.Estado = 2
      AND   d.FechaRecepcion IS NOT NULL
      AND   d0.PrecioUnitarioCongelado IS NOT NULL), 0);
-- El ISNULL replica el COALESCE(..., 0.0) de EF: SUM sobre cero filas devuelve
-- NULL, y un NULL en cualquier término envenenaría el total entero.

-- Recibido dentro de la ventana de referencia (corrección 2026-09-14): dato
-- informativo que acompaña al saldo, no un término de la resta. Es la misma
-- aritmética de ingresos con el filtro de fecha invertido.
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


/* ---------------------------------------------------------------------------
   2) RECIBIDO REAL — el alimento que el cliente ya consumió.

   Solo pedidos efectivamente recibidos (EstadoPedidoAlimento 5 =
   RecibidoConforme, 6 = RecibidoConDiferencias) y por TotalRecibido, que es lo
   que llegó de verdad, no lo solicitado ni lo despachado.

   Corrección 2026-09-14: acá NO entran los pedidos en Solicitado, Aceptado ni
   Despachado. Ese alimento no llegó, no consumió crédito, y el pedido todavía
   puede ser rechazado o devuelto por CAISY. Descontarlo producía negativos
   falsos que rebotaban.
   --------------------------------------------------------------------------- */
PRINT '--- 2) Detalle del alimento recibido, pedido por pedido ---';
SELECT  p.Id      AS PedidoId,
        p.Estado,
        EstadoNombre = CASE p.Estado WHEN 5 THEN 'RecibidoConforme'
                                     WHEN 6 THEN 'RecibidoConDiferencias' END,
        r.TotalRecibido
FROM    gestion_avicola.pedidos_alimentos AS p
LEFT JOIN gestion_avicola.recepciones_pedidos_alimentos AS r
          ON p.Id = r.PedidoAlimentoId
WHERE   p.EstaActivo = 1
  AND   p.ClienteId = @clienteId
  AND   p.Estado IN (5, 6)
ORDER BY p.Id;

DECLARE @recibidoReal decimal(18,8) = ISNULL((
    SELECT  SUM(r.TotalRecibido)
    FROM    gestion_avicola.pedidos_alimentos AS p
    LEFT JOIN gestion_avicola.recepciones_pedidos_alimentos AS r
              ON p.Id = r.PedidoAlimentoId
    WHERE   p.EstaActivo = 1
      AND   p.ClienteId = @clienteId
      AND   p.Estado IN (5, 6)), 0);

-- Contraste útil: los pedidos que NO pesan en el saldo. Si esta consulta
-- devuelve filas y el cliente igual tiene saldo alto, eso es correcto, no un
-- error. La columna AntesRestaba marca las que sí descontaban hasta la
-- corrección del 2026-09-14: son exactamente las que producían los negativos
-- falsos, porque ese alimento todavía no había llegado.
PRINT '--- 2b) Pedidos que NO afectan el saldo ---';
SELECT  p.Id AS PedidoId,
        p.Estado,
        EstadoNombre = CASE p.Estado WHEN 0 THEN 'Borrador'
                                     WHEN 1 THEN 'Solicitado'
                                     WHEN 2 THEN 'Rechazado'
                                     WHEN 3 THEN 'Aceptado'
                                     WHEN 4 THEN 'Despachado'
                                     ELSE CAST(p.Estado AS varchar(10)) END,
        AntesRestaba = CASE WHEN p.Estado IN (1, 3, 4) THEN 'si' ELSE 'no' END
FROM    gestion_avicola.pedidos_alimentos AS p
WHERE   p.EstaActivo = 1
  AND   p.ClienteId = @clienteId
  AND   p.Estado NOT IN (5, 6)
ORDER BY p.Id;


/* ---------------------------------------------------------------------------
   3) AJUSTES — correcciones por error de precio, con signo.

   Sin desfase de 14 días: compensan algo que ya pasó. La tabla no lleva
   EstaActivo porque una corrección de precio no se borra.
   --------------------------------------------------------------------------- */
PRINT '--- 3) Ajustes de corrección ---';
SELECT  a.Id, a.Monto, a.Motivo, a.CreadoEnUtc
FROM    gestion_avicola.ajustes_credito_huevo AS a
WHERE   a.ClienteId = @clienteId
ORDER BY a.CreadoEnUtc DESC;

DECLARE @ajustes decimal(18,8) = ISNULL((
    SELECT  SUM(a.Monto)
    FROM    gestion_avicola.ajustes_credito_huevo AS a
    WHERE   a.ClienteId = @clienteId), 0);


/* ---------------------------------------------------------------------------
   TOTAL — la fórmula del repositorio, hecha a mano.
   Este número tiene que ser idéntico al que muestra la PWA al cliente en el
   formulario de pedido de alimento y en el de despacho de huevo.
   --------------------------------------------------------------------------- */
PRINT '--- Saldo resultante ---';
SELECT  ClienteId        = @clienteId,
        Ingresos         = @ingresos,
        RecibidoReal     = @recibidoReal,
        Ajustes          = @ajustes,
        RecibidoReciente = @recibidoReciente,
        Saldo            = @ingresos - @recibidoReal + @ajustes,
        Signo            = CASE WHEN @ingresos - @recibidoReal + @ajustes < 0
                                THEN 'Negativo (se muestra en rojo con chip)'
                                ELSE 'Positivo' END;
