using Icarus.BuildingBlocks.Application;
using Icarus.GestionAvicola.Application.CreditoHuevo;
using Icarus.GestionAvicola.Application.Documentos;
using Icarus.GestionAvicola.Domain;
using Icarus.GestionAvicola.Infrastructure;
using Icarus.GestionAvicola.Infrastructure.Documentos;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Icarus.GestionAvicola.Infrastructure.Repositorios;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Icarus.IntegrationTests;

// El escenario de desarrollo corre solo bajo IsDevelopment() (spec: el índice
// único filtrado sobre FechaVigencia lo haría colisionar con la base
// compartida de Testing), así que ninguna otra prueba lo ejercita y se
// pudriría en silencio. Esta clase lo invoca explícitamente contra una base
// PROPIA creada dentro del contenedor que ya comparte la suite: sin
// contaminar la base común y sin levantar un segundo SQL Server.
[Collection(IntegracionCollection.Nombre)]
public sealed class SemillaDesarrolloAvicolaTests(IdentityFactory factory) : IAsyncLifetime
{
    private readonly string _baseAislada = "semilla_dev_" + Guid.NewGuid().ToString("N");
    private string _carpetaDocumentos = string.Empty;
    private ServiceProvider _servicios = null!;

    private static readonly TenantDesarrollo Holgado =
        new(Guid.NewGuid(), Guid.NewGuid(), PapelCreditoDesarrollo.PositivoHolgado);

    private static readonly TenantDesarrollo Negativo =
        new(Guid.NewGuid(), Guid.NewGuid(), PapelCreditoDesarrollo.Negativo);

    private static readonly TenantDesarrollo Insuficiente =
        new(Guid.NewGuid(), Guid.NewGuid(), PapelCreditoDesarrollo.Insuficiente);

    private static readonly TenantDesarrollo ConMovimiento =
        new(Guid.NewGuid(), Guid.NewGuid(), PapelCreditoDesarrollo.ConMovimiento);

    private static readonly TenantDesarrollo Vacio =
        new(Guid.NewGuid(), Guid.NewGuid(), PapelCreditoDesarrollo.Vacio);

    private static readonly TenantDesarrollo[] Tenants =
        [Holgado, Negativo, Insuficiente, ConMovimiento, Vacio];

    public async Task InitializeAsync()
    {
        var cadena = new SqlConnectionStringBuilder(factory.CadenaConexion)
        {
            InitialCatalog = _baseAislada,
        }.ConnectionString;

        _carpetaDocumentos = Path.Combine(Path.GetTempPath(), _baseAislada);
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AlmacenDocumentosPedido:Ruta"] = _carpetaDocumentos,
            })
            .Build();

        var coleccion = new ServiceCollection();
        coleccion.AddSingleton<IConfiguration>(configuracion);
        coleccion.AddSingleton<ICurrentUser, UsuarioSinTenant>();
        coleccion.AddScoped<IAlmacenDocumentosPedido, AlmacenDocumentosPedidoLocal>();
        coleccion.AddDbContext<GestionAvicolaDbContext>(o => o.UseSqlServer(cadena));
        _servicios = coleccion.BuildServiceProvider();

        var db = _servicios.GetRequiredService<GestionAvicolaDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        var db = _servicios.GetRequiredService<GestionAvicolaDbContext>();
        await db.Database.EnsureDeletedAsync();
        await _servicios.DisposeAsync();
        if (Directory.Exists(_carpetaDocumentos))
            Directory.Delete(_carpetaDocumentos, recursive: true);
    }

    // Un ICurrentUser sin tenant: la semilla escribe filas de varios clientes
    // a la vez, así que necesita ver la base sin el filtro por tenant, igual
    // que hacen las semillas del Host al arrancar.
    private sealed class UsuarioSinTenant : ICurrentUser
    {
        public bool EstaAutenticado => false;
        public Guid? UsuarioId => null;
        public string? Rol => null;
        public Guid? ClienteId => null;
        public Guid? TrabajadorId => null;
    }

    private async Task<decimal> SaldoDe(TenantDesarrollo tenant)
    {
        var db = _servicios.GetRequiredService<GestionAvicolaDbContext>();
        var repositorio = new RepositorioBalanceCreditoHuevo(db);
        return await repositorio.ObtenerSaldoDisponibleAsync(
            tenant.ClienteId, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public async Task CadaTenantQuedaConElSignoDeSaldoQueSuPapelDeclara()
    {
        await SemillaDesarrolloAvicola.SembrarAsync(_servicios, Tenants);

        var holgado = await SaldoDe(Holgado);
        var negativo = await SaldoDe(Negativo);
        var insuficiente = await SaldoDe(Insuficiente);
        var conMovimiento = await SaldoDe(ConMovimiento);
        var vacio = await SaldoDe(Vacio);

        Assert.True(holgado > 0, $"El tenant holgado debía quedar positivo, quedó en {holgado}.");
        Assert.True(negativo < 0, $"El tenant negativo debía quedar negativo, quedó en {negativo}.");
        Assert.True(insuficiente > 0,
            $"El tenant insuficiente debía quedar positivo, quedó en {insuficiente}.");
        Assert.True(insuficiente < holgado,
            "El tenant insuficiente debía tener menos saldo que el holgado " +
            $"({insuficiente} contra {holgado}).");
        Assert.True(conMovimiento > 0,
            $"El tenant con movimiento debía quedar positivo, quedó en {conMovimiento}.");
        Assert.Equal(0m, vacio);
    }

    // Los comentarios de SemillaDesarrolloAvicola declaran el saldo de cada
    // papel. Si la aritmética de la semilla cambia sin actualizar el
    // comentario, el comentario miente: este test lo impide.
    [Fact]
    public async Task LosSaldosCoincidenConLosDeclaradosEnLaSemilla()
    {
        await SemillaDesarrolloAvicola.SembrarAsync(_servicios, Tenants);

        Assert.Equal(9990m, await SaldoDe(Holgado));
        Assert.Equal(-5985m, await SaldoDe(Negativo));
        Assert.Equal(315m, await SaldoDe(Insuficiente));
        Assert.Equal(5085m, await SaldoDe(ConMovimiento));
        Assert.Equal(0m, await SaldoDe(Vacio));
    }

    [Fact]
    public async Task ElDespachoRecibidoDentroDeLaVentanaDeCatorceDiasNoCuentaTodavia()
    {
        await SemillaDesarrolloAvicola.SembrarAsync(_servicios, Tenants);

        var db = _servicios.GetRequiredService<GestionAvicolaDbContext>();
        var repositorio = new RepositorioBalanceCreditoHuevo(db);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var corte = hoy.AddDays(-ReglasCreditoHuevo.DiasDisponibilidadCredito);

        // Despachos ya recibidos pero todavía dentro de la ventana: hoy no
        // cuentan, y catorce días después sí.
        var recientes = await db.DespachosHuevo.IgnoreQueryFilters()
            .Where(d => d.ClienteId == ConMovimiento.ClienteId
                && d.Estado == EstadoDespachoHuevo.Recibido
                && d.FechaRecepcion > corte)
            .ToListAsync();
        Assert.NotEmpty(recientes);

        var importeReciente = recientes
            .SelectMany(d => d.Detalles)
            .Where(det => det.PrecioUnitarioCongelado is not null)
            .Sum(det => det.CantidadHuevos * det.PrecioUnitarioCongelado!.Value);
        Assert.True(importeReciente > 0, "El despacho reciente debía tener precio congelado.");

        // Mismo dato, dos fechas de negocio: la única diferencia posible es
        // que los despachos recientes entren en la ventana.
        var saldoHoy = await repositorio.ObtenerSaldoDisponibleAsync(ConMovimiento.ClienteId, hoy);
        var saldoEnCatorceDias = await repositorio.ObtenerSaldoDisponibleAsync(
            ConMovimiento.ClienteId, hoy.AddDays(ReglasCreditoHuevo.DiasDisponibilidadCredito));

        Assert.Equal(importeReciente, saldoEnCatorceDias - saldoHoy);
    }

    [Fact]
    public async Task ElTenantConMovimientoTieneUnAjusteDeCreditoYSuNotificacion()
    {
        await SemillaDesarrolloAvicola.SembrarAsync(_servicios, Tenants);

        var db = _servicios.GetRequiredService<GestionAvicolaDbContext>();

        var ajustes = await db.AjustesCreditoHuevo.IgnoreQueryFilters()
            .Where(a => a.ClienteId == ConMovimiento.ClienteId)
            .ToListAsync();
        Assert.NotEmpty(ajustes);

        var notificaciones = await db.NotificacionesInternasDespachoHuevo.IgnoreQueryFilters()
            .Where(n => n.ClienteId == ConMovimiento.ClienteId && !n.Leida)
            .ToListAsync();
        Assert.NotEmpty(notificaciones);
    }

    [Fact]
    public async Task SembrarDosVecesNoDuplicaNiCambiaLosSaldos()
    {
        await SemillaDesarrolloAvicola.SembrarAsync(_servicios, Tenants);
        var db = _servicios.GetRequiredService<GestionAvicolaDbContext>();
        var despachosPrimeraVez = await db.DespachosHuevo.IgnoreQueryFilters().CountAsync();
        var pedidosPrimeraVez = await db.PedidosAlimento.IgnoreQueryFilters().CountAsync();
        var publicacionesPrimeraVez = await db.PublicacionesPreciosHuevo.IgnoreQueryFilters().CountAsync();
        var saldoPrimeraVez = await SaldoDe(Holgado);

        await SemillaDesarrolloAvicola.SembrarAsync(_servicios, Tenants);

        Assert.Equal(despachosPrimeraVez, await db.DespachosHuevo.IgnoreQueryFilters().CountAsync());
        Assert.Equal(pedidosPrimeraVez, await db.PedidosAlimento.IgnoreQueryFilters().CountAsync());
        Assert.Equal(
            publicacionesPrimeraVez,
            await db.PublicacionesPreciosHuevo.IgnoreQueryFilters().CountAsync());
        Assert.Equal(saldoPrimeraVez, await SaldoDe(Holgado));
    }

    [Fact]
    public async Task LosDocumentosDeNotaSembradosSeAbrenDeVerdad()
    {
        await SemillaDesarrolloAvicola.SembrarAsync(_servicios, Tenants);

        var db = _servicios.GetRequiredService<GestionAvicolaDbContext>();
        var almacen = _servicios.GetRequiredService<IAlmacenDocumentosPedido>();

        var despachos = await db.DespachosHuevo.IgnoreQueryFilters()
            .Where(d => d.Estado != EstadoDespachoHuevo.Borrador)
            .ToListAsync();
        Assert.NotEmpty(despachos);

        foreach (var despacho in despachos)
        {
            var documento = despacho.DocumentoNota;
            Assert.NotNull(documento);
            await using var vista = await almacen.AbrirVistaAsync(documento.ClaveVista);
            Assert.NotNull(vista);
            await using var original = await almacen.AbrirOriginalAsync(documento.ClaveOriginal);
            Assert.NotNull(original);
        }
    }
}
