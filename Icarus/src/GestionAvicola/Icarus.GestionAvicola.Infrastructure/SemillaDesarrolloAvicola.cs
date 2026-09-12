using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Icarus.GestionAvicola.Infrastructure;

// Papel que juega cada tenant dentro del escenario de desarrollo. El signo del
// saldo se controla con la magnitud de los datos, no con estados distintos:
// las rutas de UI son idénticas y solo cambia el número.
public enum PapelCreditoDesarrollo
{
    // Flujo sano de punta a punta: despachos cobrados y un pedido recibido.
    PositivoHolgado = 0,

    // Dispara el chip «Negativo» y la notificación de saldo negativo.
    Negativo = 1,

    // Saldo positivo pero corto: al enviar un pedido salta la confirmación de
    // crédito insuficiente (SP9E).
    Insuficiente = 2,

    // Desglose completo: despachos dentro y fuera de la ventana de catorce
    // días, pedidos en varios estados y un ajuste por corrección de precio.
    ConMovimiento = 3,

    // Granja y galpones, pero sin precios ni despachos: estados vacíos.
    Vacio = 4,
}

// Tenant del escenario de desarrollo. ActorId es el trabajador que figura como
// autor técnico de los movimientos sembrados.
public sealed record TenantDesarrollo(Guid ClienteId, Guid ActorId, PapelCreditoDesarrollo Papel);

// Escenario de desarrollo de la cadena de crédito de huevo (SP8/SP9).
//
// SOLO corre bajo IsDevelopment(), nunca en Testing: PublicacionPrecioHuevo y
// NotificacionPreciosAlimentos son recursos GLOBALES con un índice único
// filtrado sobre la vigencia, y las pruebas de integración eligen sus propias
// vigencias sobre una base compartida. Sembrar precios en Testing tumbaría
// varias de ellas.
//
// Tiene que ser semilla y no un script contra la API porque FechasNegocio.Hoy()
// lee DateTime.UtcNow y no hay abstracción de reloj: por HTTP no se pueden
// retroceder fechas, y sin retroceder el crédito siempre da cero (solo cuentan
// los despachos con FechaRecepcion <= hoy - 14). Los agregados sí reciben la
// fecha como parámetro, así que desde aquí se retrocede respetando los
// invariantes del dominio.
//
// Datos ficticios: ningún dato nominal real (anti-PII).
public static class SemillaDesarrolloAvicola
{
    // Precios al productor por tamaño, los mismos en la publicación vigente y
    // en la correctiva. El precio que se congela en un despacho es el precio
    // al productor más el servicio.
    private const decimal Servicio = 0.05m;

    private static readonly (TamanoHuevo Tamano, decimal Precio)[] PreciosHuevo =
    [
        (TamanoHuevo.Extra, 1.50m),
        (TamanoHuevo.Primera, 1.40m),
        (TamanoHuevo.Segunda, 1.30m),
        (TamanoHuevo.Tercera, 1.20m),
        (TamanoHuevo.Cuarta, 1.10m),
        (TamanoHuevo.Quinta, 1.00m),
    ];

    // El error que justifica el ajuste: la publicación errónea puso el Extra
    // por debajo de su precio real.
    private const decimal PrecioExtraErroneo = 1.30m;

    private const decimal PrecioBolsaPostura = 180m;

    // Ids fijos de los recursos globales (prefijo propio, no colisiona con los
    // «aa…» de SemillaGestionAvicola).
    private static readonly Guid PublicacionVigenteId = new("ab000000-0000-0000-0000-000000000001");
    private static readonly Guid PublicacionErroneaId = new("ab000000-0000-0000-0000-000000000002");
    private static readonly Guid PublicacionCorrectivaId = new("ab000000-0000-0000-0000-000000000003");
    private static readonly Guid NotificacionPreciosId = new("ab000000-0000-0000-0000-000000000004");

    public static async Task SembrarAsync(
        IServiceProvider servicios, IReadOnlyList<TenantDesarrollo> tenants)
    {
        var db = servicios.GetRequiredService<GestionAvicolaDbContext>();
        var almacen = servicios.GetRequiredService<IAlmacenDocumentosPedido>();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        await SembrarPreciosGlobalesAsync(db, hoy);

        foreach (var tenant in tenants)
            await SembrarTenantAsync(db, almacen, tenant, hoy);

        await db.SaveChangesAsync();
    }

    // Publicaciones de precio: la vigente que desbloquea el despacho de huevo,
    // el par errónea/correctiva que justifica el ajuste de SP9D, y la
    // notificación de precios de alimento que desbloquea el envío de pedidos.
    // Las tres vigencias son distintas para no chocar con el índice único
    // filtrado sobre la vigencia de las publicaciones publicadas.
    private static async Task SembrarPreciosGlobalesAsync(GestionAvicolaDbContext db, DateOnly hoy)
    {
        if (!await db.PublicacionesPreciosHuevo.IgnoreQueryFilters()
                .AnyAsync(p => p.Id == PublicacionVigenteId))
        {
            var vigente = new PublicacionPrecioHuevo(
                PublicacionVigenteId, hoy.AddDays(-41), hoy.AddDays(-40), Servicio);
            vigente.ActualizarBorrador(
                [.. PreciosHuevo.Select(p => new DatosDetallePrecioHuevo(p.Tamano, p.Precio))]);
            vigente.Publicar();

            // La errónea quedó Corregida, así que sale del índice único: solo
            // la correctiva y la vigente siguen Publicadas, con vigencias
            // distintas entre sí.
            var erronea = new PublicacionPrecioHuevo(
                PublicacionErroneaId, hoy.AddDays(-26), hoy.AddDays(-25), Servicio);
            erronea.ActualizarBorrador(
                [.. PreciosHuevo.Select(p => new DatosDetallePrecioHuevo(
                    p.Tamano, p.Tamano == TamanoHuevo.Extra ? PrecioExtraErroneo : p.Precio))]);
            erronea.Publicar();

            var correctiva = new PublicacionPrecioHuevo(
                PublicacionCorrectivaId, hoy.AddDays(-26), hoy.AddDays(-24), Servicio);
            correctiva.ActualizarBorrador(
                [.. PreciosHuevo.Select(p => new DatosDetallePrecioHuevo(p.Tamano, p.Precio))]);
            correctiva.Publicar();

            erronea.CorregirVigente(correctiva.Id, "Precio del Extra mal digitado en el documento.");

            db.PublicacionesPreciosHuevo.AddRange(vigente, erronea, correctiva);
        }

        if (!await db.NotificacionesPreciosAlimentos.IgnoreQueryFilters()
                .AnyAsync(n => n.Id == NotificacionPreciosId))
        {
            var precios = Enum.GetValues<TipoAlimento>()
                .SelectMany(tipo => new[]
                {
                    new DatosDetallePrecio(tipo, PresentacionAlimento.Bolsa, PrecioBolsaPostura, null, null),
                    new DatosDetallePrecio(tipo, PresentacionAlimento.Granel, PrecioBolsaPostura - 8m, null, null),
                })
                .ToList();
            var notificacion = new NotificacionPreciosAlimentos(
                NotificacionPreciosId, hoy.AddDays(-41), hoy.AddDays(-40), 0.10m, 0.05m, 0.03m);
            notificacion.ActualizarBorrador(precios);
            notificacion.Publicar();
            db.NotificacionesPreciosAlimentos.Add(notificacion);
        }
    }

    private static async Task SembrarTenantAsync(
        GestionAvicolaDbContext db, IAlmacenDocumentosPedido almacen,
        TenantDesarrollo tenant, DateOnly hoy)
    {
        var granjaId = await SembrarGranjaYGalponesAsync(db, tenant, hoy);

        // El primer despacho del tenant hace de centinela del escenario: si ya
        // está, el escenario se sembró en un arranque anterior.
        var centinela = Derivar(tenant.ClienteId, 0x41);
        if (tenant.Papel == PapelCreditoDesarrollo.Vacio
            || await db.DespachosHuevo.IgnoreQueryFilters().AnyAsync(d => d.Id == centinela))
            return;

        switch (tenant.Papel)
        {
            case PapelCreditoDesarrollo.PositivoHolgado:
                await SembrarHolgadoAsync(db, almacen, tenant, granjaId, hoy);
                break;
            case PapelCreditoDesarrollo.Negativo:
                await SembrarNegativoAsync(db, almacen, tenant, granjaId, hoy);
                break;
            case PapelCreditoDesarrollo.Insuficiente:
                await SembrarInsuficienteAsync(db, almacen, tenant, granjaId, hoy);
                break;
            case PapelCreditoDesarrollo.ConMovimiento:
                await SembrarConMovimientoAsync(db, almacen, tenant, granjaId, hoy);
                break;
            default:
                break;
        }
    }

    // Reutiliza la granja activa que el tenant ya tenga (el cliente demo la
    // recibe de SemillaGestionAvicola); si no tiene ninguna, la crea con el
    // centinela como id. Una granja activa por cliente es invariante del
    // dominio, así que nunca se crea una segunda.
    private static async Task<Guid> SembrarGranjaYGalponesAsync(
        GestionAvicolaDbContext db, TenantDesarrollo tenant, DateOnly hoy)
    {
        var granjaExistente = await db.Granjas.IgnoreQueryFilters()
            .Where(g => g.ClienteId == tenant.ClienteId)
            .Select(g => (Guid?)g.Id)
            .FirstOrDefaultAsync();

        if (granjaExistente is not null)
            return granjaExistente.Value;

        var granjaId = Derivar(tenant.ClienteId, 0x01);
        db.Granjas.Add(new Granja(granjaId, tenant.ClienteId, "Granja de desarrollo"));

        var galponUno = Derivar(tenant.ClienteId, 0x11);
        var galponDos = Derivar(tenant.ClienteId, 0x12);
        db.Galpones.AddRange(
            new Galpon(galponUno, granjaId, tenant.ClienteId, "1", 5000, 4800,
                hoy.AddDays(-300), "Galpón norte"),
            new Galpon(galponDos, granjaId, tenant.ClienteId, "2", 5000, 4950,
                hoy.AddDays(-120), "Galpón sur"));

        db.RegistrosProduccion.AddRange(
            new RegistroProduccion(Derivar(tenant.ClienteId, 0x21), galponUno, tenant.ClienteId,
                hoy.AddDays(-1), new TimeOnly(8, 30), 96, 12, 2, 8, 4800, null),
            new RegistroProduccion(Derivar(tenant.ClienteId, 0x22), galponUno, tenant.ClienteId,
                hoy, new TimeOnly(8, 45), 102, 5, 1, 3, 4800, null));

        db.RegistrosMortalidad.AddRange(
            new RegistroMortalidad(Derivar(tenant.ClienteId, 0x31), galponUno, tenant.ClienteId,
                hoy.AddDays(-1), new TimeOnly(7, 15), 4, 4800, null),
            new RegistroMortalidad(Derivar(tenant.ClienteId, 0x32), galponUno, tenant.ClienteId,
                hoy, new TimeOnly(7, 20), 2, 4800, null));

        return granjaId;
    }

    // Ingresos 8370 + 5220 = 13590; recibido real 3600. Saldo 9990.
    private static async Task SembrarHolgadoAsync(
        GestionAvicolaDbContext db, IAlmacenDocumentosPedido almacen,
        TenantDesarrollo tenant, Guid granjaId, DateOnly hoy)
    {
        await AgregarDespachoRecibidoAsync(db, almacen, tenant, granjaId,
            Derivar(tenant.ClienteId, 0x41), TamanoHuevo.Extra, 30, hoy.AddDays(-42), hoy.AddDays(-40));
        await AgregarDespachoRecibidoAsync(db, almacen, tenant, granjaId,
            Derivar(tenant.ClienteId, 0x42), TamanoHuevo.Primera, 20, hoy.AddDays(-32), hoy.AddDays(-30));

        await AgregarPedidoRecibidoAsync(db, almacen, tenant,
            Derivar(tenant.ClienteId, 0x51), 20, 20, hoy.AddDays(-20));

        db.PedidosAlimento.Add(new PedidoAlimento(
            Derivar(tenant.ClienteId, 0x52), tenant.ClienteId, tenant.ActorId,
            [new DatosDetallePedido(TipoAlimento.PosturaDos, PresentacionAlimento.Bolsa, 12)]));
    }

    // Ingresos 1215; recibido real 7200. Saldo -5985.
    private static async Task SembrarNegativoAsync(
        GestionAvicolaDbContext db, IAlmacenDocumentosPedido almacen,
        TenantDesarrollo tenant, Guid granjaId, DateOnly hoy)
    {
        await AgregarDespachoRecibidoAsync(db, almacen, tenant, granjaId,
            Derivar(tenant.ClienteId, 0x41), TamanoHuevo.Segunda, 5, hoy.AddDays(-32), hoy.AddDays(-30));

        await AgregarPedidoRecibidoAsync(db, almacen, tenant,
            Derivar(tenant.ClienteId, 0x51), 40, 40, hoy.AddDays(-18));
    }

    // Ingresos 3915; comprometido pendiente 3600. Saldo 315: alcanza para muy
    // poco, así que el siguiente envío dispara la confirmación de SP9E.
    private static async Task SembrarInsuficienteAsync(
        GestionAvicolaDbContext db, IAlmacenDocumentosPedido almacen,
        TenantDesarrollo tenant, Guid granjaId, DateOnly hoy)
    {
        await AgregarDespachoRecibidoAsync(db, almacen, tenant, granjaId,
            Derivar(tenant.ClienteId, 0x41), TamanoHuevo.Primera, 15, hoy.AddDays(-37), hoy.AddDays(-35));

        var solicitado = new PedidoAlimento(
            Derivar(tenant.ClienteId, 0x51), tenant.ClienteId, tenant.ActorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 20)]);
        solicitado.EnviarACaisy(hoy.AddDays(-3), tenant.ActorId, PreciosEnvio());
        db.PedidosAlimento.Add(solicitado);
        db.NotificacionesInternas.Add(NotificacionInterna.ParaCaisy(
            TipoNotificacionPedido.PedidoSolicitado, solicitado.Id));
    }

    // Ingresos computables 6075 + 2250 = 8325 (el despacho de hace cinco días
    // todavía no cuenta), recibido real 2340, comprometido 1800 y ajuste +900.
    // Saldo 5085.
    private static async Task SembrarConMovimientoAsync(
        GestionAvicolaDbContext db, IAlmacenDocumentosPedido almacen,
        TenantDesarrollo tenant, Guid granjaId, DateOnly hoy)
    {
        // Este despacho congeló el precio de la publicación errónea: es el que
        // el ajuste de SP9D compensa.
        var conPrecioErroneo = Derivar(tenant.ClienteId, 0x41);
        await AgregarDespachoRecibidoAsync(db, almacen, tenant, granjaId,
            conPrecioErroneo, TamanoHuevo.Extra, 25, hoy.AddDays(-42), hoy.AddDays(-40),
            PrecioExtraErroneo + Servicio, PublicacionErroneaId);

        await AgregarDespachoRecibidoAsync(db, almacen, tenant, granjaId,
            Derivar(tenant.ClienteId, 0x42), TamanoHuevo.Tercera, 10, hoy.AddDays(-32), hoy.AddDays(-30));

        // Recibido pero todavía dentro de la ventana de catorce días: se ve en
        // el desglose como pendiente de disponibilidad.
        await AgregarDespachoRecibidoAsync(db, almacen, tenant, granjaId,
            Derivar(tenant.ClienteId, 0x43), TamanoHuevo.Primera, 8, hoy.AddDays(-6), hoy.AddDays(-5));

        // Despachado sin confirmar y un borrador editable.
        var despachado = new DespachoHuevo(
            Derivar(tenant.ClienteId, 0x44), tenant.ClienteId, granjaId, tenant.ActorId,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Segunda, 6, 40)]);
        despachado.Despachar(hoy.AddDays(-2), tenant.ActorId,
            [new DatosPrecioDespachoHuevo(TamanoHuevo.Segunda, PrecioCongelado(TamanoHuevo.Segunda), PublicacionCorrectivaId)],
            await GuardarNotaAsync(almacen));
        db.DespachosHuevo.Add(despachado);

        db.DespachosHuevo.Add(new DespachoHuevo(
            Derivar(tenant.ClienteId, 0x45), tenant.ClienteId, granjaId, tenant.ActorId,
            [new DatosDetalleDespachoHuevo(TamanoHuevo.Cuarta, 3, 0)]));

        // Recepción con diferencias: se solicitaron 15 bolsas, se entregaron
        // 14 y llegaron 13.
        await AgregarPedidoRecibidoAsync(db, almacen, tenant,
            Derivar(tenant.ClienteId, 0x51), 15, 13, hoy.AddDays(-16), entregadas: 14);

        var aceptado = new PedidoAlimento(
            Derivar(tenant.ClienteId, 0x52), tenant.ClienteId, tenant.ActorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, 10)]);
        aceptado.EnviarACaisy(hoy.AddDays(-4), tenant.ActorId, PreciosEnvio());
        aceptado.Aceptar(hoy.AddDays(3), hoy, tenant.ActorId);
        db.PedidosAlimento.Add(aceptado);
        db.NotificacionesInternas.Add(NotificacionInterna.ParaTenant(
            TipoNotificacionPedido.PedidoAceptado, aceptado.Id, tenant.ClienteId));

        var rechazado = new PedidoAlimento(
            Derivar(tenant.ClienteId, 0x53), tenant.ClienteId, tenant.ActorId,
            [new DatosDetallePedido(TipoAlimento.PosturaDos, PresentacionAlimento.Bolsa, 8)]);
        rechazado.EnviarACaisy(hoy.AddDays(-9), tenant.ActorId, PreciosEnvio());
        rechazado.Rechazar("Sin stock de Postura 2 esta semana.", tenant.ActorId);
        db.PedidosAlimento.Add(rechazado);
        db.NotificacionesInternas.Add(NotificacionInterna.ParaTenant(
            TipoNotificacionPedido.PedidoRechazado, rechazado.Id, tenant.ClienteId));

        var devuelto = new PedidoAlimento(
            Derivar(tenant.ClienteId, 0x54), tenant.ClienteId, tenant.ActorId,
            [new DatosDetallePedido(TipoAlimento.Crecimiento, PresentacionAlimento.Bolsa, 6)]);
        devuelto.EnviarACaisy(hoy.AddDays(-7), tenant.ActorId, PreciosEnvio());
        devuelto.DevolverParaCorreccion("Falta detallar la edad del lote.", tenant.ActorId);
        db.PedidosAlimento.Add(devuelto);
        db.NotificacionesInternas.Add(NotificacionInterna.ParaTenant(
            TipoNotificacionPedido.PedidoDevuelto, devuelto.Id, tenant.ClienteId));

        // Ajuste por la corrección: (1,55 - 1,35) x 25 amarras x 180 huevos.
        const decimal monto = 900m;
        const string motivo = "Corrección del precio del Extra en la publicación vigente.";
        db.AjustesCreditoHuevo.Add(new AjusteCreditoHuevo(
            tenant.ClienteId, conPrecioErroneo, PublicacionErroneaId, PublicacionCorrectivaId,
            monto, motivo, tenant.ActorId));
        db.NotificacionesInternasDespachoHuevo.Add(
            NotificacionInternaDespachoHuevo.ParaAjusteCredito(
                conPrecioErroneo, tenant.ClienteId,
                $"{monto} | {motivo}"));
    }

    private static async Task AgregarDespachoRecibidoAsync(
        GestionAvicolaDbContext db, IAlmacenDocumentosPedido almacen, TenantDesarrollo tenant,
        Guid granjaId, Guid despachoId, TamanoHuevo tamano, int amarras,
        DateOnly fechaDespacho, DateOnly fechaRecepcion,
        decimal? precioCongelado = null, Guid? publicacionId = null)
    {
        var despacho = new DespachoHuevo(
            despachoId, tenant.ClienteId, granjaId, tenant.ActorId,
            [new DatosDetalleDespachoHuevo(tamano, amarras, 0)]);
        despacho.Despachar(fechaDespacho, tenant.ActorId,
            [new DatosPrecioDespachoHuevo(
                tamano,
                precioCongelado ?? PrecioCongelado(tamano),
                publicacionId ?? PublicacionVigenteId)],
            await GuardarNotaAsync(almacen));
        despacho.ConfirmarRecepcion(fechaRecepcion, tenant.ActorId);
        db.DespachosHuevo.Add(despacho);
        db.NotificacionesInternasDespachoHuevo.Add(
            NotificacionInternaDespachoHuevo.ParaRecepcionConfirmada(despacho.Id, tenant.ClienteId));
    }

    private static async Task AgregarPedidoRecibidoAsync(
        GestionAvicolaDbContext db, IAlmacenDocumentosPedido almacen, TenantDesarrollo tenant,
        Guid pedidoId, int bolsasSolicitadas, int bolsasRecibidas, DateOnly fechaPedido,
        int? entregadas = null)
    {
        var pedido = new PedidoAlimento(
            pedidoId, tenant.ClienteId, tenant.ActorId,
            [new DatosDetallePedido(TipoAlimento.PosturaUno, PresentacionAlimento.Bolsa, bolsasSolicitadas)]);
        pedido.EnviarACaisy(fechaPedido, tenant.ActorId, PreciosEnvio());
        pedido.Aceptar(fechaPedido.AddDays(2), fechaPedido, tenant.ActorId);
        pedido.RegistrarDespacho(
            $"NE-{bolsasSolicitadas:D4}", fechaPedido.AddDays(2), null,
            [new DatosLineaEntrega(TipoAlimento.PosturaUno, entregadas ?? bolsasSolicitadas)],
            fechaPedido.AddDays(2), tenant.ActorId);
        pedido.ConfirmarRecepcion(
            [new DatosLineaRecepcion(TipoAlimento.PosturaUno, bolsasRecibidas)],
            await GuardarNotaAsync(almacen), tenant.ActorId);
        db.PedidosAlimento.Add(pedido);
        db.NotificacionesInternas.Add(NotificacionInterna.ParaTenant(
            pedido.Estado == EstadoPedidoAlimento.RecibidoConforme
                ? TipoNotificacionPedido.RecepcionConforme
                : TipoNotificacionPedido.RecepcionConDiferencias,
            pedido.Id, tenant.ClienteId));
    }

    private static decimal PrecioCongelado(TamanoHuevo tamano) =>
        PreciosHuevo.Single(p => p.Tamano == tamano).Precio + Servicio;

    private static IReadOnlyList<DatosPrecioEnvio> PreciosEnvio() =>
    [
        .. Enum.GetValues<TipoAlimento>().SelectMany(tipo => new[]
        {
            new DatosPrecioEnvio(tipo, PresentacionAlimento.Bolsa, PrecioBolsaPostura, NotificacionPreciosId),
            new DatosPrecioEnvio(tipo, PresentacionAlimento.Granel, PrecioBolsaPostura - 8m, NotificacionPreciosId),
        }),
    ];

    // Guarda una foto de nota real por el puerto de aplicación: así la clave,
    // el hash y los tamaños corresponden a un archivo que existe de verdad y
    // las pantallas que abren la imagen funcionan.
    private static async Task<DatosDocumentoNota> GuardarNotaAsync(IAlmacenDocumentosPedido almacen)
    {
        using var contenido = new MemoryStream(GenerarNotaJpeg());
        var guardado = await almacen.GuardarAsync(contenido);
        return new DatosDocumentoNota(
            guardado.ClaveOriginal, guardado.ClaveVista, guardado.Mime,
            guardado.TamanoOriginalBytes, guardado.TamanoVistaBytes,
            guardado.HashSha256, "nota-demo.jpg");
    }

    // Imagen generada, no embebida: el almacén valida firma, dimensiones y
    // ausencia de datos sobrantes con SkiaSharp, así que se produce con la
    // misma librería para garantizar un JPEG que siempre pasa la validación.
    private static byte[] GenerarNotaJpeg()
    {
        using var bitmap = new SKBitmap(640, 400);
        using (var lienzo = new SKCanvas(bitmap))
        {
            lienzo.Clear(SKColors.White);
            using var borde = new SKPaint { Color = SKColors.SlateGray, Style = SKPaintStyle.Stroke, StrokeWidth = 6 };
            lienzo.DrawRect(30, 30, 580, 340, borde);
            using var franja = new SKPaint { Color = SKColors.LightSteelBlue };
            lienzo.DrawRect(60, 70, 520, 60, franja);
            using var renglon = new SKPaint { Color = SKColors.Gainsboro };
            for (var y = 170; y < 340; y += 34)
                lienzo.DrawRect(60, y, 520, 16, renglon);
        }

        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Jpeg, 80);
        return datos.ToArray();
    }

    // Id estable derivado del tenant: el mismo cliente siempre produce los
    // mismos ids, sin depender del orden de la lista de tenants.
    private static Guid Derivar(Guid clienteId, byte discriminador)
    {
        var bytes = clienteId.ToByteArray();
        bytes[0] = discriminador;
        bytes[1] = 0xAB;
        return new Guid(bytes);
    }
}
