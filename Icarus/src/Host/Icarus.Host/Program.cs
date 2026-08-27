using System.Threading.RateLimiting;
using FluentValidation;
using Icarus.BuildingBlocks.Application;
using Icarus.BuildingBlocks.Application.Behaviors;
using Icarus.BuildingBlocks.Application.Observability;
using Icarus.BuildingBlocks.Observability;
using Icarus.Clientes.Application.Clientes;
using Icarus.Clientes.Infrastructure;
using Icarus.Clientes.Infrastructure.Persistencia;
using Icarus.GestionAvicola.Application.Granjas;
using Icarus.GestionAvicola.Infrastructure;
using Icarus.GestionAvicola.Infrastructure.Persistencia;
using Icarus.Host.Endpoints;
using Icarus.Host.Middleware;
using Icarus.Host.Observability;
using Icarus.Host.Servicios;
using Icarus.Identity.Application.Sesiones;
using Icarus.Identity.Infrastructure;
using Icarus.Identity.Infrastructure.Persistencia;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservabilidad();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(IniciarSesionCommand).Assembly, typeof(CrearClienteCommand).Assembly,
    typeof(CrearGranjaCommand).Assembly));
builder.Services.AddValidatorsFromAssemblies([
    typeof(IniciarSesionCommand).Assembly, typeof(CrearClienteCommand).Assembly,
    typeof(CrearGranjaCommand).Assembly]);
builder.Services.AddScoped<IRegistroVuelo, RegistroVuelo>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RegistroVueloBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddIdentidadInfraestructura(builder.Configuration);
builder.Services.AddClientesInfraestructura(builder.Configuration);
builder.Services.AddGestionAvicolaInfraestructura(builder.Configuration);
builder.Services.AddScoped<AltaCuentasServicio>();
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy("diagnosticos-frontend", contexto =>
    {
        var sessionId = contexto.Request.Headers[DiagnosticIds.SessionHeader].FirstOrDefault();
        var particion = DiagnosticIds.EsSessionId(sessionId)
            ? sessionId!
            : contexto.Connection.RemoteIpAddress?.ToString() ?? "anonimo";
        return RateLimitPartition.GetFixedWindowLimiter(
            particion,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
});

var app = builder.Build();

// nginx termina TLS y llega desde la red bridge de Docker. Se aceptan sus
// cabeceras porque el puerto de la aplicación solo se publica en loopback en
// el compose de producción (paridad con Caserito).
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost,
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseMiddleware<ClienteActivoMiddleware>();
app.UseMiddleware<RequestObservabilityMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ClientDiagnosticsBodyLimitMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

// La API vive bajo /api (paridad con Caserito): la PWA y su service worker se
// sirven desde wwwroot y el fallback de SPA cubre el enrutado del frontend.
var api = app.MapGroup("/api");
api.MapIdentidad();
api.MapClientes();
api.MapGestionAvicola();
api.MapDiagnosticos();

app.UseStaticFiles();
app.MapFallbackToFile("index.html");

var esDesarrollo = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");
var ejecutarMigraciones = esDesarrollo
    || app.Configuration.GetValue<bool>("Migraciones:EjecutarAlArranque");

if (esDesarrollo)
{
    // Sondeo de entitlement (spec: el mecanismo se construye y se prueba en
    // este incremento aunque aún no haya endpoints de módulos de negocio).
    api.MapSondeoEntitlement();
}

if (ejecutarMigraciones)
{
    // En Development y Testing migra y siembra los datos de prueba por rol (la
    // factory de Testing inyecta la cadena y las claves fijas). En Production
    // es opt-in vía Migraciones:EjecutarAlArranque (ruta de migración
    // controlada por el despliegue, paridad con Caserito) y solo siembra el
    // administrador de plataforma si SeedSettings está completo.
    using var alcance = app.Services.CreateScope();
    var db = alcance.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
    var clientesDb = alcance.ServiceProvider.GetRequiredService<ClientesDbContext>();
    await clientesDb.Database.MigrateAsync();
    var avicolaDb = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
    await avicolaDb.Database.MigrateAsync();

    if (esDesarrollo)
    {
        await SemillaIdentidad.SembrarAsync(
            alcance.ServiceProvider,
            app.Configuration["Semilla:ContrasenaPrueba"] ?? "Solo-Desarrollo-123");
        // Los ids fijos vienen de SemillaIdentidad: el claim clienteId de las
        // cuentas semilla debe coincidir con el cliente sembrado.
        await SemillaClientes.SembrarAsync(
            alcance.ServiceProvider, SemillaIdentidad.ClienteDemoId, SemillaIdentidad.TrabajadorDemoId);
        await SemillaGestionAvicola.SembrarAsync(alcance.ServiceProvider, SemillaIdentidad.ClienteDemoId);
    }
    else
    {
        var opcionesSeedAdmin = app.Configuration
            .GetSection(OpcionesSeedAdmin.Seccion)
            .Get<OpcionesSeedAdmin>() ?? new OpcionesSeedAdmin();
        var seedAdmin = new SeedAdminPlataforma(
            alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>(),
            alcance.ServiceProvider.GetRequiredService<ILogger<SeedAdminPlataforma>>());
        await seedAdmin.EjecutarAsync(opcionesSeedAdmin);
    }
}

await app.RunAsync();

// Expone Program a WebApplicationFactory en los tests de integración.
public partial class Program
{
    protected Program() { }
}
