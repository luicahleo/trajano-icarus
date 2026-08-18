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

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseMiddleware<ClienteActivoMiddleware>();
app.UseMiddleware<RequestObservabilityMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ClientDiagnosticsBodyLimitMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));
app.MapIdentidad();
app.MapClientes();
app.MapGestionAvicola();
app.MapDiagnosticos();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    // Sondeo de entitlement (spec: el mecanismo se construye y se prueba en
    // este incremento aunque aún no haya endpoints de módulos de negocio).
    app.MapSondeoEntitlement();

    // Migra ambos schemas y siembra las cuentas y los datos de prueba por rol
    // (dev y tests de integración). En Testing la factory inyecta la cadena
    // de conexión y las claves fijas.
    using var alcance = app.Services.CreateScope();
    var db = alcance.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
    await SemillaIdentidad.SembrarAsync(
        alcance.ServiceProvider,
        app.Configuration["Semilla:ContrasenaPrueba"] ?? "Solo-Desarrollo-123");

    var clientesDb = alcance.ServiceProvider.GetRequiredService<ClientesDbContext>();
    await clientesDb.Database.MigrateAsync();
    // Los ids fijos vienen de SemillaIdentidad: el claim clienteId de las
    // cuentas semilla debe coincidir con el cliente sembrado.
    await SemillaClientes.SembrarAsync(
        alcance.ServiceProvider, SemillaIdentidad.ClienteDemoId, SemillaIdentidad.TrabajadorDemoId);
    var avicolaDb = alcance.ServiceProvider.GetRequiredService<GestionAvicolaDbContext>();
    await avicolaDb.Database.MigrateAsync();
    await SemillaGestionAvicola.SembrarAsync(alcance.ServiceProvider, SemillaIdentidad.ClienteDemoId);
}

await app.RunAsync();

// Expone Program a WebApplicationFactory en los tests de integración.
public partial class Program
{
    protected Program() { }
}
